using LensLoom.Core.Models;

namespace LensLoom.Core.Abstractions;

public interface IPaletteAnalyzer
{
    Task<PaletteInfo> AnalyzeAsync(string imagePath, int maxColors = 5, CancellationToken cancellationToken = default);
}

public interface ICatalogCardGenerator
{
    CatalogCard CreateCard(PhotoAsset asset, PaletteInfo palette, string? customTitle = null);
}

public interface ICatalogExporter
{
    Task ExportAsync(
        IReadOnlyList<CatalogCard> cards,
        string outputDirectory,
        CancellationToken cancellationToken = default);
}