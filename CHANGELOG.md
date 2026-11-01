# Changelog

## 2026-11-01 — Windows support

- Add the Windows .NET / WPF implementation under `windows/`, preserving the macOS source layout.
- Keep idle USB touch reads connected instead of reconnecting on timeout; fix a duplicate handle close.
- Add a Task Manager-style hardware overview, a centered clock sidebar and media / volume controls.
- Verify 29 unit tests, source builds and publishing on Windows; verify touch input on the physical panel.
- Rename the GitHub repository to `vk03-panel` to cover both platforms.


## 1.2.0 — source release

- Native monitoring panel and touch controls for the verified VK03 / MCVKC03 hardware.
- Larger touch targets, USB touch fallback and reconnect support.
- Now Playing artwork fallback and optional login startup.
- Source-only build that creates its own app bundle and media helper.
- Synthetic light/dark previews, dependency notices and build documentation.

The source-only release has not replaced the installed hardware-tested app. The refreshed build was compiled and smoke-tested locally; physical display regression remains unverified.
