    using System.Globalization;
using LensLoom.Core.Abstractions;
using LensLoom.Core.Models;

namespace LensLoom.Catalog;

public sealed class DefaultCatalogCardGenerator : ICatalogCardGenerator
{
    public CatalogCard CreateCard(PhotoAsset asset, PaletteInfo palette, string? customTitle = null)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(palette);

        var title = !string.IsNullOrWhiteSpace(customTitle)
            ? customTitle.Trim()
            : Path.GetFileNameWithoutExtension(asset.FileName);

        var exif = asset.Exif;
        var details = new List<string>();

        if (!string.IsNullOrWhiteSpace(exif.CameraModel))
        {
            details.Add(exif.CameraModel);
        }

        if (!string.IsNullOrWhiteSpace(exif.LensModel))
        {
            details.Add(exif.LensModel);
        }

        if (exif.FocalLengthMm is not null && exif.ApertureFNumber is not null)
        {
            details.Add(string.Format(CultureInfo.InvariantCulture, "{0:0.#}mm · f/{1:0.#}", exif.FocalLengthMm, exif.ApertureFNumber));
        }
        else if (exif.FocalLengthMm is not null)
        {
            details.Add(string.Format(CultureInfo.InvariantCulture, "{0:0.#}mm", exif.FocalLengthMm));
        }

        var topHex = palette.DominantColors.FirstOrDefault()?.Hex;
        var moodPart = string.IsNullOrWhiteSpace(topHex)
            ? $"{palette.TemperatureProfile} tones"
            : $"{palette.TemperatureProfile} tones ({topHex})";

        var caption = details.Count > 0
            ? $"{title} — {string.Join(" | ", details)} · {moodPart}"
            : $"{title} — {moodPart}";

        return new CatalogCard(
            Title: title,
            FileName: asset.FileName,
            Exif: exif,
            Palette: palette,
            SuggestedCaption: caption);
    }
}