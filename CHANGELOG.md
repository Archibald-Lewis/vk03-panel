# Changelog

## 2026-11-02 — Simplify settings and refine immersive playback

- Remove frame counters and render timings from the settings sidebar and display preview; retain connection and error information.
- Remove the Immersive theme-switch control and its touch target; theme selection remains available in settings and the tray.
- Enlarge artwork, soften its cached background, replace gold decoration with neutral controls, and allow two title lines.
- Hide an unavailable timeline and show volume numbers only during interaction.
- Verify the existing 72 tests and offline renders of dark/light, long-title, missing-artwork and settings layouts.

## 2026-11-01 — Instrument and immersive playback themes

- Add Nebula instruments and a cover-led Immersive now-playing theme, including dark and light appearances.
- Add touch theme cycling, recent load peaks and timeline seeking when the media player supports it.
- Add configurable idle NetEase playlist playback while preserving existing media-session priority.
- Store only a numeric playlist ID locally and debounce repeated client startup requests.
- Verify 72 unit and rendering tests, the installed NetEase 3.1 protocol parser, and physical playlist startup / playback.
- Detach decoded album-art pixels from their loader-thread decoder before sharing them with rendering threads; cover the real-image cross-thread regression.


## 2026-11-01 — Windows theme and sampling update

- Redesign the Windows app macOS theme around Control Center-style translucent modules, capsule volume controls and monochrome playback buttons.
- Sample hardware metrics on a monotonic schedule independently of touch, volume and lock-progress redraws.
- Keep all three history graphs at the configured 1 or 2 Hz cadence, including while holding the lock button.
- Add regression tests for rapid redraws, refresh-rate changes, sensor failures and touch targets in both themes (37 tests passing).


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
