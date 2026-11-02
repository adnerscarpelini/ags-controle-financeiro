using Ags.Finance.Modules.Identity.Contracts;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace Ags.Finance.Web.Authentication;

/// <summary>
/// Um circuito Blazor fica aberto sem novas requisições HTTP. Reconfere o carimbo de segurança a cada minuto, para
/// que trocar a senha encerre também as telas que já estavam abertas.
/// </summary>
internal sealed class RevalidatingSessionStateProvider : RevalidatingServerAuthenticationStateProvider
{
    private readonly IServiceScopeFactory _scopeFactory;

    public RevalidatingSessionStateProvider(ILoggerFactory loggerFactory, IServiceScopeFactory scopeFactory)
        : base(loggerFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState,
        CancellationToken cancellationToken)
    {
        if (!SessionClaims.TryRead(authenticationState.User, out var userId, out var securityStamp))
        {
            return false;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var validateSession = scope.ServiceProvider.GetRequiredService<IValidateSession>();
        return await validateSession.ExecuteAsync(userId, securityStamp, cancellationToken);
    }
}
