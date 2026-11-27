using System.Globalization;
using System.Text;
using System.Text.Json;
using LensLoom.Core.Abstractions;
using LensLoom.Core.Models;

namespace LensLoom.Catalog;

public sealed class CatalogFileExporter : ICatalogExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task ExportAsync(
        IReadOnlyList<CatalogCard> cards,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cards);
        Directory.CreateDirectory(outputDirectory);

        var jsonPath = Path.Combine(outputDirectory, "catalog.json");
        var markdownPath = Path.Combine(outputDirectory, "catalog.md");

        var jsonPayload = JsonSerializer.Serialize(cards, JsonOptions);
        await File.WriteAllTextAsync(jsonPath, jsonPayload, Encoding.UTF8, cancellationToken);

        var markdownPayload = BuildMarkdown(cards);
        await File.WriteAllTextAsync(markdownPath, markdownPayload, Encoding.UTF8, cancellationToken);
    }

    private static string BuildMarkdown(IReadOnlyList<CatalogCard> cards)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# LensLoom Catalog");
        sb.AppendLine();
        sb.AppendLine($"- **Generated at:** {DateTimeOffset.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"- **Total items:** {cards.Count}");
        sb.AppendLine();

        foreach (var card in cards)
        {
            sb.AppendLine($"## {card.Title}");
            sb.AppendLine();
            sb.AppendLine($"- **File:** `{card.FileName}`");
            sb.AppendLine($"- **Temperature:** {card.Palette.TemperatureProfile}");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **Average brightness:** {0:0.###}", card.Palette.AverageBrightness));
            sb.AppendLine($"- **Suggested caption:** {card.SuggestedCaption}");
            sb.AppendLine();

            if (card.Palette.DominantColors.Count > 0)
            {
                sb.AppendLine("| Color | Hex | Share |");
                sb.AppendLine("| --- | --- | --- |");
                foreach (var swatch in card.Palette.DominantColors)
                {
                    sb.AppendLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "| ![{0}](https://dummyimage.com/24x24/{1}/{1}.png) | `{0}` | {2:0.##}% |",
                        swatch.Hex,
                        swatch.Hex.TrimStart('#'),
                        swatch.Percentage));
                }
                sb.AppendLine();
            }

            AppendExifSection(sb, card.Exif);
        }

        return sb.ToString();
    }

    private static void AppendExifSection(StringBuilder sb, CameraExifData exif)
    {
        var items = new List<string>();

        if (exif.CapturedAt is not null)
        {
            items.Add($"Date: {exif.CapturedAt:yyyy-MM-dd HH:mm}");
        }
        if (!string.IsNullOrWhiteSpace(exif.CameraMake) || !string.IsNullOrWhiteSpace(exif.CameraModel))
        {
            items.Add($"Camera: {$"{exif.CameraMake} {exif.CameraModel}".Trim()}");
        }
        if (!string.IsNullOrWhiteSpace(exif.LensModel))
        {
            items.Add($"Lens: {exif.LensModel}");
        }
        if (exif.FocalLengthMm is not null)
        {
            items.Add(string.Format(CultureInfo.InvariantCulture, "Focal: {0:0.#}mm", exif.FocalLengthMm));
        }
        if (exif.ApertureFNumber is not null)
        {
            items.Add(string.Format(CultureInfo.InvariantCulture, "Aperture: f/{0:0.#}", exif.ApertureFNumber));
        }
        if (!string.IsNullOrWhiteSpace(exif.ShutterSpeed))
        {
            items.Add($"Shutter: {exif.ShutterSpeed}");
        }
        if (exif.Iso is not null)
        {
            items.Add($"ISO: {exif.Iso}");
        }

        if (items.Count > 0)
        {
            sb.AppendLine("**EXIF:** " + string.Join(" · ", items));
            sb.AppendLine();
        }
    }
}