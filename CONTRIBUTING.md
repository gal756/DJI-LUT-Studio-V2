# Contributing to DJI LUT Studio

Thank you for your interest in contributing to **DJI LUT Studio**! We welcome bug reports, feature requests, documentation improvements, and pull requests.

## How to Contribute

### 1. Adding New DJI 3D LUTs
If DJI releases a new drone or camera model (or updated color profile .cube file):
1. Place the `.cube` file into the appropriate subfolder inside `lut_library/` (`drones/`, `action_pocket/`, `cinema/`, or `creative/`).
2. Update `lut_manifest.json` with the new device entry:
   - `id`: unique machine-readable identifier (e.g. `mavic_5_pro`)
   - `name`: user-facing name
   - `default_profile`: profile type (`D-Log M`, `D-Log`, `HLG`)
   - `filename_patterns`: substrings used for auto-detection
   - `luts`: array of available `.cube` files with `recommended: true/false`
3. Verify using:
   ```bash
   python3 -m dji_lut_studio.cli --list-luts
   ```

### 2. Modifying Grading Pipeline or Filters
The core engine is located at `dji_lut_studio/engine.py`.
- Filter graph logic is in `build_filter_chain()`.
- NVENC hardware acceleration parameters are configured in `grade_video()`.
- Please ensure tests pass and changes maintain 10-bit pipeline integrity (`yuv420p10le`).

### 3. Web & Desktop GUI Enhancements
- **Native Windows WPF GUI**: `gui/DJI_LUT_Studio_GUI.ps1`
- **Web Studio GUI**: `web/index.html`, `web/style.css`, `web/app.js`, and `dji_lut_studio/web_server.py`.
- Keep dependencies minimal: zero external Python package dependencies are required to run the Web GUI server (standard library only).

## Code Style
- Python code should adhere to PEP 8 standards.
- Maintain documentation and clean docstrings.
