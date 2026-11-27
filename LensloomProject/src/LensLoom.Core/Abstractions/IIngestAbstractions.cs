using LensLoom.Core.Models;

namespace LensLoom.Core.Abstractions;

public interface IExifReader
{
    Task<CameraExifData> ReadAsync(string filePath, CancellationToken cancellationToken = default);
}

public interface IFileHasher
{
    Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken = default);
}

public interface IPhotoOrganizer
{
    Task<OrganizedPhotoResult> OrganizeAsync(
        PhotoAsset asset,
        string destinationRoot,
        string? sessionName = null,
        CancellationToken cancellationToken = default);
}

public interface ISessionSummaryWriter
{
    Task WriteSummaryAsync(
        string sessionDirectory,
        string sessionName,
        IReadOnlyList<OrganizedPhotoResult> results,
        CancellationToken cancellationToken = default);
}