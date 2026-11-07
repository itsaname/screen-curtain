# ScreenCurtain（幕帘）设计约束与实现方案

> 文档版本：1.2.0（实现与交付）  
> 目标平台：Windows 11（23H2 / 24H2 / 25H2，x64）  
> 工作名：ScreenCurtain，中文名“幕帘”  
> 目标：按需关闭或全黑**所有显示器**；只有鼠标点击或键盘按键才恢复正常显示；程序运行期间阻止 Windows 自动更新导致的重启。  
> 状态：1.2.0 已实现完整 UI、无边框全屏隐私层、系统静音、品牌图标、按用户安装器和绿色版；增加笔记本外接屏 DDC/CI 逐屏物理关屏，修复 UAC 握手、音频恢复与高 DPI 布局。UAC 真实 HKLM 策略写入、真实关机否决和物理 DPMS 关屏仍需在普通 Windows 环境实测。详见 `README.md`。

**阅读路径**：产品决策看 §0、§3、§7、§12；**界面与交互**看 [ui-design.md](ui-design.md)；技术评审看 §2、§5、§6、§9；实施看 §11 与附录。

---

## 0. 结论摘要

### 0.1 推荐方案

采用一个**便携式 WinForms 托盘程序 + 按需提权的更新保护辅助进程**：

1. **全黑是保底层**：为每台活动显示器创建一个无边框、纯黑、置顶窗口，覆盖包括任务栏在内的整个物理屏幕。即使显示器不支持软件关机，也能在普通桌面/应用窗口之上保证所有屏幕“完全黑暗”（安全桌面、独占全屏等边界见 5.9）。
2. **物理熄屏是增强层**：广播 `SC_MONITORPOWER = 2`，尽力让显示器进入 DPMS 关闭/待机。它不保证在所有显卡、显示器或 Modern Standby 机器上成功，因此不能作为唯一实现。
3. **唤醒判定只看点击/按键**：熄屏期间安装临时低级鼠标/键盘钩子；鼠标移动不退出，鼠标按键或任意键盘按键退出（滚轮默认不唤醒，可在设置中打开）。第一个唤醒输入被吞掉，不落到下方应用。钩子只存在于熄屏期间，不记录键值。
4. **会话保持可用**：熄屏期间用 `SetThreadExecutionState` 抑制系统睡眠、显示空闲和屏保锁屏；检测到会话锁定/断开/挂起时自动退出熄屏，避免把用户困在黑屏或锁屏后面。代价是熄屏期间桌面保持解锁、机器保持唤醒，需在 UI 中明确提示。
5. **更新重启双层防护**：
   - **策略层（主）**：提权辅助进程临时写入 Windows Update 的自动更新/自动重启策略，并保存原始值的完整快照；退出时逐项恢复。保护期间 Windows 自动更新保持暂停。
   - **关机否决层（辅）**：主程序保持一个可见的顶层窗口，处理 `WM_QUERYENDSESSION`；当策略层未覆盖到的重启尝试发生时，调用 `ShutdownBlockReasonCreate` 并在系统关机 UI 中阻止本次重启，显示明确原因。
6. **默认开启保护，失败必须明示**：首次运行先展示风险说明并取得用户确认，然后立即请求 UAC 启用保护；后续启动按已保存配置自动尝试。用户拒绝 UAC、策略被组策略覆盖、关机否决不可用时，状态显示为“降级”，不能向用户宣称已受保护。

### 0.2 关键设计判断

- **不能把 `SC_MONITORPOWER` 当作可靠 API**：它在不同 Windows 11 版本、显卡驱动、显示器和 Modern Standby 系统上的行为不一致。全黑覆盖层才是确定性保证。
- **鼠标移动可能在应用收到事件前就把显示器硬件唤醒**：这是显示链路/固件行为，用户态程序无法阻止。因此采用“物理屏可能被移动唤醒，但覆盖层继续全黑，直到点击/按键”的语义。这仍满足“只有点击鼠标或按键盘才恢复正常显示”。
- **Windows 不保证允许“无可见窗口”的进程取消关机**。因此更新保护期间主窗口不隐藏到托盘，而是最小化到任务栏；“退出并恢复更新”必须是显式动作。
- **用户态程序无法对 Windows 强制重启作 100% 保证**。策略被系统/组策略覆盖、用户在阻止界面选择“仍要重启”、断电、内核故障，都不在可控范围内。方案目标是可靠阻止**正常 Windows Update 自动重启路径**，并把所有降级情况透明化。

### 0.3 产品交付形态

- 单文件 `ScreenCurtain.exe`（同一个 EXE 带 `--update-guard` 隐藏参数，用于提权辅助进程）。
- 无外部运行库、无联网、无遥测；提供按用户安装器和绿色版 ZIP。
- 目标框架 `.NET Framework 4.8`（Windows 11 自带）；使用系统自带的 `csc.exe` 可编译，避免依赖本机不存在的 .NET SDK。
- 中文界面优先，字符串资源化，便于后续增加英文。

---

## 1. 需求与验收语义

### 1.1 功能需求

| 编号 | 需求 | 设计语义 |
|---|---|---|
| F1 | 按需关闭屏幕 | 托盘菜单、全局热键、主界面按钮均可触发 |
| F2 | 所有显示器全黑 | 对当前和熄屏期间新增的每一台活动显示器，各创建一个全屏纯黑置顶窗口 |
| F3 | 屏幕完全关闭的增强 | 尽力广播 DPMS 关闭；失败不影响 F2 |
| F4 | 只有鼠标点击或键盘按键恢复 | 鼠标移动不退出；鼠标按键、任意键盘按键退出（滚轮默认不唤醒，可配置） |
| F5 | 第一次唤醒输入不误操作 | 第一个恢复输入被拦截，不传给下方应用；随后恢复正常桌面 |
| F6 | 正常退出恢复 | 关闭覆盖层、清空电源抑制、尽力打开显示器、卸载输入钩子 |
| F7 | 运行期间阻止更新导致的重启 | 临时更新策略 + 关机否决 + 策略健康检查；默认开启 |
| F8 | 保护可退出、可恢复 | 退出程序、系统强制重启、UAC 失败等路径均不得留下永久系统改动 |
| F9 | 可观测 | 主界面/托盘明确显示“更新重启保护：受保护 / 降级 / 已关闭”及原因 |
| F10 | 安全失败 | 任意异常、崩溃、会话锁定、远程断开、显示拓扑异常时，优先恢复显示而不是继续黑屏 |

### 1.2 非功能需求

| 编号 | 约束 |
|---|---|
| N1 | 在 Windows 11 23H2/24H2/25H2、UAC 开启的普通用户环境可运行 |
| N2 | 不要求安装驱动、服务、内核组件；不修改系统文件 |
| N3 | 主 UI 以普通完整性运行；只有更新策略需要管理员权限 |
| N4 | 无网络访问；不采集、不记录键盘内容 |
| N5 | 单实例；重复启动只唤起已有窗口 |
| N6 | 多显示器、不同分辨率/缩放/旋转、负坐标、热插拔可处理 |
| N7 | 空闲 CPU 占用接近 0，常驻内存目标 < 80 MB |
| N8 | 关键状态转换幂等，重复触发不会创建多套覆盖层或重复写策略 |
| N9 | 所有系统修改可精确回滚；崩溃后能自动或半自动恢复 |
| N10 | 提供 `--status`、`--blackout`、`--restore`、`--dry-run` 等命令行入口，便于验收和自动化 |

### 1.3 状态定义

- **Idle**：程序运行，屏幕正常，更新保护按配置开启。
- **Arming**：已显示黑色覆盖层，正在等待触发输入释放、安装钩子，尚未接受唤醒输入。
- **Dark**：覆盖层已稳定，物理熄屏已尝试；等待鼠标按键/键盘按键。
- **Restoring**：收到恢复输入，正在卸载钩子、关闭覆盖层、恢复显示器/电源状态。
- **GuardActive**：更新策略已写入并验证，关机否决窗口可用。
- **GuardDegraded**：程序仍在运行，但更新保护缺少某个必要条件（例如 UAC 被拒绝、策略被覆盖、窗口不可见）。
- **GuardOff**：用户显式关闭更新保护。

`Dark` 与更新保护状态相互独立：熄屏失败/恢复不应破坏更新保护，更新保护降级也不应阻止熄屏。

### 1.4 需求追踪

| 需求 | 设计位置 | 主要验收 |
|---|---|---|
| F1 按需触发 | §5.2、§7.2、§7.3 | D1 |
| F2 所有显示器全黑 | §2.2、§5.3、§5.7 | D2–D6 |
| F3 物理熄屏增强 | §5.8 | D13–D15 |
| F4 只点击/按键恢复 | §5.4 | D7–D12 |
| F5 首个输入不误操作 | §5.4、§5.5、§6.4 | D8、D9 |
| F6 正常恢复 | §5.5、§5.6 | D1、D24 |
| F7 更新重启防护 | §6.1–§6.6 | U1–U15 |
| F8 可退出/可恢复 | §6.3、§6.5、§8.3 | U3–U7、U13 |
| F9 状态可观测 | §6.6、§7 | U1、U2、U11 |
| F10 失败安全 | §5.6、§5.9、§8.3 | D18–D22 |

---

## 2. 设计约束分析

### 2.1 Windows 没有“可靠关闭任意显示器”的通用用户态 API

可选路径及现实行为：

| 方式 | 是否能真正关面板 | 覆盖全部显示器 | 可靠性 | 结论 |
|---|---|---|---|---|
| 广播 `WM_SYSCOMMAND / SC_MONITORPOWER = 2` | 通常可以，取决于显卡驱动、显示器和电源策略 | 一次广播通常覆盖全部 | 中；可能变待机、可能被驱动忽略，Modern Standby 下行为不同 | 作为增强层 |
| DDC/CI `SetVCPFeature(0xD6)` 按显示器关闭 | 可以 | 需逐台处理，兼容性差 | 低到中；部分显示器/转接器不支持 | 不纳入 v1 |
| `SetMonitorPowerState`（DXVA2） | 可以 | 需逐台处理 | 依赖驱动 | 不纳入 v1 |
| 修改电源计划让显示器立即超时 | 延迟生效，会改变系统设置 | 是 | 低且侵入性强 | 不采用 |
| 锁定工作站/睡眠 | 屏幕确实关闭 | 是 | 高 | 会引入登录/唤醒问题，不采用 |
| 纯黑置顶覆盖窗口 | 面板仍通电，但所有像素为黑 | 可精确控制 | 高 | **作为保底层** |

**结论**：同时实现“全黑覆盖”与“尽力物理关闭”。只要覆盖层完整，物理关闭失败仍满足“完全黑暗”；物理关闭成功则进一步省电并隐藏背光。

### 2.2 覆盖所有显示器比“创建一个虚拟桌面大小的窗口”更稳

Windows 多显示器坐标可以：

- 为负值（副屏在主屏左侧/上方）；
- 使用不同分辨率、缩放比例、旋转方向；
- 在运行中热插拔、切换复制/扩展模式、修改主屏；
- 由不同 GPU 驱动。

设计要求：

- 用 `EnumDisplayMonitors` 枚举物理显示器矩形，而不是用主屏尺寸推算。
- 每台显示器一个窗口，窗口矩形等于 `MONITORINFO.rcMonitor`，从而覆盖任务栏。
- 进程声明 **Per-Monitor V2 DPI 感知**，确保拿到物理像素坐标，避免 150%/200% 缩放下覆盖不全。
- 监听 `WM_DISPLAYCHANGE`、`WM_SETTINGCHANGE`、DPI 变化，防抖后重建覆盖层。
- Windows 11 的圆角/阴影可能导致边角露出桌面：对这些窗口设置 `DWMWA_WINDOW_CORNER_PREFERENCE = DWMWCP_DONOTROUND`，并用无边框 `WS_POPUP` 窗口。

当前环境已实测到两台显示器：`0,0 2560×1440` 与 `2560,0 1707×960`，虚拟桌面宽 4267。说明多显示器不是理论场景，设计必须从第一版就支持。

### 2.3 输入唤醒的物理链路不受应用控制

显示器被 DPMS 关闭后，鼠标移动、点击、键盘按键都可能由**硬件/驱动**直接触发显示器上电，应用通常只能在之后收到输入事件。因此：

- 无法用纯软件保证“鼠标移动绝不点亮背光”；
- 但可以保证鼠标移动**不退出全黑覆盖层、不恢复正常桌面**；
- 点击/按键才触发恢复，满足用户可感知的“必须点击或按键”；
- 第一个点击/按键必须被吞掉，避免恢复后误触下方按钮。

低级钩子的约束：

- `WH_KEYBOARD_LL` / `WH_MOUSE_LL` 是会话级全局钩子，只在 `Dark` 期间安装，恢复后立刻卸载。
- 回调必须极快；只做状态判断和 `PostMessage`，不能在回调里关闭窗口或做 I/O。
- 安全软件、系统钩子超时策略可能使钩子失效；覆盖窗口自身的 `MouseDown` / `KeyDown` 是后备路径。
- 安全桌面（UAC、Ctrl+Alt+Del、锁屏）优先于应用，钩子和覆盖层都不可用时不得声称可拦截。
- 不得使用 `BlockInput`，否则一旦程序异常会真正锁死用户输入。

### 2.4 会话、电源和“熄屏后还能正常回来”

若只关显示器而不抑制系统空闲，Windows 可能：

1. 进入睡眠；
2. 启动屏保并锁定会话；
3. 在用户点击后显示锁屏而不是原桌面。

因此 `Dark` 期间需要：

- `SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED)`：阻止系统睡眠、显示空闲关闭和屏保启动。
- 退出时恢复为 `ES_CONTINUOUS`，避免永久阻止睡眠。
- 注册 WTS 会话通知：锁屏、注销、控制台/远程断开时立即退出 `Dark`。
- 监听挂起/恢复；系统挂起前退出 `Dark`，恢复后不自动重新进入，避免锁屏/显示状态不确定。
- 组策略“计算机不活动限制”等强制锁屏不一定被 `ES_DISPLAY_REQUIRED` 阻止；因此锁屏事件必须走“退出 Dark”路径，宁可恢复正常也绝不把用户困在黑屏后。

**安全代价**：熄屏期间桌面保持解锁、机器保持唤醒，任何能物理接触鼠标/键盘的人都能点击恢复并操作当前会话。黑屏不是锁屏，这一点必须在首次运行和主界面明确警告。

### 2.5 更新重启防护的边界

Windows 11 的更新/重启不是单一可控入口：

- `wuauserv`、`UsoSvc`、`WaaSMedicSvc`、`DoSvc` 等多个服务参与；
- UpdateOrchestrator 使用计划任务和工作站空闲判定；
- Windows Update for Business/组策略可以覆盖本地设置；
- “Active Hours”、截止期限、强制重启策略会改变重启时机；
- 关机/重启请求可来自 Windows Update、用户、企业策略或系统服务；
- `WM_QUERYENDSESSION` 本身不告诉应用“这次是自动更新重启还是用户主动重启”；
- 用户可以在 Windows 的阻止界面选择“仍要重启”；
- 应用必须保持可见顶层窗口，隐藏到托盘后系统可能不再允许它取消关机。

因此采用分层策略，而不是单一“银弹”：

1. **自动更新暂停**：运行期间写 `NoAutoUpdate = 1`，从源头减少新的更新安装和重启请求。
2. **自动重启抑制**：`NoAutoRebootWithLoggedOnUsers = 1`、`AlwaysAutoRebootAtScheduledTime = 0`，覆盖自动更新已准备重启的情况。
3. **关机否决**：处理 `WM_QUERYENDSESSION`，阻止实际发生的重启尝试。
4. **健康检查与精确恢复**：辅助进程监视策略是否被改回，退出时恢复原值；主进程监视辅助进程与可见窗口状态。

这套组合仍在以下情况失效，应在文档和 UI 中如实说明：

- 微软/企业策略强制重启，绕过用户态阻止；
- 用户在系统阻止界面主动选择“仍要重启”；
- 更新通过非 Windows Update 渠道或离线安装触发重启；
- 电源故障、蓝屏、内核崩溃；
- 设备受 Intune/域组策略强管控，本地策略被刷新覆盖。

> 如果验收标准要求“绝对不可能自动重启”，单靠用户态程序无法承诺。可行路线是把设备纳入 Windows 11 企业版/教育版的 Windows Update for Business、维护窗口和设备管理策略，或使用专用展示设备/虚拟机。该方向不属于本工具 v1 的“单文件便携程序”范围，可作为企业版扩展。

### 2.6 可见窗口与更新保护 UI 的强关联

Microsoft 文档明确说明：没有可见窗口的应用，系统不允许其取消关机。隐藏到通知区域的窗口不算可见顶层窗口。这带来一个产品约束：

- 更新保护启用时，主窗口不得只藏在托盘；
- 主窗口可以最小化到任务栏，但是否被系统计入“可见顶层窗口”需要在目标 Windows 11 版本做验证；若不满足，保护期间必须保留一个很小的可见状态窗口，或者明确降级。
- 本方案默认：**“关闭”按钮 = 最小化到任务栏，不退出；托盘“退出并恢复更新”才退出保护。**
- 熄屏期间覆盖窗口本身是可见顶层窗口，也可承担关机否决；但空闲（未熄屏）时仍需主窗口可见。

这是本设计里最容易为了“托盘程序的整洁”而牺牲功能的地方，必须显式处理。

### 2.7 权限约束

当前实测环境：

- Windows 11 25H2，Build 26200.9711；
- UAC 已开启，当前会话不是管理员；
- .NET Framework 4.8.09221 可用；
- 没有 .NET SDK，但有系统自带 C# 编译器；
- 存在两台活动显示器。

结论：

- 全黑、输入捕获、物理熄屏、关机否决都不需要管理员权限；
- 写 `HKLM\...\WindowsUpdate` 策略必须提权；
- 若把整个 UI 进程设为 `requireAdministrator`，所有窗口和全局钩子都会在高完整性运行，扩大攻击面，也增加每次启动的 UAC 阻力。因此采用“普通 UI + 按需提权辅助进程”。

### 2.8 企业/受管设备约束

在域加入、Intune 或组策略管理设备上，本地写入的 Update 策略可能：

- 被组策略刷新覆盖；
- 违反企业安全策略；
- 在用户不知情时影响其他用户会话（策略是机器级的）。

设计必须：

- 读取并保留原始值，绝不永久禁用更新；
- 检测到策略被外部改写时显示“降级”，而不是静默反复对抗；
- 在受管设备首次启用前提示“可能与企业策略冲突”，要求用户确认；
- 提供一个“一键恢复 Windows Update 设置”的修复入口。

---

## 3. 方案选型

### 3.1 显示关闭方案

**选定：黑色覆盖层为确定性主层，`SC_MONITORPOWER` 为增强层。**

理由：

- 用户需求允许“完全关闭或完全黑暗”；
- 覆盖层不受显卡/显示器兼容性影响；
- 覆盖层在普通桌面场景中能保证所有显示器无内容泄漏；安全桌面、独占全屏等例外见 5.9；
- 物理熄灭成功时兼具省电和真正无背光；
- 不引入 DDC/CI 的兼容性和潜在闪烁问题。

### 3.2 唤醒输入方案

**选定：低级鼠标/键盘钩子（唤醒判定 + 吞掉首个事件） + 覆盖窗口自身输入事件（后备）。**

对比：

| 方案 | 能吞掉首个事件 | 能覆盖全部按键 | 失效风险 | 结论 |
|---|---|---|---|---|
| 低级钩子 | 是 | 是 | 安全软件/超时可能移除 | 主方案 |
| 覆盖窗口键鼠事件 | 部分是 | 仅当窗口有焦点 | 焦点被抢则失效 | 后备 |
| Raw Input（`RIDEV_INPUTSINK`） | 否 | 可后台收键 | 实现复杂，不能抑制系统键 | 可作为第三层，不进 v1 |
| 轮询 `GetAsyncKeyState` | 否 | 不完整 | 漏事件、CPU 浪费 | 不采用 |

只安装**临时钩子**；不安装持久键盘记录器；不记录键值，仅记录“键盘”或“鼠标”这一类别。

### 3.3 更新保护方案

| 方案 | 权限 | 效果 | 侵入性 | 结论 |
|---|---|---|---|---|
| 仅 `WM_QUERYENDSESSION` 否决 | 无 | 可被强制重启/不可见窗口/用户覆盖破坏 | 低 | 不够，作为辅助 |
| 临时 Windows Update 策略 + 关机否决 | 管理员 | 覆盖正常自动更新重启路径 | 中，且只在运行期间 | **选定** |
| 停止/禁用更新服务、禁用计划任务 | 管理员/SYSTEM | 更强，但可能破坏商店/Defender/更新状态 | 高 | 不纳入 v1 |
| Windows 服务 + 驱动/内核拦截 | SYSTEM/驱动 | 最接近防篡改 | 极高 | 不适用于便携工具 |
| 企业 MDM/WUfB 设备策略 | 管理员/IT | 最可靠、可审计 | 需要企业环境 | 高端替代方案 |

### 3.4 进程与权限模型

**选定：普通完整性主进程 + 同一 EXE 的提权辅助进程。**

- 主进程：UI、覆盖层、钩子、关机否决、状态展示。
- 辅助进程：仅做策略快照、写入、健康检查、恢复；无常驻 UI；通过受限命名管道与父进程通信；监视父进程句柄，父进程消失即恢复。
- 不用 Windows 服务（v1）；这降低了部署复杂度，也意味着普通用户可以结束进程从而提前解除保护，这是便携工具的固有限制。

### 3.5 技术栈

- C# / WinForms / `.NET Framework 4.8`。
- 不依赖 NuGet 包；Win32 通过 P/Invoke。
- 选择 WinForms 而非 WPF：托盘、全屏无边框窗口、低级消息循环更直接，体积更小。
- 单一 EXE，嵌入图标、清单和透明光标资源。
- 代码需兼容系统自带 `csc.exe` 支持的 C# 版本（避免依赖新 SDK 语法）。

---

## 4. 总体架构

### 4.1 进程与组件

```text
┌──────────────────────────── 主进程 ScreenCurtain.exe（asInvoker / 中完整性） ────────────────────────────┐
│                                                                                                         │
│  MainForm / TrayIcon / Settings                                                                          │
│        │                                                                                                 │
│        ├── ProtectionController ──────────────┐                                                          │
│        │      │                              │                                                          │
│        │      ├── BlackoutController         │                                                          │
│        │      │      ├── DisplayManager ─────┼── EnumDisplayMonitors                                      │
│        │      │      ├── OverlayForm × N     │   per-monitor black topmost windows                      │
│        │      │      ├── InputGate ──────────┼── WH_MOUSE_LL / WH_KEYBOARD_LL (Arming/Dark 期间)         │
│        │      │      ├── PowerKeeper ────────┼── SetThreadExecutionState                                 │
│        │      │      └── MonitorPower ───────┼── SC_MONITORPOWER (best effort)                           │
│        │      │                              │                                                          │
│        │      ├── SessionWatcher ────────────┼── WTSRegisterSessionNotification / WM_POWERBROADCAST      │
│        │      ├── ShutdownGuard ─────────────┼── visible top-level window + WM_QUERYENDSESSION           │
│        │      └── GuardClient ───────────────┼── named pipe                                             │
│        │                                     │                                                          │
│        └── Diagnostics / Logging / CLI                                                                   │
│                                                                                                         │
└─────────────────────────────────────────────────────────────────────────────────────────────────────────┘
                                          │ 命名管道（握手、状态、恢复命令）
                                          │ 父进程句柄（等待退出）
┌──────────────────────────── 辅助进程 ScreenCurtain.exe --update-guard（高完整性） ─────────────────────────┐
│                                                                                                         │
│  GuardServer ── PolicyManager ── HKLM WindowsUpdate AU 策略                                             │
│        │             │                                                                                  │
│        │             └── SnapshotStore (HKLM 备份 / 孤儿恢复)                                            │
│        └── ParentWatcher + PolicyHealthLoop                                                             │
│                                                                                                         │
└─────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

### 4.2 模块职责

| 模块 | 职责 | 失败时 |
|---|---|---|
| `ProtectionController` | 总状态机，协调熄屏与更新保护，保证幂等 | 进入安全恢复 |
| `DisplayManager` | 枚举显示器、创建/重建覆盖窗口、DPI/热插拔处理 | 不能完整覆盖则取消熄屏 |
| `OverlayForm` | 纯黑置顶窗口、透明光标、窗口消息、覆盖任务栏 | 任一台创建失败则整体回滚 |
| `InputGate` | 安装/卸载钩子，识别点击/按键，吞掉首个恢复输入 | 降级为窗口事件，仍可点击恢复 |
| `PowerKeeper` | 抑制睡眠/显示空闲/屏保锁屏，退出时清理 | 记录错误并恢复 `ES_CONTINUOUS` |
| `MonitorPower` | 广播显示器关闭/打开，报告是否成功 | 忽略失败，保持全黑 |
| `SessionWatcher` | 锁屏/注销/控制台/远程断开/挂起 | 立即退出 Dark |
| `Watchdog` | Dark 期间监视 UI 心跳；严重卡死时最后手段终止自身进程，让覆盖窗口/钩子随进程销毁 | 记录、终止进程；辅助进程恢复策略 |
| `TopmostGuard` | Dark 期间监听新顶层窗口并重新置顶全屏层，降低第三方 popup 出现在黑层上的概率 | 记录/忽略；安全桌面不可覆盖 |
| `AudioMuteService` | 进入 Dark 时静音活动播放端点，恢复时按设备 ID 还原；持久化异常恢复状态 | 无设备/音频服务失败时继续黑屏并记录 |
| `ShutdownGuard` | 可见窗口、`ShutdownBlockReasonCreate`、`WM_QUERYENDSESSION` | 标记 GuardDegraded |
| `GuardClient` | 启动提权辅助进程、命名管道握手、状态、退出恢复 | 标记 GuardDegraded，不影响熄屏 |
| `GuardServer` | 高完整性策略快照、写入、健康检查、恢复、父进程看门狗 | 保留备份，供下次恢复 |
| `Settings` | 热键、启动行为、日志、提示确认 | 使用安全默认值 |
| `Diagnostics` | 日志、状态导出、CLI | 不影响核心 |

### 4.3 线程模型

v1 采用单一 UI 线程处理窗口/托盘/钩子/定时器，另有一个不依赖 UI 消息循环的极简看门狗线程；要求所有 UI 回调不做阻塞操作：

- 钩子回调只设置原子标志、`PostMessage` 自定义唤醒消息，然后立即返回。
- 覆盖层创建、关闭、显示器重枚举由 UI 定时器串行执行。
- 更新辅助进程是独立进程，不阻塞 UI。
- 提权启动（`ShellExecuteEx` + `runas`）放到后台线程执行：UAC 同意界面属于安全桌面，不能让主 UI 线程在等待提权结果时停止处理 `WM_QUERYENDSESSION`。
- 任何文件/注册表/管道操作放到后台任务，完成后 `BeginInvoke` 回 UI。
- 禁止在 UI 线程执行 `Thread.Sleep`、同步网络、长文件 I/O。需要等待显示刷新的短暂时序用定时器分阶段执行，而不是阻塞。

> 若实现后发现覆盖层/钩子会被主 UI 的复杂操作拖累，可将 `BlackoutController` 拆到专用 STA 消息线程；v1 以“UI 线程不阻塞”为约束先保持简单。

---

## 5. 详细设计：全屏关闭/全黑

### 5.1 状态机

```text
               用户触发 (热键/托盘/按钮/CLI)
                          │
                          ▼
                     [Arming]
             创建每屏覆盖层、隐藏光标
             安装输入钩子、进入输入静默期
             SetThreadExecutionState(...)
                          │
        ┌─────────────────┼──────────────────┐
        │                 │                  │
 t0+400ms 广播      触发输入释放且       覆盖层/枚举失败
 关屏（失败不改变     静默 700ms              │
 状态）                 │                    ▼
        │              ▼                 [Idle]（恢复显示并发错）
        └──────────► [Dark]
                          │
       ┌──────────────────┼───────────────────┐
       │                  │                   │
   鼠标移动         鼠标按键/键盘键      锁屏/断开/挂起
   (保持 Dark)           │                   │
       │                 ▼                   ▼
       └────────────► [Restoring] ◄───────────┘
                          │
                 卸载钩子、关闭覆盖层
                 SC_MONITORPOWER = -1
                 恢复 ES_CONTINUOUS
                 恢复原前台窗口
                          │
                          ▼
                        [Idle]
```

关键不变量：

- 任一时刻最多一个 `BlackoutController` 操作在运行。
- 覆盖层创建失败或任一显示器未被覆盖时，不进入 `Dark`。
- `Dark` 必须有可用的恢复路径（钩子或覆盖窗口事件至少一个）。
- `Restoring` 必须幂等，重复调用只执行一次清理。
- 更新保护状态与 `Dark` 状态互不覆盖，但 `GuardDegraded` 不能静默恢复为受保护。

### 5.2 进入熄屏的时序

以热键 `Ctrl+Alt+Shift+L` 触发为例：

1. **t0：前置检查**
   - 单实例、当前无 `Arming/Dark`；
   - 取得当前前台窗口句柄（用于恢复）；
   - 检查显示枚举结果至少有一个显示器；
   - 若更新保护未启用，不阻止熄屏，但主界面提示。

2. **t0+0ms：先显示覆盖层，再关物理屏**
   - 对每台显示器创建 `OverlayForm`；
   - `Show()` 后设置 `TopMost`、全屏矩形、纯黑背景、隐藏光标；
   - 对覆盖窗口调用 `DwmSetWindowAttribute(DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_DONOTROUND)`；
   - `DwmFlush()`（或短定时器）确保黑帧已提交；
   - 此步骤先于物理熄屏，避免显示器关闭前闪现桌面内容。

3. **t0+50ms：安装输入钩子并进入静默期**
   - 安装 `WH_MOUSE_LL`、`WH_KEYBOARD_LL`；
   - 设置 `inputArmedAt = now + 700ms`；
   - 静默期内不响应唤醒，防止触发热键的按键释放、托盘菜单点击的鼠标抬起立即退出；
   - 更稳妥的规则：Arming 期间任何键盘按键（包括长按重复）或鼠标按键按下/抬起都会把计时器重置为“现在 + 700ms”；鼠标移动不重置。这样长按热键不会在 700ms 后因按键重复而立即恢复，而是等按键释放且静默 700ms 后才进入可唤醒状态；
   - 静默期内依旧允许覆盖层接收事件但不处理退出。

4. **t0+400ms：尝试物理熄屏**
   - 调用 `SendMessageTimeout(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, 2, SMTO_ABORTIFHUNG, 1000, ...)`；
   - 失败/超时只记录，不改变 `Dark`；
   - 不因物理熄屏失败而关闭覆盖层。

5. **静默期结束：进入可唤醒状态**
   - 鼠标移动：直接放行但覆盖层继续存在；不退出。
   - 鼠标按键（以及用户显式开启的滚轮）：拦截并 `PostMessage(WM_APP_WAKE_BY_MOUSE)`；
   - 键盘按下：拦截并 `PostMessage(WM_APP_WAKE_BY_KEY)`；
   - 只响应 `WM_KEYDOWN/WM_SYSKEYDOWN`，不响应 KeyUp；避免按键释放退出。
   - 保持 `SetThreadExecutionState` 状态。

6. **t0 后：可选物理重关**
   - v1 不重复广播关闭。鼠标移动可能让面板亮起，但覆盖层仍是全黑；重复关闭可能造成背光/继电器频繁切换，收益低且体验差。

### 5.3 覆盖窗口具体参数

每台显示器创建：

```text
FormBorderStyle = None
BackColor       = Color.Black
TopMost         = true
ShowInTaskbar   = false
StartPosition   = Manual
Bounds          = rcMonitor（物理像素）
AutoScaleMode   = None
Cursor          = transparent cursor
CreateParams    : WS_POPUP
ExtendedStyle   : WS_EX_TOOLWINDOW
```

补充：

- 不允许窗口透明或半透明；纯黑才能保证内容不可见。
- 不设置 `WS_EX_NOACTIVATE`（键盘焦点需要激活窗口，因此保持可激活），但输入钩子是主要恢复路径。
- 窗口激活后把焦点放到主覆盖窗口；点击任意副屏覆盖窗口也应恢复。
- 使用透明光标资源，避免黑屏上出现白色箭头；程序退出/恢复时随窗口销毁恢复系统光标，不使用全局 `ShowCursor` 计数，降低“光标消失后回不来”的风险。
- 不用一个虚拟桌面大窗口，避免跨 DPI/跨屏窗口被系统限制或边角计算错误。
- .NET Framework 4.8 的 WinForms 除 `app.manifest` 中声明 `PerMonitorV2` 外，还需在 `app.config` 中启用 `System.Windows.Forms.ApplicationConfigurationSection` 的 `DpiAwareness = PerMonitorV2`；并在创建任何窗口前调用 `SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)` 作为兜底。

### 5.4 输入语义（验收口径）

| 输入 | 是否恢复 | 是否吞掉该输入 | 备注 |
|---|---|---|---|
| 鼠标移动 | 否 | 否 | 面板可能因硬件唤醒，但覆盖层保持全黑 |
| 左键/右键/中键/侧键按下 | 是 | 是 | 第一个按下被拦截，不落到下方应用 |
| 鼠标滚轮 | 默认否，可配置 | 开启时是 | 严格对齐“点击鼠标”语义，默认不让滚轮唤醒 |
| 任意键盘按键按下 | 是 | 是 | 包括修饰键；不响应 keyup |
| 触摸屏点击 | 视窗口消息而定 | 视情况 | 不在 v1 保证范围；触摸板通常映射为鼠标左键，可恢复 |
| Ctrl+Alt+Del | 不可拦截 | 否 | 安全桌面；若用户进入，覆盖层仍在，返回桌面后点击/按键可恢复 |
| Win+L | 通常触发恢复 | 视钩子/系统响应 | 若系统先锁屏，则按“会话锁定”安全路径退出 Dark，绝不留黑层 |
| 系统锁屏/远程断开 | 自动退出 Dark | — | 防止在黑屏后叠加锁屏 |
| 显示器热插拔 | 重新覆盖新屏 | — | 新屏最多短暂亮起，随后覆盖 |

### 5.5 恢复时序

当 `InputGate` 收到合格的点击/按键：

1. 设置状态 `Restoring`，阻止再次进入恢复流程。
2. 低层钩子先返回“已处理”，吞掉当前唤醒事件。恢复流程立即开始，不做长时间全局吞键。
3. 卸载两个低级钩子（失败也继续）；此时覆盖窗口仍在，鼠标抬起/键盘抬起不会落到下方应用。
4. 广播 `SC_MONITORPOWER = -1` 尽力点亮显示器；不等待其返回。
5. 恢复 `SetThreadExecutionState(ES_CONTINUOUS)`。
6. 依次关闭覆盖窗口（`Close`/`Dispose`），确保所有显示器露出桌面。
7. 若之前记录的前台窗口仍有效且不是本程序窗口，调用 `SetForegroundWindow` 尽量把焦点还给用户原来的应用；失败可忽略，不模拟点击。
8. 状态回到 `Idle`，记录“被鼠标/键盘恢复”，不记录键值。
9. 若恢复期间收到会话锁定/退出消息，继续完成资源清理，不重新进入 Dark。

### 5.6 电源与锁屏策略

进入 `Dark`：

```text
SetThreadExecutionState(
    ES_CONTINUOUS |
    ES_SYSTEM_REQUIRED |
    ES_DISPLAY_REQUIRED
)
```

退出：

```text
SetThreadExecutionState(ES_CONTINUOUS)
```

说明与风险：

- `ES_SYSTEM_REQUIRED`：防止系统因空闲进入睡眠。
- `ES_DISPLAY_REQUIRED`：防止 Windows 自身显示超时/屏保导致会话锁定。
- 这两个标志是“请求”，不能覆盖组策略的机器不活动锁屏、用户手动 Win+L、合盖电源动作、电池策略。
- 某些显卡驱动下，`ES_DISPLAY_REQUIRED` 可能抵消 `SC_MONITORPOWER`，导致面板未真正断电；此时全黑覆盖仍成立，属于可接受降级。实现阶段必须在目标机器验证，不把“物理断电”作为验收硬条件。
- 熄屏会阻止系统空闲睡眠，笔记本使用电池时会增加耗电。UI 必须提示，可在设置中提供“允许系统睡眠”的关闭选项（默认否；开启后如果系统睡眠，输入唤醒后仍由覆盖层/恢复流程处理）。

会话监视：

- `WTS_SESSION_LOCK`：立即退出 Dark。
- `WTS_SESSION_LOGOFF`：退出 Dark 并清理保护。
- `WTS_CONSOLE_DISCONNECT` / `WTS_REMOTE_DISCONNECT`：退出 Dark，防止远程用户面对无法解释的黑屏。
- `WM_POWERBROADCAST / PBT_APMSUSPEND`：退出 Dark 后再允许挂起。
- `PBT_APMRESUMEAUTOMATIC`：只刷新状态，不自动再次熄屏。

### 5.7 显示器热插拔与 DPI 变化

`DisplayManager` 在 `Dark` 期间监听：

- `WM_DISPLAYCHANGE`：分辨率/显示器数量变化；
- `WM_SETTINGCHANGE` 且涉及工作区/显示；
- `WM_DPICHANGED`：缩放变化；
- `WM_WTSSESSION_CHANGE`：会话切换。

处理：

1. 收到显示变化后先立即重新枚举并覆盖新增显示器；500 ms 防抖只用于合并重复的布局/移除事件，避免新显示器长时间显示桌面。
2. 重新 `EnumDisplayMonitors`。
3. 为新显示器创建覆盖层；移除已不存在显示器的窗口。
4. 对已有屏幕重新应用 `TopMost` 和物理矩形。
5. 若枚举失败或任一覆盖层创建失败：退出 Dark，恢复全部显示，并记录错误。
6. 不重新发送物理熄屏（除非用户重新触发），避免新显示器接入时反复闪断；新屏会立即被黑层覆盖。

### 5.8 物理熄屏 API 的具体用法

```text
SendMessageTimeout(
    HWND_BROADCAST,
    WM_SYSCOMMAND,
    SC_MONITORPOWER,
    2,                     // 2 = off；-1 = on；1 = low power
    SMTO_ABORTIFHUNG,
    1000 ms,
    out result
)
```

- 广播失败、超时、返回 0 都不视为致命错误。
- 进入恢复时发送 `-1`，仅作为“唤醒兜底”，实际唤醒通常由输入硬件完成。
- 不调用 DDC/CI，不对显示器发 I2C 命令，避免损坏或不兼容。
- 不依赖 `SC_MONITORPOWER` 的判断结果；无法查询“屏幕是否真的断电”。

### 5.9 全屏/置顶与黑屏隐私的边界

- 每个覆盖窗口为无边框、覆盖 `rcMonitor` 的 topmost 前台窗口；覆盖任务栏和通知区域。
- **不隐藏 `NotifyIcon`**：托盘图标由全屏层自然遮挡；恢复后不会出现重新出现的闪烁。
- **本程序弹窗/气泡在 Dark 期间全部静默**：`AppNotifier` 拒绝任何气泡，错误进入队列，恢复后再提示；Dark 中不显示应用对话框。
- **Topmost Guard**：`SetWinEventHook(EVENT_OBJECT_SHOW, EVENT_SYSTEM_FOREGROUND)` + 150ms 定时器重新置顶，压住普通第三方顶层窗口/popup/普通 topmost 悬浮窗。该机制是受控的，不与系统安全提示做无休止 z-order 竞争。
- **明确不能覆盖**：UAC、安全桌面、Ctrl+Alt+Del、锁屏、系统关键/安全提示。物理 DPMS 成功时可一并隐藏；这不是可绕过的用户态边界。
- **系统 Toast** 若仍显示在覆盖层之上，记录实际结果并以物理关屏作为兜底，不修改通知中心或使用未公开 API。
- **系统静音**：`AudioMuteService` 通过 Core Audio 枚举活动 `eRender` 设备，在进入 Dark 时静音并记录原状态；恢复时逐设备恢复。新设备在 Dark 期间每 2 秒重新枚举并静音。状态写入 `audio-restore`；看门狗终止前尝试紧急恢复，下次启动检测遗留状态。异常强杀/断电可能暂时保持静音。
- **音频恢复时序**：音频恢复放在显示器 ON、黑层关闭之后，而不是物理关屏期间；恢复会校验 `GetMute`，并在 500ms/1500ms/3000ms 延迟重试；锁屏后在 `WTS_SESSION_UNLOCK` 重试保留的恢复状态。
- **笔记本策略**：检测到电池时默认不发送全局 `SC_MONITORPOWER`，只用无边框全屏黑层，避免内置屏背光因鼠标移动反复亮/灭，以及物理关屏触发的唤醒锁屏。用户可在设置中单独启用“在笔记本上尝试物理关屏”，默认关闭；桌面设备仍按 `TryPhysicalOff` 执行。
- **屏幕仅关背光**：优先通过 `DisplayConfigGetDeviceInfo` 的 `outputTechnology` 判定连接类型：`Internal`、`LVDS`、`DisplayPort Embedded`、`UDI Embedded` 视为内置屏，HDMI/DisplayPort/DVI 等视为外接屏；只有连接类型未知时才使用物理尺寸 fallback（>21 英寸视为外接）。外接屏通过 DDC/CI VCP `0x10=0` 将背光亮度降到 0；内置屏通过 WMI `WmiMonitorBrightnessMethods.WmiSetBrightness` 尝试设为 0。显示器保持供电，USB Hub 和键鼠保持可用；唤醒时恢复原亮度。状态写入 `backlight-restore`，异常启动时恢复。
- **锁屏边界**：如果系统启用“唤醒时要求登录”或强制空闲锁定，普通应用无法绕过；UI/文档明确提示。恢复显示只是解除幕帘的覆盖层，不是解锁系统。
- **安全兜底**：Dark 期间看门狗监视 UI 心跳；若 UI 连续 10 秒无响应（且系统未挂起），记录事件后终止本进程，销毁覆盖窗口/钩子，并由辅助进程恢复更新策略。恢复前看门狗先尝试恢复音频。
- 最后手段的进程终止会中断本次熄屏，但比永久黑屏/无响应安全。

---

## 6. 详细设计：更新重启防护

### 6.1 防护分层

```text
第一层：临时更新策略（提权辅助进程）
   NoAutoUpdate = 1
   NoAutoRebootWithLoggedOnUsers = 1
   AlwaysAutoRebootAtScheduledTime = 0
        │
        ▼
第二层：会话关机否决（主进程，可见顶层窗口）
   WM_QUERYENDSESSION → ShutdownBlockReasonCreate → 返回 FALSE
        │
        ▼
第三层：健康检查 / 降级提示
   策略被外部改写 → 重新应用或标记降级
   辅助进程消失 → 主进程提示
        │
        ▼
第四层：退出/异常恢复
   精确恢复原值；孤儿快照恢复；强制重启前尽量清理
```

### 6.2 临时策略的精确内容

注册表位置（64 位视图）：

```text
HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU
```

| 值名 | 类型 | 保护期间值 | 作用 | 恢复 |
|---|---|---|---|---|
| `NoAutoUpdate` | REG_DWORD | `1` | 暂停自动更新扫描/安装，减少新的更新重启请求 | 恢复原值或删除 |
| `NoAutoRebootWithLoggedOnUsers` | REG_DWORD | `1` | 有用户登录时不允许自动更新自动重启 | 恢复原值或删除 |
| `AlwaysAutoRebootAtScheduledTime` | REG_DWORD | `0` | 明确禁止“到计划时间强制重启” | 恢复原值或删除 |

设计取舍：

- **不设置 `AUOptions`**：`NoAutoUpdate=1` 已覆盖自动更新行为；额外改 `AUOptions` 只会增加恢复项和 UI 副作用。
- **不停止 `wuauserv`/`UsoSvc`**：避免破坏 Microsoft Store、Defender 更新、系统维护；不写服务启动类型。
- **不修改计划任务**：计划任务的 ACL 和系统保护复杂，且容易留下副作用。
- **不修改 `ConfigureDeadlineNoAutoReboot...`**：该策略只在同时配置了 WUfB deadline 时生效，单独设置不可靠，且会引入更复杂的策略语义。
- **不删除“待重启”标记**：这会伪造系统状态，可能破坏更新完整性。
- 写入后不调用 `gpupdate /force`：避免触发域策略刷新、覆盖本地设置或引起长时间系统操作。Windows Update 在下一次读取策略时生效；关机否决立即生效。

已知风险：部分 Windows 11 版本上，`NoAutoRebootWithLoggedOnUsers` 可能被新版 UpdateOrchestrator 忽略，或组策略/Intune 覆盖本地值。因此不能只设置该项，也不能仅凭注册表回读宣称“已阻止重启”。

### 6.3 策略快照与精确恢复

在写任何值之前保存完整快照：

```text
GuardBackup (HKLM\SOFTWARE\ScreenCurtain\GuardBackup)
  Version
  SessionId / ParentPid
  CreatedAtUtc
  AppVersion
  KeyExisted
  NoAutoUpdate:      { existed, value }
  NoAutoReboot...:   { existed, value }
  AlwaysAutoReboot:  { existed, value }
```

规则：

- 原值类型可能不是 DWORD；按实际 `RegistryValueKind` 保存并按原类型恢复。
- 原本不存在的值：恢复时删除，不能写成 0。
- 原本不存在的 `AU` 键：若由本程序创建且恢复后为空，则删除该键；不要删除用户原有键。
- 恢复后读回校验，写入恢复日志。
- 只在备份完整写入成功后，才修改 Windows Update 策略。
- 备份本身使用管理员可写、普通用户不可篡改的位置；选择 `HKLM\SOFTWARE\ScreenCurtain`，恢复成功后删除该键（若没有其他状态）。
- 恢复前检查当前值是否仍等于本程序写入的临时值；若已被外部管理员/组策略改成其他值，默认**不覆盖外部值**，只清理由本程序创建的备份并提示“检测到外部修改，未回滚覆盖”。避免退出时把 IT 新下的策略改回旧值。
- 若备份已存在且没有活动辅助进程：视为“孤儿策略”，在下次请求提权时先按上述规则恢复，再建立新快照；主界面提供“修复上次未恢复的更新设置”。

崩溃恢复矩阵：

| 场景 | 处理 |
|---|---|
| 主进程正常退出 | 向辅助进程发 restore，等待确认，再退出 |
| 主进程崩溃/被杀 | 辅助进程检测父进程句柄失效，恢复策略后退出 |
| 辅助进程崩溃 | 主进程检测管道断开，显示 GuardDegraded；策略可能仍在，可重新提权重启辅助进程或手动修复 |
| 两者都崩溃，未重启 | 策略保留；下次启动检测孤儿快照并恢复 |
| 两者都崩溃后系统重启 | 策略保留到下次启动；应用启动时优先恢复孤儿快照；企业环境需额外注意 |
| 用户在系统阻止界面选择“仍要重启” | `WM_ENDSESSION` 中立即请求辅助进程恢复并等待短暂超时；系统可能来不及，下次启动兜底 |
| 断电/蓝屏 | 与上类似；下次启动恢复 |

### 6.4 关机否决实现

`ShutdownGuard` 使用主窗口/覆盖窗口接收：

- `WM_QUERYENDSESSION`
  - 解析 `lParam` 的 `ENDSESSION_LOGOFF`、`ENDSESSION_CRITICAL` 等标志。
  - 用户注销：允许，并启动清理。
  - 系统严重关机：记录并允许（不可阻挡）。
  - 其余且在 GuardActive 且未处于退出/恢复流程：确认阻止原因已注册（必要时在创建窗口的同一 UI 线程调用 `ShutdownBlockReasonCreate`），设置窗口消息结果为 `FALSE`，取消本次关机。
  - 阻止原因示例：
    `“幕帘”正在阻止 Windows 自动更新重启。请先退出“幕帘”或选择“仍要重启”。`
- `ShutdownBlockReasonCreate` 的注册时机：保护进入 GuardActive 时就在创建主窗口的 UI 线程注册，而不是临时在别的线程调用；API 要求必须由创建窗口的线程调用。注册失败时标记 GuardDegraded。
- Microsoft 明确说明“Applications cannot rely on being able to block shutdown.”，因此策略层仍是主防护，本层只是补充。
- `WM_ENDSESSION`
  - 若系统仍继续关机（用户选择“仍要重启”或被强制），尽量在 5 秒内完成策略恢复和资源清理；不做长时间阻塞。
- `ShutdownBlockReasonDestroy`：保护关闭、允许重启、程序退出时调用。

产品行为：

- 保护期间默认阻止**所有**重启/关机尝试，而不只是自动更新，因为系统消息不提供可靠的“此次重启来源”字段。
- 用户如果确实要重启：托盘菜单选择“退出并恢复更新”，或在 Windows 阻止界面选择“仍要重启”。
- 不提供“临时放行 5 分钟”之类的隐藏状态，避免策略暂停/恢复和 UAC 状态过于复杂；退出程序是唯一的显式放行路径。
- 阻止 UI 不应无限打扰用户；只在系统发起关机时显示，平时不弹窗。

> 关键验证项 G1：Windows 11 25H2 下，最小化到任务栏的 WinForms 主窗口是否仍被系统视为“可见窗口”并允许取消关机。若不满足，保护期间显示一个小型可见状态窗口（例如 360×120 的“保护运行中”条），或者将“可见窗口要求”作为 GuardDegraded 条件。此验证必须在实现阶段完成，不能假设托盘隐藏窗口可行。

### 6.5 提权辅助进程的生命周期

启动：

0. 先创建并显示主窗口，注册 `ShutdownBlockReasonCreate`、启动 `ShutdownGuard`，然后再请求提权。这样从进程可响应消息开始就有否决层，不需等 UAC 结束。
1. 主进程创建随机命名管道（ACL：当前用户 + Administrators）和随机握手令牌。
2. 通过 `ShellExecuteEx` 以 `runas` 启动同一 EXE：
   `ScreenCurtain.exe --update-guard --parent <pid> --pipe <name> --token <...>`
3. 辅助进程连接管道，验证令牌与父进程 ID。
4. 辅助进程检查是否有孤儿快照：
   - 有：先恢复原策略，再重新快照；
   - 无：建立新快照。
5. 写入策略并读回验证。
6. 打开父进程句柄（`SYNCHRONIZE`），启动健康检查循环；向主进程报告 `GuardActive`。
7. 若 UAC 被拒（`ERROR_CANCELLED`）或管道握手失败，主进程标记 `GuardDegraded`，不显示“已受保护”。

退出：

1. 主进程发送 `restore`。
2. 辅助进程恢复策略、读回、删除备份、回复 `restored`。
3. 辅助进程等待父进程或直接退出。
4. 主进程等待最多 5 秒；超时则显示“更新设置恢复超时”，保留修复入口，不静默退出。
5. 若父进程先消失：辅助进程检测到后自动恢复。

健康检查：

- 每 60 秒读取三条策略。
- 若被外部改回：最多重新应用 3 次；之后标记 `GuardDegraded`，并记录“检测到系统/企业策略覆盖”。
- 避免无限策略战：超过重试次数不再写，只提示用户。
- 同时检测父进程存活；父进程退出则进入恢复。
- 策略写入频率有上限，避免日志/注册表抖动。

安全：

- 辅助进程无 UI、无网络、只接受来自父进程令牌的管道命令。
- 父进程 ID + 随机令牌不能作为强认证，但配合命名管道 ACL，足以防止普通同会话进程误发。
- 不将敏感信息写入日志；不实现远程控制。

### 6.6 保护状态判定

| 状态 | 条件 | UI |
|---|---|---|
| `GuardActive` | 策略写入并读回成功 + 辅助进程存活 + 主窗口具备可见性 | 绿色“更新重启保护已开启” |
| `GuardDegraded` | UAC 被拒 / 策略写入失败 / 策略被覆盖 / 管道断开 / 可见窗口条件不满足 | 琥珀色“保护降级：{原因}” + 修复按钮 |
| `GuardOff` | 用户显式关闭或程序退出中 | 灰色“更新保护已关闭” |
| `GuardRestoring` | 退出/恢复中 | 仅在日志和短暂提示中显示 |

绝对禁止：在 UAC 被拒绝或策略未读回成功时显示“已屏蔽自动更新重启”。

### 6.7 受管环境与“绝对保证”

- 检测域加入/Intune 管理痕迹（如 `NetGetJoinInformation`、`HKLM\SOFTWARE\Microsoft\PolicyManager`），首次启用时提示可能与企业策略冲突。
- 对组策略覆盖，不反复篡改；显示降级。
- 若用户需要企业级保证，建议使用 Windows Update for Business 的维护窗口、截止期限和合规策略，而非便携工具。
- 本程序不实现“禁止用户手动重启”；只阻止运行期间的关机/重启请求，且用户可以从系统 UI 强制继续。

---

## 7. 交互与界面设计

完整 UI/UX 规范、线框、控件状态、启动流程、设置页、托盘、错误态、无障碍与 UI 验收清单见：

**[《幕帘 UI/UX 设计规范》](ui-design.md)**

### 7.1 目标界面摘要

目标界面由 7 个表面组成：

- 首次运行向导（3 步：限制确认 → 更新保护选择/UAC → 热键与完成）；
- 主窗口（状态 + 主操作 + 显示状态 + 更新保护状态 + 快速选项）；
- 设置窗口（常规 / 熄屏 / 更新保护 / 诊断与关于）；
- 托盘菜单；
- 退出/恢复失败对话框；
- 托盘通知；
- 熄屏状态（纯黑，无应用 UI；全屏层覆盖任务栏，托盘图标保留不隐藏；应用通知静默；音频默认静音）。
- 安装/卸载：按用户安装器、开始菜单/可选桌面快捷方式、HKCU 卸载项；卸载前恢复遗留更新策略；绿色版 ZIP。

关键交互决策：

1. 主窗口是控制中心，完整设置放入独立模态窗口；首版不做多级仪表盘。
2. 首次运行不自动弹 UAC；用户在第 2 步主动点击“启用并继续”后才请求管理员权限。
3. 更新保护期间主窗口可以最小化到任务栏，但不能隐藏到通知区域；关闭按钮在保护期间只最小化，以保证关机否决需要的可见顶层窗口。
4. 熄屏期间不显示任何应用提示、倒计时或按钮；鼠标移动不退出，点击/按键恢复。
5. 保护状态失败必须显示原因和下一步动作；不能用“失败”一个词结束。
6. 首版采用简体中文、浅色 Fluent 风格和标准 Windows 标题栏；深色主题/英文/开机自启作为后续阶段，详见 UI 规范。
7. 设置自动保存；退出、关闭保护、恢复失败等高风险动作必须明确确认。

### 7.2 当前构建与目标 UI 的关系

1.1 已按 `ui-design.md` 完成首次运行 3 步向导、主窗口、独立设置窗口（常规/熄屏/更新保护/诊断与关于）、托盘、退出确认和恢复失败处理。`build\ScreenCurtain.exe` 与安装版/绿色版使用同一主程序；绿色版靠同目录 `portable.mode` 切换数据路径。首版仍为浅色中文，不提供开机自启、深色主题或英文界面。

### 7.3 命令行接口（非 GUI）

```text
ScreenCurtain.exe --self-test
ScreenCurtain.exe --dry-run
ScreenCurtain.exe --test-blackout
ScreenCurtain.exe --test-policy
ScreenCurtain.exe --test-shutdown-guard
ScreenCurtain.exe --test-guard-protocol
ScreenCurtain.exe --update-guard ...
```

- 测试/开发命令不替代普通 UI；真实用户入口是主窗口、托盘和全局热键。
- 不打印策略值之外的敏感信息，不记录按键内容。
- UI 层通过 Presenter/服务调用核心逻辑；命令与 UI 不应各自维护状态。

---

## 8. 安全、隐私与可靠性

### 8.1 安全

- 主进程 `asInvoker`；只有策略辅助进程提权。
- 不安装驱动/服务；不修改系统文件。
- 输入钩子只在熄屏流程（Arming/Dark）期间存在；不缓存、不记录、不上传键值。
- 不实现远程唤醒/远程控制。
- 不隐藏系统 UAC、安全桌面或强制关机 UI。
- 提权辅助进程使用受限命名管道和随机令牌；没有网络监听。
- 退出、崩溃、会话切换路径都必须清理输入钩子和电源请求。

### 8.2 隐私

- 无网络访问，无遥测，无崩溃上传。
- 日志仅记录：
  - 时间、状态转换、显示器数量、API 返回码；
  - “鼠标恢复”/“键盘恢复”类别；
  - 策略值名称与恢复结果（非敏感）。
- 不记录按键码、不记录窗口标题、不截屏、不记录用户输入内容。

### 8.3 可靠性

- `try/finally` + 幂等清理 + 进程退出兜底。
- 所有窗口/钩子/电源状态由单一控制器持有，避免多处重复释放。
- 处理 `Application.ThreadException`、`AppDomain.UnhandledException`、`WM_ENDSESSION`、Ctrl+C（控制台模式）。
- Dark 期间禁止弹出本程序的模态对话框；错误写入日志，恢复后再提示，避免阻塞 UI 消息循环。
- Dark 期间 UI 心跳由看门狗线程监视；严重卡死时终止自身进程，避免永久黑屏（见 5.9）。
- 关闭覆盖层前先卸载钩子，避免关闭过程中钩子仍拦截输入造成“点了没反应”。
- 恢复 `ES_CONTINUOUS` 时即使先前调用失败也执行，避免电源状态泄漏。
- 覆盖层创建中途失败：关闭已创建窗口，恢复显示，绝不留下部分黑屏。
- 详细日志滚动：单文件 1 MB，保留 7 天。

### 8.4 安全警告文案

> 黑屏不是锁屏。熄屏期间电脑保持唤醒、当前 Windows 会话保持解锁；任何能接触到鼠标/键盘的人都可以唤醒并操作电脑。若在公共场所使用，请先手动锁屏或退出本程序。

> 更新保护会临时暂停 Windows 自动更新并阻止运行期间的关机/重启。退出程序后自动更新与重启行为恢复；若已有待重启更新，系统可能在之后重启。

---

## 9. 测试与验收计划

> 开发环境补充（2026-11-07）：已使用内存 `IPolicyStore` 对策略快照、临时写入、外部修改保护、孤儿恢复和健康检查执行 `--test-policy`，5 组均 PASS，结果见 `artifacts/policy-test-result.txt`。另用最小化窗口执行 `--test-shutdown-guard`：`IsWindowVisible=true`、模拟普通关机查询被阻止、`ShutdownBlockReasonCreate=true`、模拟注销放行，结果见 `artifacts/shutdown-guard-result.txt`。这些测试证明了策略决策和关机消息处理逻辑，但不能替代真实 HKLM/UAC、真实系统关机和 Windows Update 行为验收。

> 本节其余内容是设计验收方案，尚未全部在目标机上执行。真实熄屏和 Windows Update 策略测试必须在用户可恢复、最好有快照的 VM 上进行；不能静默修改当前机器的系统更新设置。

### 9.1 显示/输入测试矩阵

| ID | 场景 | 预期 |
|---|---|---|
| D1 | 单显示器 100% 缩放触发熄屏 | 全屏纯黑，任务栏不可见，光标不可见 |
| D2 | 双显示器，不同分辨率，扩展模式 | 每台显示器各自全黑，无缝隙/边角露出 |
| D3 | 副屏在左侧（负坐标）/上方 | 覆盖矩形正确 |
| D4 | 150%/200% 混合缩放 | 覆盖层仍按物理像素铺满 |
| D5 | 熄屏期间修改分辨率/旋转/主屏 | 500 ms 内重建覆盖，所有屏仍黑 |
| D6 | 熄屏期间插入新显示器 | 新屏被覆盖；若创建失败则自动恢复全部显示 |
| D7 | 鼠标移动 30 秒 | 仍保持 Dark；若物理面板亮起，画面仍全黑 |
| D8 | 左/右/中/侧键点击 | 恢复；第一个点击不传入下方测试窗口 |
| D9 | 任意键（字母、功能键、Win 键、修饰键） | 恢复；首个按键不传入下方测试窗口 |
| D10 | 触发后 300 ms 内按键释放/鼠标抬起 | 不立即恢复；700 ms 静默期生效 |
| D11 | 长按热键 | 长按期间按键重复重置静默期，不立即恢复；释放后静默 700ms，再下一次点击/按键恢复 |
| D12 | 鼠标滚轮 | 默认不恢复；开关打开时恢复，行为一致 |
| D13 | 物理熄屏支持的机器 | 显示器进入 DPMS off/待机；输入可唤醒 |
| D14 | 物理熄屏不支持的机器/驱动 | 保持全黑；点击/按键正常恢复 |
| D15 | 独占全屏游戏/视频 | 记录实际结果；若覆盖失败必须有物理熄屏或明确限制说明 |
| D15A | 第三方 topmost popup/通知 | Topmost Guard 后截图应为全黑；安全桌面除外 |
| D15B | 系统音频静音/恢复 | 多播放设备静音，恢复原状态；强杀后下次启动恢复 |
| D16 | UAC 安全桌面出现 | 不崩溃；退出安全桌面后仍可正常恢复 |
| D17 | Ctrl+Alt+Del | 不崩溃；安全桌面可操作；返回后输入可恢复 |
| D18 | Win+L / 自动锁屏 | 自动退出 Dark，不留下黑屏覆盖 |
| D19 | 远程桌面断开/重连 | 自动退出 Dark，重连看到正常桌面 |
| D20 | 合盖睡眠/恢复 | 睡眠前清理，恢复后正常，不自动再黑 |
| D21 | 强制结束主进程 | 覆盖窗口随进程消失；显示器可由输入唤醒；无输入钩子残留 |
| D22 | 主进程无响应（模拟） | 看门狗在约 10 秒后终止进程；覆盖消失、输入可唤醒；辅助进程恢复策略；最坏情况仍可用 Ctrl+Alt+Del 干预 |
| D23 | 8 小时持续 Dark | 不睡眠、不锁屏（在策略允许范围内）、内存/句柄稳定 |
| D24 | 连续触发/恢复 100 次 | 无窗口/钩子/电源状态泄漏 |
| D25 | 多用户快速切换 | 当前会话退出 Dark；不把另一会话困住 |

### 9.2 更新保护测试矩阵

| ID | 场景 | 预期 |
|---|---|---|
| U1 | 全新启动，允许 UAC | 策略写入、读回；状态 GuardActive |
| U2 | 拒绝 UAC | 状态 GuardDegraded；熄屏功能仍可用；不显示“已保护” |
| U3 | 原策略值为 1/0/不存在 | 退出后逐项恢复原值/删除；键存在性一致 |
| U4 | 强制关闭主进程 | 辅助进程恢复策略；日志记录 |
| U5 | 强制关闭辅助进程 | 主进程检测到管道断开，显示降级并可重试 |
| U6 | 主/辅都崩溃后重新启动 | 检测孤儿快照并恢复，再建立新快照 |
| U7 | 系统重启后首次启动 | 孤儿策略恢复；不重复残留 |
| U8 | `shutdown.exe /r /t 60` 模拟重启 | 系统显示阻止；应用提供原因；重启被取消 |
| U9 | Task Scheduler 以 SYSTEM 发起重启 | 记录实际结果；若无法阻止，必须能说明限制 |
| U10 | 用户主动重启/注销 | 注销允许；重启被阻止或显示原因；可选择“退出并恢复更新”后重启 |
| U11 | 组策略/Intune 覆盖策略 | 最多重试 3 次后降级；不无限对抗 |
| U12 | 策略写入后 Windows Update 服务刷新 | 读回值仍正确；健康检查正常 |
| U13 | 退出程序 | 5 秒内恢复原值；无残留 `ScreenCurtain` 键 |
| U14 | 企业受管设备 | 首次启用有警告；降级原因可解释 |
| U15 | UAC 被拒后用户点击“修复” | 再次提权；成功则 GuardActive |

模拟更新重启的边界：

- `shutdown.exe` 触发的是普通关机流程，可验证 `WM_QUERYENDSESSION` 和阻止 UI；
- 它不能完全等价于 Windows Update 的 UpdateOrchestrator 强制重启路径；
- 真实更新测试必须在可回滚 VM 中做，并记录 Windows 版本、补丁级别、策略状态。

### 9.3 性能/质量验收

- 启动到托盘可用：< 2 秒（不含 UAC 输入）。
- 触发熄屏到所有覆盖层显示：< 1 秒。
- 点击/按键到覆盖层消失、桌面可用：< 500 ms（不含显示器硬件唤醒时间）。
- 空闲 CPU 平均 < 1%，常驻工作集 < 80 MB。
- 8 小时测试无句柄增长、无重复窗口、无电源状态泄漏。
- 程序退出后无输入钩子、无覆盖窗口、无 `ES_*` 电源请求残留。
- 程序目录、日志目录、注册表恢复状态可被审计。

### 9.4 验收定义（必须全部满足）

1. 任意测试显示器都无可见桌面内容；物理面板关闭成功与否不影响“全黑”。
2. 鼠标移动不退出 Dark；鼠标按键或键盘按键才退出。
3. 第一次唤醒点击/按键不被下方应用接收。
4. 更新保护在正常 UAC 同意、非受管 Windows 11 环境中保持 GuardActive，并能在模拟重启中取消重启。
5. 退出后所有 Update 注册表值与保护前完全一致。
6. 用户拒绝 UAC 时，界面明确显示降级，不虚假宣称保护。
7. 主进程强杀后无输入钩子/黑屏窗口残留；更新策略由辅助进程或下次启动恢复。
8. 日志不包含按键值、窗口内容或网络数据。

---

## 10. 风险与缓解

| 风险 | 影响 | 缓解 |
|---|---|---|
| `SC_MONITORPOWER` 在部分机器无效/变睡眠 | 不能真正断电 | 全黑覆盖主层；不依赖返回值；验收只要求全黑 |
| 鼠标移动唤醒面板 | 背光先亮 | 覆盖层保持全黑；移动不退出；文档说明 |
| 低级钩子被安全软件拦截/超时移除 | 首个输入可能落入下方应用 | 覆盖窗口事件后备；恢复后测试；不宣称绝对 |
| 主窗口最小化不满足关机否决要求 | 空闲时无法取消更新重启 | G1 实测；不符合则保留小状态窗口或标记降级 |
| `NoAutoUpdate`/`NoAutoReboot` 被 Win11 新更新器/组策略忽略 | 更新仍可能重启 | 关机否决兜底；健康检查；降级提示；企业方案 |
| 强制重启/用户选择“仍要重启” | 保护失效 | 无法阻止；文档明确；重启前尽力恢复策略 |
| 辅助进程/主进程崩溃导致策略残留 | 自动更新被意外暂停 | 父进程看门狗、`WM_ENDSESSION` 清理、孤儿快照恢复 |
| UAC 被拒绝 | 没有策略层保护 | 明确 GuardDegraded；不虚假宣称；核心熄屏不受影响 |
| 自动更新在熄屏期间重启 | 黑屏中被重启 | 策略+否决；若仍发生，下次启动检测；用户可查看日志 |
| 自动锁屏策略（机器不活动限制） | 唤醒后停在锁屏 | 会话锁定即退出 Dark；ES_DISPLAY_REQUIRED；文档说明残余风险 |
| 独占全屏/UAC/安全桌面 | 覆盖不完整 | 物理熄屏；安全桌面不可拦截；限制说明 |
| 混合 DPI/热插拔覆盖不全 | 桌面内容泄漏 | PerMonitorV2 + 每屏窗口 + 热插拔重建 + 失败回滚 |
| 程序崩溃/死循环 | 用户面对黑屏 | 覆盖窗口随进程销毁；任意输入/物理键可恢复；钩子不持久 |
| 笔记本电池耗电 | 续航下降 | 提示“保持唤醒”；设置可允许睡眠；不建议长时间电池使用 |
| 未签名 EXE 触发 SmartScreen | 用户不信任 | 提供校验和/源码；可选代码签名；文档说明 |
| 黑屏不是锁屏 | 隐私/安全风险 | 首次运行强提示；未来可加“锁屏模式” |
| 企业设备上修改策略 | 违反 IT 政策 | 检测域/Intune；警告；不强制覆盖；提供恢复 |

---

## 11. 实施计划（设计通过后）

### 阶段 A：骨架与显示覆盖

- 创建 C# WinForms 工程结构、app.manifest、图标、单实例、托盘。
- 实现 `EnumDisplayMonitors` + 每屏 `OverlayForm` + DPI 感知。
- 实现 `--dry-run`（显示灰色/彩色轮廓，不真黑、不关屏），用当前双屏验证几何。
- 验收：所有显示器矩形精确覆盖，任务栏被遮盖，无圆角漏边。

### 阶段 B：熄屏输入与恢复

- 实现 `BlackoutController` 状态机、700ms 静默期。
- 安装/卸载低级钩子；点击/按键吞噬；鼠标移动忽略。
- 实现 `SC_MONITORPOWER` 广播、透明光标、电源抑制、前台窗口恢复。
- 实现主进程退出/崩溃/会话锁定安全路径。
- 验收：D1–D25 的非破坏性项 + 用户监督下的真实熄屏测试。

### 阶段 C：更新保护

- 实现 `update-guard` 辅助模式、命名管道、UAC 启动。
- 实现策略快照、写入、读回、健康检查、孤儿恢复。
- 实现 `ShutdownGuard` 和 G1 可见窗口验证。
- 验收：U1–U15，在 VM 中做模拟重启，避免污染主机。

### 阶段 D：产品化

- 设置页、状态卡片、首次运行提示、日志与 CLI。
- 完善失败路径、精确恢复、版本信息、使用说明。
- 单文件构建与签名建议；在 Windows 11 23H2/24H2/25H2 回归。
- 最终交付：EXE + 使用说明 + 验收记录（真实测试结果）。

---

## 12. 待确认决策

UI 相关决策（开机自启、关闭行为、自动保存、主题/语言、预览、窗口尺寸、安全提示）见 [ui-design.md §13.2](ui-design.md)。以下为产品/技术决策，推荐值列在括号中：

1. **UAC 与更新保护**：是否接受程序启动时弹出 UAC 以启用临时更新策略？（推荐：接受；否则只能提供降级保护。）
2. **临时暂停自动更新**：为可靠阻止重启，保护期间会写 `NoAutoUpdate=1`，退出后恢复。是否接受“运行期间连自动更新扫描也暂停”？（推荐：接受，UI 明示。）
3. **手动重启的处理**：保护期间默认阻止所有重启/关机请求；用户需通过托盘“退出并恢复更新”或系统“仍要重启”。是否接受？（推荐：接受，避免无法区分更新重启和用户重启。）
4. **会话保持解锁**：熄屏期间默认保持会话解锁、系统唤醒，以保证唤醒即桌面。是否接受？还是要求唤醒后必须登录？（推荐：默认保持解锁；后续可加锁屏模式。）
5. **程序名称/热键**：工作名“幕帘 / ScreenCurtain”，默认热键 `Ctrl+Alt+Shift+L`。是否调整？
6. **通知区域窗口策略**：更新保护期间主窗口只最小化到任务栏、不隐藏到托盘，以保证关机否决可见。是否接受？（推荐：接受。）
7. **企业设备**：是否需要在域/Intune 设备上默认拒绝修改策略，仅提示？（推荐：首次警告并要求明确确认。）

---

## 附录 A：主要 Win32 API / 消息

| API / 消息 | 用途 |
|---|---|
| `EnumDisplayMonitors` / `GetMonitorInfo` | 枚举物理显示器矩形 |
| `SetProcessDpiAwarenessContext` | Per-Monitor V2 DPI 感知 |
| `SendMessageTimeout` | 广播 `WM_SYSCOMMAND / SC_MONITORPOWER` |
| `SetWindowsHookEx(WH_MOUSE_LL/WH_KEYBOARD_LL)` | 熄屏期间检测/吞掉首个唤醒输入 |
| `CallNextHookEx` / `UnhookWindowsHookEx` | 输入链与清理 |
| `SetThreadExecutionState` | 抑制系统/显示空闲睡眠 |
| `WTSRegisterSessionNotification` / `WM_WTSSESSION_CHANGE` | 锁屏/断开时安全恢复 |
| `ShutdownBlockReasonCreate` / `ShutdownBlockReasonDestroy` | 关机阻止原因 UI |
| `WM_QUERYENDSESSION` / `WM_ENDSESSION` | 取消/响应关机 |
| `RegisterHotKey` / `WM_HOTKEY` | 全局热键 |
| `DwmSetWindowAttribute(DWMWA_WINDOW_CORNER_PREFERENCE)` | 禁止 Win11 圆角露出桌面 |
| `DwmFlush` | 熄屏前确认黑帧已提交 |
| `CreateIconIndirect` / 透明光标资源 | 隐藏鼠标指针 |
| `EnumDisplaySettings`（可选） | 诊断显示器模式 |
| `NetGetJoinInformation`（可选） | 检测域加入，提示策略冲突 |

## 附录 B：注册表与数据文件

**更新策略（保护期间临时写入）**

```text
HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU
  NoAutoUpdate                  REG_DWORD  1
  NoAutoRebootWithLoggedOnUsers REG_DWORD  1
  AlwaysAutoRebootAtScheduledTime REG_DWORD 0
```

**保护快照（仅运行期间存在）**

```text
HKLM\SOFTWARE\ScreenCurtain\GuardBackup
```

**用户配置**

```text
%LocalAppData%\ScreenCurtain\settings.ini
%LocalAppData%\ScreenCurtain\logs\app-YYYYMMDD.log
```

**建议配置字段**

```text
hotkey                  = "Ctrl+Alt+Shift+L"
guardEnabled            = true
allowWheelWake          = false
allowSystemSleep        = false
startWithWindows        = false
startMinimized          = false
logLevel                = "info"
```

## 附录 C：建议目录结构

```text
screen-curtain/
├─ docs/
│  └─ design.md              # 本文档
├─ src/
│  ├─ Program.cs
│  ├─ App.manifest
│  ├─ app.config
│  ├─ Properties/
│  │  └─ AssemblyInfo.cs
│  ├─ UI/
│  │  ├─ MainForm.cs
│  │  ├─ TrayController.cs
│  │  └─ OverlayForm.cs
│  ├─ Core/
│  │  ├─ BlackoutController.cs
│  │  ├─ DisplayManager.cs
│  │  ├─ InputGate.cs
│  │  ├─ PowerKeeper.cs
│  │  ├─ MonitorPower.cs
│  │  ├─ SessionWatcher.cs
│  │  └─ ShutdownGuard.cs
│  ├─ UpdateGuard/
│  │  ├─ GuardClient.cs
│  │  ├─ GuardServer.cs
│  │  ├─ PolicyManager.cs
│  │  └─ PolicySnapshot.cs
│  ├─ Native/
│  │  └─ NativeMethods.cs
│  └─ Infrastructure/
│     ├─ Log.cs
│     ├─ Settings.cs
│     └─ PipeProtocol.cs
├─ build.ps1
└─ README.md
```

## 附录 D：参考资料

- Microsoft Learn — [WM_SYSCOMMAND](https://learn.microsoft.com/en-us/windows/win32/menurc/wm-syscommand)（`SC_MONITORPOWER`）
- Microsoft Learn — [ShutdownBlockReasonCreate](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-shutdownblockreasoncreate)
- Microsoft Learn — [Shutdown Changes for Windows Vista](https://learn.microsoft.com/en-us/windows/win32/shutdown/shutdown-changes-for-windows-vista)（无可见窗口的进程不能取消关机）
- Microsoft Learn — [WM_QUERYENDSESSION](https://learn.microsoft.com/en-us/windows/win32/shutdown/wm-queryendsession)
- Microsoft Learn — [SetThreadExecutionState](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-setthreadexecutionstate)
- Microsoft Learn — [EnumDisplayMonitors](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaymonitors)
- Microsoft Learn — [LowLevelMouseProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc)
- Microsoft Learn — [High DPI Desktop Application Development](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows)
- Microsoft Learn — [Policy CSP - Update](https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-update)
- Microsoft Learn — [Windows Update compliance deadlines](https://learn.microsoft.com/en-us/windows/deployment/update/wufb-compliancedeadlines)
- Microsoft Q&A 讨论：`NoAutoRebootWithLoggedOnUsers` 在部分 Windows 11 设备上仍出现自动重启的报告 — [链接](https://learn.microsoft.com/en-us/answers/questions/5890337/windows11)

---

## 结论

**可行，但必须把“全黑”和“物理熄屏”分开承诺。** 覆盖层是普通桌面/窗口场景下对所有显示器最可靠的保证；`SC_MONITORPOWER` 是增强而非依赖。安全桌面和独占全屏有明确边界，物理熄屏仍可能失效。输入上，用户态无法阻止显示器硬件被鼠标移动唤醒，但可以保证移动不退出全黑，只有点击/按键才恢复正常，且首个事件被吞掉。

更新重启防护采用“临时更新策略 + 关机否决 + 精确恢复”的纵深方案，能可靠覆盖正常的 Windows Update 自动重启路径，但无法对用户强制重启、组策略覆盖、断电或内核故障作绝对保证。所有降级必须显式可见，绝不虚假宣称“绝对屏蔽”。

下一步是在设计确认后按阶段 A→D 实现，并优先完成 G1（可见窗口与关机否决）和真实双显示器熄屏测试。

