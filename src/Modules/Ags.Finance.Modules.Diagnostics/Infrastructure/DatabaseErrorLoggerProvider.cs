using Ags.Finance.Modules.Diagnostics.Application.RecordError;
using Microsoft.Extensions.Logging;

namespace Ags.Finance.Modules.Diagnostics.Infrastructure;

/// <summary>
/// Leva à tabela os logs <c>Error</c> e <c>Critical</c> que trazem exceção. O EF Core fica de fora porque registra
/// como erro até o conflito de restrição que o chamador trata; o que ninguém trata reaparece no log do host.
/// </summary>
internal sealed class DatabaseErrorLoggerProvider(ErrorQueue queue, TimeProvider clock) : ILoggerProvider
{
    private const string EntityFrameworkCategory = "Microsoft.EntityFrameworkCore";

    public ILogger CreateLogger(string categoryName) => new DatabaseErrorLogger(categoryName, queue, clock);

    public void Dispose()
    {
    }

    private sealed class DatabaseErrorLogger(string category, ErrorQueue queue, TimeProvider clock) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel is LogLevel.Error or LogLevel.Critical;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel) || exception is null || IsExpected(exception) || category.StartsWith(EntityFrameworkCategory, StringComparison.Ordinal))
            {
                return;
            }

            queue.Add(ErrorOccurrence.From(category, exception, clock.GetUtcNow()));
        }

        // Cancelamento e circuito desconectado acontecem em uso normal, por exemplo ao fechar a aba.
        private static bool IsExpected(Exception exception)
            => exception is OperationCanceledException || exception.GetType().Name == "JSDisconnectedException";
    }
}
