using System.Globalization;
using Ags.Finance.Modules.Imports.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Web.Csv;

/// <summary>Downloads do layout CSV de lançamentos e das séries recorrentes. Somente leitura; exigem login.</summary>
internal static class TransactionCsvEndpoints
{
    private const string ContentType = "text/csv; charset=utf-8";

    public static IEndpointRouteBuilder MapTransactionCsvEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/csv/lancamentos").RequireAuthorization();
        group.MapGet(string.Empty, async Task<IResult> (HttpRequest request, ITransactionExport export, CancellationToken cancellationToken) =>
            Filter(request.Query) is { } filter ? Download(await export.ExportAsync(filter, cancellationToken)) : Results.BadRequest("Filtro inválido."));
        group.MapGet("/series", async Task<IResult> (HttpRequest request, ITransactionExport export, CancellationToken cancellationToken) =>
            Filter(request.Query) is { } filter ? Download(await export.ExportSeriesAsync(filter, cancellationToken)) : Results.BadRequest("Filtro inválido."));
        group.MapGet("/modelo", IResult (ITransactionExport export) => File(export.Template(TransactionFileKind.Transactions)));
        group.MapGet("/exemplo", IResult (ITransactionExport export) => File(export.Example(TransactionFileKind.Transactions)));
        group.MapGet("/series/modelo", IResult (ITransactionExport export) => File(export.Template(TransactionFileKind.RecurringSeries)));
        group.MapGet("/series/exemplo", IResult (ITransactionExport export) => File(export.Example(TransactionFileKind.RecurringSeries)));
        return app;
    }

    private static IResult Download(CsvExportResult result) => result.File is { } file ? File(file) : Results.BadRequest(result.Error);

    private static IResult File(CsvFile file) => Results.File(file.Content, ContentType, file.FileName);

    /// <summary>Lê o filtro da query string; devolve <c>null</c> se algum valor informado for inválido (nada é ignorado em silêncio).</summary>
    internal static TransactionExportFilter? Filter(IQueryCollection query)
    {
        string Value(string key) => query[key].ToString().Trim();
        if (!TryMonth(Value("month"), out var month) || !TryDate(Value("from"), out var from) || !TryDate(Value("to"), out var to)) { return null; }
        Guid? saved = null, account = null, card = null, final = null, category = null, subcategory = null;
        foreach (var (key, target) in new (string, Action<Guid?>)[] { ("savedFilter", x => saved = x), ("account", x => account = x), ("card", x => card = x), ("final", x => final = x), ("category", x => category = x), ("subcategory", x => subcategory = x) })
        {
            if (Value(key).Length == 0) { continue; }
            if (!Guid.TryParse(Value(key), out var id)) { return null; }
            target(id);
        }
        bool? effective = null;
        if (Value("effective").Length > 0) { if (!bool.TryParse(Value("effective"), out var parsed)) { return null; } effective = parsed; }
        CategoryKind? kind = null;
        if (Value("kind").Length > 0) { if (!Enum.TryParse<CategoryKind>(Value("kind"), true, out var parsedKind) || !Enum.IsDefined(parsedKind)) { return null; } kind = parsedKind; }
        return new(month, from, to, saved, account, card, final, category, subcategory, Value("text"), effective, kind);
    }

    private static bool TryMonth(string text, out AccountingMonth? month)
    {
        month = null;
        if (text.Length == 0) { return true; }
        if (!DateOnly.TryParseExact(text + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) { return false; }
        month = AccountingMonth.FromDate(date);
        return true;
    }

    private static bool TryDate(string text, out DateOnly? date)
    {
        date = null;
        if (text.Length == 0) { return true; }
        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) { return false; }
        date = parsed;
        return true;
    }
}
