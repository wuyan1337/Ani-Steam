namespace AniSteam;

public static class AppAssets
{
    public static Icon Icon { get; } = ReadIcon();
    public static Image Logo { get; } = ReadLogo();

    private static Icon ReadIcon()
    {
        using var stream = typeof(AppAssets).Assembly.GetManifestResourceStream("AniSteam.logo.ico")!;
        using var source = new Icon(stream);
        return (Icon)source.Clone();
    }

    private static Image ReadLogo()
    {
        using var stream = typeof(AppAssets).Assembly.GetManifestResourceStream("AniSteam.logo.png")!;
        using var source = Image.FromStream(stream);
        return new Bitmap(source, new Size(60, 66));
    }
}
