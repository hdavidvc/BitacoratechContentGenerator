using BitacoraTech.Domain.Common;

namespace BitacoraTech.Domain.Tenancy;

public sealed class Tenant : Entity
{
    private Tenant()
    {
        Name = string.Empty;
    }

    public Tenant(Guid id, string name)
        : base(id)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Tenant name is required.", nameof(name)) : name.Trim();
    }

    public string Name { get; private set; }
}
