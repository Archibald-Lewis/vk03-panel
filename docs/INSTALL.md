# 预编译安装包 / Prebuilt installation

本版本为 AMD64 / x86_64 社区构建，实机验证设备为 AMD Ryzen 7 9700X（8 核 16 线程）+ GeForce RTX 5070 + 32 GB 内存的 macOS 26 黑苹果。不是 Apple Silicon 通用包；Intel CPU 设备尚未实测。安装前退出正在使用的同名应用，保留旧版本；打开 DMG，将应用拖入 Applications，然后退出磁盘映像并从应用程序目录打开。安装不会修改 EFI 或安装内核驱动。

The disk image contains a signed app and an Applications shortcut. Quit the existing app, keep a backup, drag the new app to Applications, eject the disk image, then open the installed app. No EFI changes or kernel extensions are installed. AMD64 / x86_64 binary. Tested hardware: AMD Ryzen 7 9700X (8 cores / 16 threads), GeForce RTX 5070 and 32 GB RAM, running macOS 26. Intel CPU machines and Apple Silicon are unverified.

## 签名 / Signing

代码签名为 ad-hoc，未使用 Developer ID，也未通过 Apple 公证。因此浏览器下载后可能被 Gatekeeper 拦截；构建和本地签名检查通过不代表 Apple 已批准运行。请阅读 Apple 的说明，自行决定是否允许该应用运行：
https://support.apple.com/en-us/102445

This build is ad-hoc signed, not Developer ID signed or notarized. A downloaded copy may be blocked by Gatekeeper. Review Apple's guidance and decide whether to approve this app yourself. No security settings are changed by the package.

## 校验 / Integrity

下载同一 Release 的 SHA256SUMS.txt，将文件放在同一目录后运行：

```bash
shasum -a 256 -c SHA256SUMS.txt
```

只下载部分文件时，可先从清单中选择对应行再校验。校验用于检查下载完整性，不替代开发者身份验证。

## VK03

需要 macOS 26 和 MCVKC03 / MS912C 小屏。硬件数值还需要配套的 Stats 社区版及工作正常的读数来源。应用包已包含 libusb 和 MediaRemoteAdapter，使用者不必安装 Homebrew、Xcode 或 Python。

The app bundles libusb and MediaRemoteAdapter; no Homebrew, Xcode or Python installation is needed to run it. Metrics require the companion Stats community build and working drivers.

libusb 1.0.30 对应源码随包提供；可替换独立动态库并重新本地签名。应用源码、构建和打包脚本位于本仓库。
