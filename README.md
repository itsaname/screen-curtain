# 幕帘 ScreenCurtain

ScreenCurtain 是一个面向 Windows 11 的熄屏工具。它会把所有显示器盖上一层无边框纯黑画面，并在支持的情况下只关闭屏幕背光，让显示器、USB Hub 和接在 Hub 上的键鼠继续工作。鼠标移动不会退出，点击鼠标或按键即可恢复。

当前版本：**1.3.3**。应用界面、安装器和卸载器都支持中文/English。

**English:** ScreenCurtain is a Windows 11 blackout tool. It covers every display with a borderless black layer and turns off only the panel backlight where supported, keeping monitors and their USB hubs powered. Move the mouse freely; a click or keypress restores the desktop. The app, installer and uninstaller are bilingual.

## 下载

| 版本 | 说明 |
| --- | --- |
| [安装版](https://github.com/itsaname/screen-curtain/releases/download/v1.3.3/ScreenCurtain-Setup-1.3.3.exe) | 按用户安装，不需要管理员权限 |
| [绿色版](https://github.com/itsaname/screen-curtain/releases/download/v1.3.3/ScreenCurtain-1.3.3-portable.zip) | 解压即用，配置和日志保存在同目录 `data\` |
| [SHA256SUMS](https://github.com/itsaname/screen-curtain/releases/download/v1.3.3/SHA256SUMS.txt) | 下载后校验文件完整性 |

也可以直接从 [GitHub Releases](https://github.com/itsaname/screen-curtain/releases/latest) 获取最新版本。

安装版默认装到 `%LOCALAPPDATA%\Programs\ScreenCurtain`，会创建开始菜单入口、可选桌面快捷方式和“应用与功能”卸载项。安装器本身不写 HKLM；更新保护只在运行时请求 UAC。

绿色版不写注册表，也不会创建快捷方式。若解压目录不可写，程序会自动回退到 `%LOCALAPPDATA%\ScreenCurtain` 并给出提示。

> 目前没有代码签名证书，Windows SmartScreen 可能提示“未知发布者”。请用 SHA256SUMS 校验下载文件。

## 它怎么工作

- **全屏黑层**：每台显示器各有一个无边框、置顶的全屏窗口，覆盖任务栏和通知区域，支持多显示器、竖屏和热插拔。
- **只关背光**：外接屏通过 DDC/CI `VCP 0x10` 把亮度设为 0；内置屏尝试通过 WMI `WmiSetBrightness(0)` 调暗。显示器保持供电，所以外接屏的 USB Hub 和键鼠不会掉线。
- **不关闭显示器电源**：黑色熄屏流程不会发送 `VCP 0xD6` 电源命令。这样即使显示器对 DDC 电源控制支持不好，也不会出现“关了以后醒不过来”的情况。
- **点击或按键恢复**：鼠标移动不会退出；鼠标按键、滚轮（可关闭）或键盘按键会恢复桌面、亮度和音频。首个唤醒输入会被吞掉，避免误触到桌面。
- **无操作自动熄屏**：可设置连续多少分钟没有键盘/鼠标输入后自动进入熄屏；时间可编辑，锁屏、对话框打开或已经熄屏时不会触发。
- **系统静音**：熄屏时静音当前所有活动播放设备，恢复时还原每台设备原来的静音状态。黑屏期间新接入的播放设备也会被静音。
- **更新重启保护**：运行时以管理员权限临时暂停 Windows Update 自动更新并阻止更新重启，退出或关闭保护时恢复原策略。可在设置中选择“程序启动后”或“仅熄屏期间”；两种模式都只在程序启动时请求一次 UAC。后者启动后先暂停并恢复策略，熄屏时通过已提权助手启用，唤醒后暂停，不会反复弹 UAC。
- **预览 3 秒**：预览会复用实际熄屏的背光调节、静音、黑层、隐藏光标和去圆角效果，3 秒后按相同顺序恢复。
- **托盘与通知**：黑屏时托盘图标不会被隐藏，只是被黑层自然盖住；本程序的托盘提示和错误消息会在恢复后合并显示。
- **中英文界面**：默认跟随系统语言，也可以在设置中手动切换，重启后生效。
- **开机自启动**：可在设置中写入当前用户启动项，不需要管理员权限；移动便携版目录后会刷新启动路径。
- **暗色主题**：支持自动、浅色、深色三种外观；跟随系统或手动选择，重启后生效。

## 系统要求

- Windows 11 23H2 / 24H2 / 25H2，x64。
- .NET Framework 4.8（Windows 11 自带）。
- 熄屏、静音和绿色版运行不需要管理员权限；更新重启保护需要 UAC。

## 使用

### 第一次运行

首次启动会打开 3 步向导：

1. 了解“黑屏不是锁屏”和默认静音行为。
2. 选择启用更新重启保护（需要 UAC），或只使用熄屏功能。
3. 设置全局快捷键，选择是否启动后最小化，完成向导。

如果直接关闭首次向导，向导不会保存“已完成”状态，下次启动仍会出现。

### 熄屏与恢复

你可以点击主窗口的“立即熄屏”，按默认快捷键 `Ctrl+Alt+Shift+L`，或从托盘菜单进入熄屏。

熄屏期间：

- 所有显示器被纯黑覆盖；
- 台式机默认尝试物理关屏，笔记本默认只覆盖黑层并关闭背光；
- 系统音频默认静音；
- 鼠标移动不会恢复，点击鼠标或按键后恢复；
- 系统锁屏、远程断开、系统挂起或程序退出时，会自动恢复显示，避免把人困在黑屏后面。

### 更新重启保护

启用保护后，程序会启动一个管理员权限的辅助进程，在运行期间临时写入：

```text
HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU
  NoAutoUpdate = 1
  NoAutoRebootWithLoggedOnUsers = 1
  AlwaysAutoRebootAtScheduledTime = 0
```

原值会先保存，退出或关闭保护时恢复。如果主进程异常退出，辅助进程会尝试恢复；如果两个进程都被强制结束，下一次启动会检查遗留备份并先恢复策略。

Windows 设置里可能临时出现“部分设置由组织管理”，这是临时策略造成的，退出保护后会消失。

## 设置

安装版的配置位于：

```text
%LOCALAPPDATA%\ScreenCurtain\settings.ini
%LOCALAPPDATA%\ScreenCurtain\logs\
```

绿色版位于：

```text
<解压目录>\data\settings.ini
<解压目录>\data\logs\
```

如果程序意外退出，会尽量在 `%LOCALAPPDATA%\ScreenCurtain\logs\crash-*.log` 生成一份崩溃报告；便携版数据目录可写时也会复制一份到 `data\logs\`。报告包含退出原因、黑屏状态、心跳延迟、最后一次日志和数据目录，便于排查网络盘/同步盘卡住导致的 UI 卡死。

可以在设置里调整：

- 启动后最小化、开机自启动、关闭窗口行为、退出确认、托盘通知；
- 界面语言：自动 / 中文 / English；
- 主题：自动 / 浅色 / 深色；
- 全局快捷键；
- 是否尝试物理关屏；
- 笔记本上的物理关屏（默认关闭）；
- 外接屏/内置屏仅关背光（默认开启）；
- 保持系统唤醒（执行状态 + PowerRequest 阻止自动睡眠/休眠；不覆盖合盖、电源键或主动休眠）、滚轮恢复、恢复前台窗口；
- 熄屏时静音系统音频；
- 更新保护开关和启动时自动尝试；
- 诊断信息、日志目录和重置设置。

## 安装器静默参数

```text
/SILENT            静默安装
/DIR=<path>        自定义安装目录
/NODESKTOP         不创建桌面快捷方式
/NORUN             安装后不启动
/NOARP             不写“应用与功能”卸载项（测试用）
/NOSHORTCUTS       不创建任何快捷方式（测试用）
/LOG=<path>        安装日志路径
/LANG=EN           强制英文界面
/LANG=ZH           强制中文界面
```

卸载器支持 `/SILENT`，默认保留设置和日志，也可以在交互界面里勾选一并删除。卸载前会尝试关闭正在运行的幕帘，并恢复遗留的 Windows Update 策略。

## 从源码构建

不需要 .NET SDK，使用 Windows 自带的 .NET Framework C# 编译器即可。

```powershell
cd screen-curtain
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

完整发布构建（图标、安装器、绿色版、哈希和发布说明）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-release.ps1
```

图标由 `tools\generate_icons.py` 生成（Pillow）；生成结果已提交，常规构建不依赖 Python。

## 开发和验证命令

```text
ScreenCurtain.exe --self-test           # 环境、显示器、透明光标自检
ScreenCurtain.exe --dry-run             # 显示 5 秒测试覆盖层，不关物理屏
ScreenCurtain.exe --test-blackout       # 黑屏/输入钩子集成测试
ScreenCurtain.exe --test-policy         # 策略快照/回滚测试
ScreenCurtain.exe --test-shutdown-guard # 最小化窗口关机否决测试
ScreenCurtain.exe --test-audio          # 播放设备静音/恢复测试
ScreenCurtain.exe --test-backlight      # 外接屏背光 100 → 0 → 100 测试
ScreenCurtain.exe --request-exit        # 请运行中的实例正常退出
ScreenCurtain.exe --repair-update       # 提权恢复遗留更新策略
```

`--test-guard-protocol` 依赖命名管道，在受限沙箱里无法运行；完整安装、UAC 和 HKLM 验收需要在普通 Windows 桌面完成。

## 已知边界

1. **全黑是主保证，物理关屏是尽力而为。** 不同显卡和显示器对 `SC_MONITORPOWER` 的支持不一致；失败时黑层仍然有效。
2. **背光控制取决于显示器/驱动。** 外接屏需要支持 DDC/CI `VCP 0x10`；内置屏需要 WMI 亮度接口。部分显示器、扩展坞、OLED/HDR 模式或受限账户下可能不可用，这时会保留全屏黑层。
3. **亮度 0 不一定等于完全灭灯。** 有些显示器的最低背光仍然可见，这是固件限制，不是黑层失效。
4. **安全桌面无法覆盖。** UAC、Ctrl+Alt+Del、锁屏和系统强制界面属于 Windows 的安全边界，普通窗口无法盖住。
5. **唤醒后可能仍需登录。** 如果系统启用了“唤醒时要求登录”或强制空闲锁定，这是 Windows 的安全策略，程序不会绕过。
6. **更新保护不是 100% 保证。** 组策略/Intune、用户在系统界面选择“仍要重启”、断电、蓝屏等情况无法拦截；本工具针对正常的 Windows Update 自动重启路径。
7. **关机否决依赖可见顶层窗口。** 保护期间主窗口只最小化，不隐藏到托盘。
8. **静音恢复可能延迟。** 恢复显示或退出后会校验并重试音频状态；如果进程被强制结束，下一次启动会读取恢复标记并还原。
9. **黑屏不是锁屏。** 熄屏期间当前会话仍然解锁，请不要在公共场所无人值守运行。
10. **当前测试环境有限。** DSH 沙箱禁止 HKCU/HKLM 写入和命名管道，因此真实 UAC、完整快捷方式/ARP、系统关机和物理 DPMS 验收仍需在普通桌面完成。

## 文档

- [设计约束与实现方案](docs/design.md)
- [完整 UI/UX 设计规范](docs/ui-design.md)
- [1.3.3 发布说明](dist/1.3.3/RELEASE-NOTES.md)

## 隐私

- 不联网、无遥测、无崩溃上传。
- 不记录键值、不截屏、不上传输入内容。
- 日志只记录状态变化、API 返回码和“鼠标/键盘恢复”类别。
- 输入钩子只在熄屏流程中临时安装，恢复后立即卸载。
- 音频静音只操作系统播放设备，不访问麦克风或采集设备。
- 更新策略备份保存在 HKLM，退出或卸载时恢复。

---

本软件由 **deepseek-v4.1-flash1017** 开发。
