# 构建说明

## 环境

- Windows 11 22H2 或更新版本（DWM 圆角、Windows 11 控件样式）。
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)，`dotnet --version` 应输出 9.x。
- PowerShell 7（`pwsh`），或 Windows PowerShell 5.1 也能运行脚本，但建议 7。
- 可选：libusb-win32 驱动 + 副屏，用于实机自检。

不需要安装 Visual Studio；WPF、WinRT 投影与测试包都由 SDK 与 NuGet 提供。

## 一键构建

```powershell
.\build.ps1
```

参数：

| 参数 | 作用 |
|---|---|
| `-Configuration Debug` | 使用 Debug 配置 |
| `-SkipTests` | 跳过单元测试 |
| `-SkipAssets` | 不重新生成图标与演示图 |
| `-SkipPublish` | 只编译，不发布 |

脚本依次执行：生成 `assets/app.ico` → 编译解决方案 → 渲染四张演示图 → 单元测试 →
自包含发布到 `dist/VK03Panel/` 并复制文档。

## 手工步骤

```powershell
dotnet run --project tools\VK03Panel.IconGen -c Release -- assets
dotnet build VK03Panel.sln -c Release
dotnet test src\VK03Panel.Core.Tests\VK03Panel.Core.Tests.csproj -c Release
dotnet publish src\VK03Panel.App\VK03Panel.App.csproj -c Release -r win-x64 `
    --self-contained true -p:DebugType=None -o dist\VK03Panel
```

## 演示图与自检

```powershell
$cli = 'src\VK03Panel.Cli\bin\Release\net9.0-windows10.0.19041.0\VK03Panel.Cli.exe'
& $cli --demo assets\demo-win11-dark.png --theme win11 --dark
& $cli --demo assets\demo-mac-light.png  --theme mac  --light
& $cli --render out.png                  # 本机真实数据
& $cli --diagnose                        # 设备 / 传感器 / 媒体诊断
& $cli --self-test                       # 校验芯片、EDID、端口，不写画面
& $cli --self-test --frame --hold 10     # 实际点亮副屏并保持 10 秒
```

`--self-test --frame` 会短时间占用副屏接口；运行前请退出厂商程序。

## 发布产物

`dist/VK03Panel/` 为自包含目录（约 170 MB），目标机器无需安装 .NET。
若希望更小的体积，可改用框架依赖发布：

```powershell
dotnet publish src\VK03Panel.App\VK03Panel.App.csproj -c Release -r win-x64 `
    --self-contained false -o dist\VK03Panel-fx
```

此时目标机器需要安装 .NET 9 Desktop Runtime。

## 项目布局

- `src/VK03Panel.Core/`：协议、渲染、主题、指标、媒体、音频、触控、配置。
- `src/VK03Panel.App/`：WPF 外壳（托盘、设置窗口、工作线程）。
- `src/VK03Panel.Cli/`：命令行入口。
- `src/VK03Panel.Core.Tests/`：xUnit 单元测试（不需要硬件）。
- `tools/VK03Panel.IconGen/`：图标生成器。
- `assets/`：演示图、设置窗口截图、图标。
- `docs/`：构建、安装、数据接口与主题文档。
