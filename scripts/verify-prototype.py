#!/usr/bin/env python3
"""Verify the package's exact inventory and hashes, then rebuild its extracted source."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import zipfile

from importlib.util import spec_from_file_location, module_from_spec
spec = spec_from_file_location("package_prototype", Path(__file__).with_name("package-prototype.py"))
package = module_from_spec(spec)
spec.loader.exec_module(package)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path)
    args = parser.parse_args()
    with tempfile.TemporaryDirectory(prefix="rimcontroller-zip-") as directory:
        with zipfile.ZipFile(args.archive) as archive:
            names = archive.namelist()
            prefix = package.PREFIX
            expected_source = (archive.read(prefix + "scripts/package-files.txt").decode().splitlines())
            expected = {prefix + n for n in expected_source if n and not n.startswith("#")}
            expected |= {prefix + "BUILD-STATUS.json", prefix + "SHA256SUMS.json"}
            if len(names) != len(set(names)) or set(names) != expected:
                raise ValueError("Unexpected archive inventory")
            if archive.testzip() is not None:
                raise ValueError("ZIP integrity failure")
            manifest = json.loads(archive.read(prefix + "SHA256SUMS.json"))
            if {prefix + n for n in manifest} != expected - {prefix + "SHA256SUMS.json"}:
                raise ValueError("Incomplete hash inventory")
            for name in names:
                relative = name.removeprefix(prefix)
                path = Path(relative)
                if not name.startswith(prefix) or path.is_absolute() or ".." in path.parts or "\\" in relative:
                    raise ValueError("Unsafe archive path")
                if relative in manifest and hashlib.sha256(archive.read(name)).hexdigest() != manifest[relative]:
                    raise ValueError(f"Hash mismatch: {relative}")
            archive.extractall(directory)
        root = Path(directory) / prefix
        package.read_files(root)
        packaged = {name: (root / name).read_bytes() for name in package.OWN_DLLS}
        subprocess.run(["bash", "scripts/build-managed.sh"], cwd=root, check=True)
        # Paths may be embedded in debug metadata; executing the extracted build/tests is the gate.
        for name, data in packaged.items():
            if not data or not (root / name).is_file():
                raise ValueError(f"Managed artifact missing: {name}")
        print("Verified exact inventory, all SHA-256 hashes, extraction, and extracted managed build/tests.")


if __name__ == "__main__":
    main()
