"""
Video Grading Engine for DJI LUT Studio.
Accelerated with NVIDIA NVENC (hevc_nvenc 10-bit) and 3D tetrahedral LUT interpolation.
"""

import os
import re
import sys
import time
import json
import shutil
import subprocess
from pathlib import Path
from typing import Dict, List, Optional, Callable, Any, Tuple

_pkg_root = str(Path(__file__).resolve().parent.parent)
if _pkg_root not in sys.path:
    sys.path.insert(0, _pkg_root)

try:
    from .manifest import manifest
except (ImportError, ValueError):
    from dji_lut_studio.manifest import manifest

VIDEO_EXTENSIONS = {".mp4", ".mov", ".mkv", ".m4v"}


def find_binary(name: str) -> str:
    """Finds ffmpeg or ffprobe in system PATH or common user directories."""
    candidates = [
        shutil.which(name),
        str(Path.home() / ".local" / "bin" / name),
        f"/usr/local/bin/{name}",
        f"/usr/bin/{name}",
        f"C:\\ffmpeg\\bin\\{name}.exe",
        f"C:\\Program Files\\ffmpeg\\bin\\{name}.exe",
    ]
    for c in candidates:
        if c and os.path.isfile(c) and os.access(c, os.X_OK):
            return c
    raise FileNotFoundError(f"Could not find binary '{name}'. Ensure FFmpeg is installed.")


FFMPEG = find_binary("ffmpeg")
FFPROBE = find_binary("ffprobe")


def check_nvenc_available() -> bool:
    """Verifies whether NVIDIA NVENC hevc_nvenc is available and functional."""
    try:
        proc = subprocess.run(
            [
                FFMPEG, "-hide_banner", "-f", "lavfi", "-i", "color=c=black:s=256x256:d=0.1",
                "-c:v", "hevc_nvenc", "-f", "null", "-"
            ],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            timeout=5
        )
        return proc.returncode == 0
    except Exception:
        return False


def get_video_info(video_path: Path) -> Dict[str, Any]:
    """Inspects video metadata via ffprobe."""
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
            "filename": video_path.name,
            "size_bytes": video_path.stat().st_size if video_path.exists() else 0,
        }
    except Exception as e:
        return {
            "duration": 0, "width": 0, "height": 0, "fps": 0,
            "has_audio": False, "pix_fmt": "", "filename": video_path.name, "error": str(e)
        }


def format_seconds(seconds: float) -> str:
    """Formats seconds into readable string (HH:MM:SS or MM:SS)."""
    seconds = max(0, int(seconds))
    h = seconds // 3600
    m = (seconds % 3600) // 60
    s = seconds % 60
    if h > 0:
        return f"{h:02d}:{m:02d}:{s:02d}"
    return f"{m:02d}:{s:02d}"


def build_filter_chain(
    primary_lut: Path,
    secondary_lut: Optional[Path] = None,
    look_style: str = "natural",
    sharpen: bool = False,
    scale: str = "keep"
) -> str:
    """
    Constructs the FFmpeg video filter chain.
    Applies tetrahedral 3D LUT interpolation, optional secondary LUT,
    look curve refinements, and lanczos scaling.
    """
    filters = []

    # 1. Primary conversion LUT (Technical Log to Rec.709)
    filters.append(f"lut3d=file='{primary_lut}':interp=tetrahedral")

    # 2. Optional secondary creative LUT (e.g. Master Looks or Film Looks)
    if secondary_lut and secondary_lut.exists():
        filters.append(f"lut3d=file='{secondary_lut}':interp=tetrahedral")

    # 3. Look style adjustments (built-in curve refinements)
    if look_style == "vibrant":
        # Rich punchy colors, micro-contrast and fine detail sharpening
        filters.append("eq=saturation=1.14:contrast=1.04")
        filters.append("unsharp=lx=3:ly=3:la=0.35:cx=3:cy=3:ca=0.0")
    elif look_style == "cinematic":
        # Filmic contrast, deep blacks, controlled highlights
        filters.append("eq=contrast=1.06:brightness=-0.01:saturation=1.08")
        filters.append("unsharp=lx=3:ly=3:la=0.30:cx=3:cy=3:ca=0.0")
    elif look_style == "golden":
        # Warm golden-hour sunlight glow with rich contrast
        filters.append("colorbalance=rs=0.03:gs=0.01:bs=-0.03:rm=0.04:gm=0.01:bm=-0.04")
        filters.append("eq=contrast=1.05:saturation=1.10")
        filters.append("unsharp=lx=3:ly=3:la=0.30:cx=3:cy=3:ca=0.0")
    elif look_style == "teal_orange":
        # Complementary blockbuster look
        filters.append("colorbalance=rs=-0.02:gs=0.01:bs=0.04:rm=0.04:gm=0.01:bm=-0.03:rh=0.05:gh=0.02:bh=-0.04")
        filters.append("eq=contrast=1.06:saturation=1.12")
        filters.append("unsharp=lx=3:ly=3:la=0.35:cx=3:cy=3:ca=0.0")
    elif sharpen:
        # Subtle fine-edge sharpening without noise
        filters.append("unsharp=lx=3:ly=3:la=0.40:cx=3:cy=3:ca=0.0")

    # 4. Optional resolution scaling
    if scale == "1080p":
        filters.append("scale=1920:1080:flags=lanczos")
    elif scale == "2.7k":
        filters.append("scale=2704:1520:flags=lanczos")
    elif scale == "4k":
        filters.append("scale=3840:2160:flags=lanczos")

    # 5. Output format standard (10-bit YUV 4:2:0)
    filters.append("format=yuv420p10le")
    return ",".join(filters)


def generate_preview_snapshot(
    video_path: Path,
    primary_lut: Path,
    out_preview: Path,
    secondary_lut: Optional[Path] = None,
    look_style: str = "natural",
    sharpen: bool = False
) -> bool:
    """Generates a high-quality side-by-side comparison JPEG (Original vs Graded)."""
    try:
        info = get_video_info(video_path)
        duration = info.get("duration", 0)
        ss = 1.0
        if duration > 1.0:
            ss = max(0.5, min(duration * 0.25, duration - 0.5))
        elif duration > 0:
            ss = max(0.1, duration * 0.5)

        vf_graded = build_filter_chain(
            primary_lut, secondary_lut=secondary_lut, look_style=look_style, sharpen=sharpen, scale="keep"
        )
        label = f"GRADED ({look_style.upper()})"

        filter_complex = (
            f"[0:v]trim=start={ss}:duration=1,scale=1920:1080:flags=lanczos,split=2[left_raw][right_raw];"
            f"[right_raw]{vf_graded}[graded];"
            f"[left_raw]drawtext=text='ORIGINAL (RAW)':x=40:y=40:fontsize=36:fontcolor=white:box=1:boxcolor=black@0.6[left_labeled];"
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
            "-update", "1",
            "-q:v", "2",
            str(out_preview)
        ]
        out_preview.parent.mkdir(parents=True, exist_ok=True)
        subprocess.run(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=True, timeout=30)
        return True
    except Exception:
        return False


def grade_video(
    video_path: Path,
    output_path: Path,
    primary_lut: Path,
    secondary_lut: Optional[Path] = None,
    look_style: str = "natural",
    codec_choice: str = "hevc",
    use_nvenc: bool = True,
    cq: int = 19,
    sharpen: bool = False,
    scale: str = "keep",
    generate_preview: bool = True,
    progress_callback: Optional[Callable[[Dict[str, Any]], None]] = None
) -> Tuple[bool, Optional[str]]:
    """
    Grades a single video file applying the selected LUT and look options.
    Returns (success, error_message).
    """
    if not video_path.exists():
        return False, f"Input video not found: {video_path}"

    if not primary_lut.exists():
        return False, f"Primary LUT file not found: {primary_lut}"

    info = get_video_info(video_path)
    duration = info.get("duration", 0)

    output_path.parent.mkdir(parents=True, exist_ok=True)

    # Base video filter
    vf = build_filter_chain(
        primary_lut, secondary_lut=secondary_lut, look_style=look_style, sharpen=sharpen, scale=scale
    )

    # Encoder settings
    has_nvenc = check_nvenc_available() if use_nvenc else False

    if codec_choice == "prores":
        c_v = ["-c:v", "prores_ks", "-profile:v", "3", "-pix_fmt", "yuv422p10le"]
    elif codec_choice == "h264":
        if has_nvenc:
            c_v = [
                "-c:v", "h264_nvenc",
                "-preset", "p6",
                "-tune", "hq",
                "-rc", "vbr",
                "-cq", str(cq),
                "-b:v", "0",
                "-spatial-aq", "1",
                "-temporal-aq", "1"
            ]
        else:
            c_v = ["-c:v", "libx264", "-crf", str(cq), "-preset", "slow"]
    else:  # hevc (default)
        if has_nvenc:
            c_v = [
                "-c:v", "hevc_nvenc",
                "-preset", "p6",
                "-tune", "hq",
                "-rc", "vbr",
                "-cq", str(cq),
                "-b:v", "0",
                "-spatial-aq", "1",
                "-temporal-aq", "1"
            ]
        else:
            c_v = ["-c:v", "libx265", "-crf", str(cq), "-preset", "medium"]

    # Audio handling
    c_a = ["-c:a", "copy"] if info.get("has_audio") else ["-an"]

    cmd = [
        FFMPEG, "-y",
        "-i", str(video_path),
        "-vf", vf,
        *c_v,
        *c_a,
        "-tag:v", "hvc1" if codec_choice == "hevc" else "avc1",
        "-map_metadata", "0",
        "-movflags", "+faststart",
        str(output_path)
    ]

    start_time = time.time()
    proc = subprocess.Popen(
        cmd,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        universal_newlines=True,
        bufsize=1
    )

    time_regex = re.compile(r"time=(\d+):(\d+):(\d+\.\d+)")
    fps_regex = re.compile(r"fps=\s*(\d+(?:\.\d+)?)")
    speed_regex = re.compile(r"speed=\s*(\d+(?:\.\d+)?)x")

    try:
        while True:
            line = proc.stderr.readline()
            if not line and proc.poll() is not None:
                break
            if not line:
                continue

            t_match = time_regex.search(line)
            if t_match and duration > 0:
                hours = float(t_match.group(1))
                minutes = float(t_match.group(2))
                secs = float(t_match.group(3))
                cur_sec = hours * 3600 + minutes * 60 + secs
                pct = min(100.0, (cur_sec / duration) * 100.0)

                fps_m = fps_regex.search(line)
                cur_fps = float(fps_m.group(1)) if fps_m else 0.0

                spd_m = speed_regex.search(line)
                speed = float(spd_m.group(1)) if spd_m else 1.0

                rem_sec = (duration - cur_sec) / speed if speed > 0 else 0
                elapsed = time.time() - start_time

                if progress_callback:
                    progress_callback({
                        "video": video_path.name,
                        "percentage": round(pct, 1),
                        "fps": round(cur_fps, 1),
                        "speed": f"{speed:.1f}x",
                        "eta_seconds": max(0, int(rem_sec)),
                        "elapsed_seconds": max(0, int(elapsed)),
                        "status": "encoding"
                    })

        proc.wait()
    except Exception as e:
        proc.kill()
        return False, str(e)

    if proc.returncode != 0:
        return False, f"FFmpeg exited with error code {proc.returncode}"

    # Side-by-side preview image
    if generate_preview:
        preview_path = output_path.parent / f"{output_path.stem}_preview.jpg"
        generate_preview_snapshot(
            video_path, primary_lut, preview_path,
            secondary_lut=secondary_lut, look_style=look_style, sharpen=sharpen
        )

    if progress_callback:
        progress_callback({
            "video": video_path.name,
            "percentage": 100.0,
            "status": "complete",
            "elapsed_seconds": int(time.time() - start_time)
        })

    return True, None
