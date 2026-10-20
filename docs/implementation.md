# 实现依据

## 固定的上游版本

- Animeko：`v6.3.0`，commit `68e7b75547f6e2bb3b9b6ea2bb6215ce4d759442`。
- Windows 包：`ani-6.3.0-windows-x86_64.zip`。
- 官方 `app/Ani.cfg` SHA-256：`6952d7df31df89d46e08be831a6139963cf2613d1dcde9ee4e1da3079adddd8f`。
- yySync 参考 commit：`1820a20e9099b7350d306483cd93083f68d2153f`。

## 播放数据

[Animeko 的 AniDesktop.kt](https://github.com/open-ani/animeko/blob/v6.3.0/app/desktop/src/main/kotlin/AniDesktop.kt) 将窗口标题固定为 `Ani`，窗口标题不能提供番名和集数。

[EpisodeViewModel.kt](https://github.com/open-ani/animeko/blob/v6.3.0/app/shared/src/commonMain/kotlin/ui/subject/episode/EpisodeViewModel.kt) 默认注册 `WatchTogetherPlayerExtension`。该扩展会在本地播放时向 `LocalPlaybackBridge` 更新 `AniWatchTogetherWatchingInfo`，报告过程不要求加入房间。`onBeforeSwitchEpisode` 和 `onClose` 清理所属播放状态。

agent 在 `LocalPlaybackBridge` 的无参数构造器正常返回时记录弱引用，每 250 毫秒通过 `getLocalWatching()` / `StateFlow.getValue()` 读取数据模型，包含播放倍速和源报告时间。ASM 仅插入这个观察调用，不改动播放器逻辑。反射或适配失败会报告错误并清空同步数据；Steam 网络操作在独立进程执行。

模型读取的是上游最后报告的状态。上游在切换媒体源或播放器错误期间可能短暂保留上一次稳定状态；补丁没有额外修改这一策略。外部播放器不在此数据通道内。

## 自动启动与进程边界

官方 Windows 包使用 jpackage，且自带 Java 21 JVM 和 `java.instrument`。覆盖包在官方 `Ani.cfg` 末尾追加一条 `-javaagent`，其余原始字节保持不变。补丁不替换官方 EXE 或 JAR。

精简安装包仅包含启动配置、助手 EXE、agent JAR 和简短 `README.txt`。源码、第三方许可和维护脚本放在同版本源码压缩包中独立提供；SHA-256 清单与 ZIP 校验文件也留在安装包外。运行二进制与 0.2.2 完整包一致。

agent 通过 `ProcessBuilder` 启动旁边的 `AniSteam.exe --parent-pid PID`，经匿名标准输入管道发送 UTF-8 JSON 行。没有开放 TCP/HTTP 端口，也不把观看内容写入磁盘。助手验证协议版本和父进程 PID，8 秒未收到有效心跳即清空状态；父进程退出或 EOF 时清理 Steam 会话。异常退出的助手最多每 15 秒重启一次。

agent 的工作线程为 daemon，不在 JVM 退出时等待管道写入。助手自身监控父进程；这避免助手无响应拖住 Animeko 的退出。

助手使用不绑定主窗口的 `ApplicationContext`，启动时只创建托盘和消息调度控件。已保存登录时完全不创建窗口；首次登录或凭据失效时只创建 `LoginForm`。主窗口通过托盘按需创建，关闭主窗口不会退出同步会话。普通网络断线不会触发登录弹窗。

0.2.2 将消息调度控件的父窗口设为 `HWND_MESSAGE`，使其不进入桌面窗口树。主窗口隐藏时不刷新连接、播放及预览控件，重新打开时一次性加载当前状态。托盘提示只随连接或发布状态变化更新；后台启动异常只记日志。此修改减少助手对桌面显示的干扰，不修改播放器渲染、HDR、Gamma 或刷新率；整屏闪烁和颜色变化尚未在自动化环境中复现。

## 进度与字节预算

`PlaybackFeed` 以单调时钟作为显示时间基准，使用源报告更新校正。较小的正常采样偏差保留连续时间轴；暂停、缓冲、倍速变化、换集、后退跳转及超过 1.5 秒的源偏差重置基准。源报告停止变化时最多外推 2 秒，8 秒没有心跳后清空。

主循环每 100 毫秒计算当前状态，格式化名称只在秒数或播放状态改变时发生变化。Steam 会话按名称去重，仅合并 500 毫秒内的快速连续变化，避免固定 5 秒等待和累计计时漂移。此策略改善发送节奏；Steam 服务及好友端的显示刷新由其自身决定。

字节上限固定为 63 UTF-8 字节，不再保存或读取可配置上限。容量足够时包含全部字段；不足时先保留暂停/缓冲标记和至少一个完整番名字符，再按用户选择保留集数或进度，剩余预算分配给前缀和番名。两种优先级始终保留番名；集数和时间作为完整字段加入或省略，Unicode 按 Rune 边界截断。旧版设置中的自定义字节上限被忽略，“番名优先”（数值 1）转为默认的“集数”（数值 0），旧“进度优先”（数值 2）继续使用。

## Steam 会话

沿用 [yySync 的 SteamSessionManager](https://github.com/wuyan1337/yySync/blob/1820a20e9099b7350d306483cd93083f68d2153f/SteamSessionManager.cs) 思路：`CMsgClientGamesPlayed` + `GameID.GameType.Shortcut` + `game_extra_info`。

刷新令牌通过 SteamKit2 的 QR 认证取得，DPAPI `CurrentUser` 加密保存。单实例互斥防止多个助手争抢同账号状态。每次登录/断线重置发送缓存，空列表消息用于明确清空；其他游戏占用时将本会话目标置空。

状态预览使用 `PresencePublisher.PublishedNameChanged` 事件：构造并调用 `SteamClient.Send` 后才通知界面，显示实际消息中的 `game_extra_info`。未保存的设置、被限流的目标及发送失败的内容不会进入预览；连接重置时清空。这里确认的是本机提交给 Steam 的内容，协议没有好友端显示完成的确认，不能保证各个客户端同一时刻刷新。

## 验证边界

自动测试验证 UTF-8 边界、长标题/前缀、暂停/缓冲/加载、心跳过期、EOF、断线重新上报、其他游戏让位、Steam 消息结构、数据来源验证。发布包集成测试通过官方 `Ani.exe` 和其自带 JVM 加载真实类，覆盖中文、引号、换行、emoji、换集、旧 owner 清理和退出。

界面测试使用注入的 Steam 会话，在屏幕外渲染真实控件，不登录 Steam。覆盖记住登录时不创建窗口、失效时只显示登录、扫码成功关闭登录、断网重连保持静默、主窗口关闭继续同步、稍后登录与手动恢复、设置保存后按实际发布内容更新预览。检查断线、其他游戏占用、关闭同步及结束播放时清空预览；限流或未保存时不显示目标草稿，隐藏窗口不刷新。通过 Win32 窗口枚举检查静默启动、重连及托盘运行没有可见原生窗口，并验证消息调度器位于 `HWND_MESSAGE`。计时测试在存在采样抖动的连续 30 秒中检查逐秒发送，也覆盖停滞、暂停、缓冲、跳转、倍速和时长上限。

0.1.0 整体功能已获用户本机确认；自动化不验证新版真实 Steam 服务及好友端的最终刷新节奏。

## 人工验收

1. 关闭其他 Steam 同步工具，将补丁覆盖到指定官方版本，正常启动 `Ani.exe`。
2. Steam 手机扫码并确认，助手显示“Steam 已登录”。
3. 播放番剧，从另一账号/好友端确认显示番名和集数。
4. 暂停、恢复、拖动进度、切换集数、退出播放页，分别确认状态变化及清空。
5. 断网再恢复，确认当前状态恢复上报；启动另一个 Steam 游戏，确认番剧上报让位。
6. 退出 Animeko，确认助手退出；再次启动验证凭据自动登录。
7. 彻底退出并卸载补丁，验证官方 `Ani.exe` 正常启动。
