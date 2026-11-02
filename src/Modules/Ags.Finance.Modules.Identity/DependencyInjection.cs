using Ags.Finance.Modules.Identity.Application;
using Ags.Finance.Modules.Identity.Application.ChangePassword;
using Ags.Finance.Modules.Identity.Application.CreateUser;
using Ags.Finance.Modules.Identity.Application.ResetPassword;
using Ags.Finance.Modules.Identity.Application.SignIn;
using Ags.Finance.Modules.Identity.Application.UserExists;
using Ags.Finance.Modules.Identity.Application.ValidateSession;
using Ags.Finance.Modules.Identity.Contracts;
using Ags.Finance.Modules.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ags.Finance.Modules.Identity;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<AuditTimestampsInterceptor>();
        services.AddDbContext<IdentityDbContext>((provider, options) => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(IdentityDbContext.MigrationsHistoryTable, IdentityDbContext.Schema))
            .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.NoEntityTypeConfigurationsWarning))
            .AddInterceptors(provider.GetRequiredService<AuditTimestampsInterceptor>()));

        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddScoped<IUserStore, UserStore>();
        services.AddScoped<ICreateUser, CreateUserHandler>();
        services.AddScoped<IResetPassword, ResetPasswordHandler>();
        services.AddScoped<IChangePassword, ChangePasswordHandler>();
        services.AddScoped<ISignIn, SignInHandler>();
        services.AddScoped<IValidateSession, ValidateSessionHandler>();
        services.AddScoped<IUserExists, UserExistsHandler>();

        return services;
    }

    public static async Task MigrateIdentityModuleAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
