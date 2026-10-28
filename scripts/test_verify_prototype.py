#!/usr/bin/env python3
"""Scoped regression tests: python3 -m unittest discover -s scripts -p test_verify_prototype.py."""
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

spec = importlib.util.spec_from_file_location("verifier", Path(__file__).with_name("verify-prototype.py"))
verifier = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verifier)


class VerifierTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def make_archive(self, build_script=b"echo trusted"):
        names = ["scripts/build-managed.sh", "scripts/package-files.txt", *verifier.package.OWN_DLLS]
        contents = {
            "scripts/build-managed.sh": build_script,
            "scripts/package-files.txt": "\n".join(names).encode(),
            "BUILD-STATUS.json": b"{}",
            **{name: b"packaged DLL" for name in verifier.package.OWN_DLLS},
        }
        contents["SHA256SUMS.json"] = json.dumps({
            name: hashlib.sha256(data).hexdigest() for name, data in contents.items()
        }).encode()
        path = self.root / "package.zip"
        with zipfile.ZipFile(path, "w") as archive:
            for name, data in contents.items():
                archive.writestr(verifier.package.PREFIX + name, data)
        return path

    def test_rewritten_script_and_self_consistent_manifest_fail_before_extraction_or_execution(self):
        path = self.make_archive()
        trusted_digest = hashlib.sha256(path.read_bytes()).hexdigest()
        self.make_archive(b"echo malicious")  # Also updates the archive's own manifest.
        with patch.object(verifier.zipfile.ZipFile, "extractall") as extract, \
                patch.object(verifier.subprocess, "run") as run, \
                patch.object(verifier.tempfile, "TemporaryDirectory") as temp:
            with self.assertRaisesRegex(ValueError, "authentication failed"):
                verifier.verify_archive(path, trusted_digest)
            extract.assert_not_called()
            run.assert_not_called()
            temp.assert_not_called()

    def test_missing_or_malformed_trust_is_rejected_before_reading_archive(self):
        path = self.root / "absent.zip"
        for digest in ("", "0" * 63, "g" * 64):
            with self.subTest(digest=digest), self.assertRaisesRegex(ValueError, "trusted SHA-256"):
                verifier.verify_archive(path, digest)

    def test_parses_authenticated_bytes_even_if_archive_path_is_replaced(self):
        path = self.make_archive()
        before = path.read_bytes()
        authenticated = verifier.authenticate_archive(path, hashlib.sha256(before).hexdigest().upper())
        path.write_bytes(b"replacement")
        self.assertEqual(before, authenticated.getvalue())

    def test_authenticated_matching_build_passes_and_mismatched_dll_fails(self):
        for matched in (True, False):
            with self.subTest(matched=matched):
                path = self.make_archive()
                digest = hashlib.sha256(path.read_bytes()).hexdigest()

                def build(command, cwd, check):
                    self.assertEqual(["bash", "scripts/build-managed.sh"], command)
                    self.assertTrue(check)
                    for name, output in verifier.BUILD_OUTPUTS.items():
                        target = cwd / output
                        target.parent.mkdir(parents=True, exist_ok=True)
                        target.write_bytes(b"packaged DLL" if matched else b"different DLL")
                        # The packaging destination's existence alone must never suffice.
                        (cwd / name).write_bytes(target.read_bytes())

                with patch.object(verifier.subprocess, "run", side_effect=build) as run:
                    if matched:
                        verifier.verify_archive(path, digest)
                    else:
                        with self.assertRaisesRegex(ValueError, "differs from verified build"):
                            verifier.verify_archive(path, digest)
                    run.assert_called_once()

    def test_missing_empty_or_changed_artifacts_are_rejected_for_each_dll(self):
        original = {name: b"original" for name in verifier.BUILD_OUTPUTS}
        for name, output in verifier.BUILD_OUTPUTS.items():
            target = self.root / output
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(original[name])
            destination = self.root / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(original[name])
        verifier.verify_artifacts(self.root, original)
        for name, output in verifier.BUILD_OUTPUTS.items():
            with self.subTest(name=name):
                target = self.root / output
                target.write_bytes(b"modified")  # Same length, different content.
                with self.assertRaisesRegex(ValueError, "differs"):
                    verifier.verify_artifacts(self.root, original)
                target.unlink()
                with self.assertRaisesRegex(ValueError, "missing"):
                    verifier.verify_artifacts(self.root, original)
                target.write_bytes(original[name])
                with self.assertRaisesRegex(ValueError, "missing"):
                    verifier.verify_artifacts(self.root, {**original, name: b""})
                (self.root / name).unlink()
                with self.assertRaisesRegex(ValueError, "missing"):
                    verifier.verify_artifacts(self.root, original)
                (self.root / name).write_bytes(original[name])


if __name__ == "__main__":
    unittest.main()
