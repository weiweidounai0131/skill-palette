#!/bin/zsh
set -euo pipefail

cd "$(dirname "$0")"

APP_NAME="Skill Palette"
BUILD_DIR="build"
APP_DIR="$BUILD_DIR/$APP_NAME.app"
CONTENTS_DIR="$APP_DIR/Contents"
MACOS_DIR="$CONTENTS_DIR/MacOS"
RESOURCES_DIR="$CONTENTS_DIR/Resources"
SDKROOT="$(xcrun --show-sdk-path)"

# Some macOS installations have the Swift 5.10 compiler beside a newer 15.x
# SDK. Those versions cannot compile against each other. Prefer a compatible
# installed 14.x SDK automatically, while keeping the script portable to a
# full Xcode installation where the default SDK is already compatible.
if swift --version | grep -q "Apple Swift version 5.10" && [[ "$SDKROOT" == *"MacOSX15"* ]]; then
  for candidate in \
    /Library/Developer/CommandLineTools/SDKs/MacOSX14.5.sdk \
    /Library/Developer/CommandLineTools/SDKs/MacOSX14.4.sdk; do
    if [[ -d "$candidate" ]]; then
      SDKROOT="$candidate"
      break
    fi
  done
fi

rm -rf "$BUILD_DIR"
mkdir -p "$MACOS_DIR" "$RESOURCES_DIR"

swiftc \
  -target arm64-apple-macos13.0 \
  -O \
  -parse-as-library \
  -sdk "$SDKROOT" \
  -framework AppKit \
  -framework SwiftUI \
  -framework ApplicationServices \
  -o "$MACOS_DIR/$APP_NAME" \
  Sources/*.swift

cp Resources/Info.plist "$CONTENTS_DIR/Info.plist"
cp Resources/AppIcon.icns "$RESOURCES_DIR/AppIcon.icns"
codesign --force --sign - "$APP_DIR"

echo "Built: $APP_DIR"
