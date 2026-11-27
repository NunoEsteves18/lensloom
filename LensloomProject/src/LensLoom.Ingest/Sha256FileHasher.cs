using System.Security.Cryptography;
using LensLoom.Core.Abstractions;

namespace LensLoom.Ingest;

public sealed class Sha256FileHasher : IFileHasher
{
    public async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        byte[] hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}