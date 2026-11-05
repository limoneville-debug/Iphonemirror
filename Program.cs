using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm(new MirrorState()));
    }
}

sealed class AppTile
{
    public string Name { get; }
    public Color Color { get; }
    public int Row { get; }
    public int Col { get; }
    public string Initial => Name.Length > 0 ? Name.Substring(0, 1).ToUpperInvariant() : "?";
    public AppTile(string name, Color color, int row, int col)
    { Name = name; Color = color; Row = row; Col = col; }
}

sealed class Ripple
{
    public int X { get; }
    public int Y { get; }
    public DateTime Started { get; } = DateTime.UtcNow;
    public Ripple(int x, int y) { X = x; Y = y; }
    public double AgeMs => (DateTime.UtcNow - Started).TotalMilliseconds;
}

sealed class MirrorState
{
    public const int DeviceW = 1170;
    public const int DeviceH = 2532;
    public const int IconSize = 180;
    public const int IconGap = 70;
    public const int IconTop = 240;
    public const int IconLeft = 120;
    public const int RowPitch = IconSize + 120;

    public List<Ripple> Ripples { get; } = new List<Ripple>();
    public List<AppTile> Apps { get; } = new List<AppTile>
    {
        new AppTile("Maps",   Color.FromArgb(59, 130, 246), 0, 0),
        new AppTile("Photos", Color.FromArgb(245, 158, 11), 0, 1),
        new AppTile("Mail",   Color.FromArgb(16, 185, 129), 0, 2),
        new AppTile("Notes",  Color.FromArgb(139, 92, 246), 0, 3),
        new AppTile("Safari", Color.FromArgb(239, 68, 68),  1, 0),
        new AppTile("Music",  Color.FromArgb(6, 182, 212),  1, 1),
        new AppTile("Camera", Color.FromArgb(244, 114, 182), 1, 2),
        new AppTile("Clock",  Color.FromArgb(132, 204, 22),  1, 3),
        new AppTile("News",   Color.FromArgb(249, 115, 22),  2, 0),
        new AppTile("Files",  Color.FromArgb(168, 85, 247),  2, 1),
        new AppTile("Health", Color.FromArgb(20, 184, 166),  2, 2),
        new AppTile("Wallet", Color.FromArgb(99, 102, 241),  2, 3),
    };

    public string Banner { get; private set; } = "";
    public DateTime BannerUntil { get; private set; }
    public bool BannerVisible => Banner.Length > 0 && DateTime.UtcNow < BannerUntil;

    public void HandleTap(int x, int y)
    {
        Ripples.Add(new Ripple(x, y));
        while (Ripples.Count > 40) Ripples.RemoveAt(0);
        var hit = HitTest(x, y);
        if (hit != null) ShowBanner("Launched: " + hit.Name);
    }

    public void HandleType(string text) => ShowBanner("Typed: " + text);
    public void HandleButton(string name) => ShowBanner("Pressed: " + name);

    private void ShowBanner(string text)
    {
        Banner = text;
        BannerUntil = DateTime.UtcNow.AddSeconds(3);
    }

    private AppTile HitTest(int x, int y)
    {
        foreach (var a in Apps)
        {
            int tx = IconLeft + a.Col * (IconSize + IconGap);
            int ty = IconTop + a.Row * RowPitch;
            if (x >= tx && x <= tx + IconSize && y >= ty && y <= ty + IconSize)
                return a;
        }
        return null;
    }

    public void CleanupExpired() => Ripples.RemoveAll(r => r.AgeMs > 900);
}

sealed class PhonePanel : Control
{
    private readonly MirrorState _state;

    public PhonePanel(MirrorState state)
    {
        _state = state;
        SetStyle(ControlStyles.AllPaintingInWmPaint
               | ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.UserPaint
               | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(20, 20, 28);
    }

    private (float scale, float ox, float oy) ComputeLayout()
    {
        float scale = Math.Min(Width / (float)MirrorState.DeviceW,
                               Height / (float)MirrorState.DeviceH);
        float w = MirrorState.DeviceW * scale;
        float h = MirrorState.DeviceH * scale;
        return (scale, (Width - w) / 2f, (Height - h) / 2f);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        var (scale, ox, oy) = ComputeLayout();
        var st = g.Save();
        g.TranslateTransform(ox, oy);
        g.ScaleTransform(scale, scale);

        using (var path = RoundedRect(0, 0, MirrorState.DeviceW, MirrorState.DeviceH, 90))
        {
            g.SetClip(path);
            PaintDevice(g);
        }

        g.Restore(st);
    }

    private void PaintDevice(Graphics g)
    {
        using (var brush = new LinearGradientBrush(
            new Point(0, 0),
            new Point(MirrorState.DeviceW, MirrorState.DeviceH),
            Color.FromArgb(28, 38, 78),
            Color.FromArgb(72, 24, 108)))
        {
            g.FillRectangle(brush, 0, 0, MirrorState.DeviceW, MirrorState.DeviceH);
        }

        using (var font = new Font("Segoe UI", 44f, FontStyle.Bold))
        using (var brush = new SolidBrush(Color.FromArgb(230, 255, 255, 255)))
        {
            g.DrawString("9:41", font, brush, 60f, 60f);
            string pct = "100%";
            var sz = g.MeasureString(pct, font);
            g.DrawString(pct, font, brush, MirrorState.DeviceW - sz.Width - 60f, 60f);
        }

        foreach (var a in _state.Apps)
        {
            int tx = MirrorState.IconLeft + a.Col * (MirrorState.IconSize + MirrorState.IconGap);
            int ty = MirrorState.IconTop + a.Row * MirrorState.RowPitch;

            using (var path = RoundedRect(tx, ty, MirrorState.IconSize, MirrorState.IconSize, 42))
            using (var brush = new SolidBrush(a.Color))
                g.FillPath(brush, path);

            using (var font = new Font("Segoe UI", 52f, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.White))
            {
                var sz = g.MeasureString(a.Initial, font);
                g.DrawString(a.Initial, font, brush,
                    tx + (MirrorState.IconSize - sz.Width) / 2f,
                    ty + (MirrorState.IconSize - sz.Height) / 2f);
            }

            using (var font = new Font("Segoe UI", 26f))
            using (var brush = new SolidBrush(Color.FromArgb(230, 255, 255, 255)))
            {
                var sz = g.MeasureString(a.Name, font);
                g.DrawString(a.Name, font, brush,
                    tx + (MirrorState.IconSize - sz.Width) / 2f,
                    ty + MirrorState.IconSize + 18f);
            }
        }

        using (var brush = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
            g.FillRectangle(brush, MirrorState.DeviceW / 2f - 180f,
                MirrorState.DeviceH - 70f, 360f, 12f);

        foreach (var r in _state.Ripples)
        {
            double age = r.AgeMs;
            if (age > 900) continue;
            double t = age / 900.0;
            int radius = (int)(30 + t * 90);
            int alpha = (int)(255 * (1 - t));
            if (alpha < 0) alpha = 0;
            using (var pen = new Pen(Color.FromArgb(alpha, Color.Cyan), 8f))
                g.DrawEllipse(pen, r.X - radius, r.Y - radius, radius * 2, radius * 2);
        }

        if (_state.BannerVisible)
        {
            using (var brush = new SolidBrush(Color.FromArgb(210, 0, 0, 0)))
            using (var path = RoundedRect(60f, 150f, MirrorState.DeviceW - 120f, 130f, 32f))
                g.FillPath(brush, path);

            using (var font = new Font("Segoe UI", 40f, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.White))
            using (var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            })
            {
                g.DrawString(_state.Banner, font, brush,
                    new RectangleF(60f, 150f, MirrorState.DeviceW - 120f, 130f), sf);
            }
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        var (scale, ox, oy) = ComputeLayout();
        int vx = (int)((e.X - ox) / scale);
        int vy = (int)((e.Y - oy) / scale);
        if (vx < 0 || vy < 0 || vx > MirrorState.DeviceW || vy > MirrorState.DeviceH) return;
        _state.HandleTap(vx, vy);
        Invalidate();
    }

    private static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
    {
        var path = new GraphicsPath();
        float d = r * 2f;
        path.AddArc(x, y, d, d, 180f, 90f);
        path.AddArc(x + w - d, y, d, d, 270f, 90f);
        path.AddArc(x + w - d, y + h - d, d, d, 0f, 90f);
        path.AddArc(x, y + h - d, d, d, 90f, 90f);
        path.CloseFigure();
        return path;
    }
}

sealed class MainForm : Form
{
    private readonly MirrorState _state;
    private readonly PhonePanel _phone;
    private readonly TextBox _typeBox;
    private readonly Label _status;
    private readonly Timer _timer;

    public MainForm(MirrorState state)
    {
        _state = state;
        Text = "iPhoneMirror 2.0 — simulated device";
        Width = 1200;
        Height = 900;
        MinimumSize = new Size(900, 700);
        BackColor = Color.FromArgb(28, 28, 36);
        StartPosition = FormStartPosition.CenterScreen;

        _phone = new PhonePanel(_state) { Dock = DockStyle.Fill };

        var side = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 340,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(24),
            BackColor = Color.FromArgb(22, 22, 30),
        };

        var title = new Label
        {
            Text = "Simulated iPhone 16 Pro Max",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12),
        };

        var subtitle = new Label
        {
            Text = "Click the phone to tap. Click icons to launch apps.",
            ForeColor = Color.LightGray,
            Font = new Font("Segoe UI", 9f),
            Width = 280,
            Height = 50,
            Margin = new Padding(0, 0, 0, 12),
        };

        _typeBox = new TextBox
        {
            Width = 280,
            Text = "hello world",
            Font = new Font("Segoe UI", 11f),
            Margin = new Padding(0, 0, 0, 6),
        };

        var typeBtn = MakeButton("Send typed text");
        var homeBtn = MakeButton("Home");
        var volUp = MakeButton("Volume +");
        var volDown = MakeButton("Volume \u2212");
        var lockBtn = MakeButton("Lock");
        var selfTest = MakeButton("Run self-test");

        typeBtn.Click += (_, _) => { _state.HandleType(_typeBox.Text); _phone.Invalidate(); };
        homeBtn.Click += (_, _) => { _state.HandleButton("home"); _phone.Invalidate(); };
        volUp.Click += (_, _) => { _state.HandleButton("volumeUp"); _phone.Invalidate(); };
        volDown.Click += (_, _) => { _state.HandleButton("volumeDown"); _phone.Invalidate(); };
        lockBtn.Click += (_, _) => { _state.HandleButton("lock"); _phone.Invalidate(); };
        selfTest.Click += OnSelfTest;

        _status = new Label
        {
            Text = "Ready.",
            ForeColor = Color.LightGray,
            Font = new Font("Segoe UI", 9f),
            Width = 280,
            Height = 80,
            Margin = new Padding(0, 12, 0, 0),
        };

        side.Controls.Add(title);
        side.Controls.Add(subtitle);
        side.Controls.Add(_typeBox);
        side.Controls.Add(typeBtn);
        side.Controls.Add(homeBtn);
        side.Controls.Add(volUp);
        side.Controls.Add(volDown);
        side.Controls.Add(lockBtn);
        side.Controls.Add(selfTest);
        side.Controls.Add(_status);

        Controls.Add(_phone);
        Controls.Add(side);

        _timer = new Timer { Interval = 33 };
        _timer.Tick += (_, _) =>
        {
            _state.CleanupExpired();
            _phone.Invalidate();
        };
        _timer.Start();
    }

    private static Button MakeButton(string text)
    {
        return new Button
        {
            Text = text,
            Width = 280,
            Height = 38,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(55, 55, 75),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10f),
            Margin = new Padding(0, 4, 0, 4),
        };
    }

    private async void OnSelfTest(object sender, EventArgs e)
    {
        _status.Text = "Self-test running...";
        var rnd = new Random(42);

        for (int i = 0; i < 3; i++)
        {
            int x = rnd.Next(200, 900);
            int y = rnd.Next(600, 1800);
            _state.HandleTap(x, y);
            _phone.Invalidate();
            await System.Threading.Tasks.Task.Delay(400);
        }

        _state.HandleTap(MirrorState.IconLeft + 90, MirrorState.IconTop + 90);
        _phone.Invalidate();
        await System.Threading.Tasks.Task.Delay(400);

        _state.HandleType("hello from self-test");
        _phone.Invalidate();
        await System.Threading.Tasks.Task.Delay(400);

        _state.HandleButton("home");
        _phone.Invalidate();
        _status.Text = "Self-test complete.";
    }
}
