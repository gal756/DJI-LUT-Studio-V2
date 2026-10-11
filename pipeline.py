#!/usr/bin/env python3
"""
DJI O4 Pro / Air Unit D-Log M Automatic Color Grading Pipeline
==============================================================
Applies the official DJI D-Log M to Rec.709 3D LUT to O4 video footage.
Supports:
  - NVIDIA NVENC hardware acceleration (RTX GPU) with 10-bit color pipeline
  - Batch processing files and folders
  - Continuous directory monitoring (--watch)
  - Drag-and-drop from Windows Explorer via WSL
  - Before/after image preview generation
"""

import os
import sys
import re
import time
import json
import shutil
import argparse
import subprocess
from pathlib import Path

# ANSI colors for terminal
GREEN = "\033[92m"
CYAN = "\033[96m"
YELLOW = "\033[93m"
RED = "\033[91m"
BOLD = "\033[1m"
DIM = "\033[2m"
RESET = "\033[0m"

SCRIPT_DIR = Path(__file__).resolve().parent
DEFAULT_LUT = SCRIPT_DIR / "lut" / "DJI_O4_D-Log_M_to_Rec.709.cube"
DEFAULT_WATCH_DIR = SCRIPT_DIR / "input"
DEFAULT_OUTPUT_DIR = SCRIPT_DIR / "output"

VIDEO_EXTENSIONS = {".mp4", ".mov", ".mkv", ".m4v"}


def to_wsl_path(path_str: str) -> Path:
    """Converts Windows paths (e.g. D:\\dji\\clip.mp4) or Linux paths to WSL Path."""
    p_str = path_str.strip().strip('"').strip("'")
    m = re.match(r"^([a-zA-Z]):[\\/](.*)", p_str)
    if m:
        drive = m.group(1).lower()
        rest = m.group(2).replace("\\", "/")
        return Path(f"/mnt/{drive}/{rest}").resolve()
    return Path(p_str).resolve()


def find_binary(name: str) -> str:
    """Finds ffmpeg or ffprobe in system PATH or common user directories."""
    candidates = [
        shutil.which(name),
        str(Path.home() / ".local" / "bin" / name),
        f"/usr/local/bin/{name}",
        f"/usr/bin/{name}",
    ]
    for c in candidates:
        if c and os.path.isfile(c) and os.access(c, os.X_OK):
            return c
    raise FileNotFoundError(f"Could not find '{name}'. Please make sure it is installed.")


FFMPEG = find_binary("ffmpeg")
FFPROBE = find_binary("ffprobe")


def check_nvenc_available() -> bool:
    """Verifies whether NVIDIA NVENC hevc_nvenc is available and operational."""
    try:
        proc = subprocess.run(
            [FFMPEG, "-hide_banner", "-f", "lavfi", "-i", "color=c=black:s=256x256:d=0.1",
             "-c:v", "hevc_nvenc", "-f", "null", "-"],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            timeout=5
        )
        return proc.returncode == 0
    except Exception:
        return False


def get_video_info(video_path: Path) -> dict:
    """Probes video metadata using ffprobe."""
    cmd = [
        FFPROBE,
        "-v", "quiet",
        "-print_format", "json",
        "-show_format",
        "-show_streams",
        str(video_path)
    ]
    try:
        res = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, check=True)
        data = json.loads(res.stdout)
        video_stream = next((s for s in data.get("streams", []) if s.get("codec_type") == "video"), None)
        has_audio = any(s.get("codec_type") == "audio" for s in data.get("streams", []))
        duration = float(data.get("format", {}).get("duration", 0) or 0)
        if not duration and video_stream:
            duration = float(video_stream.get("duration", 0) or 0)

        width = int(video_stream.get("width", 0)) if video_stream else 0
        height = int(video_stream.get("height", 0)) if video_stream else 0
        pix_fmt = video_stream.get("pix_fmt", "") if video_stream else ""
        fps_eval = video_stream.get("r_frame_rate", "60/1") if video_stream else "60/1"

        try:
            num, den = fps_eval.split("/")
            fps = round(float(num) / float(den), 2)
        except Exception:
            fps = 60.0

        encoder_tag = data.get("format", {}).get("tags", {}).get("encoder", "")

        return {
            "duration": duration,
            "width": width,
            "height": height,
            "fps": fps,
            "pix_fmt": pix_fmt,
            "has_audio": has_audio,
            "encoder": encoder_tag,
        }
    except Exception as e:
        return {"duration": 0, "width": 0, "height": 0, "fps": 0, "has_audio": False, "pix_fmt": "", "error": str(e)}


def is_dji_dlog(video_path: Path, info: dict) -> bool:
    """Heuristic check to detect if file is DJI D-Log / D-Log M footage."""
    stem = video_path.stem.upper()
    if stem.endswith("_D") or "DLOG" in stem or "D-LOG" in stem:
        return True
    if "DJI" in info.get("encoder", "").upper() or "O4" in info.get("encoder", "").upper():
        return True
    # 10-bit HEVC from DJI
    if "10" in info.get("pix_fmt", ""):
        return True
    return False


def format_seconds(seconds: float) -> str:
    """Formats seconds into HH:MM:SS."""
    seconds = max(0, int(seconds))
    h = seconds // 3600
    m = (seconds % 3600) // 60
    s = seconds % 60
    if h > 0:
        return f"{h:02d}:{m:02d}:{s:02d}"
    return f"{m:02d}:{s:02d}"


def build_video_filter(lut_path: Path, look: str = "natural", sharpen: bool = False, scale: str = "keep") -> str:
    """Builds the FFmpeg video filter chain including LUT, look enhancements, and scaling."""
    filters = [f"lut3d=file='{lut_path}':interp=tetrahedral"]

    if look == "vibrant":
        # Rich punchy colors, micro-contrast and fine detail sharpening
        filters.append("eq=saturation=1.14:contrast=1.04")
        filters.append("unsharp=lx=3:ly=3:la=0.35:cx=3:cy=3:ca=0.0")
    elif look == "cinematic":
        # Filmic contrast, deep blacks, controlled highlights
        filters.append("eq=contrast=1.06:brightness=-0.01:saturation=1.08")
        filters.append("unsharp=lx=3:ly=3:la=0.3:cx=3:cy=3:ca=0.0")
    elif look == "golden":
        # Warm golden-hour sunlight glow with rich contrast
        filters.append("colorbalance=rs=0.03:gs=0.01:bs=-0.03:rm=0.04:gm=0.01:bm=-0.04")
        filters.append("eq=contrast=1.05:saturation=1.10")
        filters.append("unsharp=lx=3:ly=3:la=0.3:cx=3:cy=3:ca=0.0")
    elif sharpen:
        # Subtle fine-edge sharpening without noise
        filters.append("unsharp=lx=3:ly=3:la=0.4:cx=3:cy=3:ca=0.0")

    if scale == "1080p":
        filters.append("scale=1920:1080:flags=lanczos")
    elif scale == "2.7k":
        filters.append("scale=2704:1520:flags=lanczos")
    elif scale == "4k":
        filters.append("scale=3840:2160:flags=lanczos")

    filters.append("format=yuv420p10le")
    return ",".join(filters)


def generate_preview_snapshots(video_path: Path, lut_path: Path, out_preview: Path, look: str = "natural", sharpen: bool = False):
    """Generates a side-by-side comparison image between Original and Graded."""
    try:
        info = get_video_info(video_path)
        ss = 5.0
        if info.get("duration", 0) > 10:
            ss = min(info["duration"] * 0.25, 30.0)

        vf_graded = build_video_filter(lut_path, look=look, sharpen=sharpen, scale="keep")
        label = f"GRADED ({look.upper()})"

        filter_complex = (
            f"[0:v]trim=start={ss}:duration=1,scale=1920:1080:flags=lanczos,split=2[left_raw][right_raw];"
            f"[right_raw]{vf_graded}[graded];"
            f"[left_raw]drawtext=text='ORIGINAL (D-LOG M)':x=40:y=40:fontsize=36:fontcolor=white:box=1:boxcolor=black@0.6[left_labeled];"
            f"[graded]drawtext=text='{label}':x=40:y=40:fontsize=36:fontcolor=white:box=1:boxcolor=black@0.6[right_labeled];"
            f"[left_labeled][right_labeled]hstack[out]"
        )

        cmd = [
            FFMPEG, "-y",
            "-ss", str(ss),
            "-i", str(video_path),
            "-filter_complex", filter_complex,
            "-map", "[out]",
            "-vframes", "1",
            "-q:v", "2",
            str(out_preview)
        ]
        subprocess.run(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=True)
    except Exception:
        pass


def grade_video(
    video_path: Path,
    output_path: Path,
    lut_path: Path,
    codec_choice: str = "hevc",
    use_nvenc: bool = True,
    cq: int = 19,
    look: str = "natural",
    sharpen: bool = False,
    scale: str = "keep",
    generate_preview: bool = False,
) -> bool:
    """Applies the 3D LUT to the video using FFmpeg with real-time progress."""
    info = get_video_info(video_path)
    duration = info.get("duration", 0)

    print(f"\n{BOLD}{CYAN}▶ Processing:{RESET} {video_path.name}")
    print(f"  {DIM}Path:{RESET}       {video_path}")
    print(f"  {DIM}Resolution:{RESET} {info.get('width')}x{info.get('height')} @ {info.get('fps')} fps" + (f" -> {scale}" if scale != "keep" else ""))
    print(f"  {DIM}Duration:{RESET}   {format_seconds(duration)}")
    print(f"  {DIM}Look Style:{RESET} {BOLD}{look.upper()}{RESET}" + (" + Sharpness" if sharpen else ""))
    print(f"  {DIM}LUT:{RESET}        {lut_path.name}")

    output_path.parent.mkdir(parents=True, exist_ok=True)

    # Base video filter
    vf = build_video_filter(lut_path, look=look, sharpen=sharpen, scale=scale)

    # Encoding configuration
    if codec_choice == "prores":
        c_v = ["-c:v", "prores_ks", "-profile:v", "3", "-pix_fmt", "yuv422p10le"]
    elif codec_choice == "h264":
        if use_nvenc:
            c_v = ["-c:v", "h264_nvenc", "-preset", "p5", "-cq", str(cq), "-b:v", "0", "-pix_fmt", "yuv420p"]
        else:
            c_v = ["-c:v", "libx264", "-crf", str(cq), "-preset", "slow", "-pix_fmt", "yuv420p"]
    else:  # hevc (default)
        if use_nvenc:
            # 10-bit HEVC on RTX GPU
            c_v = ["-c:v", "hevc_nvenc", "-preset", "p5", "-cq", str(cq), "-b:v", "0", "-pix_fmt", "p010le"]
        else:
            c_v = ["-c:v", "libx265", "-crf", str(cq), "-preset", "medium", "-pix_fmt", "yuv420p10le"]

    color_tags = [
        "-colorspace", "bt709",
        "-color_primaries", "bt709",
        "-color_trc", "bt709",
        "-color_range", "tv",
    ]

    audio_args = ["-map", "0:a?", "-c:a", "copy"] if info.get("has_audio") else []

    cmd = [
        FFMPEG, "-y",
        "-hide_banner",
        "-i", str(video_path),
        "-map", "0:v:0",
        *audio_args,
        "-vf", vf,
        *c_v,
        *color_tags,
        "-movflags", "+faststart",
        "-map_metadata", "0",
        "-progress", "pipe:1",
        str(output_path)
    ]

    start_time = time.time()

    process = subprocess.Popen(
        cmd,
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
        text=True,
        bufsize=1
    )

    current_out_time = 0.0
    fps_val = 0.0
    speed_val = "1.0x"

    try:
        while True:
            line = process.stdout.readline()
            if not line and process.poll() is not None:
                break
            line = line.strip()
            if not line:
                continue

            if line.startswith("out_time_ms="):
                val = line.split("=")[1]
                if val.isdigit():
                    current_out_time = float(val) / 1_000_000.0
            elif line.startswith("fps="):
                val = line.split("=")[1]
                try:
                    fps_val = float(val)
                except ValueError:
                    pass
            elif line.startswith("speed="):
                speed_val = line.split("=")[1]
            elif line.startswith("progress="):
                state = line.split("=")[1]
                if duration > 0:
                    pct = min(100.0, (current_out_time / duration) * 100.0)
                    elapsed = time.time() - start_time
                    eta = (elapsed / (pct / 100.0)) - elapsed if pct > 0.5 else 0
                    bar_len = 30
                    filled = int(bar_len * (pct / 100.0))
                    bar = "█" * filled + "░" * (bar_len - filled)
                    sys.stdout.write(
                        f"\r  [{GREEN}{bar}{RESET}] {pct:5.1f}% | "
                        f"{format_seconds(current_out_time)}/{format_seconds(duration)} | "
                        f"{speed_val} ({fps_val:4.1f} fps) | ETA: {format_seconds(eta)}  "
                    )
                    sys.stdout.flush()

        process.wait()
        sys.stdout.write("\n")

        if process.returncode == 0:
            total_time = time.time() - start_time
            out_size_mb = output_path.stat().st_size / (1024 * 1024)
            print(f"  {GREEN}✔ Completed in {total_time:.1f}s ({out_size_mb:.1f} MB){RESET}")
            print(f"  {DIM}Saved to:{RESET} {output_path}")

            # Preserve original timestamps
            try:
                st = video_path.stat()
                os.utime(output_path, (st.st_atime, st.st_mtime))
            except Exception:
                pass

            if generate_preview:
                preview_path = output_path.with_name(f"{output_path.stem}_preview.jpg")
                print(f"  {CYAN}Generating preview split snapshot...{RESET}")
                generate_preview_snapshots(video_path, lut_path, preview_path)
                print(f"  {GREEN}✔ Preview image saved to:{RESET} {preview_path.name}")

            return True
        else:
            print(f"  {RED}✖ Encoding failed with exit code {process.returncode}{RESET}")
            return False

    except KeyboardInterrupt:
        process.kill()
        print(f"\n{YELLOW}Cancelled by user.{RESET}")
        if output_path.exists():
            output_path.unlink()
        sys.exit(130)


def process_path(
    target_path: Path,
    output_dir: Path | None,
    lut_path: Path,
    codec_choice: str,
    use_nvenc: bool,
    force_all: bool,
    overwrite: bool,
    generate_preview: bool,
    cq: int,
    look: str = "natural",
    sharpen: bool = False,
    scale: str = "keep"
):
    """Processes a single file or directory recursively."""
    if not target_path.exists():
        print(f"{RED}Path not found:{RESET} {target_path}")
        return

    files_to_process = []
    if target_path.is_file():
        if target_path.suffix.lower() in VIDEO_EXTENSIONS:
            files_to_process.append(target_path)
    elif target_path.is_dir():
        for root, _, files in os.walk(target_path):
            # Skip output folders
            if "graded" in root.lower() or "output" in root.lower():
                continue
            for f in files:
                p = Path(root) / f
                if p.suffix.lower() in VIDEO_EXTENSIONS:
                    files_to_process.append(p)

    if not files_to_process:
        print(f"{YELLOW}No supported video files found in:{RESET} {target_path}")
        return

    print(f"\n{BOLD}Found {len(files_to_process)} video(s) to inspect.{RESET}")

    success_count = 0
    skip_count = 0

    for v_path in sorted(files_to_process):
        # Determine output location
        if output_dir:
            out_folder = output_dir
        else:
            out_folder = v_path.parent / "graded"

        # Output filename: replace _D with _Graded or append _Graded
        stem = v_path.stem
        tag = f"_{look.capitalize()}" if look != "natural" else "_Graded"
        if stem.endswith("_D"):
            out_stem = stem[:-2] + tag
        else:
            out_stem = f"{stem}{tag}"
        out_ext = ".mov" if codec_choice == "prores" else ".mp4"
        out_path = out_folder / f"{out_stem}{out_ext}"

        if out_path.exists() and not overwrite:
            print(f"{DIM}Skipping (already exists): {v_path.name} -> {out_path.name}{RESET}")
            skip_count += 1
            continue

        info = get_video_info(v_path)
        if not force_all and not is_dji_dlog(v_path, info):
            print(f"{YELLOW}Skipping (not identified as D-Log M footage, use --force to override): {v_path.name}{RESET}")
            skip_count += 1
            continue

        ok = grade_video(
            video_path=v_path,
            output_path=out_path,
            lut_path=lut_path,
            codec_choice=codec_choice,
            use_nvenc=use_nvenc,
            cq=cq,
            look=look,
            sharpen=sharpen,
            scale=scale,
            generate_preview=generate_preview,
        )
        if ok:
            success_count += 1

    print(f"\n{BOLD}{GREEN}Done! Processed {success_count} file(s), skipped {skip_count}.{RESET}")


def wait_until_file_stable(path: Path, timeout: int = 120, check_interval: float = 2.0) -> bool:
    """Waits until a file stops changing size (e.g. while being copied from SD card)."""
    last_size = -1
    stable_count = 0
    start = time.time()

    while time.time() - start < timeout:
        try:
            cur_size = path.stat().st_size
        except OSError:
            cur_size = -1

        if cur_size > 0 and cur_size == last_size:
            stable_count += 1
            if stable_count >= 2:
                return True
        else:
            stable_count = 0
            last_size = cur_size

        time.sleep(check_interval)
    return False


def run_watcher(
    watch_dir: Path,
    output_dir: Path,
    lut_path: Path,
    codec_choice: str,
    use_nvenc: bool,
    force_all: bool,
    generate_preview: bool,
    cq: int,
    look: str = "natural",
    sharpen: bool = False,
    scale: str = "keep"
):
    """Monitors watch_dir continuously for new files."""
    watch_dir.mkdir(parents=True, exist_ok=True)
    output_dir.mkdir(parents=True, exist_ok=True)

    print(f"\n{BOLD}{CYAN}=== DJI O4 Pro LUT Auto-Pipeline (Watch Mode) ==={RESET}")
    print(f"  {DIM}Monitoring folder:{RESET} {watch_dir}")
    print(f"  {DIM}Graded output:{RESET}    {output_dir}")
    print(f"  {DIM}Active LUT:{RESET}       {lut_path.name}")
    print(f"  {DIM}Look Style:{RESET}       {look.upper()}" + (" + Sharpness" if sharpen else ""))
    print(f"  {DIM}GPU Acceleration:{RESET} {'NVIDIA NVENC (RTX 3070)' if use_nvenc else 'CPU (Software)'}")
    print(f"\n{YELLOW}Drop raw DJI video files into the folder to grade automatically.{RESET}")
    print(f"{DIM}Press Ctrl+C to stop the watcher.{RESET}\n")

    processed_files = set()

    for f in watch_dir.iterdir():
        if f.is_file() and f.suffix.lower() in VIDEO_EXTENSIONS:
            processed_files.add(f.resolve())

    try:
        while True:
            for item in watch_dir.iterdir():
                if item.is_file() and item.suffix.lower() in VIDEO_EXTENSIONS:
                    resolved = item.resolve()
                    if resolved not in processed_files:
                        print(f"\n{CYAN}⚡ New video detected:{RESET} {item.name}")
                        print(f"  Waiting for file transfer to finish...")
                        if wait_until_file_stable(item):
                            stem = item.stem
                            tag = f"_{look.capitalize()}" if look != "natural" else "_Graded"
                            out_stem = stem[:-2] + tag if stem.endswith("_D") else f"{stem}{tag}"
                            out_ext = ".mov" if codec_choice == "prores" else ".mp4"
                            out_path = output_dir / f"{out_stem}{out_ext}"

                            info = get_video_info(item)
                            if force_all or is_dji_dlog(item, info):
                                grade_video(
                                    video_path=item,
                                    output_path=out_path,
                                    lut_path=lut_path,
                                    codec_choice=codec_choice,
                                    use_nvenc=use_nvenc,
                                    cq=cq,
                                    look=look,
                                    sharpen=sharpen,
                                    scale=scale,
                                    generate_preview=generate_preview,
                                )
                            else:
                                print(f"{YELLOW}Skipped {item.name}: Not detected as D-Log M.{RESET}")
                            processed_files.add(resolved)
                        else:
                            print(f"{RED}File timeout or still incomplete: {item.name}{RESET}")

            time.sleep(2)
    except KeyboardInterrupt:
        print(f"\n{YELLOW}Watcher stopped.{RESET}")


def main():
    parser = argparse.ArgumentParser(
        description="DJI O4 Pro / Air Unit D-Log M Automatic Color Grading Pipeline",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Grade with Vibrant Look (Recommended):
  python3 pipeline.py /path/to/DJI_0001_D.MP4 --look vibrant

  # Grade with Cinematic Look & Sharpening:
  python3 pipeline.py /path/to/DJI_0001_D.MP4 --look cinematic --sharpen

  # Export quick 1080p proxy for Discord/WhatsApp:
  python3 pipeline.py /path/to/DJI_0001_D.MP4 --scale 1080p

  # Run auto-watcher daemon:
  python3 pipeline.py --watch /mnt/d/dji/incoming -o /mnt/d/dji/graded --look vibrant
"""
    )

    parser.add_argument("inputs", nargs="*", help="Video file(s) or folder(s) to process (Windows or Linux paths)")
    parser.add_argument("-w", "--watch", metavar="DIR", nargs="?", const=str(DEFAULT_WATCH_DIR),
                        help="Watch directory for incoming videos and auto-grade them continuously")
    parser.add_argument("-o", "--output", help="Output directory (defaults to 'graded/' inside input folder)")
    parser.add_argument("-l", "--lut", default=str(DEFAULT_LUT), help=f"Path to .cube LUT (default: {DEFAULT_LUT.name})")
    parser.add_argument("--look", choices=["natural", "vibrant", "cinematic", "golden"], default="vibrant",
                        help="Color grading style: vibrant (punchy colors & micro-contrast, default), natural (standard Rec.709), cinematic (filmic contrast), golden (warm glow)")
    parser.add_argument("-s", "--sharpen", action="store_true", help="Apply fine-edge unsharp mask for trees and foliage")
    parser.add_argument("--scale", choices=["keep", "4k", "2.7k", "1080p"], default="keep",
                        help="Output resolution: keep (original, default), 4k, 2.7k, 1080p (proxy)")
    parser.add_argument("-c", "--codec", choices=["hevc", "h264", "prores"], default="hevc",
                        help="Output video codec: hevc (10-bit H.265, default), h264 (8-bit), prores (Apple ProRes 422 HQ)")
    parser.add_argument("--cq", type=int, default=19, help="Constant Quality CQ/CRF (lower = higher quality, default: 19)")
    parser.add_argument("--cpu", action="store_true", help="Force CPU encoding instead of NVIDIA NVENC")
    parser.add_argument("-f", "--force", action="store_true", help="Process all videos even if not marked as D-Log M")
    parser.add_argument("--overwrite", action="store_true", help="Overwrite existing output files")
    parser.add_argument("-p", "--preview", action="store_true", help="Generate side-by-side comparison JPEG snapshot")

    args = parser.parse_args()

    # Resolve LUT path
    lut_path = to_wsl_path(args.lut)
    if not lut_path.exists():
        print(f"{RED}Error: LUT file not found at:{RESET} {lut_path}")
        sys.exit(1)

    # Determine GPU / NVENC capability
    has_nvenc = check_nvenc_available()
    use_nvenc = has_nvenc and not args.cpu
    if not use_nvenc and not args.cpu:
        print(f"{YELLOW}Notice: NVIDIA NVENC not available, falling back to CPU encoder.{RESET}")

    # Output directory
    output_dir = to_wsl_path(args.output) if args.output else None

    # Banner
    print(f"\n{BOLD}{CYAN}╔══════════════════════════════════════════════════════════════╗{RESET}")
    print(f"{BOLD}{CYAN}║     DJI O4 PRO / AIR UNIT D-LOG M COLOR GRADING PIPELINE     ║{RESET}")
    print(f"{BOLD}{CYAN}╚══════════════════════════════════════════════════════════════╝{RESET}")
    print(f"  {DIM}Engine:{RESET}      FFmpeg with 3D LUT (Tetrahedral interpolation)")
    print(f"  {DIM}GPU Device:{RESET}  {'NVIDIA GeForce RTX (NVENC Hardware Accelerated)' if use_nvenc else 'CPU (Software)'}")
    print(f"  {DIM}LUT Model:{RESET}   {lut_path.name}")
    print(f"  {DIM}Look Preset:{RESET} {BOLD}{args.look.upper()}{RESET}" + (" + Sharpen" if args.sharpen else ""))
    print(f"  {DIM}Codec:{RESET}       {args.codec.upper()} (CQ: {args.cq})")

    if args.watch is not None:
        watch_path = to_wsl_path(args.watch)
        out_path = output_dir if output_dir else watch_path / "graded"
        run_watcher(
            watch_dir=watch_path,
            output_dir=out_path,
            lut_path=lut_path,
            codec_choice=args.codec,
            use_nvenc=use_nvenc,
            force_all=args.force,
            generate_preview=args.preview,
            cq=args.cq,
            look=args.look,
            sharpen=args.sharpen,
            scale=args.scale,
        )
    elif args.inputs:
        for inp in args.inputs:
            p = to_wsl_path(inp)
            process_path(
                target_path=p,
                output_dir=output_dir,
                lut_path=lut_path,
                codec_choice=args.codec,
                use_nvenc=use_nvenc,
                force_all=args.force,
                overwrite=args.overwrite,
                generate_preview=args.preview,
                cq=args.cq,
                look=args.look,
                sharpen=args.sharpen,
                scale=args.scale,
            )
    else:
        # No arguments given: explain usage or prompt user
        print(f"\n{YELLOW}No input files or folders specified.{RESET}")
        print("Usage:")
        print(f"  {BOLD}python3 pipeline.py <video_or_folder>{RESET}")
        print(f"  {BOLD}python3 pipeline.py --watch /path/to/folder{RESET}")
        print(f"\nOr drop files into {DEFAULT_WATCH_DIR} and run:")
        print(f"  {BOLD}python3 pipeline.py --watch{RESET}\n")


if __name__ == "__main__":
    main()
