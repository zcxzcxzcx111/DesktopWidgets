# 桌面小组件

Windows 11 桌面小组件原生应用，包含时钟、天气、日历和待办。

## 运行

双击 `run.cmd`，或在 PowerShell 中执行：

```powershell
.\run.ps1
```

如果系统禁止执行 PowerShell 脚本，直接使用 `run.cmd`。

程序启动后常驻任务栏托盘。双击托盘图标打开设置，或右键托盘图标选择“设置”。

## 操作

- 普通模式下可直接操作组件。
- 按住 Alt 并拖动组件，可临时调整位置。
- 从托盘菜单进入“编辑布局”，可直接拖动所有组件。
- 右键组件可切换小、中、大尺寸、隐藏组件或打开设置。
- 组件会自动吸附，并保持至少 16 logical px 间距。

## 天气配置

在设置中填写城市与和风天气 API Key。凭据使用 Windows DPAPI 加密保存到：

`secrets\weather-credentials.dat`

API Key 不会写入源码或日志。

## 数据

- `config\settings.json`：外观和模块设置。
- `config\layout.json`：组件布局。
- `data\todos.json`：待办数据。
- `cache\weather.json`：天气缓存。
- `logs\`：诊断日志。

项目使用本地便携式 .NET SDK，位于 `.tools\dotnet`，不修改系统全局 SDK 配置。
