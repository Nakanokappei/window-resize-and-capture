using WindowResizeCapture;

namespace WindowResizeCapture.Studio;

// The settings a picture is taken with.
//
// A listing picture shows the settings window as it stands, and on a machine
// where the operator never turned capture on that window is one check box and a
// lot of empty space. What a reader should see is the feature doing something:
// every option on the tab being photographed, filled in.
//
// Nothing here is saved. The store's properties are changed in memory and the
// process ends when the picture is written, so the operator's own preferences
// are never touched - only SaveAndNotify writes the file, and no pose calls it.
// For the same reason the custom sizes are added to the list directly rather
// than through AddSize, which would save.
internal static class StudioPhotogenicSettings
{
    // A folder that reads as somebody's own without being anybody's. The path
    // is drawn in the picture, so the account name on the machine taking it
    // must not reach the listing.
    private const string CaptureFolder = @"C:\Users\Alex\Pictures\Captures";

    internal static void Apply(string view)
    {
        var store = SettingsStore.Shared;

        switch (view)
        {
            // The General tab is mostly the two size lists, and the custom one
            // is empty until somebody adds a size. Two sizes show what the list
            // is for. They carry no name, because a name shown in sixteen
            // languages would be sixteen more strings to translate for a
            // picture, and the dimensions are what the feature is about.
            case "settings-general":
                store.CustomSizes.Clear();
                store.CustomSizes.Add(new PresetSize(1512, 982));
                store.CustomSizes.Add(new PresetSize(2048, 1280));
                store.ResizeClientArea = false;
                break;

            // Every capture option on, so the tab shows the destinations rather
            // than a single unchecked line. The client-area option stays off:
            // it is the one choice that changes what the picture contains, and
            // a reader meeting the feature for the first time wants the whole
            // window.
            case "settings-capture":
                store.CaptureEnabled = true;
                store.CaptureSaveToFile = true;
                store.CaptureSaveFolderPath = CaptureFolder;
                store.CaptureCopyToClipboard = true;
                store.CaptureClientArea = false;
                break;

            // One position chosen, so a tile is lit and the grid reads as a
            // choice rather than nine identical buttons.
            case "settings-behavior":
                store.BringToFront = true;
                store.MoveToMainScreen = false;
                store.Position = WindowPosition.TopLeft;
                break;

            // choose-a-size is photographed with the settings as they are. The
            // menu it shows depends on them only through the "current size"
            // item, which the default BringToFront already brings.
        }
    }
}
