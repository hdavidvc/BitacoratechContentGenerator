using System.Text;
using BitacoraTech.Api.Middleware;
using BitacoraTech.Application.Common;
using BitacoraTech.Contracts.Articles;
using BitacoraTech.Contracts.Auth;
using BitacoraTech.Contracts.Keywords;
using BitacoraTech.Contracts.Seo;
using BitacoraTech.Contracts.Sites;
using BitacoraTech.Infrastructure;
using BitacoraTech.Infrastructure.Configuration;
using BitacoraTech.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    loggerConfiguration.ReadFrom.Configuration(context.Configuration).ReadFrom.Services(services));

builder.Services.AddBitacoraTechInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "BitacoraTech API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var jwtSecret = builder.Configuration["JWT_SECRET"];
if (!string.IsNullOrWhiteSpace(jwtSecret))
{
    jwtOptions.Secret = jwtSecret;
}

if (Encoding.UTF8.GetByteCount(jwtOptions.Secret) < 32)
{
    throw new InvalidOperationException("Jwt:Secret or JWT_SECRET must be at least 32 bytes long.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Editors", policy => policy.RequireRole(AuthRoles.Admin, AuthRoles.Editor));
    options.AddPolicy("Admins", policy => policy.RequireRole(AuthRoles.Admin));
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "BitacoraTech API v1"));
}

if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
    await initializer.InitializeAsync(app.Lifetime.ApplicationStopping);
}

app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api/v1").RequireAuthorization();

api.MapPost("/auth/login", (LoginRequest request, IAuthService auth, CancellationToken cancellationToken) =>
    auth.LoginAsync(request, cancellationToken)).AllowAnonymous();

api.MapPost("/auth/register", (RegisterRequest request, IAuthService auth, CancellationToken cancellationToken) =>
    auth.RegisterAsync(request, cancellationToken)).AllowAnonymous();

api.MapPost("/auth/refresh", (RefreshTokenRequest request, IAuthService auth, CancellationToken cancellationToken) =>
    auth.RefreshAsync(request, cancellationToken)).AllowAnonymous();

api.MapPost("/auth/logout", (LogoutRequest request, IAuthService auth, CancellationToken cancellationToken) =>
    auth.LogoutAsync(request, cancellationToken));

api.MapGet("/articles", (IArticleGenerationService articles, CancellationToken cancellationToken) =>
    articles.GetArticlesAsync(cancellationToken));

api.MapGet("/articles/{id:guid}", async (Guid id, IArticleGenerationService articles, CancellationToken cancellationToken) =>
{
    var article = await articles.GetArticleAsync(id, cancellationToken);
    return article is null ? Results.NotFound() : Results.Ok(article);
});

api.MapPost("/articles/generate", (GenerateArticleRequest request, IArticleGenerationService articles, CancellationToken cancellationToken) =>
    articles.GenerateAsync(request, cancellationToken)).RequireAuthorization("Editors");

api.MapPost("/articles/{id:guid}/seo/analyze", (Guid id, ISeoAnalysisService seo, CancellationToken cancellationToken) =>
    seo.AnalyzeArticleAsync(id, cancellationToken));

api.MapPost("/articles/{id:guid}/approve", (Guid id, ApproveArticleRequest request, IArticleWorkflowService workflow, CancellationToken cancellationToken) =>
    workflow.ApproveAsync(id, request.ApprovedByUserId, cancellationToken)).RequireAuthorization("Editors");

api.MapPost("/articles/{id:guid}/publish", (Guid id, PublishArticleRequest request, IArticleWorkflowService workflow, CancellationToken cancellationToken) =>
    workflow.PublishAsync(id, request.SiteId, cancellationToken)).RequireAuthorization("Editors");

api.MapGet("/keywords", (IKeywordResearchService keywords, CancellationToken cancellationToken) =>
    keywords.GetKeywordsAsync(cancellationToken));

api.MapGet("/keywords/clusters", (IKeywordResearchService keywords, CancellationToken cancellationToken) =>
    keywords.GetClustersAsync(cancellationToken));

api.MapPost("/keywords/research", (ResearchKeywordRequest request, IKeywordResearchService keywords, CancellationToken cancellationToken) =>
    keywords.ResearchAsync(request, cancellationToken)).RequireAuthorization("Editors");

api.MapPost("/seo/analyze", (SeoAnalyzeRequest request, ISeoAnalysisService seo, CancellationToken cancellationToken) =>
    seo.AnalyzeAsync(request, cancellationToken));

api.MapGet("/sites", (ISiteService sites, CancellationToken cancellationToken) =>
    sites.GetSitesAsync(cancellationToken));

api.MapPost("/sites", (CreateSiteRequest request, ISiteService sites, CancellationToken cancellationToken) =>
    sites.CreateAsync(request, cancellationToken)).RequireAuthorization("Admins");

api.MapPut("/sites/{id:guid}/wordpress-settings", (Guid id, UpdateWordPressSettingsRequest request, ISiteService sites, CancellationToken cancellationToken) =>
    sites.ConfigureWordPressAsync(id, request, cancellationToken)).RequireAuthorization("Admins");

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "BitacoraTech.Api" }));
app.MapGet("/", () => Results.Redirect("/health"));

app.Run();
