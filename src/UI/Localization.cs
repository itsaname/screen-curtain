using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;

namespace ScreenCurtain
{
    internal static class Localization
    {
        public static bool IsEnglish { get; private set; }

        private static readonly Dictionary<string, string> English =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "幕帘 ScreenCurtain", "ScreenCurtain" },
                { "幕帘", "ScreenCurtain" },
                { "设置 - 幕帘", "Settings - ScreenCurtain" },
                { "幕帘 - 首次使用", "ScreenCurtain - First Run" },
                { "一键关闭所有显示器 · 点击或按键恢复 · 熄屏时静音系统音频",
                  "Blank every display · click or press a key to restore · mute system audio while dark" },
                { "正在读取状态", "Reading status" },
                { "设置", "Settings" },
                { "立即熄屏", "Blackout Now" },
                { "覆盖所有显示器 · 鼠标移动不会退出 · 点击鼠标或按键盘恢复",
                  "Covers all displays · mouse movement won't exit · click or key to restore" },
                { "快捷键会显示在按钮旁边", "The shortcut is shown next to the button" },
                { "显示器", "Displays" },
                { "正在枚举…", "Enumerating…" },
                { "刷新", "Refresh" },
                { "每台显示器使用独立无边框全屏层；点击或按键后恢复。",
                  "Each display gets its own borderless fullscreen layer; click or press a key to restore." },
                { "更新重启保护", "Update Restart Guard" },
                { "未启用", "Off" },
                { "不修改 Windows Update 设置。", "Does not modify Windows Update settings." },
                { "启用保护…", "Enable protection…" },
                { "详情", "Details" },
                { "仅在本程序运行期间临时暂停自动更新；退出时恢复。",
                  "Temporarily pauses automatic updates only while this app runs; restores on exit." },
                { "快速选项", "Quick options" },
                { "保持系统唤醒", "Keep system awake" },
                { "滚轮也可恢复", "Mouse wheel also restores" },
                { "熄屏时静音", "Mute while dark" },
                { "查看日志", "View logs" },
                { "退出并恢复系统设置…", "Exit and restore…" },
                { "保护期间关闭窗口只会最小化到任务栏。",
                  "Closing during protection only minimizes to taskbar." },
                { " · 便携模式", " · Portable" },
                { " · 安装版", " · Installed" },
                { "托盘恢复", "Restore from tray" },
                { "退出程序", "Exit" },
                { "无效的快捷键。", "Invalid shortcut." },
                { "主窗口尚未准备好，请稍后再试。", "The main window is not ready yet; try again shortly." },
                { "快捷键 ", "Shortcut " },
                { " 注册失败（可能被其他程序占用，Win32=", " registration failed (possibly in use by another app, Win32=" },
                { " · 托盘菜单也可熄屏", " · The tray menu can also black out" },
                { "黑屏中 · 点击或按键恢复", "Dark · click or press a key to restore" },
                { "保护已开启", "Protection on" },
                { "保护待命", "Protection armed" },
                { "更新保护已启用（临时策略）", "Update protection enabled (temporary policy)" },
                { "更新保护已就绪（等待熄屏）", "Update protection ready (waiting for blackout)" },
                { "更新保护已暂停（仅熄屏期间启用）", "Update protection paused (blackout only)" },
                { "更新保护已恢复（仅熄屏期间启用）", "Update protection resumed (blackout only)" },
                { "更新保护已启用", "Update protection enabled" },
                { "更新保护已就绪", "Update protection ready" },
                { "更新保护已经开启", "Update protection is enabled" },
                { "更新重启保护未启动", "Update restart guard not started" },
                { "更新重启保护已关闭", "Update restart guard is off" },
                { "更新设置恢复失败，请用“修复更新设置”重试", "Failed to restore update settings. Use Repair update settings to retry." },
                { "保护未启用", "Protection off" },
                { "保护启动中", "Protection starting" },
                { "正在请求管理员权限并启动更新保护…", "Requesting administrator rights and starting update protection…" },                { "保护启动时机", "Protection start timing" },
                { "两种方式都在程序启动时请求一次 UAC。“仅熄屏期间”启动后先暂停并恢复策略，进入熄屏时才启用。", "Both modes request UAC once at app startup. Blackout-only starts paused with policies restored, then activates when blackout begins." },
                { "仅熄屏期间", "Blackout only" },
                { "程序启动后", "At app startup" },
                { "进入熄屏时", "When blackout starts" },
                { "已启用 · 等待熄屏", "Enabled · waiting for blackout" },
                { "已启用 · 等待首次熄屏", "Enabled · waiting for first blackout" },
                { "进入熄屏时会请求管理员权限并启动保护。", "Administrator permission will be requested when blackout starts." },
                { "进入熄屏时会请求管理员权限；保护启动后持续到退出或手动关闭。", "Administrator permission is requested when blackout starts; protection then stays active until exit or manual stop." },
                { "等待熄屏", "Waiting for blackout" },
                { "保护待命 · 熄屏时启动", "Protection armed · starts on blackout" },
                { "保护待命 · 仅熄屏期间", "Protection armed · blackout only" },
                { "保护待命 · 仅熄屏期间启用", "Protection armed · active during blackout only" },
                { "更新重启保护：仅熄屏期间", "Update restart guard: blackout only" },
                { "更新策略已恢复；下次进入熄屏会复用已启动的保护进程，不再请求 UAC。", "Temporary policies are restored; the next blackout reuses the running helper and does not ask for UAC again." },
                { "保护降级", "Protection degraded" },
                { "正在恢复设置", "Restoring settings" },
                { "保护未开启", "Protection off" },
                { "已开启 · 正在阻止 Windows 更新重启", "On · blocking Windows Update restarts" },
                { "退出幕帘或关闭保护时恢复 Windows Update 原始设置。",
                  "Restores the original Windows Update settings when exiting or turning protection off." },
                { "关闭保护", "Turn off protection" },
                { "正在请求管理员权限…", "Requesting administrator rights…" },
                { "UAC 请求可能出现在桌面上；取消不会导致程序退出，但保护会降级。",
                  "The UAC prompt may appear on the desktop; cancelling will not exit the app, but protection will degrade." },
                { "启动中…", "Starting…" },
                { "正在恢复 Windows Update 原始设置…", "Restoring original Windows Update settings…" },
                { "请等待策略恢复完成。", "Please wait for policy restore to finish." },
                { "正在恢复…", "Restoring…" },
                { "保护未生效 · 更新设置需要修复", "Protection inactive · update settings need repair" },
                { "可以重试，或查看日志。", "Retry, or view the logs." },
                { "重试", "Retry" },
                { "未启用 · 不修改系统更新设置", "Off · Windows Update settings unchanged" },
                { "可在需要长时间运行时启用；启用时会请求管理员权限。",
                  "Enable for long unattended runs; requests administrator rights." },
                { "更新保护状态回调失败", "Update guard status callback failed" },
                { "更新保护降级", "Update protection degraded" },
                { "更新设置未能完整生效，请打开设置重试修复。",
                  "Update settings did not apply completely; open Settings and retry repair." },
                { "关闭更新重启保护", "Turn off update restart guard" },
                { "关闭后幕帘将恢复 Windows Update 原始设置，运行期间的自动更新重启保护会停止。熄屏功能仍然可用。",
                  "ScreenCurtain will restore the original Windows Update settings and stop blocking update restarts. Blackout remains available." },
                { "正在请求管理员权限并写入临时策略…", "Requesting administrator rights and applying temporary policy…" },
                { "停止更新保护失败", "Failed to stop update protection" },
                { "更新设置恢复失败，请重试修复。", "Failed to restore update settings; retry repair." },
                { "已关闭", "Off" },
                { "设置关机阻止状态失败", "Failed to set shutdown-block state" },
                { "无法进入熄屏状态", "Could not enter blackout" },
                { "无法进入熄屏", "Could not enter blackout" },
                { "熄屏异常", "Blackout error" },
                { "覆盖就绪", "coverage ready" },
                { "未检测到显示器", "No displays detected" },
                { "无法打开日志目录：", "Could not open log directory: " },
                { "退出幕帘会先恢复 Windows Update 原始设置，并停止更新重启保护。熄屏状态也会被解除。",
                  "Exiting restores the original Windows Update settings and stops update-restart protection. Any blackout will also end." },
                { "当前更新设置处于降级/未恢复状态。退出前请确认已经尝试修复，否则可能残留临时策略。",
                  "Update settings are currently degraded/unrestored. Repair before exiting to avoid leaving temporary policy behind." },
                { "退出幕帘后将无法继续阻止更新重启，但不会修改 Windows Update 设置。",
                  "After exiting, update restarts can no longer be blocked, but Windows Update settings will not be modified." },
                { "退出幕帘？", "Exit ScreenCurtain?" },
                { "退出并恢复", "Exit and restore" },
                { "Windows 关闭", "Windows shutdown" },
                { "幕帘仍在保护中", "ScreenCurtain is still protecting" },
                { "窗口已最小化到任务栏；请使用“退出并恢复系统设置”停止保护。",
                  "The window was minimized to the taskbar; use Exit and restore system settings to stop protection." },
                { "幕帘仍在运行", "ScreenCurtain is still running" },
                { "窗口已最小化到任务栏；可从托盘或任务栏重新打开。",
                  "The window was minimized to the taskbar; reopen it from the tray or taskbar." },
                { "全局热键 ", "Global hotkey " },
                { " 注册失败，可在设置中更换。", " registration failed; change it in Settings." },
                { "注册全局热键失败：", "Failed to register global hotkey: " },

                { "让所有显示器安静下来", "Quiet every display" },
                { "上一步", "Back" },
                { "继续", "Continue" },
                { "设置可以随时在“设置”中修改。", "You can change these settings at any time in Settings." },
                { "先了解幕帘的边界", "First, understand the boundaries" },
                { "要不要启用更新重启保护？", "Enable update restart protection?" },
                { "设置你的快捷方式", "Set your shortcut" },
                { "启用并继续", "Enable and continue" },
                { "完成", "Done" },
                { "仅熄屏继续", "Continue with blackout only" },
                { "我了解：黑屏不是锁屏，熄屏时 Windows 会话仍然保持解锁。",
                  "I understand: blackout is not a lock screen; the Windows session stays unlocked while dark." },
                { "点击“立即熄屏”后，幕帘会覆盖所有显示器，并尽力让显示器进入物理关闭。鼠标移动不会退出，点击鼠标或按键盘会恢复。",
                  "When you click Blackout Now, ScreenCurtain covers every display and tries to turn the panels off. Mouse movement does not exit; a click or keypress restores." },
                { "熄屏期间系统保持唤醒、当前会话保持解锁，因此这不是安全锁屏。如果需要离开座位，请先按 Win+L 锁屏。",
                  "The system stays awake and the session stays unlocked, so this is not a security lock. Press Win+L before leaving your desk." },
                { "下一步可以选择是否启用更新重启保护。启用需要管理员权限；它只在幕帘运行期间临时暂停自动更新，退出时会恢复原设置。",
                  "Next you can enable update-restart protection. It needs administrator rights and only pauses updates while ScreenCurtain runs; settings are restored on exit." },
                { "启用更新重启保护（推荐）", "Enable update restart protection (recommended)" },
                { "需要一次管理员权限（UAC）。保护期间临时暂停 Windows 自动更新，并阻止运行期间的更新重启；退出幕帘时恢复原策略。",
                  "Needs one UAC approval. Temporarily pauses Windows Update and blocks update restarts while running; restores the original policy on exit." },
                { "仅使用熄屏，不修改系统更新设置", "Use blackout only; do not modify Windows Update" },
                { "不需要管理员权限。熄屏功能仍可用，但系统自动更新可能在你离开时触发重启。",
                  "No administrator rights needed. Blackout works, but Windows Update may still restart while you are away." },
                { "尚未启用更新保护", "Update protection is not enabled yet" },
                { "仍然仅使用熄屏继续", "Continue with blackout only" },
                { "选择一个全局快捷键。在任何应用中使用它，都可以立即进入黑屏。",
                  "Choose a global shortcut. Use it in any app to black out immediately." },
                { "熄屏快捷键", "Blackout shortcut" },
                { "点击输入框后再按下组合键；至少包含 Ctrl / Alt / Shift / Win 之一。按 Esc 取消录制。",
                  "Click the box, then press a combination containing at least Ctrl, Alt, Shift or Win. Esc cancels recording." },
                { "启动后最小化", "Start minimized" },
                { "启用后启动时不抢占焦点；如果更新保护已开启，UAC 仍可能出现在启动时。",
                  "Starts without stealing focus; if update protection is enabled, UAC may still appear at startup." },
                { "将启用更新重启保护。退出幕帘时会恢复系统更新设置。",
                  "Update restart protection will be enabled. Original update settings are restored when ScreenCurtain exits." },
                { "不会修改 Windows 更新设置；随时可以在设置中启用保护。",
                  "Windows Update settings will not be modified; protection can be enabled later in Settings." },
                { "正在请求权限…", "Requesting permission…" },
                { "正在请求管理员权限，请稍候…", "Requesting administrator rights, please wait…" },
                { "无法启动更新保护。可以重试，或仅使用熄屏继续。",
                  "Could not start update protection. Retry, or continue with blackout only." },
                { "更新保护已开启。点击继续完成向导。", "Update protection is enabled. Click Continue to finish." },
                { "保护未开启：", "Protection is off: " },
                { "管理员权限请求已取消，或其他程序阻止了更新保护。",
                  "The administrator request was cancelled, or another program blocked update protection." },
                { "更新保护未能启动。", "Update protection could not start." },
                { "退出首次设置", "Exit first-run setup" },
                { "如果现在退出，幕帘不会保存首次设置，下次启动仍会显示这个向导。已启用的更新保护会被关闭并恢复系统设置。",
                  "If you exit now, first-run settings are not saved and this wizard appears again. Any enabled update protection is turned off and system settings are restored." },
                { "退出", "Exit" },

                { "设置会自动保存；高风险操作会先确认。", "Settings save automatically; risky actions ask for confirmation." },
                { "界面语言 / Language", "Language" },
                { "界面语言", "Language" },
                { "切换后重启程序生效。", "Restart the app to apply." },
                { "界面语言将在重启程序后生效。", "The UI language will change after restarting the app." },
                { "外观 / Appearance", "Appearance" },
                { "主题", "Theme" },
                { "主题将在重启程序后生效。", "The theme will change after restarting the app." },
                { "自动", "Auto" },
                { "浅色", "Light" },
                { "深色", "Dark" },
                { "常规", "General" },
                { "熄屏", "Blackout" },
                { "诊断与关于", "Diagnostics and About" },
                { "更新保护", "Protection" },
                { "设置自动保存到数据目录；路径见“诊断与关于”。",
                  "Settings are saved in the data directory; see Diagnostics and About for the path." },
                { "窗口与启动", "Window and Startup" },
                { "开机自启动", "Start with Windows" },
                { "登录 Windows 后自动启动幕帘。是否最小化由上面的“启动后最小化”决定。", "Starts ScreenCurtain after Windows sign-in. Minimize behavior follows Start minimized above." },
                { "无法写入开机自启动项，请检查当前用户注册表权限。", "Could not write the startup entry. Check the current user's registry permissions." },
                { "主题和界面语言在重启程序后生效；开机自启动使用当前用户注册表，不需要管理员权限。", "Theme and language changes apply after restart. Start with Windows uses the current-user registry and needs no administrator rights." },
                { "启动后最小化到任务栏", "Start minimized to taskbar" },
                { "启动时不抢占焦点；更新保护仍会在后台尝试启动。",
                  "Does not steal focus at startup; update protection still starts in the background." },
                { "关闭主窗口时", "When closing the main window" },
                { "保护开启时无论此选项如何都会最小化到任务栏，以保证关机否决的可见窗口。",
                  "While protection is on, the window always minimizes to the taskbar so the shutdown-block window remains visible." },
                { "最小化到任务栏", "Minimize to taskbar" },
                { "退出幕帘", "Exit ScreenCurtain" },
                { "退出前确认", "Confirm before exit" },
                { "在退出、关闭保护或恢复失败时显示确认提示。",
                  "Show a confirmation when exiting, turning protection off, or when restore fails." },
                { "显示托盘通知", "Show tray notifications" },
                { "最小化、保护降级等提示。黑屏期间始终不会显示。",
                  "Notifications for minimize, protection degradation, etc. Never shown while dark." },
                { "开机自启、深色主题和英文界面属于后续版本，本版本不会显示不可用的开关。",
                  "Start with Windows and dark theme are not available in this version; unavailable switches are hidden." },
                { "快捷键", "Shortcuts" },
                { "点击后按下组合键；至少包含 Ctrl/Alt/Shift/Win 之一。",
                  "Click, then press a combination containing at least Ctrl/Alt/Shift/Win." },
                { "恢复默认", "Reset" },
                { "熄屏行为", "Blackout behavior" },
                { "尝试物理关闭显示器", "Try physical display power-off" },
                { "发送 DPMS 关闭请求。部分显示器/虚拟机不支持，覆盖层仍会保持全黑；笔记本内置屏默认不发送。",
                  "Sends a DPMS off request. Some monitors/VMs ignore it; the overlay stays black. Laptop internal panels are skipped by default." },
                { "在笔记本上尝试物理关屏", "Try physical power-off on laptop" },
                { "默认关闭。检测到电池时只使用全屏黑层；开启后可能引起内置屏背光反复亮灭，或唤醒后停留在锁屏。",
                  "Off by default. On battery, only the black overlay is used; enabling may cause internal backlight cycling or wake at the lock screen." },
                { "屏幕仅关闭背光（内置 WMI / 外接 DDC）", "Backlight only (internal WMI / external DDC)" },
                { "默认开启。外接屏通过 DDC/CI VCP 0x10 将亮度设为 0；内置屏通过 WMI WmiSetBrightness 尝试设为 0。显示器保持供电，USB Hub 和键鼠继续工作；不支持时保留全屏黑层。",
                  "On by default. External panels set DDC/CI VCP 0x10 to 0; internal panels try WMI WmiSetBrightness(0). Monitors stay powered, so USB hubs and input keep working. Unsupported panels stay on the black overlay." },
                { "当前检测到电池：默认不会发送物理关屏，避免笔记本内置屏背光反复和唤醒锁屏。",
                  "Battery detected: physical power-off is skipped by default to avoid internal backlight cycling and wake-at-lock-screen." },
                { "当前未检测到电池：物理关屏按“尝试物理关闭显示器”设置执行。",
                  "No battery detected: physical power-off follows the Try physical display power-off setting." },
                { "如果系统启用了“唤醒时要求登录”或强制空闲锁定策略，黑屏结束后可能仍需登录；这是 Windows 安全策略，本程序不会尝试绕过。",
                  "If Windows requires sign-in on wake or has a forced idle-lock policy, sign-in may still be required; this app does not bypass it." },
                { "熄屏期间阻止系统自动睡眠；不会阻止系统关机。",
                  "Prevents automatic sleep while dark; does not block shutdown." },
                { "允许鼠标滚轮恢复", "Allow mouse wheel to restore" },
                { "默认只有点击鼠标或按键恢复，避免轻微滚动唤醒。",
                  "By default only a click or keypress restores, avoiding accidental wake from slight scrolling." },
                { "恢复时还原前台窗口", "Restore foreground window" },
                { "恢复前记忆当前窗口，并尝试切回。",
                  "Remembers the current window and tries to switch back after restore." },
                { "熄屏时静音系统音频", "Mute system audio while dark" },
                { "静音所有活动播放设备；恢复显示时还原之前的状态。默认开启。",
                  "Mutes all active playback devices and restores their state on wake. On by default." },
                { "显示器与预览", "Displays & Preview" },
                { "预览 3 秒", "Preview 3 seconds" },
                { "内置屏（WMI 背光 0）", "Internal (WMI backlight 0)" },
                { "外接屏（DDC 背光 0）", "External (DDC backlight 0)" },
                { "尺寸未知（不自动调背光）", "Unknown size (no auto dim)" },
                { "尺寸判断", "Size estimate" },
                { "启用后需要管理员权限。保护仅在幕帘运行期间临时暂停自动更新，退出或关闭保护时恢复原设置。",
                  "Needs administrator rights. Protection only pauses updates while running and restores settings on exit or when turned off." },
                { "状态：读取中", "Status: reading" },
                { "关闭更新重启保护？", "Turn off update restart protection?" },
                { "幕帘会恢复 Windows Update 原始设置。若更新设置恢复失败，会保留降级状态并允许重试。",
                  "ScreenCurtain will restore original Windows Update settings. If restore fails, it keeps a degraded state and allows retry." },
                { "在本程序运行期间启用保护", "Enable protection while this app runs" },
                { "启用保护", "Enable protection" },
                { "修复更新设置", "Repair update settings" },
                { "“更新保护降级”通常表示管理员权限被取消，或退出时未能恢复原更新策略。修复会再次请求 UAC，并先恢复遗留快照再重新建立保护。",
                  "Degraded protection usually means UAC was cancelled or the original policy was not restored. Repair requests UAC again, restores any leftover snapshot, then re-enables protection." },
                { "幕帘无法阻止用户在系统界面选择“仍要重启”，也无法阻止组策略/强制断电。",
                  "ScreenCurtain cannot stop a user choosing Restart anyway, Group Policy, or a forced power loss." },
                { "诊断信息", "Diagnostics" },
                { "版本：", "Version: " },
                { "系统：", "System: " },
                { "64 位进程", "64-bit process" },
                { "32 位进程", "32-bit process" },
                { "显示器：", "Displays: " },
                { " 台", " display(s)" },
                { "数据目录：", "Data directory: " },
                { "配置文件：", "Config file: " },
                { "日志目录：", "Log directory: " },
                { "便携模式：", "Portable: " },
                { "是", "Yes" },
                { "否", "No" },
                { "（已回退到 LocalAppData）", " (fell back to LocalAppData)" },
                { "更新保护：", "Update protection: " },
                { "复制诊断信息", "Copy diagnostics" },
                { "复制失败：", "Copy failed: " },
                { "打开日志目录", "Open log directory" },
                { "重置", "Reset" },
                { "恢复默认设置会关闭当前更新保护并恢复策略，然后重置熄屏、窗口、快捷键和通知选项。不会删除日志。",
                  "Reset turns off current update protection and restores policy, then resets blackout, window, shortcut and notification options. Logs are kept." },
                { "恢复默认设置", "Reset settings" },
                { "关于", "About" },
                { "。一个不锁屏、不驻留驱动、不改系统服务的屏幕黑屏与更新重启保护工具。",
                  ". A blackout and update-restart protection tool that does not lock the session, install drivers or change services." },
                { "恢复默认设置？", "Reset settings?" },
                { "当前更新保护会先关闭并恢复系统策略；如果恢复失败，请先处理错误再重置。",
                  "Current update protection will be turned off and policies restored; if restore fails, fix that before resetting." },
                { "重置设置时关闭更新保护失败", "Failed to stop update protection while resetting" },
                { "已开启", "On" },
                { "正在启动", "Starting" },
                { "降级 · 需要修复", "Degraded · repair needed" },
                { "正在恢复", "Restoring" },
                { "状态：", "Status: " },
                { "重试保护", "Retry protection" },

                { "取消", "Cancel" },
                { "更新设置恢复失败", "Failed to restore update settings" },
                { "系统更新设置尚未恢复", "System update settings are not restored" },
                { "重试恢复", "Retry restore" },
                { "仍然退出", "Exit anyway" },
                { "预览静音失败", "Preview mute failed" },

                { "打开幕帘", "Open ScreenCurtain" },
                { "停止熄屏并恢复", "Stop blackout and restore" },
                { "更新重启保护：未启动", "Update restart guard: not started" },
                { "修复更新设置…", "Repair update settings…" },
                { "设置…", "Settings…" },
                { "幕帘 · 熄屏就绪", "ScreenCurtain · ready" },
                { "更新重启保护：已开启", "Update restart guard: on" },
                { "更新重启保护：降级", "Update restart guard: degraded" },
                { "更新重启保护：启动中", "Update restart guard: starting" },
                { "更新重启保护：停止中", "Update restart guard: stopping" },
                { "更新重启保护：未启用", "Update restart guard: off" },
                { "保护停止中", "Protection stopping" },
                { "托盘菜单回调失败", "Tray menu callback failed" },
                { "处理", "Processing" },
                { "请按下组合键…", "Press a shortcut…" }
            };

        public static void Initialize(string setting)
        {
            string code = (setting ?? "auto").Trim().ToLowerInvariant();
            if (code == "zh" || code == "zh-cn")
            {
                IsEnglish = false;
            }
            else if (code == "en" || code == "en-us")
            {
                IsEnglish = true;
            }
            else
            {
                string culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                IsEnglish = !string.Equals(culture, "zh", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static string T(string chinese)
        {
            if (!IsEnglish || chinese == null) return chinese;
            string translated;
            return English.TryGetValue(chinese, out translated) ? translated : chinese;
        }

        public static string T(string chinese, string english)
        {
            return IsEnglish ? english : chinese;
        }

        public static void Apply(Control root)
        {
            if (root == null) return;
            try
            {
                string text = root.Text;
                if (!string.IsNullOrEmpty(text))
                {
                    string translated = T(text);
                    if (!ReferenceEquals(translated, text)) root.Text = translated;
                }
                if (root.ContextMenuStrip != null)
                {
                    Apply(root.ContextMenuStrip.Items);
                }
                for (int i = 0; i < root.Controls.Count; i++)
                {
                    Apply(root.Controls[i]);
                }
            }
            catch
            {
            }
        }

        public static void Apply(ToolStripItemCollection items)
        {
            if (items == null) return;
            for (int i = 0; i < items.Count; i++)
            {
                ToolStripItem item = items[i];
                string translated = T(item.Text);
                if (!ReferenceEquals(translated, item.Text)) item.Text = translated;
                ToolStripDropDownItem drop = item as ToolStripDropDownItem;
                if (drop != null) Apply(drop.DropDownItems);
            }
        }
    }
}
