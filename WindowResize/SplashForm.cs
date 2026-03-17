using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WindowResize;

public class SplashForm : Form
{
    private readonly System.Windows.Forms.Timer _timer;
    private float _opacity = 1.0f;

    public SplashForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(300, 200);
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(45, 45, 48);
        DoubleBuffered = true;

        _timer = new System.Windows.Forms.Timer { Interval = 30 };
        _timer.Tick += Timer_Tick;
    }

    public void ShowSplash(int displayMs = 1500)
    {
        Show();

        // Start fade-out after displayMs
        var delayTimer = new System.Windows.Forms.Timer { Interval = displayMs };
        delayTimer.Tick += (_, _) =>
        {
            delayTimer.Stop();
            delayTimer.Dispose();
            _timer.Start();
        };
        delayTimer.Start();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _opacity -= 0.05f;
        if (_opacity <= 0)
        {
            _timer.Stop();
            Close();
            return;
        }
        Opacity = _opacity;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        // Draw app icon from embedded resource
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("WindowResize.Resources.splash.png");
        if (stream != null)
        {
            using var icon = Image.FromStream(stream);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(icon, (Width - 64) / 2, 15, 64, 64);
        }

        // App name
        using var titleFont = new Font("Segoe UI", 18, FontStyle.Bold);
        using var titleBrush = new SolidBrush(Color.White);
        var titleRect = new RectangleF(0, 85, Width, 35);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("Window Resize", titleFont, titleBrush, titleRect, sf);

        // Version
        using var versionFont = new Font("Segoe UI", 10);
        using var versionBrush = new SolidBrush(Color.FromArgb(160, 160, 160));
        var versionRect = new RectangleF(0, 120, Width, 20);
        g.DrawString("v1.6", versionFont, versionBrush, versionRect, sf);

        // Copyright
        using var copyrightFont = new Font("Segoe UI", 8);
        using var copyrightBrush = new SolidBrush(Color.FromArgb(120, 120, 120));
        var copyrightRect = new RectangleF(0, 150, Width, 20);
        g.DrawString("\u00a9 2026 Window Resize", copyrightFont, copyrightBrush, copyrightRect, sf);

        // Border
        using var borderPen = new Pen(Color.FromArgb(80, 80, 85), 1);
        g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
        }
        base.Dispose(disposing);
    }
}
