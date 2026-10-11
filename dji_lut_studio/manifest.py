"""
LUT Manifest and Catalog Manager for DJI LUT Studio.
"""

import json
from pathlib import Path
from typing import Dict, List, Optional, Tuple, Any

PKG_DIR = Path(__file__).resolve().parent
REPO_DIR = PKG_DIR.parent
MANIFEST_PATH = REPO_DIR / "lut_manifest.json"
LUT_LIBRARY_DIR = REPO_DIR / "lut_library"


class LUTManifest:
    def __init__(self, manifest_path: Optional[Path] = None, lut_dir: Optional[Path] = None):
        self.manifest_path = manifest_path or MANIFEST_PATH
        self.lut_dir = lut_dir or LUT_LIBRARY_DIR
        self.data: Dict[str, Any] = {}
        self.devices: Dict[str, Dict[str, Any]] = {}
        self.luts: Dict[str, Dict[str, Any]] = {}
        self.load()

    def load(self):
        if not self.manifest_path.exists():
            raise FileNotFoundError(f"Manifest not found at {self.manifest_path}")
        with open(self.manifest_path, "r", encoding="utf-8") as f:
            self.data = json.load(f)

        self.devices.clear()
        self.luts.clear()

        for category in self.data.get("categories", []):
            cat_id = category.get("id")
            for dev in category.get("devices", []):
                dev_id = dev.get("id")
                dev["category_id"] = cat_id
                self.devices[dev_id] = dev
                for lut in dev.get("luts", []):
                    lut_id = lut.get("id")
                    lut["device_id"] = dev_id
                    lut["device_name"] = dev.get("name")
                    lut["category_id"] = cat_id
                    lut["full_path"] = str(self.lut_dir / lut.get("file", ""))
                    self.luts[lut_id] = lut

    def get_categories(self) -> List[Dict[str, Any]]:
        return self.data.get("categories", [])

    def get_all_devices(self) -> List[Dict[str, Any]]:
        return list(self.devices.values())

    def get_device(self, device_id: str) -> Optional[Dict[str, Any]]:
        return self.devices.get(device_id)

    def get_lut(self, lut_id: str) -> Optional[Dict[str, Any]]:
        return self.luts.get(lut_id)

    def get_all_luts(self) -> List[Dict[str, Any]]:
        return list(self.luts.values())

    def get_luts_for_device(self, device_id: str) -> List[Dict[str, Any]]:
        dev = self.get_device(device_id)
        if not dev:
            return []
        return dev.get("luts", [])

    def get_creative_looks(self) -> List[Dict[str, Any]]:
        return [lut for lut in self.luts.values() if lut.get("type") == "look"]

    def resolve_lut_file(self, identifier: str) -> Path:
        """Resolves an ID, relative path, or absolute path to a verified .cube file Path."""
        # 1. Direct LUT ID match
        if identifier in self.luts:
            p = Path(self.luts[identifier]["full_path"])
            if p.exists():
                return p

        # 2. Check if it's already an existing file path
        candidate = Path(identifier)
        if candidate.is_file():
            return candidate.resolve()

        # 3. Check relative to lut_dir
        rel_candidate = self.lut_dir / identifier
        if rel_candidate.is_file():
            return rel_candidate.resolve()

        # 4. Search recursively inside lut_dir by filename
        for p in self.lut_dir.rglob("*.cube"):
            if p.name.lower() == candidate.name.lower() or p.stem.lower() == candidate.stem.lower():
                return p.resolve()

        raise FileNotFoundError(f"Could not resolve LUT '{identifier}' in {self.lut_dir}")

    def detect_device_and_lut(self, filename: str, metadata: Optional[Dict[str, Any]] = None) -> Tuple[Optional[str], Optional[str]]:
        """
        Auto-detects the most suitable DJI device and LUT based on filename patterns
        and video stream metadata.
        """
        fn_upper = filename.upper()

        # Check explicit device patterns
        best_dev_id = None
        for dev_id, dev in self.devices.items():
            for pat in dev.get("filename_patterns", []):
                if pat.upper() in fn_upper:
                    best_dev_id = dev_id
                    break
            if best_dev_id:
                break

        # Fallback heuristic checks
        if not best_dev_id:
            if "O4" in fn_upper:
                best_dev_id = "o4"
            elif "AVATA" in fn_upper:
                best_dev_id = "avata_2"
            elif "ACTION" in fn_upper:
                best_dev_id = "action_5_pro"
            elif "POCKET" in fn_upper:
                best_dev_id = "pocket_3"
            elif "MAVIC" in fn_upper:
                best_dev_id = "mavic_3"
            elif "AIR" in fn_upper:
                best_dev_id = "air_3_3s"
            elif "MINI" in fn_upper:
                best_dev_id = "mini_4_5_pro"
            elif fn_upper.endswith("_D") or "_D." in fn_upper:
                best_dev_id = "o4"  # Default O4 / D-Log M
            else:
                best_dev_id = "generic_dji"

        # Find recommended LUT for this device
        recommended_lut_id = None
        dev = self.get_device(best_dev_id)
        if dev:
            for lut in dev.get("luts", []):
                if lut.get("recommended"):
                    recommended_lut_id = lut.get("id")
                    break
            if not recommended_lut_id and dev.get("luts"):
                recommended_lut_id = dev["luts"][0].get("id")

        return best_dev_id, recommended_lut_id


# Global singleton instance
manifest = LUTManifest()
