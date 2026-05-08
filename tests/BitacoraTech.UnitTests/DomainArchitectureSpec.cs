namespace BitacoraTech.UnitTests;

public static class DomainArchitectureSpec
{
    public const string HumanApprovalBeforePublishing = "Publishing is allowed only from Approved status with approved user and timestamp.";
    public const string SeoScoreRange = "SEO score must be clamped between 0 and 100.";
}

