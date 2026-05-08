using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BitacoraTech.Application.Common;
using BitacoraTech.Contracts.Auth;
using BitacoraTech.Domain.Tenancy;
using BitacoraTech.Domain.Users;
using BitacoraTech.Infrastructure.Configuration;
using BitacoraTech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BitacoraTech.Infrastructure.Identity;

public sealed class JwtAuthService(
    IUserRepository users,
    IRoleRepository roles,
    IUnitOfWork unitOfWork,
    BitacoraTechDbContext dbContext,
    IPasswordHasher passwordHasher,
    IOptions<JwtOptions> jwtOptions) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TenantName))
        {
            throw new InvalidOperationException("Tenant name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new InvalidOperationException("Email and password are required.");
        }

        if (await users.ExistsByEmailAsync(request.Email, cancellationToken))
        {
            throw new InvalidOperationException("A user with this email already exists.");
        }

        var tenant = new Tenant(Guid.NewGuid(), request.TenantName);
        await dbContext.Tenants.AddAsync(tenant, cancellationToken);

        var adminRole = await roles.GetOrCreateAsync(AuthRoles.Admin, cancellationToken);
        var user = new User(Guid.NewGuid(), tenant.Id, request.Email, passwordHasher.Hash(request.Password));
        user.AddRole(adminRole);

        await users.AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await users.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(request.RefreshToken);
        var storedToken = await dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (storedToken is null || !storedToken.IsActive(DateTimeOffset.UtcNow))
        {
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        var user = await dbContext.Users.Include(x => x.Roles).FirstOrDefaultAsync(x => x.Id == storedToken.UserId, cancellationToken);
        if (user is null)
        {
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        var refreshToken = CreateRefreshToken();
        storedToken.Revoke(HashToken(refreshToken.Token));
        await dbContext.RefreshTokens.AddAsync(new RefreshToken(Guid.NewGuid(), user.Id, refreshToken.Hash, refreshToken.ExpiresAt), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return CreateAuthResponse(user, refreshToken.Token, refreshToken.ExpiresAt);
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(request.RefreshToken);
        var storedToken = await dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (storedToken is not null && storedToken.RevokedAt is null)
        {
            storedToken.Revoke();
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(User user, CancellationToken cancellationToken)
    {
        var refreshToken = CreateRefreshToken();
        await dbContext.RefreshTokens.AddAsync(new RefreshToken(Guid.NewGuid(), user.Id, refreshToken.Hash, refreshToken.ExpiresAt), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return CreateAuthResponse(user, refreshToken.Token, refreshToken.ExpiresAt);
    }

    private AuthResponse CreateAuthResponse(User user, string refreshToken, DateTimeOffset refreshTokenExpiresAt)
    {
        var options = jwtOptions.Value;
        if (Encoding.UTF8.GetByteCount(options.Secret) < 32)
        {
            throw new InvalidOperationException("JWT secret must be at least 32 bytes long.");
        }

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(options.ExpirationMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("tenant_id", user.TenantId.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email)
        };

        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role.Name)));

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        return new AuthResponse(
            accessToken,
            refreshToken,
            expiresAt,
            refreshTokenExpiresAt,
            user.Id,
            user.TenantId,
            user.Email,
            user.Roles.Select(role => role.Name).OrderBy(role => role).ToArray());
    }

    private RefreshTokenValue CreateRefreshToken()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return new RefreshTokenValue(token, HashToken(token), DateTimeOffset.UtcNow.AddDays(jwtOptions.Value.RefreshTokenExpirationDays));
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private sealed record RefreshTokenValue(string Token, string Hash, DateTimeOffset ExpiresAt);
}

public static class AuthRoles
{
    public const string Admin = "Admin";
    public const string Editor = "Editor";
    public const string Viewer = "Viewer";
}
