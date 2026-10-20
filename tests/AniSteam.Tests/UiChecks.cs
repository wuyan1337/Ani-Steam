using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using AniSteam;

internal static class UiChecks
{
    public static void Run(Action<bool, string> check, string? screenshotDirectory)
    {
        Exception? failure = null;
        var thread = new Thread(() => {
            try { RunOnUiThread(check, screenshotDirectory); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new Exception("UI regression failed", failure);
    }

    private static void Offscreen(Form form)
    {
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-20000, -20000);
    }

    private static void Pump() { for (int i = 0; i < 6; i++) Application.DoEvents(); }

    private static void Until(Func<bool> condition)
    {
        var elapsed = Stopwatch.StartNew();
        while (!condition()) {
            if (elapsed.ElapsedMilliseconds > 3000) throw new TimeoutException("UI callback did not complete");
            Thread.Sleep(10);
            Pump();
        }
    }

    private static void Capture(Form form, string? directory, string name)
    {
        if (directory is null) return;
        Directory.CreateDirectory(directory);
        form.PerformLayout();
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        bitmap.Save(Path.Combine(directory, name), System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void RunOnUiThread(Action<bool, string> check, string? screenshotDirectory)
    {
        ApplicationConfiguration.Initialize();
        WindowChecks.CheckDispatcher(check);
        var initialWindows = WindowChecks.VisibleWindows();
        using var parent = Process.GetCurrentProcess();
        using var input = new PendingReader();
        var steam = new FakeSteam { HasSavedLogin = true };
        var windows = new List<Form>();
        Settings? saved = null;
        using (var context = new AniSteamContext(parent, input, steam, new Settings(), s => saved = s,
            form => { Offscreen(form); windows.Add(form); })) {
            context.Start();
            Pump();
            check(steam.Started && windows.Count == 0, "remembered login starts without constructing or showing any form");
            check(WindowChecks.VisibleWindows().SetEquals(initialWindows), "silent startup creates no visible native windows");
            steam.Status("Steam 已登录", true);
            Pump();
            check(windows.Count == 0, "successful automatic login stays silent");
            steam.Status("Steam 连接已断开，正在重连…", false);
            Pump();
            check(windows.Count == 0, "ordinary network reconnect does not ask for login");
            check(WindowChecks.VisibleWindows().SetEquals(initialWindows), "background login and reconnect create no visible native windows");
            steam.RequireLogin();
            Pump();
            var login = windows.OfType<LoginForm>().Last();
            check(login.Visible && !windows.OfType<MainForm>().Any(), "expired token opens only login window");
            check(steam.QrRequests == 1, "login window requests a QR code");
            steam.Qr("anisteam://test-only-not-a-steam-login");
            steam.Status("请使用 Steam 手机 App 扫码", false);
            Pump();
            Capture(login, screenshotDirectory, "login.png");
            steam.Status("Steam 已登录", true);
            Pump();
            check(login.IsDisposed && !windows.OfType<MainForm>().Any(), "QR success closes login without opening the main window");
            context.OpenMainWindow();
            Pump();
            var main = windows.OfType<MainForm>().Single();
            check(main.Visible, "tray action opens main window");
            check(!Descendants(main).OfType<Label>().Any(c => c.Text.Contains("当前用户") || c.Text.Contains("关闭此窗口")), "removed footer stays removed");
            check(main.Controls.Find("ByteLimit", true).Length == 0 && main.Controls.Find("ByteCount", true).Length == 0,
                "byte limit control and second preview line are removed");
            check(Descendants(main).OfType<Label>().Any(c => c.Text == "字数过多时优先显示"), "overflow label matches requested wording");
            var episodePriority = (RadioButton)main.Controls.Find("EpisodePriority", true).Single();
            var progressPriority = (RadioButton)main.Controls.Find("ProgressPriority", true).Single();
            check(Descendants(main).OfType<RadioButton>().Count() == 2 && episodePriority.Checked,
                "only episode and progress priorities are available, with episode selected by default");
            var previewLabel = main.Controls.Find("StatusPreview", true).Single();
            check(previewLabel.Text == "（无 Steam 播放状态）", "idle preview contains no example title or timestamp");
            var watching = new Watching(1, 3, new string('漫', 30), "3", "", 492000, 1440000, false, false, false);
            void SendPlayback(Watching? value) => input.Send(JsonSerializer.Serialize(new Snapshot(1, parent.Id, true, null, value)));
            SendPlayback(watching);
            Until(() => steam.Desired.Contains("第3集"));
            check(previewLabel.Text == "（无 Steam 播放状态）", "playback target is not presented as a sent Steam message");
            steam.Publish(1000);
            Pump();
            string initialPreview = previewLabel.Text;
            check(initialPreview == steam.PublishedName && initialPreview.Contains('漫') && initialPreview.Contains("第3集"),
                "preview displays the exact published name");
            progressPriority.Checked = true;
            check(previewLabel.Text == initialPreview && !episodePriority.Checked, "unsaved priority does not forge a different Steam preview");
            episodePriority.Checked = true;
            check(!progressPriority.Checked, "priority radio buttons remain mutually exclusive");
            progressPriority.Checked = true;
            ((Button)main.Controls.Find("SaveSettings", true).Single()).PerformClick();
            check(saved is { Priority: DisplayPriority.Progress } && !episodePriority.Checked, "saving persists the selected display policy");
            steam.Publish(1200);
            Pump();
            check(previewLabel.Text == initialPreview, "preview waits while a changed message is throttled");
            steam.Publish(1500);
            Pump();
            check(previewLabel.Text == steam.PublishedName && previewLabel.Text.Contains("/24:00") && !previewLabel.Text.Contains("第3集")
                && StatusFormatter.ByteCount(previewLabel.Text) <= 63, "saved priority appears after actual publishing within the fixed byte budget");
            steam.Publish(1600, blocked: true);
            Pump();
            check(previewLabel.Text == "（无 Steam 播放状态）", "other-game blocking clears the preview with the outgoing clear message");
            steam.Publish(1700);
            Pump();
            check(previewLabel.Text == steam.PublishedName && steam.PublishedName.Length > 0, "preview restores after other-game blocking ends");
            steam.Status("Steam 连接已断开，正在重连…", false);
            Pump();
            check(previewLabel.Text == "（无 Steam 播放状态）", "disconnect removes the unconfirmed previous status");
            steam.Status("Steam 已登录", true);
            Pump();
            check(previewLabel.Text == "（无 Steam 播放状态）", "reconnecting does not display a status before republishing");
            steam.Publish(2000);
            Pump();
            var sync = (CheckBox)main.Controls.Find("SyncEnabled", true).Single();
            sync.Checked = false;
            steam.Publish(2100);
            Pump();
            check(steam.Desired == "" && previewLabel.Text == "（无 Steam 播放状态）", "disabling sync clears the published preview");
            sync.Checked = true;
            steam.Publish(2200);
            Pump();
            SendPlayback(null);
            Until(() => steam.Desired == "");
            steam.Publish(2300);
            Pump();
            check(previewLabel.Text == "（无 Steam 播放状态）", "leaving playback clears the preview without falling back to a sample");
            SendPlayback(watching with { SubjectName = "测试番剧", PositionMillis = 61000 });
            Until(() => steam.Desired.Contains("测试番剧"));
            steam.Publish(2500);
            Pump();
            Capture(main, screenshotDirectory, "main.png");
            var saveButton = main.Controls.Find("SaveSettings", true).Single();
            check(main.RectangleToClient(saveButton.RectangleToScreen(saveButton.ClientRectangle)).Bottom <= main.ClientSize.Height,
                "default window shows preview and save button without scrolling");
            main.Close();
            check(!main.Visible && !main.IsDisposed && !steam.Disposed, "closing main window leaves tray and Steam running");
            int hiddenUpdates = 0;
            previewLabel.TextChanged += (_, _) => hiddenUpdates++;
            steam.Publish(2600, blocked: true);
            Pump();
            check(hiddenUpdates == 0, "hidden main window does not redraw when Steam status changes");
            check(WindowChecks.VisibleWindows().SetEquals(initialWindows), "tray-only operation leaves no visible native windows");
            context.OpenMainWindow();
            Pump();
            check(previewLabel.Text == "（无 Steam 播放状态）", "reopening displays the latest published state");
            main.Close();
            steam.RequireLogin();
            Pump();
            login = windows.OfType<LoginForm>().Last();
            login.Close();
            int existing = windows.Count;
            steam.RequireLogin();
            Pump();
            check(windows.Count == existing, "dismissed login is not repeatedly reopened by connection retries");
            context.OpenLoginWindow();
            Pump();
            check(windows.OfType<LoginForm>().Last().Visible, "manual login remains available after dismissal");
            input.Complete();
            Pump();
            for (int i = 0; i < 30 && !steam.Disposed; i++) { Thread.Sleep(10); Pump(); }
            check(steam.Disposed && steam.Desired == "", "EOF shuts down tray and clears Steam");
            check(windows.All(f => f.IsDisposed), "shutdown closes both windows");
        }
        using var firstInput = new PendingReader();
        var firstSteam = new FakeSteam();
        var firstWindows = new List<Form>();
        using var firstContext = new AniSteamContext(parent, firstInput, firstSteam, new Settings(), _ => { },
            form => { Offscreen(form); firstWindows.Add(form); });
        firstContext.Start();
        Pump();
        check(firstWindows.Count == 1 && firstWindows[0] is LoginForm { Visible: true }, "first launch opens only login window");
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls) {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private sealed class PendingReader : TextReader
    {
        private readonly Channel<string> channel = Channel.CreateUnbounded<string>();
        public override async Task<string?> ReadLineAsync() => await channel.Reader.WaitToReadAsync() ? await channel.Reader.ReadAsync() : null;
        public void Send(string line) => channel.Writer.TryWrite(line);
        public void Complete() => channel.Writer.TryComplete();
        protected override void Dispose(bool disposing) { Complete(); base.Dispose(disposing); }
    }

    private sealed class FakeSteam : ISteamSession
    {
        public bool HasSavedLogin { get; set; }
        public bool Started { get; private set; }
        public bool Disposed { get; private set; }
        public int QrRequests { get; private set; }
        public string Desired { get; private set; } = "";
        private readonly PresencePublisher publisher = new();
        public string PublishedName => publisher.PublishedName;
        public event Action<string>? PublishedNameChanged {
            add => publisher.PublishedNameChanged += value;
            remove => publisher.PublishedNameChanged -= value;
        }
        public event Action<string, bool>? StatusChanged;
        public event Action<string?>? QrChanged;
        public event Action? LoginRequired;
        public void Start() { Started = true; }
        public void SetDesiredName(string name) => Desired = name;
        public void BeginQrLogin() => QrRequests++;
        public void ForgetLogin() => RequireLogin();
        public void Dispose() => Disposed = true;
        public void Publish(long now, bool blocked = false) => publisher.Publish(Desired, now, blocked, _ => { });
        public void RequireLogin() { HasSavedLogin = false; publisher.Reset(); LoginRequired?.Invoke(); }
        public void Status(string text, bool ready) { if (ready) HasSavedLogin = true; else publisher.Reset(); StatusChanged?.Invoke(text, ready); }
        public void Qr(string value) => QrChanged?.Invoke(value);
    }
}
