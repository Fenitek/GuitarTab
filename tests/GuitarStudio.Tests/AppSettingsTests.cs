using System.Text.Json;

namespace GuitarStudio.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void PreviousSettingsKeepWidthsAndReceiveDefaultPanelHeights()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("""{"PaletteWidth":226,"InspectorWidth":290,"WindowWidth":1500}""")!;
        Assert.Equal(226, settings.PaletteWidth);
        Assert.Equal(290, settings.InspectorWidth);
        Assert.Equal(162, settings.FretboardHeight);
        Assert.Equal(34, settings.TracksHeight);
    }

    [Fact]
    public void PanelHeightsRoundTripAlongsideExistingPreferences()
    {
        var settings = new AppSettings { FretboardHeight = 214, TracksHeight = 102, SoundFontPath = "suoni.sf2", RecentFiles = ["prova.gp"] };
        var restored = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
        Assert.Equal(214, restored.FretboardHeight);
        Assert.Equal(102, restored.TracksHeight);
        Assert.Equal("suoni.sf2", restored.SoundFontPath);
        Assert.Equal(["prova.gp"], restored.RecentFiles);
    }
}
