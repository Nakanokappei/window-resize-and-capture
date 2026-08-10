using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowResizeCapture.Studio;

// One run of the app produces one picture: start, put the set on screen,
// arrange the pose, photograph it, quit. Nothing is carried between pictures,
// so a run that goes wrong spoils one file rather than a whole shoot.
internal static class StudioSession
{
    // Written next to the picture so an unattended shoot leaves evidence.
    private static readonly string LogPath = Path.Combine(
        Path.GetTempPath(), "WindowResizeCapture-studio.log");

    internal static void Run(StudioRequest request)
    {
        // A copy file that was asked for and is not there would otherwise
        // produce a picture with no marketing line on it, which passes every
        // other check a shoot makes.
        if (request.SourcePath.Length > 0 && !File.Exists(request.SourcePath))
        {
            Log($"error\t{request.View}\t{request.Language.Name}\t" +
                $"no copy at {request.SourcePath}");
            return;
        }

        // The language has to be settled before any control is built, because
        // WinForms reads the culture as it creates each one.
        CultureInfo.CurrentUICulture = request.Language;
        CultureInfo.CurrentCulture = request.Language;

        // And the settings before that, because the window being photographed
        // reads them as it is built. In memory only: see StudioPhotogenicSettings.
        StudioPhotogenicSettings.Apply(request.View);

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var set = new StudioSetForm(request);
        set.Show();
        set.Activate();
        StudioCamera.Raise(set.Handle);

        // Arrange and photograph on the message loop, then close the set. The
        // work is asynchronous because the compositor needs time between the
        // resize, the pose and the shutter.
        set.BeginInvoke(new Action(async () =>
        {
            try
            {
                await Photograph(set, request);
            }
            catch (Exception failure)
            {
                Log($"error\t{request.View}\t{request.Language.Name}\t{failure.Message}");
            }
            finally
            {
                Application.Exit();
            }
        }));

        Application.Run();
    }

    private static async Task Photograph(StudioSetForm set, StudioRequest request)
    {
        Directory.CreateDirectory(
            Path.GetDirectoryName(Path.GetFullPath(request.OutputPath))!);

        // Settle once for the set itself before anything is placed on it.
        await StudioCamera.Settle(request.SettleMs);

        using var pose = await StudioPoses.Arrange(request.View, set);

        var written = await StudioCamera.Photograph(
            set, request.Size.Width, request.Size.Height, request.OutputPath, request.SettleMs);

        // An unattended shoot must shout about a wrong size here, not leave it
        // to be discovered on the store listing.
        if (written != request.Size)
        {
            Log($"error\t{request.View}\t{request.Language.Name}\t" +
                $"wanted {request.Size.Width}x{request.Size.Height}, " +
                $"wrote {written.Width}x{written.Height}");
        }
        else
        {
            Log($"ok\t{request.View}\t{request.Language.Name}\t{request.OutputPath}");
        }
    }

    private static void Log(string line)
    {
        try
        {
            File.AppendAllText(LogPath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{line}{Environment.NewLine}");
        }
        catch
        {
            // A shoot that cannot write its log still produces pictures.
        }
    }
}
