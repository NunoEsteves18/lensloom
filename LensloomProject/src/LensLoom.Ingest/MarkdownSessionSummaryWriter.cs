using System.Text;
using LensLoom.Core.Abstractions;
using LensLoom.Core.Models;

namespace LensLoom.Ingest;

public sealed class MarkdownSessionSummaryWriter : ISessionSummaryWriter
{
    public async Task WriteSummaryAsync(
        string sessionDirectory,
        string sessionName,
        IReadOnlyList<OrganizedPhotoResult> results,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(sessionDirectory);
        var summaryPath = Path.Combine(sessionDirectory, "session-summary.md");

        var imported = results.Where(r => !r.WasSkippedAsDuplicate).ToList();
        var skipped = results.Where(r => r.WasSkippedAsDuplicate).ToList();

        var rawCount = imported.Count(r => r.Asset.FormatKind == PhotoFormatKind.Raw);
        var jpegCount = imported.Count(r => r.Asset.FormatKind == PhotoFormatKind.Jpeg);

        var lenses = imported
            .Select(r => r.Asset.Exif.LensModel)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .GroupBy(l => l!)
            .OrderByDescending(g => g.Count())
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"# Session Summary — {sessionName}");
        sb.AppendLine();
        sb.AppendLine($"- **Generated at:** {DateTimeOffset.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"- **Imported:** {imported.Count} (RAW: {rawCount}, JPEG: {jpegCount})");
        sb.AppendLine($"- **Skipped (duplicates):** {skipped.Count}");
        sb.AppendLine();

        if (lenses.Count > 0)
        {
            sb.AppendLine("## Lenses Used");
            foreach (var lensGroup in lenses)
            {
                sb.AppendLine($"- {lensGroup.Key}: {lensGroup.Count()}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## Imported Files");
        foreach (var item in imported.OrderBy(r => r.Asset.FileName, StringComparer.OrdinalIgnoreCase))
        {
            var exif = item.Asset.Exif;
            var metaParts = new List<string>();

            if (!string.IsNullOrWhiteSpace(exif.CameraModel))
            {
                metaParts.Add(exif.CameraModel);
            }
            if (!string.IsNullOrWhiteSpace(exif.LensModel))
            {
                metaParts.Add(exif.LensModel);
            }
            if (exif.FocalLengthMm is not null)
            {
                metaParts.Add($"{exif.FocalLengthMm:0.#}mm");
            }
            if (exif.ApertureFNumber is not null)
            {
                metaParts.Add($"f/{exif.ApertureFNumber:0.#}");
            }
            if (!string.IsNullOrWhiteSpace(exif.ShutterSpeed))
            {
                metaParts.Add(exif.ShutterSpeed);
            }
            if (exif.Iso is not null)
            {
                metaParts.Add($"ISO {exif.Iso}");
            }

            var metaText = metaParts.Count > 0 ? $" — {string.Join(" · ", metaParts)}" : string.Empty;
            sb.AppendLine($"- `{item.Asset.FileName}` -> `{item.DestinationPath}`{metaText}");
        }

        await File.WriteAllTextAsync(summaryPath, sb.ToString(), Encoding.UTF8, cancellationToken);
    }
}