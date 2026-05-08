using BitacoraTech.Application.Common;
using BitacoraTech.Domain.Tenancy;
using BitacoraTech.Domain.Users;
using BitacoraTech.Infrastructure.Configuration;
using BitacoraTech.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BitacoraTech.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    BitacoraTechDbContext dbContext,
    IPasswordHasher passwordHasher,
    IOptions<AdminSeedOptions> adminSeedOptions) : IDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);

        var options = adminSeedOptions.Value;
        foreach (var roleName in new[] { AuthRoles.Admin, AuthRoles.Editor, AuthRoles.Viewer })
        {
            if (!await dbContext.Roles.AnyAsync(x => x.Name == roleName, cancellationToken))
            {
                await dbContext.Roles.AddAsync(new Role(Guid.NewGuid(), roleName), cancellationToken);
            }
        }

        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(x => x.Name == options.TenantName, cancellationToken);
        if (tenant is null)
        {
            tenant = new Tenant(Guid.NewGuid(), options.TenantName);
            await dbContext.Tenants.AddAsync(tenant, cancellationToken);
        }

        var adminEmail = options.Email.Trim().ToLowerInvariant();
        var admin = await dbContext.Users.Include(x => x.Roles).FirstOrDefaultAsync(x => x.Email == adminEmail, cancellationToken);
        if (admin is null)
        {
            admin = new User(Guid.NewGuid(), tenant.Id, adminEmail, passwordHasher.Hash(options.Password));
            await dbContext.Users.AddAsync(admin, cancellationToken);
        }

        var adminRole = await dbContext.Roles.FirstAsync(x => x.Name == AuthRoles.Admin, cancellationToken);
        admin.AddRole(adminRole);

        if (!await dbContext.AiProviders.AnyAsync(cancellationToken))
        {
            await dbContext.AiProviders.AddRangeAsync(
                new Domain.Ai.AiProvider(Guid.NewGuid(), "Gemini", true, 1),
                new Domain.Ai.AiProvider(Guid.NewGuid(), "OpenAI", true, 2),
                new Domain.Ai.AiProvider(Guid.NewGuid(), "Claude", true, 3),
                new Domain.Ai.AiProvider(Guid.NewGuid(), "OpenRouter", true, 4));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
