namespace BitacoraTech.Infrastructure.Configuration;

public sealed class AdminSeedOptions
{
    public const string SectionName = "AdminSeed";

    public string TenantName { get; init; } = "BitacoraTech";
    public string Email { get; init; } = "admin@bitacoratech.local";
    public string Password { get; init; } = "Admin123!";
}

