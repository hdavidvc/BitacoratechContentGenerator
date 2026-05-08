using BitacoraTech.Application.Common;
using BitacoraTech.Infrastructure.AiProviders;
using BitacoraTech.Infrastructure.Configuration;
using BitacoraTech.Infrastructure.Hangfire;
using BitacoraTech.Infrastructure.Identity;
using BitacoraTech.Infrastructure.KeywordResearch;
using BitacoraTech.Infrastructure.MediaStorage;
using BitacoraTech.Infrastructure.Persistence;
using BitacoraTech.Infrastructure.Security;
using BitacoraTech.Infrastructure.Services;
using BitacoraTech.Infrastructure.WordPress;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BitacoraTech.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBitacoraTechInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<JwtOptions>(options =>
        {
            var secret = configuration["JWT_SECRET"];
            if (!string.IsNullOrWhiteSpace(secret))
            {
                options.Secret = secret;
            }
        });
        services.Configure<AdminSeedOptions>(configuration.GetSection(AdminSeedOptions.SectionName));
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.Configure<KeywordResearchOptions>(configuration.GetSection(KeywordResearchOptions.SectionName));
        services.Configure<AiOptions>(options =>
        {
            ApplyApiKey(options, "Gemini", configuration["GEMINI_API_KEY"]);
            ApplyApiKey(options, "OpenAI", configuration["OPENAI_API_KEY"]);
            ApplyApiKey(options, "Claude", configuration["CLAUDE_API_KEY"]);
        });

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["CONNECTION_STRING"]
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' or CONNECTION_STRING is required.");

        services.AddDbContext<BitacoraTechDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(BitacoraTechDbContext).Assembly.FullName)));

        services.AddHttpContextAccessor();
        services.AddHttpClient();
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();
        services.AddScoped<ITenantContext, CurrentUserContext>();
        services.AddScoped<IArticleRepository, EfArticleRepository>();
        services.AddScoped<IKeywordRepository, EfKeywordRepository>();
        services.AddScoped<IKeywordResearchRunRepository, EfKeywordResearchRunRepository>();
        services.AddScoped<ISiteRepository, EfSiteRepository>();
        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<IRoleRepository, EfRoleRepository>();
        services.AddScoped<IAiUsageRepository, EfAiUsageRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IAuthService, JwtAuthService>();
        services.AddScoped<IKeywordResearchService, KeywordResearchService>();
        services.AddScoped<IKeywordResearchRunner, KeywordResearchRunner>();
        services.AddHttpClient<GoogleTrendsSource>();
        services.AddHttpClient<RedditTrendSource>();
        services.AddHttpClient<RssTrendSource>();
        services.AddScoped<IKeywordTrendSource>(provider => provider.GetRequiredService<GoogleTrendsSource>());
        services.AddScoped<IKeywordTrendSource>(provider => provider.GetRequiredService<RedditTrendSource>());
        services.AddScoped<IKeywordTrendSource>(provider => provider.GetRequiredService<RssTrendSource>());
        services.AddScoped<IArticleGenerationService, ArticleGenerationService>();
        services.AddScoped<ISeoAnalysisService, SeoAnalysisService>();
        services.AddScoped<IArticleWorkflowService, ArticleWorkflowService>();
        services.AddScoped<ISiteService, SiteService>();
        services.AddScoped<IAiCostTracker, SqlAiCostTracker>();
        services.AddScoped<IAiProvider, GeminiProvider>();
        services.AddScoped<IAiProvider, OpenAiProvider>();
        services.AddScoped<IAiProvider, ClaudeProvider>();
        services.AddScoped<IAiGateway, FallbackAiGateway>();
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
        services.AddSingleton<IJobScheduler, HangfireSqlJobScheduler>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IPromptTemplateService, PromptTemplateService>();
        services.AddSingleton<IWordPressPublishingService, WordPressRestPublishingService>();
        services.AddSingleton<IMediaService, LocalMediaService>();
        return services;
    }

    private static void ApplyApiKey(AiOptions options, string providerName, string? apiKey)
    {
        if (!string.IsNullOrWhiteSpace(apiKey) && options.Providers.TryGetValue(providerName, out var provider))
        {
            provider.ApiKey = apiKey;
        }
    }
}
