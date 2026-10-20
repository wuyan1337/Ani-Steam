using System.Diagnostics;

namespace AniSteam;

public sealed class AniSteamContext : ApplicationContext
{
    private readonly Process parent;
    private readonly TextReader input;
    private readonly ISteamSession steam;
    private readonly Action<Settings> saveSettings;
    private readonly Action<Form>? configureWindow;
    private readonly MessageDispatcher dispatcher = new();
    private readonly PlaybackFeed feed = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 100 };
    private readonly NotifyIcon tray = new() { Icon = AppAssets.Icon, Text = "AniSteam", Visible = true };
    private readonly ToolStripMenuItem toggle = new("暂停同步") { CheckOnClick = true };
    private MainForm? mainWindow;
    private LoginForm? loginWindow;
    private Settings settings;
    private string connection = "正在连接 Steam…";
    private string? qrUrl;
    private string publishedName;
    private bool ready;
    private bool needsLogin;
    private bool loginPrompted;
    private bool syncEnabled = true;
    private bool closing;
    private bool started;

    public AniSteamContext(Process parent, TextReader input, ISteamSession? steam = null,
        Settings? settings = null, Action<Settings>? saveSettings = null, Action<Form>? configureWindow = null)
    {
        this.parent = parent;
        this.input = input;
        this.steam = steam ?? new SteamSession();
        publishedName = this.steam.PublishedName;
        this.settings = (settings ?? Storage.LoadSettings()).Normalize();
        this.saveSettings = saveSettings ?? Storage.SaveSettings;
        this.configureWindow = configureWindow;
        needsLogin = !this.steam.HasSavedLogin;
        _ = dispatcher.Handle;
        var menu = new ContextMenuStrip();
        menu.Items.Add("打开 AniSteam", null, (_, _) => OpenMainWindow());
        menu.Items.Add("Steam 登录", null, (_, _) => OpenLoginWindow());
        toggle.CheckedChanged += (_, _) => SetSyncEnabled(!toggle.Checked);
        menu.Items.Add(toggle);
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => OpenMainWindow();
        this.steam.StatusChanged += OnStatus;
        this.steam.QrChanged += OnQr;
        this.steam.LoginRequired += OnLoginRequired;
        this.steam.PublishedNameChanged += OnPublishedName;
        timer.Tick += (_, _) => {
            if (parent.HasExited || feed.Ended) { Shutdown(); return; }
            UpdatePlayback();
        };
    }

    public void Start()
    {
        if (started || closing) return;
        started = true;
        dispatcher.BeginInvoke(() => {
            if (closing) return;
            if (needsLogin) ShowLoginWindow();
            steam.Start();
            timer.Start();
            _ = ReadFeed();
        });
    }

    public void OpenMainWindow()
    {
        if (closing) return;
        if (mainWindow is null || mainWindow.IsDisposed) {
            mainWindow = new MainForm(settings);
            configureWindow?.Invoke(mainWindow);
            mainWindow.LoginRequested += OpenLoginWindow;
            mainWindow.LogoutRequested += () => steam.ForgetLogin();
            mainWindow.SyncEnabledChanged += SetSyncEnabled;
            mainWindow.SettingsSaved += value => {
                try { saveSettings(value); settings = value; UpdatePlayback(); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                    MessageBox.Show(mainWindow, "无法保存设置：" + ex.GetType().Name, "AniSteam");
                }
            };
        }
        mainWindow.SetConnection(connection, ready, steam.HasSavedLogin);
        UpdatePlayback(refreshWindow: true);
        mainWindow.SetPublishedName(publishedName);
        mainWindow.Show();
        mainWindow.WindowState = FormWindowState.Normal;
        if (mainWindow.ShowInTaskbar) mainWindow.Activate();
    }

    public void OpenLoginWindow()
    {
        if (closing || ready) return;
        ShowLoginWindow();
        if (needsLogin && qrUrl is null) steam.BeginQrLogin();
    }

    private void ShowLoginWindow()
    {
        loginPrompted = true;
        if (loginWindow is null || loginWindow.IsDisposed) {
            loginWindow = new LoginForm();
            configureWindow?.Invoke(loginWindow);
            loginWindow.RefreshRequested += () => steam.BeginQrLogin();
        }
        loginWindow.SetStatus(connection, needsLogin);
        loginWindow.SetQr(qrUrl);
        loginWindow.Show();
        if (loginWindow.ShowInTaskbar) loginWindow.Activate();
    }

    private void OnLoginRequired() => Ui(() => {
        ready = false;
        needsLogin = true;
        UpdateTray();
        if (!loginPrompted) ShowLoginWindow();
        if (loginWindow is { IsDisposed: false }) {
            loginWindow.SetStatus(connection, true);
            steam.BeginQrLogin();
        }
    });

    private void OnStatus(string text, bool loggedOn) => Ui(() => {
        connection = text;
        ready = loggedOn;
        if (loggedOn) {
            needsLogin = loginPrompted = false;
            qrUrl = null;
            loginWindow?.Close();
            loginWindow = null;
        } else if (loginWindow is { IsDisposed: false }) loginWindow.SetStatus(text, needsLogin);
        if (mainWindow is { Visible: true }) mainWindow.SetConnection(text, loggedOn, steam.HasSavedLogin);
        UpdateTray();
        UpdatePlayback();
    });

    private void OnPublishedName(string name) => Ui(() => {
        publishedName = name;
        if (mainWindow is { Visible: true }) mainWindow.SetPublishedName(name);
        UpdateTray();
    });

    private void OnQr(string? url) => Ui(() => {
        qrUrl = url;
        if (loginWindow is { IsDisposed: false }) loginWindow.SetQr(url);
    });

    private void SetSyncEnabled(bool value)
    {
        if (syncEnabled == value) return;
        syncEnabled = value;
        toggle.Checked = !value;
        UpdatePlayback();
    }

    private async Task ReadFeed()
    {
        try {
            while (await input.ReadLineAsync() is { } line) {
                if (closing) return;
                try { feed.Accept(Snapshot.Parse(line, parent.Id), Environment.TickCount64); }
                catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidDataException) {
                    Storage.Log("Playback data rejected: " + ex.GetType().Name);
                    break;
                }
            }
        } catch (IOException) { }
        finally { feed.End(); Shutdown(); }
    }

    private void UpdatePlayback(bool refreshWindow = false)
    {
        long now = Environment.TickCount64;
        string name = syncEnabled ? feed.DesiredName(settings, now) : "";
        steam.SetDesiredName(name);
        string state = !syncEnabled ? "同步已暂停" : !feed.IsFresh(now) ? "等待播放器数据"
            : feed.Latest?.Error is not null ? "播放状态读取失败"
            : feed.Latest?.Watching is null ? "等待播放番剧"
            : feed.Latest.Watching.Loading ? "正在加载视频" : "已读取播放状态";
        if (mainWindow is not null && (mainWindow.Visible || refreshWindow)) mainWindow.SetPlayback(state, syncEnabled);
    }

    private void UpdateTray()
    {
        string text = ready ? "AniSteam · " + (publishedName.Length == 0 ? "等待播放" : "正在同步")
            : "AniSteam · " + (needsLogin ? "等待登录" : "正在连接 Steam");
        if (tray.Text != text) tray.Text = text;
    }

    private void Ui(Action action)
    {
        if (closing) return;
        try { dispatcher.BeginInvoke(() => { if (!closing) action(); }); }
        catch (InvalidOperationException) { }
    }

    public void Shutdown()
    {
        if (closing) return;
        closing = true;
        timer.Stop();
        tray.Visible = false;
        steam.StatusChanged -= OnStatus;
        steam.QrChanged -= OnQr;
        steam.LoginRequired -= OnLoginRequired;
        steam.PublishedNameChanged -= OnPublishedName;
        steam.SetDesiredName("");
        steam.Dispose();
        loginWindow?.Close();
        mainWindow?.Shutdown();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) {
            Shutdown();
            timer.Dispose();
            tray.ContextMenuStrip?.Dispose();
            tray.Dispose();
            dispatcher.Dispose();
            loginWindow?.Dispose();
            mainWindow?.Dispose();
        }
        base.Dispose(disposing);
    }
}
