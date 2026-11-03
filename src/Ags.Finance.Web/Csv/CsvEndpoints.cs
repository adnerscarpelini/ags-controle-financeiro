using Ags.Finance.Modules.Imports.Contracts;

namespace Ags.Finance.Web.Csv;

/// <summary>Downloads do layout CSV oficial. Somente leitura; exigem login como o restante da aplicação.</summary>
internal static class CsvEndpoints
{
    private const string ContentType = "text/csv; charset=utf-8";

    public static IEndpointRouteBuilder MapCsvEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/csv/cadastros").RequireAuthorization();
        group.MapGet("/{kind}", async Task<IResult> (string kind, IRegistryExport export, CancellationToken cancellationToken) =>
            Parse(kind) is { } parsed ? Download(await export.ExportAsync(parsed, cancellationToken)) : Results.NotFound());
        group.MapGet("/{kind}/modelo", IResult (string kind, IRegistryExport export) =>
            Parse(kind) is { } parsed ? Download(export.Template(parsed)) : Results.NotFound());
        group.MapGet("/{kind}/exemplo", IResult (string kind, IRegistryExport export) =>
            Parse(kind) is { } parsed ? Download(export.Example(parsed)) : Results.NotFound());
        return app;
    }

    /// <summary>Nome do tipo na URL, em minúsculas e em inglês, como no contrato (banks, accounts, cards, card-digits...).</summary>
    public static string Slug(RegistryKind kind) => kind switch
    {
        RegistryKind.CardDigits => "card-digits",
        _ => kind.ToString().ToLowerInvariant(),
    };

    private static RegistryKind? Parse(string slug)
        => Enum.GetValues<RegistryKind>().Cast<RegistryKind?>().FirstOrDefault(x => Slug(x!.Value) == slug);

    private static IResult Download(CsvFile file) => Results.File(file.Content, ContentType, file.FileName);
}
