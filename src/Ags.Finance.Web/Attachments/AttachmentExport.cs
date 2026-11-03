using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Ags.Finance.Modules.Attachments.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;

namespace Ags.Finance.Web.Attachments;

internal sealed record AttachmentExportFilter(DateOnly From, DateOnly To, AttachmentKind? Kind = null);
internal sealed record AttachmentExportItem(AttachmentDetails Attachment, string Name, DateOnly Date);
internal sealed record AttachmentExportPlan(IReadOnlyList<AttachmentExportItem> Items, string? Error);

internal sealed class AttachmentExport(IAttachmentCatalog catalog, IEntryAttachmentCatalog entries, IInvoiceAttachmentCatalog invoices, AttachmentStorageOptions options)
{
    public async Task<AttachmentExportPlan> PlanAsync(AttachmentExportFilter filter, CancellationToken cancellationToken)
    {
        if (filter.From > filter.To || (filter.Kind is { } kind && !Enum.IsDefined(kind)))
        { return new([], "Informe um período ordenado e um tipo válido."); }
        var entryReferences = (await entries.GetReferencesAsync(cancellationToken)).ToDictionary(x => x.Owner);
        var invoiceReferences = (await invoices.GetReferencesAsync(cancellationToken)).ToDictionary(x => x.Owner);
        var result = new List<AttachmentExportItem>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in (await catalog.ListAsync(cancellationToken)).Where(x => x.DeletedAt is null && (filter.Kind is null || filter.Kind == x.Kind)).OrderBy(x => x.ExternalId))
        {
            DateOnly date;
            string prefix;
            var type = row.Kind switch { AttachmentKind.Bill => "conta", AttachmentKind.Receipt => "comprovante", AttachmentKind.Invoice => "fatura", _ => "outro" };
            if (entryReferences.TryGetValue(row.Owner, out var entry))
            {
                date = entry.Date;
                if (date < filter.From || date > filter.To) { continue; }
                prefix = FormattableString.Invariant($"lancamentos/{date:yyyy/MM}/{date:yyyy-MM-dd}_{type}_{Slug(entry.Description)}");
            }
            else if (invoiceReferences.TryGetValue(row.Owner, out var invoice))
            {
                date = invoice.Month.FirstDay;
                if (date < new DateOnly(filter.From.Year, filter.From.Month, 1) || date > new DateOnly(filter.To.Year, filter.To.Month, 1)) { continue; }
                prefix = FormattableString.Invariant($"faturas/{Slug(invoice.CardName)}/{date:yyyy-MM}/{date:yyyy-MM}_{type}");
            }
            else { continue; }
            var extension = Path.GetExtension(row.StorageKey);
            var name = $"{prefix}_{row.ExternalId.ToString("N")[^8..]}{extension}";
            if (!names.Add(name)) { name = $"{prefix}_{row.ExternalId:N}{extension}"; names.Add(name); }
            result.Add(new(row, name, date));
        }
        return result.Count == 0 ? new([], "Nenhum anexo ativo encontrado neste período e tipo.") : new(result, null);
    }

    public async Task<FileStream> CreateTemporaryAsync(AttachmentExportPlan plan, CancellationToken cancellationToken)
    {
        var fileOptions = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose
        };
        if (!OperatingSystem.IsWindows()) { fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite; }
        var stream = new FileStream(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), fileOptions);
        try
        {
            await WriteAsync(plan, stream, cancellationToken);
            stream.Position = 0;
            return stream;
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    public async Task WriteAsync(AttachmentExportPlan plan, Stream output, CancellationToken cancellationToken)
    {
        if (plan.Error is not null || plan.Items.Count == 0) { throw new InvalidOperationException(plan.Error ?? "No attachment selected."); }
        if (options.Error is not null) { throw new IOException(options.Error); }
        await using var lease = await catalog.AcquireMaintenanceLeaseAsync(cancellationToken);
        await using var archive = await ZipArchive.CreateAsync(output, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: Encoding.UTF8, cancellationToken);
        var index = new StringBuilder("arquivo;tipo;dono;data_referencia;nome_original;bytes;sha256\r\n");
        var sums = new StringBuilder();
        foreach (var item in plan.Items)
        {
            var row = item.Attachment;
            await using var source = File.OpenRead(AttachmentBackup.Resolve(options.Directory, row.StorageKey));
            await using var target = await archive.CreateEntry(item.Name, CompressionLevel.Optimal).OpenAsync(cancellationToken);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long size = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                hash.AppendData(buffer, 0, read); size += read;
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            var actual = Convert.ToHexStringLower(hash.GetHashAndReset());
            if (size != row.SizeBytes || actual != row.Sha256) { throw new InvalidDataException("Attachment is corrupt; export was interrupted."); }
            sums.Append(actual).Append("  ").Append(item.Name).Append('\n');
            index.AppendJoin(';', Cell(item.Name), row.Kind.ToString(), row.Owner.ExternalId.ToString(), item.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Cell(row.OriginalFileName), row.SizeBytes.ToString(CultureInfo.InvariantCulture), row.Sha256).Append("\r\n");
        }
        var indexBytes = Encoding.UTF8.GetBytes(index.ToString());
        await using (var target = await archive.CreateEntry("indice.csv").OpenAsync(cancellationToken)) { await target.WriteAsync(indexBytes, cancellationToken); }
        sums.Append(Convert.ToHexStringLower(SHA256.HashData(indexBytes))).Append("  indice.csv\n");
        await using (var target = await archive.CreateEntry("SHA256SUMS").OpenAsync(cancellationToken)) { await target.WriteAsync(Encoding.UTF8.GetBytes(sums.ToString()), cancellationToken); }
    }

    private static string Cell(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@') { value = "'" + value; }
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
    private static string Slug(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var character in normalized)
        {
            if (char.IsAsciiLetterOrDigit(character)) { builder.Append(char.ToLowerInvariant(character)); }
            else if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && builder.Length > 0 && builder[^1] != '-') { builder.Append('-'); }
            if (builder.Length >= 60) { break; }
        }
        return builder.ToString().Trim('-') is { Length: > 0 } value ? value : "documento";
    }
}
