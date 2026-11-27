using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using LensLoom.Core.Abstractions;
using LensLoom.Core.Models;

namespace LensLoom.Ingest;

public sealed class PhotoFolderOrganizer : IPhotoOrganizer
{
    private static readonly HashSet<string> RawExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cr2", ".cr3", ".nef", ".nrw", ".arw", ".srf", ".sr2",
        ".orf", ".rw2", ".raf", ".dng", ".pef", ".srw"
    };

    private static readonly HashSet<string> JpegExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg"
    };

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _destinationLocks = new(StringComparer.OrdinalIgnoreCase);

    public static PhotoFormatKind DetectFormat(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        if (RawExtensions.Contains(ext))
        {
            return PhotoFormatKind.Raw;
        }

        if (JpegExtensions.Contains(ext))
        {
            return PhotoFormatKind.Jpeg;
        }

        return PhotoFormatKind.Other;
    }

    public async Task<OrganizedPhotoResult> OrganizeAsync(
        PhotoAsset asset,
        string destinationRoot,
        string? sessionName = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(destinationRoot);
        var gate = _destinationLocks.GetOrAdd(destinationRoot, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken);
        try
        {
            var indexPath = Path.Combine(destinationRoot, ".lensloom-hashes.txt");
            var knownHashes = await LoadHashesAsync(indexPath, cancellationToken);

            var targetDirectory = BuildTargetDirectory(asset, destinationRoot, sessionName);
            Directory.CreateDirectory(targetDirectory);

            var targetPath = Path.Combine(targetDirectory, asset.FileName);

            if (knownHashes.Contains(asset.Sha256) || File.Exists(targetPath))
            {
                return new OrganizedPhotoResult(
                    Asset: asset,
                    DestinationPath: targetPath,
                    WasSkippedAsDuplicate: true);
            }

            await CopyFileAsync(asset.SourcePath, targetPath, cancellationToken);
            await AppendHashAsync(indexPath, asset.Sha256, cancellationToken);

            return new OrganizedPhotoResult(
                Asset: asset,
                DestinationPath: targetPath,
                WasSkippedAsDuplicate: false);
        }
        finally
        {
            gate.Release();
        }
    }

    private static string BuildTargetDirectory(PhotoAsset asset, string destinationRoot, string? sessionName)
    {
        var date = asset.Exif.CapturedAt?.ToLocalTime().DateTime
                   ?? File.GetLastWriteTime(asset.SourcePath);

        var year = date.ToString("yyyy", CultureInfo.InvariantCulture);
        var month = date.ToString("MM", CultureInfo.InvariantCulture);

        var formatFolder = asset.FormatKind switch
        {
            PhotoFormatKind.Raw => "RAW",
            PhotoFormatKind.Jpeg => "JPEG",
            _ => "OTHER"
        };

        var safeSession = SanitizeFolderName(sessionName);

        return string.IsNullOrWhiteSpace(safeSession)
            ? Path.Combine(destinationRoot, year, month, formatFolder)
            : Path.Combine(destinationRoot, year, month, safeSession, formatFolder);
    }

    private static string? SanitizeFolderName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var cleaned = Regex.Replace(name.Trim(), @"[^\p{L}\p{N}\-_ ]+", "-");
        cleaned = Regex.Replace(cleaned, @"\s+", "-");
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private static async Task<HashSet<string>> LoadHashesAsync(string indexPath, CancellationToken cancellationToken)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(indexPath))
        {
            return set;
        }

        var lines = await File.ReadAllLinesAsync(indexPath, cancellationToken);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                set.Add(trimmed);
            }
        }

        return set;
    }

    private static async Task AppendHashAsync(string indexPath, string sha256, CancellationToken cancellationToken)
    {
        await File.AppendAllTextAsync(indexPath, sha256 + Environment.NewLine, cancellationToken);
    }

    private static async Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        await using var sourceStream = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        await using var destStream = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await sourceStream.CopyToAsync(destStream, cancellationToken);
    }
}