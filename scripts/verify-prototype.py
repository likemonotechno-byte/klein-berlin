#!/usr/bin/env python3
"""Verify the package's exact inventory and hashes, then rebuild its extracted source."""
import argparse
import hashlib
import hmac
import io
import json
import re
from pathlib import Path
import subprocess
import tempfile
import zipfile

from importlib.util import spec_from_file_location, module_from_spec
spec = spec_from_file_location("package_prototype", Path(__file__).with_name("package-prototype.py"))
package = module_from_spec(spec)
spec.loader.exec_module(package)


BUILD_OUTPUTS = {
    "mod/1.6/Assemblies/RimController.Core.dll": "src/RimController.Core/bin/Release/netstandard2.0/RimController.Core.dll",
    "mod/1.6/Assemblies/RimController.Mod.dll": "src/RimController.Mod/bin/Release/net472/RimController.Mod.dll",
}


def authenticate_archive(path, trusted_sha256):
    # The caller must obtain this digest independently, never from the ZIP or an untrusted sidecar.
    if not re.fullmatch(r"[0-9a-fA-F]{64}", trusted_sha256):
        raise ValueError("A trusted SHA-256 digest is required; rebuild from trusted local source if unavailable")
    data = path.read_bytes()
    if not hmac.compare_digest(hashlib.sha256(data).hexdigest(), trusted_sha256.lower()):
        raise ValueError("Archive authentication failed; rebuild from trusted local source")
    # Parse/extract these exact authenticated bytes, even if the path is replaced afterward.
    return io.BytesIO(data)


def verify_artifacts(root, packaged):
    for name, data in packaged.items():
        output = root / BUILD_OUTPUTS[name]
        if not data or not output.is_file() or not (root / name).is_file():
            raise ValueError(f"Managed artifact missing: {name}")
        # Directory.Build.props normalizes build-root metadata at compile time. Compare every byte;
        # do not mask assembly IDs or executable sections, which could hide substituted code.
        if data != output.read_bytes():
            raise ValueError(f"Packaged DLL differs from verified build output: {name}")


def verify_archive(path, trusted_sha256):
    authenticated = authenticate_archive(path, trusted_sha256)
    with tempfile.TemporaryDirectory(prefix="rimcontroller-zip-") as directory:
        with zipfile.ZipFile(authenticated) as archive:
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
        verify_artifacts(root, packaged)
        print("Verified trusted archive digest, exact inventory, all hashes, extracted build/tests, and matching DLL bytes.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path)
    parser.add_argument("--trusted-sha256", required=True,
                        help="Archive SHA-256 obtained independently from a trusted publisher or trusted local build")
    args = parser.parse_args()
    verify_archive(args.archive, args.trusted_sha256)


if __name__ == "__main__":
    main()
