"""
Embedded Web GUI Server for DJI LUT Studio.
Pure Python standard library (http.server + socketserver). Zero external dependencies.
"""

import os
import sys
import json
import time
import mimetypes
import threading
import urllib.parse
from http.server import HTTPServer, BaseHTTPRequestHandler
from pathlib import Path
from typing import Dict, Any, List

_pkg_root = str(Path(__file__).resolve().parent.parent)
if _pkg_root not in sys.path:
    sys.path.insert(0, _pkg_root)

try:
    from .manifest import manifest
    from .engine import (
        grade_video,
        get_video_info,
        check_nvenc_available,
        generate_preview_snapshot,
        VIDEO_EXTENSIONS,
    )
except (ImportError, ValueError):
    from dji_lut_studio.manifest import manifest
    from dji_lut_studio.engine import (
        grade_video,
        get_video_info,
        check_nvenc_available,
        generate_preview_snapshot,
        VIDEO_EXTENSIONS,
    )

BASE_DIR = Path(__file__).resolve().parent.parent
STATIC_DIR = BASE_DIR / "web"

# Thread-safe global state for jobs
CURRENT_JOB: Dict[str, Any] = {
    "status": "idle",
    "video": None,
    "percentage": 0.0,
    "fps": 0.0,
    "speed": "0.0x",
    "eta_seconds": 0,
    "elapsed_seconds": 0,
    "error": None,
    "history": [],
}
JOB_LOCK = threading.Lock()


def to_wsl_path(path_str: str) -> Path:
    p_str = path_str.strip().strip('"').strip("'")
    if len(p_str) >= 2 and p_str[1] == ":":
        drive = p_str[0].lower()
        rest = p_str[2:].replace("\\", "/").lstrip("/")
        return Path(f"/mnt/{drive}/{rest}").resolve()
    return Path(p_str).resolve()


class StudioHTTPHandler(BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        # Quiet standard requests, log only errors
        if args and str(args[1]) in ("500", "404"):
            sys.stderr.write(f"[WebGUI] {args[0]} - {args[1]}\n")

    def end_json(self, data: Any, status: int = 200):
        body = json.dumps(data, indent=2).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()
        self.wfile.write(body)

    def do_OPTIONS(self):
        self.send_response(200)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.end_headers()

    def do_GET(self):
        parsed = urllib.parse.urlparse(self.path)
        path = parsed.path

        if path == "/api/system":
            has_gpu = check_nvenc_available()
            self.end_json({
                "nvenc_available": has_gpu,
                "gpu_name": "NVIDIA RTX 3070" if has_gpu else "CPU Encoding",
                "version": "2.0.0",
                "lut_count": len(manifest.get_all_luts()),
                "device_count": len(manifest.get_all_devices()),
            })
            return

        if path == "/api/manifest":
            self.end_json({
                "categories": manifest.get_categories(),
                "devices": manifest.get_all_devices(),
                "luts": manifest.get_all_luts(),
                "looks": manifest.get_creative_looks(),
            })
            return

        if path == "/api/status":
            with JOB_LOCK:
                self.end_json(CURRENT_JOB)
            return

        # Serve generated preview images
        if path.startswith("/preview_cache/"):
            rel_file = path.replace("/preview_cache/", "")
            file_path = BASE_DIR / "preview_cache" / rel_file
            if file_path.is_file():
                self.serve_file(file_path)
                return
            else:
                self.send_error(404, "Preview not found")
                return

        # Static assets
        if path == "/" or path == "":
            file_to_serve = STATIC_DIR / "index.html"
        else:
            file_to_serve = STATIC_DIR / path.lstrip("/")

        if file_to_serve.is_file():
            self.serve_file(file_to_serve)
        else:
            self.send_error(404, f"File Not Found: {path}")

    def serve_file(self, file_path: Path):
        mime, _ = mimetypes.guess_type(str(file_path))
        mime = mime or "application/octet-stream"
        try:
            content = file_path.read_bytes()
            self.send_response(200)
            self.send_header("Content-Type", mime)
            self.send_header("Content-Length", str(len(content)))
            self.end_headers()
            self.wfile.write(content)
        except Exception as e:
            self.send_error(500, f"Error reading file: {e}")

    def do_POST(self):
        parsed = urllib.parse.urlparse(self.path)
        path = parsed.path
        length = int(self.headers.get("Content-Length", 0))
        raw_body = self.rfile.read(length) if length > 0 else b"{}"

        try:
            payload = json.loads(raw_body.decode("utf-8"))
        except Exception:
            payload = {}

        if path == "/api/scan":
            folder_str = payload.get("path", "")
            if not folder_str:
                self.end_json({"error": "No folder path provided"}, status=400)
                return

            fpath = to_wsl_path(folder_str)
            if not fpath.exists():
                self.end_json({"error": f"Path not found: {folder_str}"}, status=404)
                return

            clips = []
            targets = [fpath] if fpath.is_file() else list(fpath.rglob("*"))
            for p in targets:
                if p.is_file() and p.suffix.lower() in VIDEO_EXTENSIONS:
                    if "graded" not in p.parts:
                        info = get_video_info(p)
                        dev_id, lut_id = manifest.detect_device_and_lut(p.name)
                        lut_obj = manifest.get_lut(lut_id) if lut_id else None
                        clips.append({
                            "path": str(p),
                            "filename": p.name,
                            "size_bytes": p.stat().st_size,
                            "duration": info.get("duration", 0),
                            "resolution": f"{info.get('width')}x{info.get('height')}",
                            "fps": info.get("fps"),
                            "detected_device": dev_id,
                            "detected_lut": lut_id,
                            "detected_lut_name": lut_obj.get("name") if lut_obj else None,
                        })

            self.end_json({"total": len(clips), "clips": clips})
            return

        if path == "/api/preview":
            video_str = payload.get("video_path", "")
            lut_id = payload.get("lut_id", "")
            look = payload.get("look", "natural")

            if not video_str or not lut_id:
                self.end_json({"error": "Missing video_path or lut_id"}, status=400)
                return

            vpath = to_wsl_path(video_str)
            lut_file = manifest.resolve_lut_file(lut_id)

            cache_dir = BASE_DIR / "preview_cache"
            cache_dir.mkdir(parents=True, exist_ok=True)
            preview_filename = f"prev_{vpath.stem}_{lut_id}_{look}.jpg".replace(" ", "_")
            out_preview = cache_dir / preview_filename

            ok = generate_preview_snapshot(
                video_path=vpath,
                primary_lut=lut_file,
                out_preview=out_preview,
                look_style=look,
                sharpen=True
            )

            if ok and out_preview.exists():
                self.end_json({"success": True, "preview_url": f"/preview_cache/{preview_filename}"})
            else:
                self.end_json({"error": "Failed to generate preview snapshot"}, status=500)
            return

        if path == "/api/grade":
            video_str = payload.get("video_path", "")
            lut_id = payload.get("lut_id", "")
            look = payload.get("look", "natural")
            codec = payload.get("codec", "hevc")
            cq = int(payload.get("cq", 19))
            scale = payload.get("scale", "keep")
            sharpen = bool(payload.get("sharpen", False))

            if not video_str or not lut_id:
                self.end_json({"error": "Missing video_path or lut_id"}, status=400)
                return

            vpath = to_wsl_path(video_str)
            lut_file = manifest.resolve_lut_file(lut_id)

            with JOB_LOCK:
                if CURRENT_JOB.get("status") == "encoding":
                    self.end_json({"error": "Another grading job is already in progress"}, status=409)
                    return
                CURRENT_JOB["status"] = "starting"
                CURRENT_JOB["video"] = vpath.name
                CURRENT_JOB["percentage"] = 0.0
                CURRENT_JOB["error"] = None

            # Spawn grading thread
            def run_job():
                out_dir = vpath.parent / "graded"
                out_file = out_dir / f"{vpath.stem}_Graded.mp4"

                def callback(data):
                    with JOB_LOCK:
                        CURRENT_JOB.update(data)

                ok, err = grade_video(
                    video_path=vpath,
                    output_path=out_file,
                    primary_lut=lut_file,
                    look_style=look,
                    codec_choice=codec,
                    cq=cq,
                    scale=scale,
                    sharpen=sharpen,
                    generate_preview=True,
                    progress_callback=callback
                )

                with JOB_LOCK:
                    CURRENT_JOB["status"] = "complete" if ok else "error"
                    CURRENT_JOB["error"] = err
                    CURRENT_JOB["history"].append({
                        "video": vpath.name,
                        "output": str(out_file),
                        "success": ok,
                        "error": err,
                        "timestamp": time.time()
                    })

            thread = threading.Thread(target=run_job, daemon=True)
            thread.start()

            self.end_json({"success": True, "message": f"Started grading {vpath.name}"})
            return

        self.send_error(404, "Endpoint not found")


def start_server(port: int = 8080):
    server = HTTPServer(("0.0.0.0", port), StudioHTTPHandler)
    print(f"[DJI LUT Studio] Web GUI active at http://localhost:{port}")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nShutting down server...")
        server.server_close()


if __name__ == "__main__":
    p = int(sys.argv[1]) if len(sys.argv) > 1 else 8080
    start_server(p)
