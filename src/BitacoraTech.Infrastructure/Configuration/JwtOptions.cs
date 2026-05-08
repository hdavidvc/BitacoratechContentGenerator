namespace BitacoraTech.Infrastructure.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "BitacoraTech";
    public string Audience { get; set; } = "BitacoraTech";
    public string Secret { get; set; } = "replace-with-a-secure-secret-key-at-least-32-characters";
    public int ExpirationMinutes { get; set; } = 120;
    public int RefreshTokenExpirationDays { get; set; } = 14;
}
