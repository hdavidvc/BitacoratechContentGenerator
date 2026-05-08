namespace BitacoraTech.IntegrationTests;

public static class IntegrationScenarioSpec
{
    public const string PrimaryFlow = "Login -> research keyword -> generate article -> analyze SEO -> preview -> approve -> publish.";
    public const string ProviderFallback = "AI provider failure should fall back before failing the use case.";
}

