using System;
using System.Threading;
using System.Windows.Forms;

namespace WindowsResizeCapture;

// Application entry point. Enforces single-instance via a global mutex,
// then starts the tray application context that hosts the NotifyIcon.
static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    static void Main()
    {
        // Acquire a named mutex to prevent multiple instances from running.
        // If the mutex already exists, another instance is active — show a
        // message and exit immediately.
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
