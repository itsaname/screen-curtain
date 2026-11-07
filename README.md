# 幕帘 ScreenCurtain

**English:** ScreenCurtain is a Windows 11 blackout and update-restart guard. It covers every display with a borderless black layer, turns off only the panel backlight where supported (external DDC/CI, internal WMI), keeps monitors and their USB hubs powered, and restores on mouse/keyboard input. The UI supports Chinese and English; choose **Settings > General > Language** and restart. See the release table below for downloads.

Windows 11 全屏熄屏与 Windows Update 重启防护工具。它按需让所有显示器进入纯黑无边框全屏，尝试关闭显示器物理背光；只有鼠标点击或键盘按键才恢复。运行期间可临时暂停 Windows 自动更新，并阻止更新触发的重启。

当前版本：**1.2.0**。

> 1.2.0 修复/优化：
> - 安装器/卸载器增加英文界面：按系统语言自动选择，也可用 `/LANG=EN` 强制英文；英文系统下不会显示中文界面。
> - 增加英文界面：默认跟随系统语言，可在“设置 > 常规 > 界面语言 / Language”选择自动、中文或 English，重启后生效；主窗口、设置、首次向导、托盘菜单和对话框均已英文化。
> - “预览 3 秒”现在复用实际熄屏的背光/WMI 调光、静音、无边框黑层、DWM 无圆角和隐藏光标设置；3 秒后按相同顺序恢复。
> - 更新保护握手：辅助进程先发 `HELLO`，客户端回 `OK`，再应用策略并返回 `ACTIVE`；修复 UAC 同意后卡在“正在请求管理员权限”。
> - 笔记本（检测到电池）默认不发送物理关屏，只使用全屏黑层，避免鼠标移动导致内置屏背光反复亮灭，以及物理关屏触发的唤醒锁屏。
> - 使用 DisplayConfig `outputTechnology` 优先判定内置/外接（Internal / LVDS / DP Embedded 等视为内置）；已识别的内置屏只做黑层，已识别的外接屏尝试 DDC/CI 关屏；尺寸仅作为无法读取连接类型时的 fallback，不使用主屏标志。
> - 笔记本屏幕只关闭背光（默认开启）：外接屏通过 DDC/CI VCP 0x10 将亮度设为 0；内置屏通过 WMI WmiSetBrightness 尝试设为 0。显示器保持供电，USB Hub 和键鼠继续工作；唤醒时恢复原亮度。不使用 0xD6 电源命令。
> - 音频恢复改到显示器恢复/黑层关闭之后，并保留恢复状态、校验结果、延迟重试；锁屏后解锁也会重试。
> - 音频设备刷新移到后台线程，避免 Core Audio 调用阻塞低级输入钩子。

## 下载与安装

- **安装版（按用户安装，不需要管理员权限）**  
  `dist\1.2.0\installer\ScreenCurtain-Setup-1.2.0.exe`
- **绿色版（解压即用）**  
  `dist\1.2.0\portable\ScreenCurtain-1.2.0-portable.zip`

安装版默认安装到 `%LOCALAPPDATA%\Programs\ScreenCurtain`，创建开始菜单/可选桌面快捷方式和“应用与功能”卸载项。安装器本身不写 HKLM；更新保护只在运行时请求 UAC。

绿色版解压后直接运行 `ScreenCurtain.exe`；配置、日志和音频恢复标记保存在同目录 `data\`，不创建快捷方式或卸载项。若目录不可写，会自动回退到 `%LOCALAPPDATA%\ScreenCurtain` 并显示说明。

> 产物没有代码签名证书，Windows SmartScreen 可能提示未知发布者。请使用 `dist\1.2.0\SHA256SUMS.txt` 校验下载/拷贝的文件。

## 主要功能

- **全屏熄屏**：每台显示器一个无边框全屏 topmost 层，覆盖任务栏与通知区域；支持多显示器、竖屏和热插拔。
- **不会误唤醒**：鼠标移动保持黑屏；鼠标按键或键盘按键恢复。首个唤醒输入会被吞掉。
- **托盘仍保留**：不会隐藏 `NotifyIcon`，黑屏时它由全屏层自然遮挡；恢复后不会出现重新出现的闪烁。
- **应用提醒静默**：黑屏期间本程序的托盘气泡、消息框、错误提示全部不显示，恢复后合并提示。
- **Topmost Guard**：通过 WinEvent 钩子和短周期重新置顶，尝试压住第三方窗口、普通 topmost 弹窗和通知；安全桌面/UAC 除外。
- **系统静音**：黑屏时静音所有活动播放设备；恢复时还原每台设备原来的静音状态。新插入/切换的播放设备也会被静音。默认开启，可在设置和主窗口快速选项中关闭。
- **笔记本物理关屏可选**：检测到电池时默认只做全屏黑层，不发送 `SC_MONITORPOWER`；可在“设置 > 熄屏”单独开启“在笔记本上尝试物理关屏”（默认关闭，可能有背光反复/唤醒锁屏副作用）。
- **屏幕仅关背光**：优先用 DisplayConfig 连接类型识别内置/外接；外接屏在黑层建立前将 DDC/CI VCP 0x10 亮度设为 0；内置屏尝试通过 WMI WmiSetBrightness 设为 0，仅关闭背光。显示器保持供电，USB Hub 和键鼠继续工作；唤醒时恢复原亮度。WMI 不可用时内置屏保留黑层。默认开启，可单独关闭。
- **更新重启保护**：运行时以管理员权限临时写入 Windows Update 策略并阻止关机/重启；退出时恢复原设置。保护降级/恢复失败有明确重试入口。
- **完整 UI**：首次运行 3 步向导、主窗口、独立设置窗口（常规/熄屏/更新保护/诊断与关于）、退出确认、恢复失败对话框、无障碍键盘操作。
- **中英文界面**：自动跟随系统语言，也可手动选择中文或 English（重启后生效）。
- **品牌图标**：16–256px 多尺寸主图标与开启/降级/关闭三态托盘图标。

## 系统要求

- Windows 11（23H2 / 24H2 / 25H2；x64）。
- .NET Framework 4.8（Windows 11 自带）。
- 更新重启保护需要管理员权限（UAC）；熄屏、静音和绿色版运行不需要管理员权限。

## 使用

### 首次运行

首次运行会打开 3 步向导：

1. 了解“黑屏不是锁屏”和默认静音行为。
2. 选择启用更新保护（会请求 UAC）或仅使用熄屏。
3. 设置全局快捷键，选择是否启动后最小化，完成向导。

首次设置关闭向导不会保存 `FirstRun=false`，下次仍会进入向导。

### 熄屏与恢复

- 点击主窗口“立即熄屏”、按全局热键（默认 `Ctrl+Alt+Shift+L`）或使用托盘菜单。
- 所有显示器被纯黑覆盖；桌面默认尝试物理熄屏，笔记本默认只使用全屏黑层；音频默认静音。
- 鼠标移动不会恢复；点击鼠标或按键后恢复显示并还原音频。
- 系统锁屏、远程断开、系统挂起、程序退出时会自动恢复显示，避免把用户困在黑屏后面。

### 更新重启保护

启用后，程序会以管理员权限启动辅助进程，在运行期间临时写入：

```text
HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU
  NoAutoUpdate = 1
  NoAutoRebootWithLoggedOnUsers = 1
  AlwaysAutoRebootAtScheduledTime = 0
```

并保存原值；退出或关闭保护时恢复。辅助进程在父进程崩溃时会恢复；如果两者都被强制结束，下一次启动会检测孤儿备份并先恢复。

Windows 设置可能临时显示“部分设置由组织管理”，这是临时策略造成的，退出后恢复。

## 设置与配置

安装版配置：

```text
%LOCALAPPDATA%\ScreenCurtain\settings.ini
%LOCALAPPDATA%\ScreenCurtain\logs\
```

绿色版配置：

```text
<解压目录>\data\settings.ini
<解压目录>\data\logs\
```

主要设置自动保存，包括：

- 启动后最小化、关闭窗口行为、退出确认、托盘通知。
- 热键、物理关屏、笔记本物理关屏（默认关闭）、外接屏仅关背光（默认开启）、保持系统唤醒、滚轮恢复、恢复前台窗口。
- **熄屏时静音系统音频**（默认开启）。
- 更新保护开关、启动时自动尝试。
- 诊断、复制、日志目录和重置设置。

首版不提供开机自启、深色主题和英文界面。

## 安装器静默参数

```text
/SILENT            静默安装
/DIR=<path>        自定义安装目录
/NODESKTOP         不创建桌面快捷方式
/NORUN             安装后不启动
/NOARP             不写“应用与功能”卸载项（测试用）
/NOSHORTCUTS       不创建任何快捷方式（测试用）
/LOG=<path>        安装日志路径
```

卸载器支持 `/SILENT`，默认保留设置和日志，可在交互界面勾选一并删除。卸载前会尝试关闭正在运行的幕帘并恢复遗留的 Windows Update 策略。

## 从源码构建

无需 .NET SDK；使用 Windows 自带的 .NET Framework C# 编译器。

```powershell
cd screen-curtain
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

完整发布构建（图标、安装器、绿色版、哈希和发布说明）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-release.ps1
```

图标由 `tools\generate_icons.py` 生成（Pillow）；生成结果已提交，普通构建不依赖 Python。

## 开发/验证命令

```text
ScreenCurtain.exe --self-test          # 环境/显示器/透明光标自检
ScreenCurtain.exe --dry-run            # 显示 5 秒测试覆盖层，不关物理屏
ScreenCurtain.exe --test-blackout      # 黑屏/输入钩子集成测试
ScreenCurtain.exe --test-policy        # 内存存储的策略快照/回滚测试
ScreenCurtain.exe --test-shutdown-guard # 最小化窗口关机否决测试
ScreenCurtain.exe --test-audio         # 播放设备静音/恢复测试（会短暂静音）
ScreenCurtain.exe --request-exit       # 请运行中实例正常退出（安装器/卸载器使用）
ScreenCurtain.exe --repair-update      # 提权恢复遗留更新策略（卸载器使用）
```

`--test-guard-protocol` 在当前沙箱中因命名管道限制无法运行；完整安装/UAC/HKLM 验收需在普通 Windows 桌面执行。

## 已知边界

1. **全黑是主保证，物理关屏是尽力而为。** 不同显卡/显示器对 `SC_MONITORPOWER` 支持不一致，失败时仍保持全黑。
2. **鼠标移动可能从硬件层点亮显示器背光。** 笔记本默认跳过物理关屏以避免反复亮灭；若手动开启笔记本物理关屏，该现象可能仍存在。
   DDC/CI 背光控制只对支持 VCP 0x10 的外接屏有效；部分显示器/扩展坞会忽略 DDC 命令。内置屏 WMI 亮度在部分驱动、OLED/HDR 或受限账户下不可用；此时该屏保持全屏黑层。若亮度 0 仍不能完全熄灭背光，这是固件/背光最低亮度的限制。
3. **安全桌面（UAC、Ctrl+Alt+Del、锁屏）和系统强制界面无法由普通窗口覆盖。** 这是系统边界；Topmost Guard 不会尝试绕过。
   如果系统启用了“唤醒时要求登录”或强制空闲锁定策略，物理关屏后可能停留在锁屏；这是 Windows 安全策略，程序不会尝试绕过。
4. 第三方 popup/通知使用 Topmost Guard 尝试压住；若 Windows 系统 Toast 或独占全屏程序仍显示在其上，物理 DPMS 成功时可一并隐藏，失败时记录为系统限制。
5. **用户态程序不能对强制重启作 100% 保证。** 组策略/Intune 覆盖、用户在系统界面选择“仍要重启”、断电、蓝屏、内核故障均无法拦截；本工具针对正常 Windows Update 自动重启路径。
6. **关机否决依赖可见顶层窗口。** 保护期间主窗口只最小化、不隐藏到托盘。真实 Windows 11 关机测试需在普通桌面完成。
7. **静音恢复。** 恢复显示/退出后会校验并延迟重试音频恢复；锁屏时会在解锁后重试。如果进程被强杀或断电，可能暂时保持静音，下一次启动会读取恢复标记并还原。
8. **黑屏不是锁屏。** 熄屏期间当前会话是解锁的；请不要在公共场所无人值守运行。
9. **当前构建环境限制。** DSH 沙箱禁止 HKCU/HKLM 写入和命名管道，无法完成真实 UAC、完整安装快捷方式/ARP、系统关机和物理 DPMS 验收。安装器载荷和卸载器已用 `/NOARP /NOSHORTCUTS` 模式做文件级测试。

## 文档

- [设计约束与实现方案](docs/design.md)
- [完整 UI/UX 设计规范](docs/ui-design.md)
- [发布说明](dist/1.2.0/RELEASE-NOTES.md)

## 隐私

- 不联网、无遥测、无崩溃上传。
- 不记录键值、不截屏、不上传输入内容。
- 日志仅记录状态变化、API 返回码和“鼠标/键盘恢复”类别。
- 输入钩子只在熄屏流程中临时安装，恢复后立即卸载。
- 音频静音只操作系统播放设备，不访问麦克风/采集设备，不记录音频内容。
- 更新策略备份保存在 HKLM，退出/卸载时恢复。
