# MouseBatteryTray

**在 Windows 任务栏托盘显示罗技无线鼠标电量与续航趋势的轻量工具。**

[![build](https://github.com/levil94/MouseBatteryTray/actions/workflows/build.yml/badge.svg)](https://github.com/levil94/MouseBatteryTray/actions/workflows/build.yml)
[![release](https://img.shields.io/github/v/release/levil94/MouseBatteryTray)](https://github.com/levil94/MouseBatteryTray/releases)
[![license](https://img.shields.io/github/license/levil94/MouseBatteryTray)](LICENSE)

---

## 概述

MouseBatteryTray 通过 Logitech HID++ 2.0 协议直接读取无线鼠标电池电压，在系统托盘显示电量，并记录历史采样以推算续航。

工具不依赖 G HUB 运行，不安装驱动，不注入游戏进程，常驻内存约 30 MB。

- 单个可执行文件，构建不依赖 Visual Studio 或 .NET SDK
- 支持三种托盘图标样式与自定义图片
- 提供电量历史曲线与剩余时间估算
- 支持自定义低电量提示音与阈值

## 功能特性

| 特性 | 说明 |
|---|---|
| 电量显示 | 托盘图标实时显示百分比，按电量分级着色 |
| 图标样式 | 数字 / 电池图形 / 自定义图片（PNG、ICO、JPG、BMP） |
| 剩余时间估算 | 基于放电速率推算可用时长 |
| 历史曲线 | 1 小时 / 6 小时 / 24 小时 / 7 天四档区间 |
| 趋势提示 | 悬停显示最近一小时降幅与波动区间 |
| 低电量提醒 | 可调阈值，气泡提示 + 可选提示音 |
| 断连处理 | 保留最后一次读数与时间戳，图标灰显 |
| 开机自启 | 写入当前用户 Run 注册表项，可在设置中开关 |
| 单实例保护 | 全局互斥量，避免重复启动 |

## 系统要求

| 项目 | 要求 |
|---|---|
| 操作系统 | Windows 10 / Windows 11（x64） |
| 运行时 | .NET Framework 4.x（系统自带） |
| 硬件 | 支持 HID++ 2.0 电量特性的罗技无线鼠标 |
| 首次构建 | 需要网络（自动下载 hidapi） |

**已验证设备：** Logitech G502 LIGHTSPEED（接收器与有线两种连接方式均测试通过）。

其他支持 `0x1001 BATTERY_LEVEL_STATUS` 特性的罗技设备理论上可用，但未逐一验证。

## 安装

### 方式一：下载发行版

从 [Releases](https://github.com/levil94/MouseBatteryTray/releases) 下载 `MouseBatteryTray-win-x64.zip`，解压后**双击 `MouseBatteryTray.vbs`** 启动。

压缩包内包含：

```
MouseBatteryTray.vbs             启动入口（推荐）
Start-MouseBatteryTray.ps1      宿主启动脚本
MouseBatteryTray.exe            可执行版，仅适用于允许运行未签名程序的机器
hidapi.dll                      HID 访问库
README.md                       说明文档
```

所有文件需位于同一目录。两种启动方式功能完全一致，区别只在是否依赖已签名的 PowerShell 宿主：

| 启动方式 | 适用场景 |
|---|---|
| **`MouseBatteryTray.vbs`** | 通用，包括开启 Smart App Control 的机器 |
| `MouseBatteryTray.exe` | 系统允许运行未签名程序时使用，启动略快 |

### 方式二：从源码构建

详见 [从源码构建](#从源码构建)。

### 双击 exe 没有反应怎么办

部分 Windows 11 机器开启了 **Smart App Control 强制模式**，会阻止未签名的可执行文件，事件日志提示：

```
Code Integrity determined that a process attempted to load MouseBatteryTray.exe
that did not meet the Enterprise signing level requirements
```

这种情况**不需要关闭安全策略，也不需要购买签名证书**。改用 `MouseBatteryTray.vbs` 启动即可。

原理：程序源码在**已签名的 PowerShell 宿主进程**内即时编译并运行，不生成会被拦截的未签名 PE 文件。首次启动有 1–2 秒编译开销。

> 局限：任务栏图标的进程归属为 `powershell.exe`，功能与直接运行 exe 无差别。

## 使用

通过 `MouseBatteryTray.vbs`（或 `MouseBatteryTray.exe`）启动后常驻托盘，无主窗口。所有交互通过托盘图标完成。

| 操作 | 行为 |
|---|---|
| 悬停 | 显示电量、电压、最近一小时波动、预计剩余时间 |
| 左键单击 | 立即刷新 |
| 右键 | 打开菜单：当前状态 / 立即刷新 / 历史曲线 / 设置 / 退出 |

### 首次运行提示

Windows 默认将新出现的托盘图标收纳至溢出区域（`^` 箭头内）。两种处理方式：

1. 展开 `^`，将图标拖拽至任务栏可见区域
2. 打开「设置 → 个性化 → 任务栏 → 其他系统托盘图标」，将 `MouseBatteryTray` 设为「开」

### 设置项

右键菜单 → **设置…**

| 设置 | 范围 | 默认值 |
|---|---|---|
| 刷新频率 | 10 – 3600 秒 | 60 秒 |
| 低电量阈值 | 5 – 90 % | 20 % |
| 图标样式 | 数字 / 电池图形 / 自定义图片 | 数字 |
| 提示音 | 开 / 关，可选自定义 WAV | 开（系统警告音） |
| 开机自动启动 | 开 / 关 | 关 |

设置保存后立即生效，无需重启程序。

## 配置

配置文件路径：程序所在目录下的 `settings.ini`（便携模式，不写入 `%LOCALAPPDATA%`）。

```ini
# MouseBatteryTray 配置
interval_seconds=60
low_threshold=20
icon_mode=digits
custom_icon_path=
sound_enabled=1
sound_file=
```

| 键 | 取值 | 说明 |
|---|---|---|
| `interval_seconds` | 10 – 3600 | 轮询间隔（秒） |
| `low_threshold` | 5 – 90 | 低电量提醒阈值（%） |
| `icon_mode` | `digits` / `battery` / `custom` | 图标样式 |
| `custom_icon_path` | 路径 | `icon_mode=custom` 时使用的图片 |
| `sound_enabled` | `1` / `0` | 是否播放提示音 |
| `sound_file` | 路径 | 留空则使用系统警告音 |

手动编辑后需重启程序或重新保存设置以生效。

## 工作原理

```
Logitech 接收器 / 鼠标   ←── HID++ 2.0 ──→   MouseBatteryTray
```

1. 通过 `hidapi` 枚举厂商 ID 为 `0x046D` 的 HID 设备，筛选 `usage_page = 0xFF00`、`usage = 0x0002` 的长报告集合
2. 依次尝试设备索引 `0xFF`（有线直连）与 `0x01–0x06`（接收器槽位），通过 `FEATURE_SET (0x0001)` 枚举特性表
3. 定位 `0x1001 BATTERY_LEVEL_STATUS` 特性，读取返回的两个字节
4. 这两个字节为**电池电压**（mV，大端序），而非百分比
5. 按分段线性曲线换算为百分比

> 罗技 G HUB 显示的百分比同样由电压换算得出，因此两者可能存在 1–2% 的差异，属正常现象。

### 电压–电量映射

映射锚点定义于 `IconRenderer` 与 `Hidpp.cs` 的 `VoltageAnchors` / `PercentAnchors`：

| 电压 (mV) | 百分比 |
|---|---|
| 4200 | 100 |
| 4150 | 100 |
| 4050 | 88 |
| 3950 | 74 |
| 3852 | 59 |
| 3800 | 50 |
| 3700 | 32 |
| 3600 | 16 |
| 3500 | 5 |
| 3400 | 0 |

其中 `3852 → 59%` 为实测校准点。更换设备或发现系统性偏差时，补充两至三个「原始电压 / G HUB 显示」采样点即可修正曲线。

### 剩余时间算法

取最近 6 小时（样本不足则回退至 24 小时）的采样，计算放电速率：

```
放电速率 = 区间内掉电百分比 / 区间小时数
剩余时间 = 当前电量 / 放电速率
```

以下情况不显示估算值：

- 采样跨度不足 12 分钟
- 区间内无明显掉电
- 推算结果超过 200 小时（视为异常数据）

## 数据文件

全部位于**程序所在目录**（便携模式，不在系统其他位置留文件）：

| 文件 | 内容 | 生命周期 |
|---|---|---|
| `settings.ini` | 配置项 | 持久保存 |
| `history.csv` | 采样记录（时间、百分比、毫伏） | 保留 30 天，超期自动裁剪 |
| `log.txt` | 运行日志 | 追加写入 |

历史采样节流规则：同一电量每 2 分钟最多记录一次；任意两次采样间隔不小于 20 秒。

## 从源码构建

### 依赖

- Windows 10 / 11
- .NET Framework 4.x（系统自带，包含 `csc.exe`）
- 网络连接（首次构建时下载 `hidapi.dll`）

### 构建步骤

```powershell
git clone https://github.com/levil94/MouseBatteryTray.git
cd MouseBatteryTray
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

构建脚本会自动完成：

1. 检测并下载官方 hidapi 预编译库（缺失时）
2. 调用系统自带 `csc.exe` 编译全部 `.cs` 源文件
3. 输出 `MouseBatteryTray.exe`

产物约 35 KB，需与 `hidapi.dll`（约 163 KB）同目录运行。

### 持续集成

`.github/workflows/build.yml` 定义了完整流水线：

| 触发条件 | 行为 |
|---|---|
| 推送至 `master` | 编译、打包、上传构建产物 |
| 推送 `v*` 标签 | 同上，并自动创建 GitHub Release |
| Pull Request | 编译验证 |
| 手动触发 | 可随时执行 |

## 项目结构

```
MouseBatteryTray/
├─ .github/workflows/build.yml   CI 流水线
├─ build.ps1                     构建脚本
├─ Program.cs                    入口、托盘生命周期、菜单
├─ Hidpp.cs                      HID++ 2.0 协议与设备通信
├─ History.cs                    采样存储、趋势统计、续航估算
├─ IconRenderer.cs               图标绘制（三种样式）
├─ ChartForm.cs                  历史曲线窗口
├─ Settings.cs                   配置读写与设置窗口
├─ README.md
└─ LICENSE
```

## 已知限制

- 仅支持 Windows，未提供跨平台版本
- 仅在一款设备上完成完整验证，其他型号需自行测试
- 鼠标休眠或关闭时无法读取，界面显示最后已知数值
- 托盘图标受系统尺寸限制（100% 缩放下为 16×16 像素），数字显示空间有限
- 历史曲线需运行一段时间后才有数据

## 常见问题

**托盘里找不到图标**

Windows 默认收纳新图标。展开 `^` 拖出，或在任务栏设置中单独开启。

**显示 `--` 或提示未连接**

鼠标处于休眠、关机或超出接收范围状态。移动鼠标唤醒后会在下个轮询周期恢复。

**百分比与 G HUB 不一致**

两者使用各自的电压曲线，1–2% 的差异属正常。如需精确对齐，可按[电压–电量映射](#电压电量映射)自行校准。

**剩余时间不显示**

需要至少 12 分钟的采样且电量确有下降。充电状态下不会显示。

**能否支持其他品牌鼠标**

HID++ 2.0 为罗技专有协议。其他品牌需要各自实现，当前版本不支持。

## 许可证

[MIT](LICENSE)
