#!/usr/bin/env python3
"""Build and package only the explicit development-package allowlist. Python 3.9+."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import subprocess
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parent.parent
PREFIX = "RimController-Mac-Prototyp/"
OWN_DLLS = {"mod/1.6/Assemblies/RimController.Core.dll", "mod/1.6/Assemblies/RimController.Mod.dll"}


def read_files(root):
    names = (root / "scripts/package-files.txt").read_text(encoding="utf-8").splitlines()
    names = [name for name in names if name and not name.startswith("#")]
    if len(set(names)) != len(names):
        raise ValueError("Duplicate package entries")
    for name in names:
        path = PurePosixPath(name)
        if path.is_absolute() or ".." in path.parts or "\\" in name or str(path) != name:
            raise ValueError(f"Unsafe package entry: {name}")
        if any(part in {".git", "bin", "obj", ".nuget", "artifacts"} for part in path.parts):
            raise ValueError(f"Excluded directory: {name}")
        if path.suffix.lower() in {".dll", ".dylib", ".exe", ".pdb"} and name not in OWN_DLLS:
            raise ValueError(f"Unapproved binary: {name}")
        target = root / name
        if not target.is_file() or any(p.is_symlink() for p in [target, *target.parents]):
            raise ValueError(f"Missing or symlinked file: {name}")
    if not OWN_DLLS.issubset(names):
        raise ValueError("Own managed DLLs missing from allowlist")
    return sorted(names)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=Path(os.environ.get("WORKSPACE_ROOT", str(ROOT))) / "output" / "RimController-Mac-Prototyp.zip")
    args = parser.parse_args()
    # A package is published only after this invocation's managed build/test gate passes.
    subprocess.run(["bash", "scripts/build-managed.sh"], cwd=ROOT, check=True)
    names = read_files(ROOT)
    contents = {name: (ROOT / name).read_bytes() for name in names}
    status = {
        "formatVersion": 1,
        "packageKind": "Mac build prototype; not a complete or game-accepted mod",
        "createdUtc": datetime.now(timezone.utc).isoformat(),
        "dotnetSdk": subprocess.check_output(["dotnet", "--version"], cwd=ROOT, text=True).strip(),
        "managedBuildAndTests": "passed: bash scripts/build-managed.sh",
        "nativeBuildSigningAndGameAcceptance": "not verified; build and test separately on macOS",
        "referenceControllers": ["DualSense", "DualShock 4"],
    }
    contents["BUILD-STATUS.json"] = (json.dumps(status, ensure_ascii=False, indent=2) + "\n").encode()
    manifest = {name: hashlib.sha256(data).hexdigest() for name, data in sorted(contents.items())}
    # Manifest lists every other file; no circular self-hash.
    contents["SHA256SUMS.json"] = (json.dumps(manifest, indent=2) + "\n").encode()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(dir=args.output.parent, suffix=".zip", delete=False) as temp:
        staging = Path(temp.name)
    try:
        with zipfile.ZipFile(staging, "w", compression=zipfile.ZIP_DEFLATED) as archive:
            for name, data in sorted(contents.items()):
                info = zipfile.ZipInfo(PREFIX + name, date_time=(2026, 1, 1, 0, 0, 0))
                info.create_system = 3
                info.external_attr = (0o100644 << 16)
                info.compress_type = zipfile.ZIP_DEFLATED
                archive.writestr(info, data)
        with zipfile.ZipFile(staging) as archive:
            if archive.testzip() is not None:
                raise ValueError("ZIP integrity check failed")
            for name, digest in manifest.items():
                if hashlib.sha256(archive.read(PREFIX + name)).hexdigest() != digest:
                    raise ValueError(f"Hash mismatch: {name}")
        staging.replace(args.output)
    finally:
        staging.unlink(missing_ok=True)
    print(f"Created {args.output} ({len(contents)} files)")


if __name__ == "__main__":
    main()
