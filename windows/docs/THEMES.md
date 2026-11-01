# 主题说明

副屏主题决定 960×360 画布上的排版、配色与图标语言；两套主题共享同一个数据模型与触控动作。

## 可选主题

| id | 名称 | 设计语言 |
|---|---|---|
| `win11` | Windows 11（默认） | Mica 式背景、Fluent 卡片、系统强调色、状态胶囊、圆角控制条 |
| `mac` | macOS（原版） | 玻璃卡片、青蓝强调色、SF 风格填充图标、原版坐标布局 |

在设置窗口「外观」页切换，立即生效并持久化到 `config.json` 的 `ThemeId`。

## Windows 11 主题

| 项目 | 深色 | 浅色 |
|---|---|---|
| 背景 | `#1C1C1C → #151515` + 强调色径向辉光 | `#F4F4F4 → #ECECEC` + 强调色辉光 |
| 卡片 | `#2B2B2B` (84%) + 1px `#FFFFFF16` | `#FFFFFF` (92%) + 1px `#00000010` |
| 主文字 / 次文字 / 三级 | `#FFFFFF` / `#C7C7C7` / `#8F8F8F` | `#1A1A1A` / `#5C5C5C` / `#8A8A8A` |
| 强调色 | 跟随系统（暗色下自动提亮 30%）或固定色 | 同左 |
| 卡片圆角 | 8 | 8 |
| 字体 | Segoe UI Variable Display / Text / Small | 同左 |

布局（960×360）：

- 顶栏：应用标记 + 在线状态胶囊；
- 左侧：时钟（62 px Light）、日期、核心数与硬件摘要；
- 中部：CPU / 内存 / GPU 三张指标卡（220×152，含图标片、强调色大数、胶囊标签与 4 px 进度条）；
- 底部：媒体卡（封面、曲名、进度）+ 控制卡（静音、音量滑杆、±、上一首/播放/下一首、按住锁屏）。

命中区：`Mute`、`Quieter`、`Volume`、`Louder`、`Previous`、`Play`、`Next`、`Lock`，
与 macOS 主题一一对应，但坐标不同。

## macOS 主题

逐像素移植原版 `Renderer.swift`：960×360 布局、25/28 px 卡片圆角、59 px 时钟、
玻璃质感卡片与渐变背景、青蓝/紫色强调色。图标为自绘矢量，近似 SF Symbols，
因此不依赖系统中是否安装对应字体。

## 代码结构

```text
Themes/
  IPanelTheme.cs        主题接口：绘制、命中测试、滑块几何
  PanelThemes.cs        主题注册表（win11 / mac）
  PanelViewModel.cs     渲染用的不可变数据快照
  PanelViewModelFactory 由指标、媒体、音量组装视图模型
  Paint.cs              画布基础：文字（含等宽数字）、圆角、渐变、进度条
  MacTheme.cs           macOS 版式
  Win11Theme.cs         Windows 11 版式
  MacGlyphs.cs          SF 风格填充图标
  Win11Glyphs.cs        Fluent 风格线性图标
```

## 新增主题

1. 实现 `IPanelTheme`：`Bounds`、`HitTest`、`SliderLeft`、`SliderWidth`、`Draw`；
2. 在 `PanelThemes.All` 注册并分配稳定 id；
3. 若需要新控件，在 `Paint`/`Glyphs` 中补充矢量绘制，避免依赖外部字体；
4. 在 `ThemeLayoutTests` 中确认命中区与滑块几何；
5. 用 `--demo` 出图检查深浅色。

主题只负责「怎么画」和「哪里可点」；数据采集、动作执行与触控状态机都在 Core 的其它模块中。
