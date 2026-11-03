using Ags.Finance.Modules.Attachments.Contracts;

namespace Ags.Finance.Web.Attachments;

internal static class AttachmentSetup
{
    public static AttachmentStorageOptions ReadOptions(IConfiguration configuration, string dataDirectory)
    {
        var directory = Path.GetFullPath(configuration["Attachments:Directory"] ?? (OperatingSystem.IsWindows() ? "attachments" : "/attachments"));
        var backup = configuration["Backup:Directory"];
        var error = Overlaps(directory, dataDirectory) || (backup is not null && Overlaps(directory, backup))
            ? "A pasta de anexos precisa ser separada das pastas de dados e de backup. Corrija Attachments__Directory."
            : null;
        var maximum = 20L;
        if (configuration["Attachments:MaxFileSizeMb"] is { } raw && (!long.TryParse(raw, out maximum) || maximum is < 1 or > 20))
        { maximum = 20; error = "Attachments__MaxFileSizeMb deve ser um inteiro entre 1 e 20."; }
        return new(directory, maximum * 1024 * 1024, error);
    }
    private static bool Overlaps(string first, string second)
    {
        first = Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)) + Path.DirectorySeparatorChar;
        second = Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return first.StartsWith(second, comparison) || second.StartsWith(first, comparison);
    }

    public static IEndpointRouteBuilder MapAttachmentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/attachments/export", async Task<IResult> (DateOnly from, DateOnly to, AttachmentKind? kind, AttachmentExport export, HttpContext context, CancellationToken cancellationToken) =>
        {
            var plan = await export.PlanAsync(new(from, to, kind), cancellationToken);
            if (plan.Error is { } error) { return Results.Text(error, "text/plain; charset=utf-8", statusCode: 400); }
            context.Response.Headers.XContentTypeOptions = "nosniff";
            try
            {
                var stream = await export.CreateTemporaryAsync(plan, cancellationToken);
                return Results.Stream(stream, "application/zip", "anexos.zip");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            { return Results.Text("Não foi possível preparar o ZIP. Verifique a integridade dos anexos e o espaço temporário disponível.", "text/plain; charset=utf-8", statusCode: 400); }
        }).RequireAuthorization();
        app.MapGet("/attachments/{externalId:guid}", async Task<IResult> (Guid externalId, bool? download, IAttachmentCatalog catalog, HttpContext context, CancellationToken cancellationToken) =>
        {
            var file = await catalog.OpenAsync(externalId, cancellationToken);
            if (file is null) { return Results.NotFound(); }
            context.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.Stream(file.Stream, file.Details.ContentType, download == true ? file.Details.OriginalFileName : null);
        }).RequireAuthorization();
        return app;
    }
}
