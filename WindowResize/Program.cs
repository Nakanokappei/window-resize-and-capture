using System;
using System.Globalization;
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
#if DEBUG
        // The studio that takes the store pictures runs the app once per
        // picture, usually while the developer's own copy is already in the
        // tray, so it never takes part in the single-instance handshake.
        //
        // Debug only. What ships has no studio in it at all - the project file
        // leaves its files out of a build that is not Debug - so the pictures
        // are taken from a Debug build, which is what store-shots builds.
        if (Studio.StudioCommandLine.IsListRequest(args))
        {
            // Aware before the screen is measured for the list, or the size it
            // reports is the one Windows makes up for a process that does not
            // know about scaling - which is not the size a picture is copied at.
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
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
#endif

        // A language named on the command line applies to this launch only.
        // Nothing is stored: the app otherwise follows the language Windows is
        // set to, and remembering an override would leave the language decided
        // in two places with nothing in the UI to show which one won.
        //
        // Settled here, before the first string is read, because both the
        // message below and every control the tray builds take the culture as
        // they are created.
        var language = CommandLine.Language(args);
        if (language != null)
        {
            CultureInfo.CurrentUICulture = language;
            CultureInfo.CurrentCulture = language;
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
                App.Name,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1,
                App.MessageReading);
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
