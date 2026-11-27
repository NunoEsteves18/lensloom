using System.Globalization;
using LensLoom.Core.Abstractions;
using LensLoom.Core.Models;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace LensLoom.Ingest;

public sealed class MetadataExtractorExifReader : IExifReader
{
    public Task<CameraExifData> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var directories = ImageMetadataReader.ReadMetadata(filePath);

        var subIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        var ifd0Directory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();

        var capturedAt = ResolveCapturedAt(subIfdDirectory, ifd0Directory, filePath);
        var cameraMake = ifd0Directory?.GetDescription(ExifDirectoryBase.TagMake)?.Trim();
        var cameraModel = ifd0Directory?.GetDescription(ExifDirectoryBase.TagModel)?.Trim();
        var lensModel = subIfdDirectory?.GetDescription(ExifDirectoryBase.TagLensModel)?.Trim();

        var focalLengthMm = TryParseFocalLength(subIfdDirectory);
        var apertureFNumber = TryGetDouble(subIfdDirectory, ExifDirectoryBase.TagFNumber);
        var shutterSpeed = subIfdDirectory?.GetDescription(ExifDirectoryBase.TagExposureTime)?.Trim();
        var iso = TryGetInt32(subIfdDirectory, ExifDirectoryBase.TagIsoEquivalent);

        var data = new CameraExifData(
            CapturedAt: capturedAt,
            CameraMake: string.IsNullOrWhiteSpace(cameraMake) ? null : cameraMake,
            CameraModel: string.IsNullOrWhiteSpace(cameraModel) ? null : cameraModel,
            LensModel: string.IsNullOrWhiteSpace(lensModel) ? null : lensModel,
            FocalLengthMm: focalLengthMm,
            ApertureFNumber: apertureFNumber,
            ShutterSpeed: string.IsNullOrWhiteSpace(shutterSpeed) ? null : shutterSpeed,
            Iso: iso);

        return Task.FromResult(data);
    }

    private static double? TryGetDouble(ExifSubIfdDirectory? subIfd, int tagType)
    {
        if (subIfd is null)
        {
            return null;
        }

        if (subIfd.TryGetDouble(tagType, out var value))
        {
            return value;
        }

        var description = subIfd.GetDescription(tagType);
        if (!string.IsNullOrWhiteSpace(description))
        {
            var cleaned = description.Replace("f/", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            if (double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static int? TryGetInt32(ExifSubIfdDirectory? subIfd, int tagType)
    {
        if (subIfd is null)
        {
            return null;
        }

        if (subIfd.TryGetInt32(tagType, out var value))
        {
            return value;
        }

        var description = subIfd.GetDescription(tagType);
        if (!string.IsNullOrWhiteSpace(description) &&
            int.TryParse(description.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static DateTimeOffset? ResolveCapturedAt(
        ExifSubIfdDirectory? subIfd,
        ExifIfd0Directory? ifd0,
        string filePath)
    {
        if (subIfd is not null && subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var originalDate))
        {
            return new DateTimeOffset(DateTime.SpecifyKind(originalDate, DateTimeKind.Local));
        }

        if (ifd0 is not null && ifd0.TryGetDateTime(ExifDirectoryBase.TagDateTime, out var digitisedDate))
        {
            return new DateTimeOffset(DateTime.SpecifyKind(digitisedDate, DateTimeKind.Local));
        }

        if (File.Exists(filePath))
        {
            return new DateTimeOffset(File.GetLastWriteTime(filePath));
        }

        return null;
    }

    private static double? TryParseFocalLength(ExifSubIfdDirectory? subIfd)
    {
        if (subIfd is null)
        {
            return null;
        }

        var raw = subIfd.GetDescription(ExifDirectoryBase.TagFocalLength);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var token = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (token is not null && double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out var mm))
        {
            return mm;
        }

        return null;
    }
}