namespace BitacoraTech.Contracts.Sites;

public sealed record SiteResponse(Guid Id, Guid TenantId, string Name, string BaseUrl, bool HasWordPressConnection);
public sealed record CreateSiteRequest(Guid TenantId, string Name, string BaseUrl);
public sealed record UpdateWordPressSettingsRequest(string Username, string ApplicationPassword);

