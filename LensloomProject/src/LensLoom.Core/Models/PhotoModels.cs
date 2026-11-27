namespace LensLoom.Core.Models;

public enum PhotoFormatKind
{
    Raw,
    Jpeg,
    Other
}

public sealed record CameraExifData(
    DateTimeOffset? CapturedAt,
    string? CameraMake,
    string? CameraModel,
    string? LensModel,
    double? FocalLengthMm,
    double? ApertureFNumber,
    string? ShutterSpeed,
    int? Iso);

public sealed record PhotoAsset(
    string SourcePath,
    string FileName,
    string Sha256,
    long FileSizeBytes,
    PhotoFormatKind FormatKind,
    CameraExifData Exif);

public sealed record OrganizedPhotoResult(
    PhotoAsset Asset,
    string DestinationPath,
    bool WasSkippedAsDuplicate);