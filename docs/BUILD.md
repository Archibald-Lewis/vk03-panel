# Building VK03 Panel

## Requirements

- macOS 26 and the macOS 26 SDK. The settings window uses `NSGlassEffectView`.
- Xcode or Command Line Tools providing `xcrun clang`, `swiftc`, `iconutil`, and `codesign`.
- Python 3.
- A libusb installation containing `include/libusb-1.0/libusb.h` and `lib/libusb-1.0.0.dylib`.

```bash
brew install libusb
bash build.sh
```

The script discovers libusb through `brew --prefix libusb`. To supply a different installation:

```bash
VK03_LIBUSB_PREFIX=/path/to/libusb bash build.sh
```

To put build products outside the checkout:

```bash
VK03_BUILD_DIR=/path/to/build bash build.sh
```

The app is ad-hoc signed and verified locally. It is not Developer ID signed or notarized. No release binary is automatically installed. Build on your target architecture; only x86_64 has been tested on hardware.

## Synthetic previews

These commands render documentation images without opening the USB device, reading Stats, starting the media helper, or changing system controls:

```bash
"build/VK03 Panel.app/Contents/MacOS/VK03Panel" --demo /tmp/vk03-dark.png --dark
"build/VK03 Panel.app/Contents/MacOS/VK03Panel" --demo /tmp/vk03-light.png
```

`--render` is a different diagnostic mode that reads the current user's actual Stats feed. Avoid sharing its output without checking it.

## Layout

- `src/`: native panel, renderer, USB transport, touch and system controls.
- `src/ParentWatch.m`: app-specific helper lifetime guard.
- `vendor/mediaremote-adapter/`: pinned source, with upstream license retained.
- `scripts/build.py`: builds the app, framework, dynamic USB library bundle and icon.
- `licenses/`: third-party notices distributed with the app.

The build does not require CMake, a prebuilt framework, a source checkout in a particular home directory, or a previously installed VK03 application.
