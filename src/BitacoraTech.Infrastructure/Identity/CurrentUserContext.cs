using System.Security.Claims;
using BitacoraTech.Application.Common;
using Microsoft.AspNetCore.Http;

namespace BitacoraTech.Infrastructure.Identity;

public sealed class CurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext, ITenantContext
{
    public Guid? UserId => ReadGuid(ClaimTypes.NameIdentifier);
    public Guid? TenantId => ReadGuid("tenant_id");
    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    Guid ITenantContext.TenantId => TenantId ?? throw new InvalidOperationException("No tenant is available in the current request.");

    private Guid? ReadGuid(string claimType)
    {
        var value = httpContextAccessor.HttpContext?.User.FindFirstValue(claimType);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}

