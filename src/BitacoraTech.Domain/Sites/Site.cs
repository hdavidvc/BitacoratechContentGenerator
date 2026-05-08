using BitacoraTech.Domain.Common;

namespace BitacoraTech.Domain.Sites;

public sealed class Site : Entity
{
    private Site()
    {
        Name = string.Empty;
        BaseUrl = new Uri("https://example.com");
    }

    public Site(Guid id, Guid tenantId, string name, Uri baseUrl)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        BaseUrl = baseUrl;
    }

    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public Uri BaseUrl { get; private set; }
    public WordPressConnection? WordPressConnection { get; private set; }

    public void ConfigureWordPress(string username, string applicationPassword)
    {
        WordPressConnection = new WordPressConnection(Guid.NewGuid(), TenantId, Id, username, applicationPassword);
    }
}

public sealed class WordPressConnection : Entity
{
    private WordPressConnection()
    {
        Username = string.Empty;
        EncryptedApplicationPassword = string.Empty;
    }

    public WordPressConnection(Guid id, Guid tenantId, Guid siteId, string username, string encryptedApplicationPassword)
        : base(id)
    {
        TenantId = tenantId;
        SiteId = siteId;
        Username = username;
        EncryptedApplicationPassword = encryptedApplicationPassword;
    }

    public Guid TenantId { get; private set; }
    public Guid SiteId { get; private set; }
    public string Username { get; private set; }
    public string EncryptedApplicationPassword { get; private set; }
}
