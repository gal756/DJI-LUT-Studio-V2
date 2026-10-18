# Third-Party Licenses & Legal Notices

DJI LUT Studio is an open-source project licensed under the [MIT License](LICENSE).  
This application interfaces with and references third-party tools, trademarks, and colorimetric data. Below are the corresponding legal notices and licenses.

---

## 1. Trademarks & Brand Disclaimer (SZ DJI Technology Co., Ltd.)

- **DJI LUT Studio** is an **independent, community-driven open-source software project**.
- It is **NOT** affiliated with, endorsed by, sponsored by, or associated in any way with **SZ DJI Technology Co., Ltd.** or any of its subsidiaries or affiliates.
- **"DJI"**, **"Osmo"**, **"Mavic"**, **"Avata"**, **"Zenmuse"**, **"Phantom"**, **"D-Log"**, **"D-Log M"**, **"RockSteady"**, **"HorizonSteady"**, and all associated logos, camera model designations, and product names are registered trademarks of **SZ DJI Technology Co., Ltd.**
- Any use of these trademarks is solely for **nominative, descriptive, and identification purposes** to describe device compatibility and camera color spaces.

---

## 2. 3D LUT Color Profiles (.cube Files)

- The official DJI 3D LUT color conversion tables (`.cube` files) located in `lut_library/` are the proprietary colorimetric works of **SZ DJI Technology Co., Ltd.**
- DJI makes these LUT profiles publicly available at no charge on the official DJI website ([https://www.dji.com](https://www.dji.com)) for users to grade footage captured by DJI equipment.
- These profiles are included in this repository strictly for **interoperability, educational, and workflow convenience under Fair Use principles**.
- The open-source MIT License of this software applies strictly to the source code of DJI LUT Studio and **does not claim ownership or re-license** any official DJI color transformation matrices.

---

## 3. FFmpeg (Video Encoding & Filtering Engine)

- This software utilizes **FFmpeg** ([https://ffmpeg.org](https://ffmpeg.org)) for video decoding, tetrahedral 3D LUT filtering, and hardware-accelerated video encoding (NVENC, AMF, QSV).
- FFmpeg is licensed under the **GNU Lesser General Public License (LGPL) version 2.1 or later**, or the **GNU General Public License (GPL) version 2 or later** (depending on build flags).
- FFmpeg is a trademark of Fabrice Bellard and the FFmpeg team.
- DJI LUT Studio interacts with FFmpeg exclusively via standard command-line subprocess execution as an independent tool.
- Full FFmpeg source code and build instructions can be found at [https://ffmpeg.org/download.html](https://ffmpeg.org/download.html).

---

## 4. Gyroflow (Video Stabilization Engine)

- This software provides launcher integration with **Gyroflow** ([https://gyroflow.xyz](https://gyroflow.xyz)), an open-source video stabilization application based on camera gyro telemetry.
- Gyroflow is created by **Elvin Chen and contributors** and is licensed under the **GNU General Public License v3.0 (GPLv3)**.
- DJI LUT Studio interacts with Gyroflow strictly via command-line invocation and process launching, maintaining complete separation between the applications.
- Gyroflow source code is available at [https://github.com/gyroflow/gyroflow](https://github.com/gyroflow/gyroflow).

---

## 5. Application License (MIT License)

```
MIT License

Copyright (c) 2026 DJI LUT Studio Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
