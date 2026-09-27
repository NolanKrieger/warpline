#!/bin/bash
# Export release builds for Linux and Windows, add the licence files, zip them into build/dist/.
# Usage: tools/build.sh            (needs Godot 4.7.2 mono export templates installed)
set -euo pipefail
cd "$(dirname "$0")/.."
version=$(grep '^config/version' project.godot | cut -d'"' -f2)
dotnet build Warpline.csproj -c ExportRelease -v q >/dev/null 2>&1 || dotnet build Warpline.csproj -v q
rm -rf build/linux build/windows build/dist
mkdir -p build/linux build/windows build/dist
godot --headless --path . --export-release "Linux" build/linux/Warpline.x86_64 >/dev/null 2>&1
godot --headless --path . --export-release "Windows" build/windows/Warpline.exe >/dev/null 2>&1
for d in build/linux build/windows; do
  mkdir -p "$d/licences"
  cp docs/third-party/GODOT-LICENSE.txt docs/third-party/GODOT-COPYRIGHT.txt assets/fonts/Noto-OFL.txt docs/LICENCES.md "$d/licences/"
done
# (python's zipfile: no extra tools needed)
python3 -c "import shutil,sys; shutil.make_archive(sys.argv[1], 'zip', sys.argv[2])" "build/dist/Warpline-$version-linux-x86_64" build/linux
python3 -c "import shutil,sys; shutil.make_archive(sys.argv[1], 'zip', sys.argv[2])" "build/dist/Warpline-$version-windows-x86_64" build/windows
ls -la build/dist
