using System;
using System.Threading;
using System.Windows.Forms;

namespace WindowResizeCapture;

// Application entry point. Enforces single-instance via a global mutex,
// then starts the tray application context that hosts the NotifyIcon.
static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    static void Main(string[] args)
    {
        // The studio that takes the store pictures runs the app once per
        // picture, usually while the developer's own copy is already in the
        // tray, so it never takes part in the single-instance handshake.
        if (Studio.StudioCommandLine.IsListRequest(args))
        {
            Studio.StudioCommandLine.PrintViews();
            return;
        }

        if (Studio.StudioCommandLine.TryParse(args, out var studioRequest))
        {
            // The studio, unlike the product, is per-monitor DPI aware. Under
            // the product's DpiUnawareGdiScaled the set could never fill its
            // own window: custom drawing is not scaled up, and the clip stays
            // at the logical client size, so the picture came out a quarter
            // painted. Being aware puts drawing, the window and the screen
            // copy in the same physical pixels.
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Studio.StudioSession.Run(studioRequest);
            return;
        }

        // Acquire a named mutex to prevent multiple instances from running.
        // If the mutex already exists, another instance is active — show a
        // message and exit immediately. The name keeps the misspelled
        // "Windows" of the original executable so that a running older
        // version still blocks a second instance during an upgrade.
        const string mutexName = "Global\\WindowsResizeCapture_SingleInstance_F7A3B2";
        _mutex = new Mutex(true, mutexName, out bool isFirstInstance);

        if (!isFirstInstance)
        {
            MessageBox.Show(
                Strings.AlreadyRunningBody,
                "Window Resize & Capture",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        // Run the tray application, releasing the mutex on exit regardless
        // of whether the app exits normally or via an unhandled exception.
        try
        {
            // Let Windows scale the whole window at high display scaling
            // (the fixed pixel layout stays intact), but render GDI text
            // sharply instead of bitmap-stretching it. SystemAware was
            // tried and left the window unscaled (tiny) at 200%.
            Application.SetHighDpiMode(HighDpiMode.DpiUnawareGdiScaled);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayApplicationContext());
        }
        finally
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }
}
