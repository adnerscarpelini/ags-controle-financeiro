using Ags.Finance.Modules.Accounts.Contracts;
using Microsoft.Net.Http.Headers;

namespace Ags.Finance.Web.BankLogos;

internal static class BankLogoEndpoints
{
    public static IEndpointRouteBuilder MapBankLogoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/bank-logos/{externalId:guid}", async Task<IResult> (Guid externalId, IBankLogoCatalog logos, CancellationToken cancellationToken) =>
        {
            var logo = await logos.GetAsync(externalId, cancellationToken);
            return logo is null ? Results.NotFound() : Results.File(logo.Png, "image/png", entityTag: new EntityTagHeaderValue($"\"{logo.Version:N}\""));
        }).RequireAuthorization();
        return app;
    }
}
