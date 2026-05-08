using BitacoraTech.Domain.Common;

namespace BitacoraTech.Domain.Users;

public sealed class User : Entity
{
    private readonly List<Role> _roles = [];

    private User()
    {
        Email = string.Empty;
        PasswordHash = string.Empty;
    }

    public User(Guid id, Guid tenantId, string email, string passwordHash)
        : base(id)
    {
        TenantId = tenantId;
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
    }

    public Guid TenantId { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public IReadOnlyCollection<Role> Roles => _roles;

    public void AddRole(Role role)
    {
        if (_roles.All(existing => existing.Name != role.Name))
        {
            _roles.Add(role);
        }
    }
}

public sealed class Role : Entity
{
    private Role()
    {
        Name = string.Empty;
    }

    public Role(Guid id, string name)
        : base(id)
    {
        Name = name;
    }

    public string Name { get; private set; }
}

public sealed class RefreshToken : Entity
{
    private RefreshToken()
    {
        TokenHash = string.Empty;
    }

    public RefreshToken(Guid id, Guid userId, string tokenHash, DateTimeOffset expiresAt)
        : base(id)
    {
        UserId = userId;
        TokenHash = string.IsNullOrWhiteSpace(tokenHash) ? throw new ArgumentException("Token hash is required.", nameof(tokenHash)) : tokenHash;
        ExpiresAt = expiresAt;
    }

    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(string? replacedByTokenHash = null)
    {
        RevokedAt = DateTimeOffset.UtcNow;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
