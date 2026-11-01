# 副屏主题

两套主题使用相同的 960×360 设计画布、监控数据和触控状态机。在设置窗口“外观”页切换后立即生效，并保存为 `ThemeId`。

| ID | 主题 | 布局 |
|---|---|---|
| `win11` | Windows 11 | 左侧时钟与锁屏，顶部任务管理器式 CPU / 内存 / GPU 网格曲线，底部媒体与音量 |
| `mac` | macOS | 控制中心式半透明模块：时钟、媒体、硬件概览、音量和锁屏 |

## Windows 11

- 中性灰背景，Segoe UI Variable 文字与系统 Segoe Fluent Icons。
- CPU / 内存 / GPU 使用独立的网格曲线和实时数值。
- 采用 Windows 系统强调色；深色时适当提亮。
- 音量滑条替代独立的加减按钮。

## macOS

- 采用中性半透明表面、圆角模块、细边线和轻微阴影，背景色透过模块形成层次。
- 时钟和媒体位于左侧；三个硬件模块显示主要数值与近期采样趋势。
- 音量使用胶囊滑条，并保留静音和加减按钮；播放按钮采用黑白填充，锁屏需要持续按住。
- 播放、音量和硬件图标使用项目内的矢量几何，不打包 Apple 图标字体。
- 字体优先使用已安装的 Helvetica Neue，未安装时回退到 Arial 与系统中文字库。Windows 版不附带 Apple 字体。
- 半透明效果由副屏位图渲染器绘制，不会捕获桌面，也不依赖 macOS 的实时模糊接口。

## 触控与渲染

主题在 `Bounds` 和 `HitTest` 中定义自己的热区；滑条位置通过 `SliderLeft` 与 `SliderWidth` 交给触控控制器。
实际画面与命中区共用设计坐标，方向转换由公共渲染和触控代码处理。

所有可见按钮的热区至少为 48×48 像素；两套主题都有热区不重叠、边缘命中和滑条端点测试。
Windows 11 不显示独立的 `Quieter` / `Louder` 按钮，macOS 保留这两个动作。

```powershell
$cli = 'src\VK03Panel.Cli\bin\Release\net9.0-windows10.0.19041.0\VK03Panel.Cli.exe'
& $cli --demo assets\demo-mac-dark.png --theme mac --dark
& $cli --demo assets\demo-mac-light.png --theme mac --light
```

演示图使用合成数据；实时画面中缺失的传感器读数显示为 `—`。
