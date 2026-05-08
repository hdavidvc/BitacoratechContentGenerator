using BitacoraTech.Domain.Common;

namespace BitacoraTech.Domain.Ai;

public sealed class AiProvider : Entity
{
    private AiProvider()
    {
        Name = string.Empty;
    }

    public AiProvider(Guid id, string name, bool isEnabled, int priority)
        : base(id)
    {
        Name = name;
        IsEnabled = isEnabled;
        Priority = priority;
    }

    public string Name { get; private set; }
    public bool IsEnabled { get; private set; }
    public int Priority { get; private set; }
}

public sealed class AiUsageRecord : Entity
{
    private AiUsageRecord()
    {
        Provider = string.Empty;
        Model = string.Empty;
        FeatureArea = string.Empty;
    }

    public AiUsageRecord(Guid id, Guid tenantId, string provider, string model, int promptTokens, int completionTokens, decimal estimatedCost, string featureArea, Guid? relatedEntityId)
        : base(id)
    {
        TenantId = tenantId;
        Provider = provider;
        Model = model;
        PromptTokens = Math.Max(0, promptTokens);
        CompletionTokens = Math.Max(0, completionTokens);
        EstimatedCost = Math.Max(0, estimatedCost);
        FeatureArea = featureArea;
        RelatedEntityId = relatedEntityId;
    }

    public Guid TenantId { get; private set; }
    public string Provider { get; private set; }
    public string Model { get; private set; }
    public int PromptTokens { get; private set; }
    public int CompletionTokens { get; private set; }
    public decimal EstimatedCost { get; private set; }
    public string FeatureArea { get; private set; }
    public Guid? RelatedEntityId { get; private set; }
}

public sealed class PromptTemplate : Entity
{
    private PromptTemplate()
    {
        Key = string.Empty;
        Template = string.Empty;
    }

    public PromptTemplate(Guid id, Guid tenantId, string key, string template)
        : base(id)
    {
        TenantId = tenantId;
        Key = key;
        Template = template;
    }

    public Guid TenantId { get; private set; }
    public string Key { get; private set; }
    public string Template { get; private set; }
}
