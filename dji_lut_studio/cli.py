"""
Command-Line Interface (CLI) for DJI LUT Studio.
Supports interactive mode, batch grading, directory watcher, and full CLI flag controls.
"""

import os
import sys
import time
import argparse
from pathlib import Path
from typing import List, Optional

from .manifest import manifest
from .engine import (
    grade_video,
    get_video_info,
    check_nvenc_available,
    format_seconds,
    VIDEO_EXTENSIONS,
)

GREEN = "\033[92m"
CYAN = "\033[96m"
YELLOW = "\033[93m"
RED = "\033[91m"
BOLD = "\033[1m"
DIM = "\033[2m"
RESET = "\033[0m"


def to_local_path(path_str: str) -> Path:
    """Converts Windows paths (e.g. D:\\dji\\clip.mp4) or Linux paths to local Path."""
    p_str = path_str.strip().strip('"').strip("'")
    if len(p_str) >= 2 and p_str[1] == ":":
        drive = p_str[0].lower()
        rest = p_str[2:].replace("\\", "/").lstrip("/")
        return Path(f"/mnt/{drive}/{rest}").resolve()
    return Path(p_str).resolve()


def print_banner():
    print(f"{BOLD}{CYAN}====================================================================={RESET}")
    print(f"{BOLD}{CYAN}                    DJI LUT STUDIO (OPEN SOURCE)                    {RESET}")
    print(f"{CYAN}       Universal 3D LUT Color Grading Suite for DJI Drones & Cameras {RESET}")
    has_gpu = check_nvenc_available()
    gpu_badge = f"{GREEN}NVIDIA RTX NVENC (10-bit Active){RESET}" if has_gpu else f"{YELLOW}CPU Mode{RESET}"
    print(f"       Hardware Acceleration: {gpu_badge}")
    print(f"{BOLD}{CYAN}====================================================================={RESET}\n")


def list_devices():
    print_banner()
    print(f"{BOLD}Supported DJI Devices:{RESET}\n")
    for cat in manifest.get_categories():
        print(f"{BOLD}{CYAN}[{cat.get('name')}]{RESET}")
        for dev in cat.get("devices", []):
            lut_count = len(dev.get("luts", []))
            print(f"  • {BOLD}{dev.get('id'):<25}{RESET} : {dev.get('name')} ({lut_count} LUTs)")
        print()


def list_luts():
    print_banner()
    print(f"{BOLD}Available LUT Library ({len(manifest.get_all_luts())} Total):{RESET}\n")
    for cat in manifest.get_categories():
        print(f"{BOLD}{CYAN}=== {cat.get('name').upper()} ==={RESET}")
        for dev in cat.get("devices", []):
            print(f"  {BOLD}{dev.get('name')}:{RESET}")
            for lut in dev.get("luts", []):
                star = " ★ [RECOMMENDED]" if lut.get("recommended") else ""
                print(f"    - {BOLD}{lut.get('id'):<30}{RESET} {lut.get('name')}{GREEN}{star}{RESET}")
                print(f"      {DIM}{lut.get('description')}{RESET}")
        print()


def interactive_menu():
    print_banner()
    print("Welcome to DJI LUT Studio! Choose an action:")
    print("  [1] Grade a video or folder (Pick Camera & LUT)")
    print("  [2] Auto-detect Camera & Grade automatically")
    print("  [3] Start Folder Watcher (Auto-grades incoming clips)")
    print("  [4] View all 49 DJI LUTs & Devices")
    print("  [5] Exit")
    print()

    choice = input("Enter choice (1-5): ").strip()
    if choice == "4":
        list_luts()
        return
    elif choice == "5":
        sys.exit(0)

    # Input path
    path_in = input("\nEnter video path or directory to grade: ").strip()
    if not path_in:
        print("No path specified.")
        return

    input_path = to_local_path(path_in)
    if not input_path.exists():
        print(f"{RED}Error: Path not found: {input_path}{RESET}")
        return

    # Watcher mode
    if choice == "3":
        run_watcher(input_path, to_local_path(f"{input_path}/graded"))
        return

    selected_lut_id = None
    if choice == "1":
        devices = manifest.get_all_devices()
        print(f"\n{BOLD}Select DJI Camera/Drone Model:{RESET}")
        for idx, dev in enumerate(devices, 1):
            print(f"  [{idx:2d}] {dev.get('name')}")
        dev_choice = input(f"Enter choice (1-{len(devices)}, or press Enter for Auto): ").strip()
        if dev_choice.isdigit() and 1 <= int(dev_choice) <= len(devices):
            chosen_dev = devices[int(dev_choice) - 1]
            luts = chosen_dev.get("luts", [])
            print(f"\n{BOLD}Select LUT for {chosen_dev.get('name')}:{RESET}")
            for l_idx, lut in enumerate(luts, 1):
                rec = " ★ [Recommended]" if lut.get("recommended") else ""
                print(f"  [{l_idx}] {lut.get('name')}{GREEN}{rec}{RESET}")
            lut_choice = input(f"Enter choice (1-{len(luts)}, default 1): ").strip()
            if lut_choice.isdigit() and 1 <= int(lut_choice) <= len(luts):
                selected_lut_id = luts[int(lut_choice) - 1]["id"]
            else:
                selected_lut_id = luts[0]["id"] if luts else None

    # Look style
    print(f"\n{BOLD}Select Look Style:{RESET}")
    print("  [1] Vibrant Pop     (Recommended: punchy sky/grass, micro-contrast, crisp sharpness)")
    print("  [2] Natural Rec.709 (100% official DJI baseline conversion)")
    print("  [3] Cinematic Film  (Filmic 35mm curve, rich velvety shadows)")
    print("  [4] Golden Hour     (Warm sunset radiance, amber glow)")
    print("  [5] Teal & Orange   (Blockbuster cinema grade)")

    look_map = {"1": "vibrant", "2": "natural", "3": "cinematic", "4": "golden", "5": "teal_orange"}
    look_in = input("Enter choice (1-5, default 1): ").strip() or "1"
    look = look_map.get(look_in, "vibrant")

    process_inputs(
        inputs=[input_path],
        lut_choice=selected_lut_id,
        look=look,
        codec="hevc",
        cq=19,
        sharpen=True,
        scale="keep",
        generate_preview=True
    )


def process_inputs(
    inputs: List[Path],
    output_dir: Optional[Path] = None,
    camera: Optional[str] = None,
    lut_choice: Optional[str] = None,
    look: str = "natural",
    codec: str = "hevc",
    cq: int = 19,
    sharpen: bool = False,
    scale: str = "keep",
    generate_preview: bool = True,
    overwrite: bool = False
):
    """Processes a list of video files or directories."""
    files_to_grade: List[Path] = []
    for item in inputs:
        if item.is_file() and item.suffix.lower() in VIDEO_EXTENSIONS:
            files_to_grade.append(item)
        elif item.is_dir():
            for p in item.rglob("*"):
                if p.is_file() and p.suffix.lower() in VIDEO_EXTENSIONS:
                    # Ignore clips already inside graded/ folders
                    if "graded" not in p.parts:
                        files_to_grade.append(p)

    if not files_to_grade:
        print(f"{YELLOW}No video files found for processing.{RESET}")
        return

    print_banner()
    print(f"{BOLD}Found {len(files_to_grade)} video clip(s) to process.{RESET}\n")

    success_count = 0
    fail_count = 0

    for idx, video_path in enumerate(files_to_grade, 1):
        print(f"{BOLD}[{idx}/{len(files_to_grade)}]{RESET} {CYAN}{video_path.name}{RESET}")

        # Determine LUT
        primary_lut_path: Optional[Path] = None
        if lut_choice:
            try:
                primary_lut_path = manifest.resolve_lut_file(lut_choice)
            except Exception as e:
                print(f"  {RED}LUT error: {e}{RESET}")
                fail_count += 1
                continue
        elif camera and camera != "auto":
            dev = manifest.get_device(camera)
            if dev and dev.get("luts"):
                primary_lut_path = Path(dev["luts"][0]["full_path"])
        else:
            # Auto-detection
            dev_id, auto_lut_id = manifest.detect_device_and_lut(video_path.name)
            if auto_lut_id:
                lut_obj = manifest.get_lut(auto_lut_id)
                if lut_obj:
                    primary_lut_path = Path(lut_obj["full_path"])
                    dev_name = lut_obj.get("device_name", dev_id)
                    print(f"  {DIM}Auto-detected:{RESET} {dev_name} -> {lut_obj.get('name')}")

        if not primary_lut_path or not primary_lut_path.exists():
            print(f"  {RED}Could not find suitable LUT for {video_path.name}. Skipping.{RESET}")
            fail_count += 1
            continue

        # Output destination
        out_folder = output_dir if output_dir else video_path.parent / "graded"
        out_name = f"{video_path.stem}_Graded.mp4"
        out_file = out_folder / out_name

        if out_file.exists() and not overwrite:
            print(f"  {YELLOW}Skipping (already exists):{RESET} {out_file.name}")
            continue

        def cli_progress(d):
            pct = d.get("percentage", 0)
            spd = d.get("speed", "1.0x")
            fps = d.get("fps", 0)
            eta = format_seconds(d.get("eta_seconds", 0))
            bar_len = 30
            filled = int(bar_len * pct / 100.0)
            bar = "█" * filled + "░" * (bar_len - filled)
            sys.stdout.write(f"\r  [{bar}] {pct:5.1f}% | {fps:4.1f} fps ({spd}) | ETA: {eta}  ")
            sys.stdout.flush()

        print(f"  Applying LUT: {primary_lut_path.name}")
        ok, err = grade_video(
            video_path=video_path,
            output_path=out_file,
            primary_lut=primary_lut_path,
            look_style=look,
            codec_choice=codec,
            cq=cq,
            sharpen=sharpen,
            scale=scale,
            generate_preview=generate_preview,
            progress_callback=cli_progress
        )

        sys.stdout.write("\n")
        if ok:
            print(f"  {GREEN}✔ Done! Saved to:{RESET} {out_file}")
            success_count += 1
        else:
            print(f"  {RED}✖ Failed: {err}{RESET}")
            fail_count += 1
        print()

    print(f"{BOLD}Summary:{RESET} {GREEN}{success_count} completed{RESET}, {RED}{fail_count} failed{RESET}.")


def run_watcher(watch_dir: Path, output_dir: Path, look: str = "vibrant"):
    """Monitors directory for new clips and grades them immediately."""
    print_banner()
    print(f"{BOLD}Folder Watcher Active!{RESET}")
    print(f"  Watching: {CYAN}{watch_dir}{RESET}")
    print(f"  Output:   {GREEN}{output_dir}{RESET}")
    print(f"  Preset:   {BOLD}{look.upper()}{RESET}")
    print("\nPress Ctrl+C to terminate.")

    watch_dir.mkdir(parents=True, exist_ok=True)
    output_dir.mkdir(parents=True, exist_ok=True)
    seen = set()

    # Pre-populate existing
    for p in watch_dir.iterdir():
        if p.is_file() and p.suffix.lower() in VIDEO_EXTENSIONS:
            seen.add(p.name)

    try:
        while True:
            for p in watch_dir.iterdir():
                if p.is_file() and p.suffix.lower() in VIDEO_EXTENSIONS and p.name not in seen:
                    # Ensure file is done copying
                    size1 = p.stat().st_size
                    time.sleep(1.5)
                    size2 = p.stat().st_size
                    if size1 != size2:
                        continue

                    print(f"\n{BOLD}{CYAN}New clip detected:{RESET} {p.name}")
                    seen.add(p.name)
                    process_inputs(
                        inputs=[p],
                        output_dir=output_dir,
                        look=look,
                        sharpen=True,
                        generate_preview=True
                    )
            time.sleep(2)
    except KeyboardInterrupt:
        print("\nWatcher stopped.")


def main():
    parser = argparse.ArgumentParser(
        description="DJI LUT Studio - Open Source Universal Video Grading Suite for DJI Drones & Cameras."
    )
    parser.add_argument("inputs", nargs="*", help="Video file(s) or folder(s) to grade")
    parser.add_argument("-d", "--camera", help="DJI Camera / Drone ID (or 'auto')")
    parser.add_argument("-l", "--lut", help="LUT ID or direct path to .cube file")
    parser.add_argument("--look", choices=["natural", "vibrant", "cinematic", "golden", "teal_orange"], default="natural", help="Creative look style")
    parser.add_argument("-c", "--codec", choices=["hevc", "h264", "prores"], default="hevc", help="Video codec")
    parser.add_argument("--cq", type=int, default=19, help="Constant Quality factor (15-28, default 19)")
    parser.add_argument("--scale", choices=["keep", "1080p", "2.7k", "4k"], default="keep", help="Output resolution")
    parser.add_argument("--sharpen", action="store_true", help="Apply fine-edge unsharp mask")
    parser.add_argument("-p", "--preview", action="store_true", default=True, help="Generate comparison preview image")
    parser.add_argument("-o", "--output", help="Output directory")
    parser.add_argument("-w", "--watch", nargs="?", const="default", help="Watch incoming folder and grade on the fly")
    parser.add_argument("--overwrite", action="store_true", help="Overwrite existing output files")
    parser.add_argument("--list-devices", action="store_true", help="List all supported DJI drones and cameras")
    parser.add_argument("--list-luts", action="store_true", help="List all available LUTs in library")
    parser.add_argument("-i", "--interactive", action="store_true", help="Launch interactive terminal wizard")

    args = parser.parse_args()

    if args.list_devices:
        list_devices()
        return

    if args.list_luts:
        list_luts()
        return

    if args.interactive or (not args.inputs and not args.watch):
        interactive_menu()
        return

    if args.watch:
        w_dir = to_local_path(args.watch) if args.watch != "default" else Path("./incoming").resolve()
        o_dir = to_local_path(args.output) if args.output else Path("./graded").resolve()
        run_watcher(w_dir, o_dir, look=args.look)
        return

    input_paths = [to_local_path(i) for i in args.inputs]
    out_dir = to_local_path(args.output) if args.output else None

    process_inputs(
        inputs=input_paths,
        output_dir=out_dir,
        camera=args.camera,
        lut_choice=args.lut,
        look=args.look,
        codec=args.codec,
        cq=args.cq,
        sharpen=args.sharpen,
        scale=args.scale,
        generate_preview=args.preview,
        overwrite=args.overwrite
    )


if __name__ == "__main__":
    main()
