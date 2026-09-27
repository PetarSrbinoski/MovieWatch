using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieWatch.Domain.Common;
using MovieWatch.Domain.Config;
using MovieWatch.Domain.Dto;
using MovieWatch.Repository;
using MovieWatch.Service.Interface;

namespace MovieWatch.Web.Extensions;

public static class DatabaseInitializationExtensions
{
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        // Validate secrets before creating or changing a database.
        _ = services.GetRequiredService<IOptions<JwtSettings>>().Value;
        var bootstrap = services.GetRequiredService<IOptions<AdministratorSettings>>().Value;
        var database = services.GetRequiredService<ApplicationDbContext>();
        await database.Database.MigrateAsync();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Enum.GetValues<AccountRole>())
        {
            var roleName = role.ToString();
            if (await roles.RoleExistsAsync(roleName))
                continue;
            var result = await roles.CreateAsync(new IdentityRole(roleName));
            if (!result.Succeeded)
                throw new InvalidOperationException("Could not initialize account roles.");
        }
        if (bootstrap.Enabled)
            await services.GetRequiredService<IAccountService>().BootstrapAdministratorAsync(
                new RegisterViewerDto(bootstrap.Email, bootstrap.Password, bootstrap.DisplayName));
    }
}
