namespace BitacoraTech.Contracts.Auth;

public sealed record LoginRequest(string Email, string Password);
public sealed record RegisterRequest(string TenantName, string Email, string Password);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record LogoutRequest(string RefreshToken);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    Guid UserId,
    Guid TenantId,
    string Email,
    IReadOnlyCollection<string> Roles);
