# 第三方组件与来源

本项目代码以 MIT 许可发布（见 `LICENSE`）。以下组件或研究成果影响或支撑了本实现。

## 运行时依赖（不随包分发）

| 组件 | 许可 | 说明 |
|---|---|---|
| **libusb-win32 / libusb0.sys** | LGPL-2.1（驱动与 DLL） | 副屏视频接口（MI_03）使用的驱动，由厂商程序安装于系统。本程序通过系统 `libusb0.dll` 调用，不分发该文件。 |
| **NVML（`nvml.dll`）** | NVIDIA 驱动组件 | GPU 利用率与温度；随 NVIDIA 驱动安装于系统。 |
| **Windows 媒体会话（GSMTC）** | 操作系统组件 | 正在播放信息与传输控制。 |
| **Windows Core Audio** | 操作系统组件 | 默认输出端点音量与静音。 |
| **Segoe UI Variable / Segoe Fluent Icons / Bahnschrift / Arial** | Windows 字体许可 | 界面与面板渲染所用字体，不随包分发。 |

## 开发依赖

| 组件 | 许可 | 说明 |
|---|---|---|
| **.NET 9 / WPF** | MIT | 应用运行时与界面框架。 |
| **System.Management** | MIT | 读取内存条与显卡信息、触屏 PnP 状态。 |
| **xUnit / xunit.runner.visualstudio** | Apache-2.0 | 单元测试。 |
| **Microsoft.NET.Test.Sdk** | MIT | 测试宿主。 |

## 研究与设计来源

| 来源 | 许可 | 用途 |
|---|---|---|
| [codehz/msdisplay](https://github.com/codehz/msdisplay) | 见其仓库许可 | MS912x USB 显示协议、xdata 寄存器与初始化序列研究；本项目的协议实现由其文档与实测交叉验证。 |
| [vk03-panel-macos](https://gitee.com/archibald-lewis/vk03-panel-macos) | MIT | 原版 macOS 应用：布局、交互模型、触控映射与本 Windows 移植的来源。 |
| [exelban/Stats](https://github.com/exelban/stats) | MIT | 原版 macOS 版的数据来源；本移植用 Windows 原生采集替代。 |
| [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | MPL-2.0 | 可选的 CPU 温度桥（WMI），不随包分发。 |
| [HWiNFO](https://www.hwinfo.com/) | 商业软件 | 可选的 CPU 温度桥（共享内存），不随包分发。 |

本程序不复制上述项目的源代码；协议常量与布局参数由公开文档、原版 MIT 源码与本机实测推导。
