#!/usr/bin/env bash
set -euo pipefail
if [[ "$(uname -s)" != Darwin ]]; then
  echo 'Native bridge requires macOS 14+ and Xcode Command Line Tools.' >&2
  exit 1
fi
repo_root="$(cd "$(dirname "$0")/.." && pwd)"
build_dir="$repo_root/artifacts/native"
mkdir -p "$build_dir" "$repo_root/mod/Native/macOS"
# Universal output follows the architecture of RimWorld's process (including Rosetta).
xcrun clang -dynamiclib -fobjc-arc -fvisibility=hidden -Wall -Wextra -Werror \
  -mmacosx-version-min=14.0 -arch arm64 -arch x86_64 \
  -framework Foundation -framework GameController \
  "$repo_root/native/rimcontroller.m" -o "$build_dir/librimcontroller.dylib"
xcrun lipo -verify_arch arm64 x86_64 "$build_dir/librimcontroller.dylib"
# Local development signature; public distribution still requires its Gatekeeper acceptance test.
codesign --force --sign "${RC_SIGN_IDENTITY:--}" "$build_dir/librimcontroller.dylib"
codesign --verify --strict "$build_dir/librimcontroller.dylib"
cp "$build_dir/librimcontroller.dylib" "$repo_root/mod/Native/macOS/"
