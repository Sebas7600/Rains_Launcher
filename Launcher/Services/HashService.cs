using System.Security.Cryptography;

namespace Launcher.Services;

public static class HashService
{
    public static async Task<string> Sha256Async(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        await using FileStream stream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 64,
            useAsync: true);

        using SHA256 sha = SHA256.Create();

        byte[] hash = await sha.ComputeHashAsync(
            stream,
            cancellationToken);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
