using System;
using System.Windows.Forms;

namespace WindowResizeCapture;

// Leaves when Windows asks, so that the app is not killed and reported as
// having hung.
//
// A packaged app is expected to exit on its own once the session ends or a
// package update needs it gone. Windows sends WM_QUERYENDSESSION, then
// WM_ENDSESSION, waits about thirty seconds, sends WM_CLOSE, and if the
// process is still alive terminates it and files the death with Windows Error
// Reporting as MOAPPLICATION_HANG ... HANG_QUIESCE. An unpackaged app gets the
// same messages and is then terminated quietly, which is why the plain EXE
// never showed the problem.
//
// This app has no main form, so nothing in WinForms ends its message loop when
// those messages arrive: the tray icon's window and the hidden settings window
// both hand them to DefWindowProc, and the process outlives the session until
// the system gives up on it. Every sign-out and shutdown with the app running
// therefore counted as a hang in Partner Center, and with launch at login on
// the app is always running.
//
// The listener is an invisible top-level window. It has to be top-level rather
// than message-only, because the session messages go to every top-level window
// of the process and to nothing else.
internal sealed class SessionEndWindow : NativeWindow, IDisposable
{
    private const int WM_CLOSE = 0x0010;
    private const int WM_QUERYENDSESSION = 0x0011;
    private const int WM_ENDSESSION = 0x0016;

    private readonly Action _quit;

    // Must be called on the UI thread, which then owns the window.
    public SessionEndWindow(Action quit)
    {
        _quit = quit;

        // The defaults give a top-level window with no style bits, so it is
        // never shown and never listed: DiscoverWindows drops it for being
        // invisible and again for lacking a caption.
        CreateHandle(new CreateParams());
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_QUERYENDSESSION:
                // Nothing stands in the way: settings are written as they
                // change, and a capture in flight is not worth holding a
                // shutdown for.
                m.Result = (IntPtr)1;
                return;

            case WM_ENDSESSION:
                // wParam is zero when another app vetoed the session end.
                if (m.WParam != IntPtr.Zero)
                    _quit();
                m.Result = IntPtr.Zero;
                return;

            case WM_CLOSE:
                // The follow-up Windows sends when WM_ENDSESSION was not
                // enough, and what a package update sends on its own.
                _quit();
                m.Result = IntPtr.Zero;
                return;
        }

        base.WndProc(ref m);
    }

    public void Dispose() => DestroyHandle();
}
