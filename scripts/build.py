#!/usr/bin/env python3
"""Build a self-contained app from source without using an installed bundle."""
import os
from pathlib import Path
import plistlib
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
BUILD = Path(os.environ.get("VK03_BUILD_DIR", str(ROOT / "build"))).resolve()
APP = BUILD / "VK03 Panel.app"


def run(*args):
    subprocess.run([str(arg) for arg in args], check=True)


def libusb_prefix():
    override = os.environ.get("VK03_LIBUSB_PREFIX")
    candidates = [Path(override)] if override else []
    if not override and shutil.which("brew"):
        result = subprocess.run(["brew", "--prefix", "libusb"], capture_output=True, text=True)
        if result.returncode == 0:
            candidates.append(Path(result.stdout.strip()))
    if not override:
        candidates += [Path("/opt/homebrew/opt/libusb"), Path("/usr/local/opt/libusb")]
    for prefix in candidates:
        if (prefix / "include/libusb-1.0/libusb.h").is_file() and (prefix / "lib/libusb-1.0.0.dylib").is_file():
            return prefix.resolve()
    raise RuntimeError("Install libusb with `brew install libusb`, or set VK03_LIBUSB_PREFIX.")


def write_plist(path, values):
    path.write_bytes(plistlib.dumps(values))


def main():
    if sys.platform != "darwin":
        raise RuntimeError("VK03 Panel must be built on macOS with the macOS 26 SDK.")
    usb = libusb_prefix()
    sdk = subprocess.check_output(["xcrun", "--sdk", "macosx", "--show-sdk-path"], text=True).strip()
    BUILD.mkdir(parents=True, exist_ok=True)
    # Only replace our generated bundle, never /Applications or a running app.
    if APP.exists():
        shutil.rmtree(APP)
    contents = APP / "Contents"
    for name in ["MacOS", "Resources", "Frameworks"]:
        (contents / name).mkdir(parents=True)

    source = ROOT / "src"
    modules = BUILD / "CUSB"
    modules.mkdir(exist_ok=True)
    (modules / "module.modulemap").write_text(
        'module CUSB [system] {\n  header "shim.h"\n  export *\n}\n')
    (modules / "shim.h").write_text('#include <libusb.h>\n')
    run("xcrun", "clang", "-O2", "-fobjc-arc", "-isysroot", sdk,
        "-mmacosx-version-min=26.0", "-c", source / "SystemBridge.m", "-o", BUILD / "SystemBridge.o")
    swift_sources = [
        "Transport.swift", "Metrics.swift", "SystemState.swift", "Artwork.swift",
        "MediaBridge.swift", "RawTouch.swift", "Touch.swift", "LoginStartup.swift",
        "Renderer.swift", "Application.swift", "main.swift",
    ]
    executable = contents / "MacOS/VK03Panel"
    run("xcrun", "swiftc", "-O", "-sdk", sdk, "-I", modules,
        "-Xcc", "-I" + str(usb / "include/libusb-1.0"), "-L", usb / "lib", "-lusb-1.0",
        "-framework", "IOKit", "-framework", "AppKit", "-framework", "CoreText",
        "-framework", "ImageIO", "-framework", "CoreAudio",
        "-import-objc-header", source / "SystemBridge.h",
        *[source / name for name in swift_sources], BUILD / "SystemBridge.o", "-o", executable)

    bundled_usb = contents / "Frameworks/libusb-1.0.0.dylib"
    shutil.copy2(usb / "lib/libusb-1.0.0.dylib", bundled_usb)
    deps = subprocess.check_output(["otool", "-L", str(executable)], text=True).splitlines()[1:]
    usb_deps = [line.strip().split(" (", 1)[0] for line in deps if "libusb-1.0" in line]
    if len(usb_deps) != 1:
        raise RuntimeError("Expected exactly one libusb dependency in the executable.")
    run("install_name_tool", "-change", usb_deps[0],
        "@executable_path/../Frameworks/libusb-1.0.0.dylib", executable)
    run("install_name_tool", "-id", "@rpath/libusb-1.0.0.dylib", bundled_usb)

    adapter = ROOT / "vendor/mediaremote-adapter"
    framework = contents / "Frameworks/MediaRemoteAdapter.framework"
    version = framework / "Versions/A"
    resources = version / "Resources"
    resources.mkdir(parents=True)
    (framework / "Versions/Current").symlink_to("A")
    (framework / "MediaRemoteAdapter").symlink_to("Versions/Current/MediaRemoteAdapter")
    (framework / "Resources").symlink_to("Versions/Current/Resources")
    adapter_sources = sorted((adapter / "src/adapter").glob("*.m"))
    adapter_sources += sorted((adapter / "src/private").glob("*.m"))
    adapter_sources += sorted((adapter / "src/utility").glob("*.m"))
    run("xcrun", "clang", "-O2", "-dynamiclib", "-fobjc-arc", "-fblocks",
        "-isysroot", sdk, "-mmacosx-version-min=26.0",
        "-I", adapter / "include", "-I", adapter / "src",
        "-framework", "Foundation", "-framework", "AppKit", "-framework", "UniformTypeIdentifiers",
        "-install_name", "@rpath/MediaRemoteAdapter.framework/Versions/A/MediaRemoteAdapter",
        *adapter_sources, source / "ParentWatch.m", "-o", version / "MediaRemoteAdapter")
    write_plist(resources / "Info.plist", {
        "CFBundleIdentifier": "com.vandenbe.MediaRemoteAdapter",
        "CFBundleExecutable": "MediaRemoteAdapter", "CFBundlePackageType": "FMWK",
        "CFBundleVersion": "0.1.0", "CFBundleShortVersionString": "0.1.0",
    })
    shutil.copy2(adapter / "bin/mediaremote-adapter.pl", contents / "Resources")
    shutil.copy2(ROOT / "licenses/MediaRemoteAdapter-BSD-3-Clause.txt", contents / "Resources")
    shutil.copy2(ROOT / "licenses/libusb-LGPL-2.1.txt", contents / "Resources")
    shutil.copy2(ROOT / "LICENSE", contents / "Resources/VK03Panel-MIT.txt")

    iconset = BUILD / "VK03Panel.iconset"
    iconset.mkdir(exist_ok=True)
    run("xcrun", "swiftc", "-O", source / "Icon.swift", "-o", BUILD / "make-icon")
    run(BUILD / "make-icon", iconset)
    run("iconutil", "-c", "icns", iconset, "-o", contents / "Resources/VK03Panel.icns")
    write_plist(contents / "Info.plist", {
        "CFBundleDisplayName": "VK03 副屏监控", "CFBundleExecutable": "VK03Panel",
        "CFBundleIconFile": "VK03Panel", "CFBundleIdentifier": "local.codex.VK03Panel",
        "CFBundleName": "VK03 Panel", "CFBundlePackageType": "APPL",
        "CFBundleShortVersionString": "1.2.1", "CFBundleVersion": "4",
        "LSMinimumSystemVersion": "26.0", "LSUIElement": True,
        "NSHighResolutionCapable": True, "NSPrincipalClass": "NSApplication",
    })
    run("codesign", "--force", "--sign", "-", bundled_usb)
    run("codesign", "--force", "--sign", "-", framework)
    run("codesign", "--force", "--deep", "--sign", "-", APP)
    run("codesign", "--verify", "--deep", "--strict", APP)
    print("Built:", APP)


if __name__ == "__main__":
    try:
        main()
    except (RuntimeError, subprocess.CalledProcessError) as error:
        raise SystemExit(str(error))
