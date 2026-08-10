using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace WindowResizeCapture.Studio;

// The windows the studio pretends are open.
//
// A picture of the resize menu has to show a believable desktop, and the real
// enumeration shows whatever the operator happens to have running: their own
// documents, their own window titles, a different list every time. So the
// cast is written down here instead.
//
// Only the list is staged. The menu is still built by the product's own code
// from these WindowInfo values, exactly as it is built from real ones, so the
// picture shows a screen the app genuinely produces.
//
// The icons are read from the programs themselves, so the browser in the
// picture wears the browser's own icon.
internal static class StudioWindows
{
    // Five browser windows, which is past the point where the menu stops
    // listing windows flat and groups them under the application, plus one
    // window each from two more programs. That shows both halves of the menu's
    // behavior in a single picture.
    private const int BrowserWindows = 5;

    internal static List<WindowInfo> Staged(System.Globalization.CultureInfo language)
    {
        var titles = StudioScript.WindowTitles(language);
        var windows = new List<WindowInfo>();
        int handle = 1;

        // The group heading shows the program's name, so the studio gives the
        // everyday word for it rather than the executable's own name. A person
        // reading a listing knows what a browser is; msedge tells them less
        // and puts someone else's brand in our picture.
        var browser = LoadIcon(EdgePath());
        for (int index = 0; index < BrowserWindows; index++)
        {
            windows.Add(Make(handle++, 101, titles[7], titles[index], browser));
        }

        windows.Add(Make(handle++, 102, titles[8], titles[5], LoadIcon(SystemApp("notepad.exe"))));
        windows.Add(Make(handle++, 103, titles[9], titles[6], LoadIcon(Explorer())));

        return windows;
    }

    private static WindowInfo Make(
        int handle, uint process, string program, string title, Icon? icon) => new()
    {
        Handle = new IntPtr(handle),
        ProcessId = process,
        ProcessName = program,
        Title = title,

        // Somewhere on the primary display, so the menu's check against the
        // screen size behaves as it would for a real window.
        Left = 200,
        Top = 150,
        Width = 1280,
        Height = 800,
        ClientWidth = 1264,
        ClientHeight = 761,

        AppIcon = icon,
    };

    private static Icon? LoadIcon(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            return Icon.ExtractAssociatedIcon(path);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string SystemApp(string executable) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System), executable);

    // File Explorer keeps its executable in the Windows folder itself, not in
    // System32 beside the others.
    private static string Explorer() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

    private static string EdgePath()
    {
        const string key = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe";
        try
        {
            using var path = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(key);
            return path?.GetValue(null) as string ?? "";
        }
        catch (Exception)
        {
            return "";
        }
    }
}
