using System.Data.Common;
using System.Security.Cryptography;
using System.Text;

namespace Ags.Finance.Modules.Diagnostics.Application.RecordError;

/// <summary>O que é guardado de uma exceção: tipos e pilhas de toda a cadeia, nunca a mensagem nem o <c>Detail</c>.</summary>
internal sealed record ErrorOccurrence(string Fingerprint, string ExceptionType, string Source, string StackTrace, string? SqlState, DateTimeOffset OccurredAt)
{
    private const int MaxStackLength = 64_000;

    public static ErrorOccurrence From(string source, Exception exception, DateTimeOffset occurredAt)
    {
        var chain = Flatten(exception).ToList();
        var stack = string.Join("\n--- inner ---\n", chain.Select(x => $"{x.GetType().FullName}\n{x.StackTrace}"));
        if (stack.Length > MaxStackLength)
        {
            stack = stack[..MaxStackLength];
        }

        var type = exception.GetType().FullName ?? exception.GetType().Name;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{source}\n{type}\n{stack}"));
        var sqlState = chain.OfType<DbException>().Select(x => x.SqlState).FirstOrDefault(x => x is not null);
        return new ErrorOccurrence(Convert.ToHexStringLower(hash), type, source, stack, sqlState, occurredAt);
    }

    private static IEnumerable<Exception> Flatten(Exception exception)
    {
        yield return exception;
        IEnumerable<Exception> inners = exception is AggregateException aggregate ? aggregate.InnerExceptions : exception.InnerException is { } inner ? [inner] : [];
        foreach (var item in inners.SelectMany(Flatten))
        {
            yield return item;
        }
    }
}
