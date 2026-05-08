namespace BitacoraTech.Infrastructure.Configuration;

public sealed class WordPressSecurityOptions
{
    public const string SectionName = "WordPressSecurity";
    public string EncryptionKey { get; set; } = "change-me-to-a-shared-32-byte-secret";
}
