using System.Globalization;
using Ags.Finance.Modules.Attachments.Contracts;
using Ags.Finance.Web.Attachments;

namespace Ags.Finance.Web.Commands;

internal sealed class AttachmentCommands(AttachmentInventory inventory, AttachmentExport export, ITerminal terminal)
{
    public const string Usage = "Uso: attachments verify [--clean-orphans] | attachments export --from yyyy-MM-dd --to yyyy-MM-dd [--kind Bill|Receipt|Invoice|Other] --output ARQUIVO.zip";
    public async Task<int> RunAsync(string[] arguments, CancellationToken cancellationToken)
    {
        try
        {
            if (arguments is ["verify"] or ["verify", "--clean-orphans"])
            {
                var problems = await inventory.VerifyAsync(arguments.Length == 2, cancellationToken);
                foreach (var problem in problems) { terminal.WriteLine($"{problem.Status}: {problem.StorageKey}"); }
                if (problems.Count == 0) { terminal.WriteLine("Os anexos estão íntegros e não há arquivos sem registro."); }
                return problems.Any(x => x.Status is "ausente" or "corrompido" || x.Status == "sem registro") ? UserCommands.Failed : UserCommands.Success;
            }
            if (arguments.FirstOrDefault() != "export" || arguments.Length % 2 != 1) { terminal.WriteError(Usage); return UserCommands.Usage; }
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 1; index < arguments.Length; index += 2)
            {
                if (arguments[index] is not ("--from" or "--to" or "--kind" or "--output") || !options.TryAdd(arguments[index], arguments[index + 1]))
                { terminal.WriteError(Usage); return UserCommands.Usage; }
            }
            if (!options.TryGetValue("--from", out var fromText) || !options.TryGetValue("--to", out var toText) || !options.TryGetValue("--output", out var output)
                || !DateOnly.TryParseExact(fromText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from)
                || !DateOnly.TryParseExact(toText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to))
            { terminal.WriteError(Usage); return UserCommands.Usage; }
            AttachmentKind? kind = null;
            if (options.TryGetValue("--kind", out var kindText))
            {
                if (!Enum.TryParse<AttachmentKind>(kindText, true, out var parsed) || !Enum.IsDefined(parsed)) { terminal.WriteError(Usage); return UserCommands.Usage; }
                kind = parsed;
            }
            var plan = await export.PlanAsync(new(from, to, kind), cancellationToken);
            if (plan.Error is { } error) { terminal.WriteError(error); return UserCommands.Failed; }
            var temporary = Path.GetFullPath(output) + ".partial-" + Guid.NewGuid().ToString("N");
            try
            {
                await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
                { await export.WriteAsync(plan, file, cancellationToken); }
                File.Move(temporary, Path.GetFullPath(output));
            }
            finally { if (File.Exists(temporary)) { File.Delete(temporary); } }
            terminal.WriteLine($"ZIP exportado com {plan.Items.Count} documento(s). Não é criptografado e não substitui o backup.");
            return UserCommands.Success;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        { terminal.WriteError("Não foi possível concluir a operação: confira a pasta, as permissões e a integridade dos anexos."); return UserCommands.Failed; }
    }
}
