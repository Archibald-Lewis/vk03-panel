#!/bin/bash
set -euo pipefail
vk03_repo_dir="$(cd "$(dirname "$0")" && pwd)"
exec python3 "$vk03_repo_dir/scripts/build.py" "$@"
