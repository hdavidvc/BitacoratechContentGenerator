namespace BitacoraTech.ArchitectureTests;

public static class CleanArchitectureSpec
{
    public const string DomainIsolation = "Domain must not reference Infrastructure, Api, Worker, Hangfire, EF Core, or ASP.NET.";
    public const string ApplicationIsolation = "Application must depend only on Domain and Contracts.";
}

