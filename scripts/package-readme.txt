AniSteam

把 Animeko 正在播放的番名、集数和进度同步到 Steam 状态。
适用于官方 Animeko v6.3.0 / Windows x64，无需另装 Java 或 .NET。

怎么用
1. 彻底退出 Animeko，包括系统托盘中的进程。
2. 首次安装先备份安装目录里的 app\Ani.cfg。
3. 将本包的 app 文件夹复制到 Ani.exe 所在目录，合并并覆盖同名文件。
4. 正常启动 Ani.exe，首次使用时用 Steam 手机 App 扫码登录。
5. 在 Animeko 内播放番剧即可同步。以后自动静默启动，双击 AniSteam 托盘图标打开设置。

已装旧版补丁：退出 Animeko 后，只覆盖 app\ani-steam 内的两个文件即可。
自定义过 Ani.cfg：保留自己的配置，复制 app\ani-steam，并在 [JavaOptions] 下添加一次：
java-options=-javaagent:$APPDIR\ani-steam\ani-steam-agent.jar

卸载：退出 Animeko，先删除 app\Ani.cfg 中上述 java-options 行，再删除 app\ani-steam 文件夹。

源码和第三方许可单独提供于 {SOURCE_ARCHIVE}，无需解压到安装目录。
