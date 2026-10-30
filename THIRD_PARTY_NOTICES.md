# Third-party sources and licenses

| Component | Source | License / use |
|---|---|---|
| Native VK03 application and build integration | `src/`, `scripts/` | MIT, see `LICENSE` |
| MediaRemoteAdapter | https://github.com/ungive/mediaremote-adapter | BSD 3-Clause; source commit `e3ff5021eb0875858bd05f48d2e9ba2e962d1cf6` |
| libusb | https://libusb.info/ | LGPL-2.1-or-later; installed separately, dynamically linked and bundled by the build |
| Stats | https://github.com/exelban/stats | MIT; separate application, integrated through a companion patch and local JSON files |
| USB protocol reference | https://github.com/codehz/msdisplay | Protocol research reference; no upstream Python implementation, firmware or vendor application is bundled here |

The vendored MediaRemoteAdapter implementation retains upstream headers and its full license. `ParentWatch.m` adds an app-specific lifetime guard to the framework build; it exits the streaming helper when the parent app's pipe closes. The upstream sources themselves are unchanged.

The build includes the MediaRemoteAdapter and libusb license texts in the app's Resources directory. libusb remains a separate dynamic library; its source and build instructions are available from its upstream project. The app is ad-hoc signed, so a modified local bundle can be re-signed after replacing the library.

The application uses macOS system frameworks, system symbols and the Apple catalog search service. It is a community project and is not affiliated with Apple, Stats, the case vendor, or these upstream maintainers.
