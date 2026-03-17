using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WindowsResizeCapture;

// A borderless, always-on-top splash screen that shows the app icon,
// name, version, and copyright. After a configurable display period
// it fades out smoothly and closes itself.
public class SplashForm : Form
{
    private readonly System.Windows.Forms.Timer _fadeTimer;
    private float _opacity = 1.0f;

    // Configure the form as a fixed-size, borderless overlay centred on screen.
    public SplashForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(380, 200);
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(45, 45, 48);
        DoubleBuffered = true;

        _fadeTimer = new System.Windows.Forms.Timer { Interval = 30 };
        _fadeTimer.Tick += OnFadeStep;
    }

    // Display the splash screen, then begin the fade-out animation
    // after the specified delay.
    public void ShowSplash(int displayMs = 1500)
    {
        Show();

        // Schedule the start of the fade-out sequence
        var delayTimer = new System.Windows.Forms.Timer { Interval = displayMs };
        delayTimer.Tick += (_, _) =>
        {
            delayTimer.Stop();
            delayTimer.Dispose();
            _fadeTimer.Start();
        };
        delayTimer.Start();
    }

    // Reduce opacity by a fixed step each tick. When fully transparent,
    // stop the timer and close the form.
    private void OnFadeStep(object? sender, EventArgs e)
    {
        _opacity -= 0.05f;

        if (_opacity <= 0)
        {
            _fadeTimer.Stop();
            Close();
            return;
        }

        Opacity = _opacity;
    }

    // Render the splash content: centred app icon, title, version, copyright,
    // and a subtle border.
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        // Draw the app icon from the embedded resource
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("WindowsResizeCapture.Resources.splash.png");
        if (stream != null)
        {
            using var icon = Image.FromStream(stream);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(icon, (Width - 64) / 2, 15, 64, 64);
        }

        // Shared format for all centred text lines
        var centred = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        // Application name
        using var titleFont = new Font("Segoe UI", 18, FontStyle.Bold);
        using var titleBrush = new SolidBrush(Color.White);
        g.DrawString("Window Resize & Capture", titleFont, titleBrush,
            new RectangleF(0, 85, Width, 35), centred);

        // Version string
        using var versionFont = new Font("Segoe UI", 10);
        using var versionBrush = new SolidBrush(Color.FromArgb(160, 160, 160));
        g.DrawString("v1.6", versionFont, versionBrush,
            new RectangleF(0, 120, Width, 20), centred);

        // Copyright notice
        using var copyrightFont = new Font("Segoe UI", 8);
        using var copyrightBrush = new SolidBrush(Color.FromArgb(120, 120, 120));
        g.DrawString("\u00a9 2026 Window Resize", copyrightFont, copyrightBrush,
            new RectangleF(0, 150, Width, 20), centred);

        // Thin border around the form edge
        using var borderPen = new Pen(Color.FromArgb(80, 80, 85), 1);
        g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _fadeTimer.Dispose();
        base.Dispose(disposing);
    }
}
