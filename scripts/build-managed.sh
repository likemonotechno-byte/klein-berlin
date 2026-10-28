#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo_root"
dotnet restore RimController.sln --locked-mode
dotnet build RimController.sln --no-restore -c Release
dotnet test tests/RimController.Tests --no-build --no-restore -c Release
mkdir -p mod/1.6/Assemblies
# Explicit allowlist: never ship reference assemblies, framework or game libraries.
cp src/RimController.Mod/bin/Release/net472/RimController.Mod.dll mod/1.6/Assemblies/
cp src/RimController.Core/bin/Release/netstandard2.0/RimController.Core.dll mod/1.6/Assemblies/
