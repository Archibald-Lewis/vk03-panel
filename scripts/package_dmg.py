#!/usr/bin/env python3
"""Create a drag-to-Applications disk image from an already signed app."""
import argparse
from pathlib import Path
import shutil
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--app", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--name", required=True)
    parser.add_argument("--include", type=Path, action="append", default=[])
    args = parser.parse_args()
    if args.output.exists():
        raise SystemExit("Output already exists; choose a new filename.")
    subprocess.run(["codesign", "--verify", "--deep", "--strict", str(args.app)], check=True)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="macos-dmg-") as temp:
        stage = Path(temp)
        subprocess.run(["ditto", "--norsrc", "--noextattr", "--noqtn", str(args.app), str(stage / args.app.name)], check=True)
        (stage / "Applications").symlink_to("/Applications")
        for item in args.include:
            target = stage / item.name
            if item.is_dir():
                shutil.copytree(item, target, symlinks=True)
            else:
                shutil.copy2(item, target)
        subprocess.run(["hdiutil", "create", "-srcfolder", str(stage), "-volname", args.name,
                        "-format", "UDZO", str(args.output.resolve())], check=True)
    subprocess.run(["hdiutil", "verify", str(args.output)], check=True)


if __name__ == "__main__":
    main()
