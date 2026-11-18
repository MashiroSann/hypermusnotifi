# HyperMusNotifi

一个常驻托盘的小工具：监听 Windows **SMTC**（系统媒体传输控制），每当开始播放新歌时，在屏幕右下角滑出一个小窗提示 **正在播放：[歌曲名]**。

> 适用于任何接入 Windows 媒体控制的播放器：网易云音乐、Spotify、HyPlayer、浏览器网页播放等。

## 功能

- **切歌提示**：监听 `GlobalSystemMediaTransportControlsSessionManager`，歌曲（标题/歌手/专辑/音轨号）变化时弹出提示
- **滑入滑出动画**：从屏幕右侧外滑入，停留 5 秒后滑出消失；动画只作用于内容层（`TranslateTransform`），不移动真实窗口，高刷新率屏幕也顺滑
- **自适应宽度**：窗口宽度随歌名长度变化（上限 420），短歌名不会留大片空白，超长歌名末尾省略
- **单曲循环也能提示**：轮询播放进度，识别「从歌曲末尾跳回开头」的循环重启；拖动进度条回开头不会误触发
- **游戏中可见**：显示期间周期性重申 `HWND_TOPMOST`，可压在无边框全屏游戏之上；窗口带 `WS_EX_NOACTIVATE`，不抢焦点
- **连续切歌不闪烁**：提示已显示时原地更新内容；若正处于滑出过程则立刻滑回
- **托盘常驻**：托盘图标右键「退出」

## 环境要求

- Windows 10 1809 (17763) 或更高版本（含 Windows 11）
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)（仅框架依赖版发布需要）

## 运行

从 [Releases](../../releases) 下载 zip，解压后双击 `HyperMusNotifi.exe`。程序无主窗口，启动后只出现在系统托盘。

### 开机自启

把 `HyperMusNotifi.exe` 的快捷方式放进启动文件夹：

```
Win + R  →  shell:startup
```

## 从源码构建

```powershell
dotnet build

# 框架依赖发布（需目标机安装 .NET 10 Desktop Runtime，体积小）
dotnet publish -c Release -r win-x64 --self-contained false -o publish

# 独立单文件发布（不依赖运行时，体积大）
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish-standalone
```

## 实现要点

| 关注点 | 做法 |
| --- | --- |
| 会话发现 | `GlobalSystemMediaTransportControlsSessionManager.RequestAsync()`，订阅 `CurrentSessionChanged` / `SessionsChanged` / `MediaPropertiesChanged` |
| 去重 | 用 `来源应用 + 标题 + 歌手 + 专辑 + 音轨号` 组成签名，避免同一首歌重复弹窗 |
| 单曲循环 | 250 ms 轮询 `GetTimelineProperties()`，检测末尾 15 秒内跳回开头 10 秒内且歌曲签名不变 |
| 动画 | 窗口位置固定，仅对内容层做 `TranslateTransform`（`CubicEase`，`DesiredFrameRate = 120`） |
| 置顶 | 显示期间每 800 ms 调一次 `SetWindowPos(HWND_TOPMOST)` |
| 布局 | `SizeToContent="WidthAndHeight"` + `MaxWidth="420"`，`Show()` 后 `UpdateLayout()` 再读取真实尺寸定位 |

## 许可证

[MIT](LICENSE)
