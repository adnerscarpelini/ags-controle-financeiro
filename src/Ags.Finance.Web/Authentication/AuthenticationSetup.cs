using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Net.Http.Headers;

namespace Ags.Finance.Web.Authentication;

internal static class AuthenticationSetup
{
    public const string LoginPath = "/login";

    /// <summary>
    /// Autenticação por cookie e a política que exige login em tudo: toda página e endpoint exige um usuário
    /// autenticado, a menos que declare <c>[AllowAnonymous]</c> explicitamente (hoje, só a página de login).
    /// </summary>
    public static IServiceCollection AddSessionAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "ags.session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.LoginPath = LoginPath;
                options.AccessDeniedPath = LoginPath;
                options.ExpireTimeSpan = SessionPolicy.DefaultLifetime;
                options.SlidingExpiration = false;
                options.EventsType = typeof(SessionCookieEvents);
            });
        services.AddScoped<SessionCookieEvents>();

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        services.AddCascadingAuthenticationState();
        services.AddScoped<AuthenticationStateProvider, RevalidatingSessionStateProvider>();

        return services;
    }

    /// <summary>
    /// Depois de sair, o botão "voltar" do navegador não pode mostrar dados guardados em cache: respostas da
    /// aplicação (páginas e endpoints) nunca são armazenadas. Os arquivos estáticos são atendidos antes.
    /// </summary>
    public static IApplicationBuilder UseNoStore(this IApplicationBuilder app)
    {
        return app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey(HeaderNames.CacheControl))
                {
                    context.Response.Headers.CacheControl = "no-store";
                    context.Response.Headers.Pragma = "no-cache";
                }

                return Task.CompletedTask;
            });
            return next(context);
        });
    }
}
