# 🎬 DJI LUT Studio (Windows Open-Source Edition)

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20%7C%20WSL2-cyan.svg)](#)
[![Acceleration](https://img.shields.io/badge/Acceleration-NVIDIA%20NVENC%2010--Bit-brightgreen.svg)](#)
[![LUTs Loaded](https://img.shields.io/badge/LUTs%20Loaded-49%20Official%20%26%20Creative-orange.svg)](#)

**DJI LUT Studio** is an open-source, GPU-accelerated video color grading software application designed for Windows. It includes **all official DJI 3D conversion LUTs** alongside calibrated cinematic looks, giving creators a simple, professional, 1-click tool to grade flat 10-bit D-Log and D-Log M drone and action camera footage into rich, broadcast-grade Rec.709 video.

---

## ⚡ Highlights & Features

- **Complete Official DJI LUT Library (49 LUTs)**: Includes calibrated `.cube` conversion LUTs for O4 Air Unit, Avata 2, Mavic 3, Mavic 4 Pro, Mini 4/5 Pro, Air 3/3S, Osmo Pocket 3/4, Osmo Action 4/5/6, Zenmuse X7, Phantom 4, and official Master Creative Looks!
- **Dual Windows GUI Experiences**:
  1. **Native Windows Desktop GUI (WPF)**: A standalone desktop app written in modern dark-theme XAML with model dropdowns, custom LUT loader, file browse, and real-time progress. Zero installation needed on Windows!
  2. **Modern Web Studio GUI**: An interactive, glassmorphism web interface accessible locally in your browser with device cards, LUT descriptions, and split-screen previews.
- **Windows Desktop Drag & Drop**: Drag videos or folders directly onto the `DJI_LUT_Studio.bat` icon on your Windows Desktop to grade instantly.
- **NVIDIA RTX Hardware Acceleration**: Full 10-bit pipeline powered by NVIDIA NVENC (`hevc_nvenc`, `h264_nvenc`, and Apple ProRes 422 HQ).
- **Side-by-Side Comparison Previews**: Automatically generates a split comparison JPEG (`ORIGINAL (D-LOG)` vs `GRADED (Rec.709)`) for instant quality verification.
- **Intelligent Auto-Detection**: Analyzes video metadata and filenames to automatically select the matching drone/camera profile and recommended LUT.
- **Continuous Folder Watcher**: Monitored incoming folder mode (e.g. `D:\dji\incoming`) that automatically color grades footage as soon as you offload your SD card.

---

## 🚁 Supported DJI Cameras & LUT Catalog

| Category | Supported Devices | Available LUT Profiles |
|---|---|---|
| **Drones & Air Units** | **DJI O4 Pro / Air Unit** | Official D-Log M to Rec.709 V1 (Recommended) |
| | **DJI Avata 2** | Official Avata 2 D-Log M to Rec.709 V1 |
| | **DJI Avata 360** | Official Avata 360 D-Log M to Rec.709 |
| | **DJI Mavic 3 / Pro / Cine** | Official D-Log to Rec.709 (Standard & Vivid), D-Log M |
| | **DJI Mavic 4 Pro** | Official D-Log to Rec.709 (Standard & Vivid), D-Log M |
| | **DJI Air 3 & Air 3S** | Official Air 3 & Air 3S D-Log M to Rec.709 |
| | **DJI Mini 4 Pro & Mini 5 Pro**| Official Mini 4 & Mini 5 Pro D-Log M to Rec.709 |
| | **DJI Flip** | Official DJI Flip D-Log M to Rec.709 |
| **Action & Pocket** | **DJI Osmo Pocket 3** | Official D-Log M + Master Looks: Spring, Summer, Autumn, Winter Pro |
| | **DJI Osmo Pocket 4 / 4 Pro** | Official D-Log & D-Log2 to Rec.709 (Standard & Vivid) |
| | **DJI Osmo Action 4** | Official D-Log M + Action Looks: Ice Pro, Nature Pro, Forest Pro |
| | **DJI Osmo Action 5 Pro** | Official D-Log M + Master Looks: Mei, Ju, Zhu, Lan |
| | **DJI Osmo Action 6** | Official Action 6 D-Log M to Rec.709 |
| | **DJI Osmo 360 / Nano / Lito**| Official Osmo 360, Osmo Nano & Lito X1 D-Log M |
| **Cinema & Enterprise**| **DJI Zenmuse X7 / Ronin 4D** | Official Cinema D-Log to Rec.709, Linear to D-Log |
| | **DJI Phantom 4 / Pro** | Official Phantom 4 D-Log to Rec.709 & sRGB Improv |
| **Creative Film Styles**| **Universal Looks** | Vibrant Pop, Cinematic 35mm Film, Golden Hour, Teal & Orange, Clean Commercial, Moody Noir B&W, D-Cinelike Natural Booster |

---

## 🚀 How to Use on Windows

### Option 1: Native Windows Executable (`DJI_LUT_Studio.exe`) (Recommended)
1. Go to your Windows **Desktop**.
2. Double-click **`DJI_LUT_Studio.exe`**!
3. The native Windows desktop application will open:
   - Select your DJI Drone/Camera model from the dropdown.
   - Choose your preferred 3D LUT and post-LUT look style.
   - Drag and drop your video file (or browse for a clip/folder).
   - Click **`🚀 APPLY LUT & GRADE`**.
   - Watch the real-time progress bar, inspect the side-by-side preview, and click **📁 Open Folder** when complete!

### Option 2: Drag & Drop directly onto `DJI_LUT_Studio.exe`
- Select any DJI `.MP4` or `.MOV` video on your computer or SD card.
- Drag and drop it directly onto the **`DJI_LUT_Studio.exe`** icon on your Desktop!
- It will automatically launch with the video pre-loaded and auto-detect your camera model.

### Option 3: Modern Web Studio GUI
1. Double-click **`DJI_LUT_Web_Studio.bat`** on your Desktop.
2. Your browser will automatically launch at `http://localhost:8080`.
3. Pick your drone from the visual device cards, select a LUT, scan your clips, view instant side-by-side previews, and click **Apply LUT & Grade**.

### Option 4: Linux / WSL Terminal CLI
```bash
# List all 49 LUTs and device IDs
./grade.sh --list-luts

# Grade a video with auto-detection:
./grade.sh /mnt/d/dji/DJI_0001.MP4

# Grade with specific camera and LUT:
./grade.sh /mnt/d/dji/clip.mp4 -d mavic_3 -l m3_dlog_rec709_vivid --look cinematic

# Watch folder continuously:
./grade.sh -w /mnt/d/dji/incoming -o /mnt/d/dji/graded --look vibrant
```

---

## ⚙️ CLI Reference

```
python3 -m dji_lut_studio.cli [inputs...] [options]
```

| Flag | Description | Default |
|---|---|---|
| `inputs` | Video file(s) or folder(s) to process | Interactive if empty |
| `-d, --camera` | Camera/Drone ID (`o4`, `avata_2`, `mavic_3`, `pocket_3`, `action_5_pro`, etc.) | Auto-detect |
| `-l, --lut` | LUT identifier or direct path to any custom `.cube` file | Recommended for device |
| `--look` | Creative post-LUT style (`natural`, `vibrant`, `cinematic`, `golden`, `teal_orange`) | `natural` |
| `-c, --codec` | Video codec: `hevc` (10-bit NVENC), `h264`, `prores` (422 HQ) | `hevc` |
| `--cq` | Constant Quality factor (15-28; lower = higher bitrate & quality) | `19` |
| `--scale` | Output resolution: `keep`, `4k`, `2.7k`, `1080p` | `keep` |
| `--sharpen` | Apply subtle fine-detail unsharp mask | Off |
| `-p, --preview` | Generate comparison JPEG (`_preview.jpg`) | Enabled |
| `-o, --output` | Output folder directory | `graded/` next to clip |
| `-w, --watch` | Continuous directory watcher mode | Disabled |
| `--overwrite` | Overwrite existing output files | Skip existing |
| `--list-devices`| Print all supported DJI drones and cameras | - |
| `--list-luts` | Print full catalog of all 49 LUTs | - |

---

## 📂 Project Architecture

```
LUT-adder/
├── DJI_LUT_Studio.bat         # Main Windows launcher (Desktop shortcut)
├── DJI_LUT_Web_Studio.bat     # 1-Click browser GUI launcher
├── grade.sh                   # Universal Linux / WSL CLI script
├── lut_manifest.json          # Master database of 49 DJI LUTs & devices
├── lut_library/               # Official & Creative 3D LUTs (.cube files)
│   ├── drones/                # O4, Avata 2, Mavic 3/4, Air 3/3S, Mini 4/5, Flip
│   ├── action_pocket/         # Pocket 3/4, Action 4/5/6, Osmo 360, Nano, Lito
│   ├── cinema/                # Zenmuse X7, Phantom 4, Cinema Log
│   ├── creative/              # Master Looks (Action 5 Pro Mei/Ju, Pocket 3 seasons)
│   └── looks/                 # Vibrant Pop, Cinematic Film, Golden Hour, Teal & Orange
├── dji_lut_studio/            # Core Python grading engine & CLI
│   ├── __init__.py
│   ├── manifest.py            # Manifest loader & auto-detection engine
│   ├── engine.py              # NVENC 10-bit pipeline & tetrahedral interpolation
│   ├── cli.py                 # Full CLI & interactive terminal wizard
│   └── web_server.py          # Embedded local HTTP REST & SSE server
├── gui/
│   └── DJI_LUT_Studio_GUI.ps1 # Native Windows WPF Dark Theme Desktop GUI
├── web/                       # Modern Web Studio Single-Page App
│   ├── index.html             # Responsive UI with device cards & LUT selector
│   ├── style.css              # Dark glassmorphic stylesheet
│   └── app.js                 # Frontend application controller
├── LICENSE                    # Open Source MIT License
├── CONTRIBUTING.md            # Community contribution guide
└── README.md                  # Documentation
```

---

## 📚 Developer & Continuation Guide
Looking to resume development, understand the internal architecture, or build the native executable from source?  
See the full **[CONTINUATION_GUIDE.md](CONTINUATION_GUIDE.md)** for architecture diagrams, build instructions, and the roadmap.

---

## 📄 License
This project is licensed under the open-source **MIT License** - see the [LICENSE](LICENSE) file for details.
All official DJI 3D LUTs are property of SZ DJI Technology Co., Ltd.
