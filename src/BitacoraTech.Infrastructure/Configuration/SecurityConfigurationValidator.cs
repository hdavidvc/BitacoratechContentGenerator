using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace BitacoraTech.Infrastructure.Configuration;

public static class SecurityConfigurationValidator
{
    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            return;
        }

        var jwtSecret = configuration["JWT_SECRET"] ?? configuration[$"{JwtOptions.SectionName}:Secret"] ?? string.Empty;
        if (IsPlaceholder(jwtSecret, "replace-with-a-secure-secret-key-at-least-32-characters"))
        {
            throw new InvalidOperationException("Set a unique JWT secret for non-development environments.");
        }

        var wordPressKey = configuration["WORDPRESS_ENCRYPTION_KEY"] ?? configuration[$"{WordPressSecurityOptions.SectionName}:EncryptionKey"] ?? string.Empty;
        if (IsPlaceholder(wordPressKey, "change-me-to-a-shared-32-byte-secret"))
        {
            throw new InvalidOperationException("Set WORDPRESS_ENCRYPTION_KEY for non-development environments.");
        }

        var adminPassword = configuration[$"{AdminSeedOptions.SectionName}:Password"] ?? string.Empty;
        if (IsPlaceholder(adminPassword, "Admin123!"))
        {
            throw new InvalidOperationException("Replace the default admin seed password for non-development environments.");
        }
    }

    private static bool IsPlaceholder(string value, string placeholder) =>
        string.IsNullOrWhiteSpace(value) || string.Equals(value.Trim(), placeholder, StringComparison.Ordinal);
}
