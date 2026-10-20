namespace AniSteam;

public sealed class MainForm : Form
{
    private readonly Label connection = new() { AutoSize = true, Text = "正在连接 Steam…", MaximumSize = new Size(300, 0) };
    private readonly Label playback = new() { AutoSize = true, Text = "等待播放番剧" };
    private readonly Label preview = new() { Name = "StatusPreview", Text = "（无 Steam 播放状态）", AutoSize = true, MaximumSize = new Size(490, 0) };
    private readonly TextBox prefix = new() { Name = "Prefix", Dock = DockStyle.Fill, MaxLength = 63 };
    private readonly RadioButton episodePriority = new() { Name = "EpisodePriority", Text = "集数", AutoSize = true };
    private readonly RadioButton progressPriority = new() { Name = "ProgressPriority", Text = "进度", AutoSize = true };
    private readonly CheckBox progress = new() { Name = "ShowProgress", Text = "显示播放进度", AutoSize = true };
    private readonly CheckBox paused = new() { Name = "ShowPaused", Text = "暂停时保留状态", AutoSize = true };
    private readonly CheckBox enabled = new() { Name = "SyncEnabled", Text = "启用 Steam 同步", Checked = true, AutoSize = true };
    private readonly Button login = new() { Text = "Steam 登录", AutoSize = true };
    private readonly Button logout = new() { Text = "退出登录", AutoSize = true };
    private bool shuttingDown;
    public event Action<Settings>? SettingsSaved;
    public event Action? LoginRequested;
    public event Action? LogoutRequested;
    public event Action<bool>? SyncEnabledChanged;
    protected override bool ShowWithoutActivation => !ShowInTaskbar;

    public MainForm(Settings settings)
    {
        settings = settings.Normalize();
        Text = "AniSteam";
        Icon = AppAssets.Icon;
        Font = new Font("Microsoft YaHei UI", 10);
        ClientSize = new Size(620, 500);
        MinimumSize = new Size(545, 470);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(20), ColumnCount = 1, RowCount = 0 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var header = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        header.Controls.Add(new PictureBox { Image = AppAssets.Logo, SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(40, 40) });
        header.Controls.Add(new Label { Text = "Animeko → Steam", Font = new Font(Font, FontStyle.Bold), AutoSize = true, Margin = new Padding(10, 10, 0, 0) });
        AddRow(layout, header);
        var account = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 8) };
        connection.Margin = new Padding(0, 8, 12, 0);
        account.Controls.AddRange([connection, login, logout]);
        AddRow(layout, account);
        AddRow(layout, playback);
        var options = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 8) };
        options.Controls.AddRange([enabled, progress, paused]);
        AddRow(layout, options);
        var fields = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 0 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddField(fields, "状态前缀", prefix);
        var priorityRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
        priorityRow.Controls.AddRange([episodePriority, progressPriority]);
        AddField(fields, "字数过多时优先显示", priorityRow);
        AddRow(layout, fields);
        var previewGroup = new GroupBox { Text = "状态预览", AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(12), Margin = new Padding(0, 10, 0, 8) };
        var previewLayout = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 0 };
        previewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(previewLayout, preview);
        previewGroup.Controls.Add(previewLayout);
        AddRow(layout, previewGroup);
        var save = new Button { Text = "保存设置", AutoSize = true, Name = "SaveSettings" };
        AddRow(layout, save);
        scroll.Controls.Add(layout);
        Controls.Add(scroll);
        prefix.Text = settings.Prefix;
        episodePriority.Checked = settings.Priority == DisplayPriority.Episode;
        progressPriority.Checked = settings.Priority == DisplayPriority.Progress;
        progress.Checked = settings.ShowProgress;
        paused.Checked = settings.ShowPaused;
        enabled.CheckedChanged += (_, _) => SyncEnabledChanged?.Invoke(enabled.Checked);
        login.Click += (_, _) => LoginRequested?.Invoke();
        logout.Click += (_, _) => LogoutRequested?.Invoke();
        save.Click += (_, _) => SettingsSaved?.Invoke(DraftSettings);
        FormClosing += (_, e) => {
            if (!shuttingDown && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
        };
    }

    public Settings DraftSettings => new Settings(prefix.Text, progress.Checked, paused.Checked,
        progressPriority.Checked ? DisplayPriority.Progress : DisplayPriority.Episode).Normalize();

    public void SetConnection(string text, bool loggedOn, bool hasSavedLogin)
    {
        connection.Text = text;
        login.Visible = !loggedOn;
        logout.Visible = hasSavedLogin;
    }

    public void SetPlayback(string state, bool syncEnabled)
    {
        if (playback.Text != state) playback.Text = state;
        if (enabled.Checked != syncEnabled) enabled.Checked = syncEnabled;
    }

    public void Shutdown() { shuttingDown = true; Close(); }

    public void SetPublishedName(string name)
    {
        string text = name.Length == 0 ? "（无 Steam 播放状态）" : name;
        if (preview.Text != text) preview.Text = text;
    }

    private static void AddRow(TableLayoutPanel table, Control control)
    {
        int row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(control, 0, row);
    }

    private static void AddField(TableLayoutPanel table, string label, Control control)
    {
        int row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 6, 16, 8) }, 0, row);
        table.Controls.Add(control, 1, row);
    }
}
