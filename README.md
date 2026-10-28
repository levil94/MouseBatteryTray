# MouseBatteryTray

在 Windows 任务栏托盘显示罗技无线鼠标电量的轻量小工具。

- 单个 exe，**不需要安装 .NET SDK / Visual Studio**（用系统自带 `csc.exe` 编译）
- 直接与鼠标通信（Logitech HID++ 2.0），**不依赖 G HUB 运行**
- 托盘图标显示电量百分比，颜色随电量变化
- 常驻内存约 30 MB，无后台服务、无驱动

## 功能

| 功能 | 说明 |
|---|---|
| 电量显示 | 托盘图标按电量变色（绿 >50%、黄 25–50%、橙 10–25%、红 ≤10%） |
| 图标样式 | 数字 / 电池图形 / 自定义图片（png / ico / jpg / bmp） |
| 设置窗口 | 右键菜单 → 设置…，在程序内调整刷新頻率、阈值、图标 |
| 悬停详情 | 设备名、电量、电压、更新时间 |
| 左键点击 | 立即刷新 |
| 右键菜单 | 立即刷新 / 设置… / 开机自动启动 / 退出 |
| 轮询 | 默认 60 秒，可调 10–3600 秒 |
| 断连处理 | 读取失败时显示 `--`，并保留上次读数与时间 |
| 低电量提醒 | 默认 ≤20%（可调 5–90%），跳过高阈值才提醒 |

### 设置项

右击托盘图标 → **设置…**：

| 设置 | 范围 | 默认 |
|---|---|---|
| 刷新頻率 | 10 – 3600 秒 | 60 |
| 低电量阈值 | 5 – 90 % | 20 |
| 图标样式 | 数字 / 电池图形 / 自定义图片 | 数字 |
| 开机自动启动 | 开 / 关 | 关 |

配置保存于 `%LOCALAPPDATA%\MouseBatteryTray\settings.ini`，修改后立即生效，无需重启。

## 工作原理

```
Logitech 接收器 / 鼠标  ←HID++ 2.0→  本程序
```

1. 通过 `hidapi` 枚举 VID `0x046D` 的 HID 设备，只取 `usage_page=0xFF00, usage=0x0002` 的长报告集合
2. 向设备索引依次尝试 `0xFF, 0x01…0x06`（插线走 `0xFF`，无线走接收器槽位）
3. 用 `FEATURE_SET(0x0001)` 枚举特性表，找到 `0x1001`（BATTERY_LEVEL_STATUS）
4. 读该特性，前两个字节即**电池电压（mV，大端）**
5. 用分段线性曲线把电压换算成百分比

> 注意：本项目读的是 `0x1001` 返回的**电压**，而不是百分比。罗技 G HUB 的百分比同样是按电压曲线算出来的，因此两者会有 1–2% 的差异，属正常。

## 电量换算

`Hidpp.cs` 中的 `VoltageAnchors` / `PercentAnchors` 是校准点，实测数据来自 G502 LIGHTSPEED：

| 电压 (mV) | 百分比 |
|---|---|
| 4200 | 100 |
| 4150 | 100 |
| 4050 | 88 |
| 3950 | 74 |
| 3852 | 59 ← 实测校准点 |
| 3800 | 50 |
| 3700 | 32 |
| 3600 | 16 |
| 3500 | 5 |
| 3400 | 0 |

换设备或发现偏差时，按「原始电压 vs G HUB 显示」补两个点即可修正。

## 构建

需要：

- Windows 10/11
- .NET Framework 4.x（系统自带）
- 网络（首次构建自动下载 `hidapi.dll`）

```powershell
git clone https://github.com/<your-name>/MouseBatteryTray.git
cd MouseBatteryTray
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

产物：`MouseBatteryTray.exe`（约 15 KB）+ `hidapi.dll`（约 163 KB）。

## 使用

1. 双击 `MouseBatteryTray.exe`
2. 首次运行后图标可能被收进托盘的 `^` 溢出区——点开 `^` 把图标拖到可见区域，或在
   任务栏设置 →「选择哪些图标显示在任务栏上」里打开它
3. 右键菜单可勾选「开机自动启动」（写入 `HKCU\...\CurrentVersion\Run`）

日志：`%LOCALAPPDATA%\MouseBatteryTray\log.txt`

## 已知限制

- 仅 Windows
- 只在 G502 LIGHTSPEED 上验证过；其他支持 HID++ 2.0 电量特性的罗技设备理论上可用，但未逐一测试
- 鼠标休眠或关机时读不到数据，界面会显示上次值
- 16 px 托盘图标空间有限，数字使用等宽粗体；高 DPI 下会自动用更大的位图

## 许可

MIT
