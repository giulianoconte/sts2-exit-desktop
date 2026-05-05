#!/bin/bash
set -ex
SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )

RELEASE=false
for arg in "$@"; do
    [[ "$arg" == "--release" ]] && RELEASE=true
done

if $RELEASE; then
    DIST_DIR=$SCRIPT_DIR/ExitToDesktop/dist
    dotnet publish $SCRIPT_DIR/ExitToDesktop/ExitToDesktop.csproj --nologo -v quiet -c Release /p:DeployToMods=true /p:ModsPath=$DIST_DIR/
    VERSION=$(python3 -c "import json; print(json.load(open('$SCRIPT_DIR/ExitToDesktop/ExitToDesktop.json'))['version'])")
    ARCHIVE=$SCRIPT_DIR/ExitToDesktop-${VERSION}.zip
    rm -f "$ARCHIVE"
    (cd "$DIST_DIR" && zip -r "$ARCHIVE" ExitToDesktop)
    echo "Release archive: $ARCHIVE"
else
    dotnet build $SCRIPT_DIR/ExitToDesktop/ExitToDesktop.csproj --nologo -v quiet /p:DeployToMods=true
fi
