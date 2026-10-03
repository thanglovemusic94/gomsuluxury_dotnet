using System.Security.Cryptography;

namespace WebShop.Infrastructure;

/// <summary>SHA-256 helpers for media de-duplication.</summary>
public static class MediaHash
{
    public static async Task<string> Sha256HexAsync(Stream stream, CancellationToken ct = default)
    {
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Copy source → dest while computing SHA-256 (single pass).</summary>
    public static async Task<string> Sha256HexCopyAsync(Stream source, Stream dest, CancellationToken ct = default)
    {
        using var sha = SHA256.Create();
        var buffer = new byte[81_920];
        int read;
        while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            sha.TransformBlock(buffer, 0, read, null, 0);
            await dest.WriteAsync(buffer.AsMemory(0, read), ct);
        }

        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }
}
