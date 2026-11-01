# 数据接口

面板数据全部在本机采集与显示，不上传、不包含遥测。缺失的数据显示为 `—`。

## 实时指标

| 字段 | 来源 | 单位 | 说明 |
|---|---|---|---|
| CPU 负载 | `GetSystemTimes` 差分 | 0–1 | 与任务管理器总负载接近 |
| CPU 频率 | `CallNtPowerInformation(ProcessorInformation)` | GHz | 各逻辑核当前主频平均；失败时回退注册表 `~MHz` |
| CPU 型号 / 核心 | 注册表 + `GetLogicalProcessorInformationEx` | — | 去掉厂商与核心数后缀 |
| 内存用量 | `GlobalMemoryStatusEx` | GB | 总可用物理内存 − 可用内存 |
| 内存总量 / 条数 / 速率 | WMI `Win32_PhysicalMemory` | GB / MT/s | 优先 `ConfiguredClockSpeed`（实际运行速率） |
| GPU 利用率 / 温度 | NVML（`nvml.dll`，NVIDIA 驱动） | 0–1 / °C | 无 NVIDIA 驱动时仅显示名称 |
| GPU 名称 | NVML，回退 WMI `Win32_VideoController` | — | 优先独显 |
| 媒体 | Windows GSMTC | — | 标题、歌手、封面、进度、播放状态 |
| 音量 / 静音 | Core Audio `IAudioEndpointVolume` | 0–1 | 默认输出端点，端点变化时重新获取 |
| 锁屏 | `LockWorkStation` | — | 仅在交互会话可用 |

每次刷新（默认 1 Hz）重新采样；采样失败保留 `null`，渲染层显示 `—`。

## CPU 温度

优先级：AMD Ryzen Master SDK → HWiNFO 共享内存 → LibreHardwareMonitor WMI。

### AMD Ryzen Master SDK（推荐）

- 通过 Platform.dll 导出的 GetRmCpuParameters 读取处理器参数块；
- 温度取参数块中的传感器 double（物理 °C）；同时会写出 %LOCALAPPDATA%\VK03Panel\amd-sdk.json
  供核对；
- AMD 驱动设备（\\.\AMDRyzenMasterDriverV31）只允许管理员访问，因此本程序需要以管理员运行；
- 以管理员运行且开启开机自启时，会注册名为 VK03Panel 的计划任务（最高权限、登录时运行），
  避免每次登录都弹 UAC。

### 外部桥（回退）

1. **HWiNFO 共享内存**（推荐）
   - 映射名：`Global\HWiNFO_SENS_SM2`；
   - 要求 HWiNFO 正在运行并开启 Shared Memory Support；
   - 程序按 SDK 的共享内存头解析传感器与读数，优先匹配 `CPU Temperature` 读数 ID，
     其次匹配包含 CPU / Tctl / Tdie 的标签。
2. **LibreHardwareMonitor WMI**
   - 命名空间：`root\LibreHardwareMonitor`；
   - 查询 `Sensor WHERE SensorType='Temperature'`，优先 `/amdcpu/0/temperature/0`、`/intelcpu/…` 等 CPU 核心项；
   - 需要 LibreHardwareMonitor 以管理员运行并开启 WMI 提供程序；其 WinRing0 驱动在开启
     内存完整性（HVCI）的 Windows 11 上会被阻止。

两种来源都不可用时温度字段为 `null`。

## 面板协议（概要）

- 控制面：HID Feature Report，report ID 0；Windows 上报告长度为 9 字节（含 ID），
  数据位于响应下标 4；macOS 侧为 8 字节、数据位于下标 3。
- 视频面：libusb0 接口 3，bulk OUT `0x04`，分片 `0x80000`，帧尾提交零长度包。
- 帧格式：`FF 00 00 00 00 W>>4 ((W&0xF)<<4)|((H>>8)&0xF) H&0xFF` + BGR888（`0xFF→0xFE`）+
  `FF C0 00 00 00 00 00 00`。
- 初始化：`A6 03 03` → `B6 DE EE 01` → `A6 07 01` → `A6 05 00` → 关闭屏幕门 →
  1080p 中间态 → `A6 01`（360×960, color `0x11`）→ `A6 02`（VIC `0xA0`）→ `A6 04 01`；
  首帧后 `A6 00`（帧触发）、打开屏幕门、`A6 05 01`。
- 屏幕门：端口 5 用 `0xF507` bit `0x02`（低有效），端口 6 用 `0xF005` bit `0x10`。

更完整的线级协议见原版项目的 `docs/METRICS.md` 与 codehz/msdisplay 的 `PROTOCOL.md`。

## 本机文件

| 路径 | 内容 |
|---|---|
| `%APPDATA%\VK03Panel\config.json` | 主题、深浅色、强调色、方向、自启等设置 |
| `%LOCALAPPDATA%\VK03Panel\metrics.json` | 最近一次指标快照（每 15 秒） |
| `%LOCALAPPDATA%\VK03Panel\media.json` | 最近一次媒体快照 |
| `%LOCALAPPDATA%\VK03Panel\panel.json` | 连接状态、帧数、耗时、主题、触屏状态 |
| `%LOCALAPPDATA%\VK03Panel\preview.png` | 最近一次渲染的面板预览 |
| `%LOCALAPPDATA%\VK03Panel\logs\panel.log` | 滚动日志（1 MB × 5） |

设置文件损坏时会备份为 `config.json.bak` 并回退默认值。
