# AniSteam

Animeko 的 Windows Steam 状态补丁，当前适配 **官方 Animeko v6.3.0 / Windows x64**。将补丁解压到 `Ani.exe` 所在目录后，正常启动 Animeko 即可自动启动同步助手。

Steam 同步思路参考 [wuyan1337/yySync](https://github.com/wuyan1337/yySync)：使用 SteamKit2 登录，将番剧信息显示为**非 Steam 游戏名称**，例如：

```text
在看 葬送的芙莉莲 第3集 8:12/24:00
在看 葬送的芙莉莲 第3集 [暂停]
```

## 安装

1. 使用 [Animeko v6.3.0 Windows x64 官方版](https://github.com/open-ani/animeko/releases/tag/v6.3.0)。先彻底退出 Animeko，包括系统托盘中的进程。
2. 备份安装目录里的 `app\Ani.cfg`。补丁会覆盖这份启动配置；如果你修改过 JVM 参数，请使用下面的手动安装方法。
3. 将 `AniSteam-0.2.2-animeko-6.3.0-win-x64-minimal.zip` **里面的文件**解压到 `Ani.exe` 所在目录，合并 `app` 文件夹，并替换 `app\Ani.cfg`。不要额外套一层补丁文件夹。
4. 照常双击 `Ani.exe`。首次启动仅显示独立的 Steam 登录窗口，用 Steam 手机 App 扫码并确认。登录成功后窗口自动关闭。
5. 在 Animeko 中播放番剧。已保存登录时，AniSteam 直接在托盘静默启动，不显示或闪现主窗口；双击托盘图标打开主界面。仅需要重新登录时自动弹出登录窗口，普通断线重连保持静默。

目录应为：

```text
Animeko 安装目录/
├─ Ani.exe                         # 官方启动器
├─ app/
│  ├─ Ani.cfg                      # 增加一行 agent 启动参数
│  ├─ ani-steam/
│  │  ├─ AniSteam.exe              # 自带 .NET 运行库的同步助手
│  │  └─ ani-steam-agent.jar       # 使用 Animeko 自带 JVM
│  └─ ...                         # 官方 JAR 和资源
├─ runtime/                        # 官方 Java 运行时
└─ README.txt                      # 简短的用途和使用说明
```

无需另装 Java 或 .NET，无需重编译 Animeko。若安装目录需要管理员写入权限，解压和卸载时使用该目录所需的权限即可，运行补丁本身不要求管理员权限。

### 已自定义 Ani.cfg 的安装方式

保留自己的 `app\Ani.cfg`，只复制补丁里的 `app\ani-steam`。在原配置的 `[JavaOptions]` 区域添加一次：

```ini
java-options=-javaagent:$APPDIR\ani-steam\ani-steam-agent.jar
```

这里的 `$APPDIR` 由官方启动器展开，不需要替换为绝对路径。该补丁仍只支持 v6.3.0 x64。

## 行为

- 从 Animeko 内部的本地播放状态读取番名、集数、进度、暂停和缓冲信息，无需加入“一起看”房间。
- 每 250 毫秒读取本地播放状态，通过单调时钟平滑补齐采样间隔，正常速度播放时逐秒更新 Steam 名称。上报会合并 500 毫秒内的快速变化，避免反复拖动时发送过多消息；相同名称不会重复发送。
- 暂停、缓冲时停止计时，跳转或换集时重新对齐，倍速播放按实际倍速推进。源数据不再变化时最多补齐 2 秒，心跳丢失 8 秒后清空；退出播放页或加载下一集时也会清空。
- Steam 服务器和好友客户端可能自行合并或延迟刷新，补丁无法保证好友端每一秒都会单独显示。
- 暂停时默认保留 `[暂停]`，可在设置中选择清空；前缀和进度显示可配置。
- 字节上限固定为 63 UTF-8 字节，不可调整。“字数过多时优先显示”可选“集数 / 进度”，番名始终保留；容量不足时截短番名，并优先保留选中的字段，省略放不下的完整集数或时间。中文和 emoji 不会截断到半个字符，暂停/缓冲标记始终保留。超长内容中的前缀最多占用 12 字节。
- 状态预览跟随实际提交给 Steam 的状态消息，使用同一份文本；限流等待期间仍显示上一条已提交内容。未播放、断线或让位给其他游戏时显示“无 Steam 播放状态”，不显示示例番剧。修改设置后点击“保存设置”，待消息发出再更新预览。Steam 好友端仍可能存在刷新延迟。
- 后台消息调度使用不参与桌面显示的消息窗口；主窗口隐藏后不刷新控件，托盘提示仅在状态改变时更新。后台异常只记录日志，避免启动失败后反复弹窗。
- Steam 断线后重连，并重新发送当前状态。收到 Steam 的“其他游戏占用”信号后暂停本会话的番剧上报。
- 关闭 Animeko 后，通过父进程退出或管道断开清空状态并退出助手。仅关闭 Animeko 窗口但仍保留在托盘时，助手也会保留。
- 登录后将此 Steam 会话设为在线。登录凭据用 Windows DPAPI 加密，只能由当前 Windows 用户解密；不保存 Steam 密码。

Steam 只能展示一个主要游戏状态。同账号同时运行 yySync 或其他上报工具可能相互覆盖，使用时请保留一个同步工具。此功能不会增加某个 Steam 商店游戏的时长、成就或库存物品。

## 卸载及更新

从 0.1.0 / 0.2.x 升级到 0.2.2：彻底退出 Animeko 后覆盖新版文件即可，已保存的 Steam 登录和显示设置继续使用。旧版自定义字节上限统一改为 63，“番名优先”转换为默认的“集数”，“进度优先”保持不变。保留自定义 `Ani.cfg` 时只需替换 `app\ani-steam` 文件夹，既有的 agent 参数无需重复添加。

先彻底退出 Animeko，删除 `app\Ani.cfg` 中上述 `java-options=-javaagent:...` 一行，再删除 `app\ani-steam` 文件夹。**必须先去掉启动参数，再删除 JAR**，否则 Java 启动器会因找不到 agent 而无法启动。精简包不包含卸载脚本；脚本仍保留在独立源码包的 `scripts/` 目录，供维护使用。

Animeko 升级可能覆盖启动配置。此包包含 v6.3.0 专用的 JAR 路径，**不能把它的 Ani.cfg 覆盖到其他版本**；应使用相应版本的补丁。当前不支持 ARM64 和外部播放器。

## 排查

设置、加密凭据和日志位于 `%LOCALAPPDATA%\AniSteam`：

| 文件 | 用途 |
| --- | --- |
| `settings.json` | 显示选项 |
| `steam-session.dat` | 当前用户加密的 Steam 登录凭据 |
| `agent.log` | 采集器启动及兼容性错误 |
| `helper.log` | Steam 连接及助手错误 |

日志不记录登录二维码、令牌或番剧标题。扫码失败可点击“刷新登录二维码”。“退出 Steam 登录”会删除本机缓存凭据；卸载补丁默认保留设置和凭据，需要时可自行删除上述目录。

已保存登录时不弹窗是正常行为，请查看托盘。若连托盘图标也没有，检查解压位置、`app\Ani.cfg` 中的参数和 `app\ani-steam\AniSteam.exe` 是否存在。如果 Animeko 无法启动，先移除补丁启动参数或恢复安装前备份的启动配置。

整屏闪烁或颜色变化仍需在实际播放环境定位。0.2.2 已调整后台窗口与隐藏界面刷新，但自动化不能证明显示器上的闪烁已消失。可在保持播放时暂时取消“启用 Steam 同步”，观察是否随上报停止而消失，以区分 Steam 上报与播放器/显示环境；本补丁没有修改 HDR、刷新率或色彩配置。

## 构建与验证

构建需要 Windows x64、Python 3.10+、JDK 17+（`javac` 在 PATH 中）、.NET 10 SDK：

```powershell
python scripts/build.py
dotnet run --project tests/AniSteam.Tests -c Release
dotnet run --project tests/AniSteam.Tests -c Release -- --ui-snapshot build/ui
python scripts/test_release.py "D:\TestCopies\Ani"
dotnet run --project tests/AniSteam.Tests -c Release -- build/release-snapshots.jsonl
python scripts/package.py --animeko-zip "D:\Downloads\ani-6.3.0-windows-x86_64.zip"
```

`test_release.py` 仅用于**单独解压的官方测试副本**。它临时替换测试副本的启动入口，让官方启动器和自带 JVM 加载真实 `LocalPlaybackBridge` / `AniWatchTogetherWatchingInfo` 类，注入播放状态并检查输出，结束后恢复配置。它不登录 Steam，也不播放在线视频。

精简补丁包、外部校验文件及独立源码包生成在 `dist/`。补丁包只有 `app/Ani.cfg`、`app/ani-steam/AniSteam.exe`、`app/ani-steam/ani-steam-agent.jar` 和 `README.txt` 四个文件。源码、许可证、技术说明及维护脚本统一放在同版本 `AniSteam-<version>-source.zip` 中单独提供，无需复制到安装目录；文件哈希清单和 ZIP 校验文件也位于安装包外。技术依据和验证边界见 [docs/implementation.md](docs/implementation.md)。

软件图标来自 `src/AniSteam/logo.png`，对应多尺寸 Windows 图标为 `logo.ico`。替换 PNG 后可执行 `python scripts/make_icon.py` 重新生成 ICO（此步骤需要 Pillow），再重新构建。

0.1.0 的整体功能已由用户在本机确认。自动验证覆盖静默启动、登录/主窗口独立生命周期、固定 63 字节与两种优先级、旧版设置兼容、连续逐秒进度、暂停/缓冲/跳转/倍速和卸载；真实发布类/JVM 集成已在 0.2.0 验证。新版 Steam 好友端的实际刷新节奏仍需本机使用时观察。

本项目采用 AGPL-3.0-only；第三方组件见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
