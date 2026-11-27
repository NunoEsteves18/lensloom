namespace LensLoom.Core.Models;

public sealed record ColorSwatch(
    string Hex,
    double Percentage);

public sealed record PaletteInfo(
    IReadOnlyList<ColorSwatch> DominantColors,
    string TemperatureProfile,
    double AverageBrightness);

public sealed record CatalogCard(
    string Title,
    string FileName,
    CameraExifData Exif,
    PaletteInfo Palette,
    string SuggestedCaption);