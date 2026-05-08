using System.Security.Cryptography;
using System.Text;
using BitacoraTech.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace BitacoraTech.Infrastructure.Security;

public sealed class AesSecretProtector(IOptions<WordPressSecurityOptions> options) : ISecretProtector
{
    private const string Prefix = "v1:";

    public string Protect(string plaintext)
    {
        if (string.IsNullOrWhiteSpace(plaintext))
        {
            throw new InvalidOperationException("A secret value is required.");
        }

        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(GetKeyBytes(), 16);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var payload = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, payload, nonce.Length + tag.Length, ciphertext.Length);
        return $"{Prefix}{Convert.ToBase64String(payload)}";
    }

    public string Unprotect(string protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            throw new InvalidOperationException("A protected secret value is required.");
        }

        if (!protectedValue.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(protectedValue));
        }

        var payload = Convert.FromBase64String(protectedValue[Prefix.Length..]);
        if (payload.Length < 28)
        {
            throw new InvalidOperationException("Protected secret payload is invalid.");
        }

        var nonce = payload[..12];
        var tag = payload[12..28];
        var ciphertext = payload[28..];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(GetKeyBytes(), 16);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }

    private byte[] GetKeyBytes()
    {
        var configuredKey = options.Value.EncryptionKey?.Trim();
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            throw new InvalidOperationException("WordPressSecurity:EncryptionKey or WORDPRESS_ENCRYPTION_KEY is required.");
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));
    }
}
