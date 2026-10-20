using QRCoder;

namespace AniSteam;

public sealed class LoginForm : Form
{
    private readonly Label status = new() { AutoSize = true, MaximumSize = new Size(320, 0), Text = "正在连接 Steam…", Anchor = AnchorStyles.None };
    private readonly PictureBox qr = new() { SizeMode = PictureBoxSizeMode.Zoom, Width = 240, Height = 240, Anchor = AnchorStyles.None, BackColor = Color.White };
    private readonly Button refresh = new() { Text = "刷新二维码", AutoSize = true };
    public event Action? RefreshRequested;
    protected override bool ShowWithoutActivation => !ShowInTaskbar;

    public LoginForm()
    {
        Text = "Steam 登录 · AniSteam";
        Icon = AppAssets.Icon;
        Font = new Font("Microsoft YaHei UI", 10);
        ClientSize = new Size(380, 410);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 4 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = "使用 Steam 手机 App 扫码登录", AutoSize = true, Anchor = AnchorStyles.None });
        layout.Controls.Add(qr);
        layout.Controls.Add(status);
        var buttons = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.None, Margin = new Padding(0, 12, 0, 0) };
        var later = new Button { Text = "稍后登录", AutoSize = true };
        buttons.Controls.AddRange([refresh, later]);
        layout.Controls.Add(buttons);
        Controls.Add(layout);
        refresh.Click += (_, _) => RefreshRequested?.Invoke();
        later.Click += (_, _) => Close();
    }

    public void SetStatus(string text, bool canRefresh) { status.Text = text; refresh.Enabled = canRefresh; }

    public void SetQr(string? url)
    {
        Image? next = null;
        if (url is not null) {
            using var data = QRCodeGenerator.GenerateQrCode(url, QRCodeGenerator.ECCLevel.M);
            using var code = new QRCode(data);
            next = code.GetGraphic(6);
        }
        Image? previous = qr.Image;
        qr.Image = next;
        previous?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { qr.Image?.Dispose(); qr.Image = null; }
        base.Dispose(disposing);
    }
}
