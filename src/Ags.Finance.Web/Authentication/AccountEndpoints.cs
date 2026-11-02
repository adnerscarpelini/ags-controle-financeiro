using Ags.Finance.Modules.Identity.Contracts;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace Ags.Finance.Web.Authentication;

/// <summary>
/// Os únicos endpoints que mexem na sessão e na senha: entrar, sair e trocar a senha logado. Todos exigem token
/// antifalsificação. Não há endpoint para criar usuário nem para redefinir a senha sem estar logado: isso só existe
/// nos comandos <c>user create</c> e <c>user reset-password</c>, no computador do serviço.
/// </summary>
internal static class AccountEndpoints
{
    public const string SignInPath = "/account/sign-in";
    public const string SignOutPath = "/account/sign-out";
    public const string PasswordPath = "/account/password";

    public const string SettingsPath = "/configuracoes";

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var account = app.MapGroup("/account").WithMetadata(new AntiforgeryRequired());

        // O middleware só registra o resultado da validação; endpoints que não leem formulário (sair) precisam recusar.
        account.AddEndpointFilter(RejectInvalidAntiforgeryAsync);

        account.MapPost("/sign-in", (Delegate)SignInAsync).AllowAnonymous();
        account.MapPost("/sign-out", (Delegate)SignOutAsync);
        account.MapPost("/password", (Delegate)ChangePasswordAsync);

        return app;
    }

    private static async ValueTask<object?> RejectInvalidAntiforgeryAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var validation = context.HttpContext.Features.Get<IAntiforgeryValidationFeature>();
        return validation is { IsValid: true } ? await next(context) : Results.BadRequest();
    }

    private static async Task<IResult> SignInAsync(
        [FromForm] SignInForm form,
        HttpContext context,
        ISignIn signIn,
        TimeProvider timeProvider)
    {
        var returnUrl = ReturnUrl.Sanitize(form.ReturnUrl);
        var result = await signIn.ExecuteAsync(form.UserName, form.Password, context.RequestAborted);

        if (result is { Status: SignInStatus.Succeeded, User: { } user })
        {
            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                SessionClaims.ToPrincipal(user, CookieAuthenticationDefaults.AuthenticationScheme),
                SessionPolicy.CreateProperties(form.RememberMe, timeProvider.GetUtcNow()));
            return Results.LocalRedirect(returnUrl);
        }

        // Usuário inexistente e senha errada voltam com a mesma mensagem genérica.
        var error = result.Status == SignInStatus.LockedOut ? "locked" : "invalid";
        var query = returnUrl == ReturnUrl.Home ? string.Empty : $"&ReturnUrl={Uri.EscapeDataString(returnUrl)}";
        return Results.LocalRedirect($"{AuthenticationSetup.LoginPath}?error={error}{query}");
    }

    private static async Task<IResult> SignOutAsync(HttpContext context)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.LocalRedirect(AuthenticationSetup.LoginPath);
    }

    private static async Task<IResult> ChangePasswordAsync(
        [FromForm] ChangePasswordForm form,
        HttpContext context,
        IChangePassword changePassword)
    {
        if (!SessionClaims.TryRead(context.User, out var userId, out _))
        {
            return Results.LocalRedirect(AuthenticationSetup.LoginPath);
        }

        if (!string.Equals(form.NewPassword, form.ConfirmPassword, StringComparison.Ordinal))
        {
            return Redirect("mismatch");
        }

        var result = await changePassword.ExecuteAsync(userId, form.CurrentPassword, form.NewPassword, context.RequestAborted);

        switch (result)
        {
            case { Status: ChangePasswordStatus.Changed, User: { } user }:
                // Todas as outras sessões caem pelo novo carimbo; esta continua, com o mesmo prazo de antes.
                var current = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                await context.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    SessionClaims.ToPrincipal(user, CookieAuthenticationDefaults.AuthenticationScheme),
                    current.Properties);
                return Redirect("changed");
            case { Status: ChangePasswordStatus.WrongCurrentPassword }:
                return Redirect("wrong");
            case { Status: ChangePasswordStatus.PasswordTooShort }:
                return Redirect("short");
            case { Status: ChangePasswordStatus.LockedOut }:
                return Redirect("locked");
            default:
                return Results.LocalRedirect(AuthenticationSetup.LoginPath);
        }
    }

    private static IResult Redirect(string outcome)
    {
        return Results.LocalRedirect($"{SettingsPath}?password={outcome}");
    }

    /// <summary>Marca o endpoint para o middleware de antifalsificação exigir o token, também sem parâmetro de formulário.</summary>
    private sealed class AntiforgeryRequired : IAntiforgeryMetadata
    {
        public bool RequiresValidation => true;
    }

    internal sealed class SignInForm
    {
        public string? UserName { get; init; }

        public string? Password { get; init; }

        public bool RememberMe { get; init; }

        public string? ReturnUrl { get; init; }
    }

    internal sealed class ChangePasswordForm
    {
        public string? CurrentPassword { get; init; }

        public string? NewPassword { get; init; }

        public string? ConfirmPassword { get; init; }
    }
}
