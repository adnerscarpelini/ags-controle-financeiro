using Ags.Finance.Modules.Diagnostics.Application.RecordError;
using Ags.Finance.Modules.Diagnostics.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ags.Finance.Modules.Diagnostics.Infrastructure;

/// <summary>
/// Grava a fila e apaga o que passou da retenção. Falhar aqui não pode derrubar a aplicação nem gerar outro erro:
/// a falha sai como aviso, nível que o provedor ignora, e o erro original já foi ao console pelo log que o gerou.
/// </summary>
internal sealed partial class ErrorRecorder(ErrorQueue queue, IErrorStore store, TimeProvider clock, ILogger<ErrorRecorder> logger) : BackgroundService
{
    private static readonly TimeSpan s_purgeInterval = TimeSpan.FromDays(1);
    private static readonly TimeSpan s_drainTimeout = TimeSpan.FromSeconds(5);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.WhenAll(WriteAsync(stoppingToken), PurgeAsync(stoppingToken));

    private async Task WriteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var occurrence in queue.Reader.ReadAllAsync(stoppingToken))
            {
                await SaveAsync(occurrence, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            await DrainAsync();
        }
    }

    // Ao desligar, grava o que ainda está na fila, com prazo curto para não segurar o encerramento.
    private async Task DrainAsync()
    {
        using var timeout = new CancellationTokenSource(s_drainTimeout);
        while (!timeout.IsCancellationRequested && queue.Reader.TryRead(out var occurrence))
        {
            await SaveAsync(occurrence, timeout.Token);
        }
    }

    private async Task SaveAsync(ErrorOccurrence occurrence, CancellationToken cancellationToken)
    {
        try
        {
            await store.SaveAsync(occurrence, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            RecorderLog.WriteFailed(logger, exception.GetType().Name);
        }
        catch (OperationCanceledException)
        {
            RecorderLog.WriteFailed(logger, nameof(OperationCanceledException));
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private async Task PurgeAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(s_purgeInterval, clock);
        try
        {
            do
            {
                try
                {
                    await store.PurgeAsync(clock.GetUtcNow().AddDays(-ErrorRecord.RetentionDays), stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
                {
                    RecorderLog.PurgeFailed(logger, exception.GetType().Name);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private static partial class RecorderLog
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "Não foi possível gravar um erro na tabela de diagnóstico ({ErrorType}).")]
        public static partial void WriteFailed(ILogger logger, string errorType);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Não foi possível apagar os erros antigos da tabela de diagnóstico ({ErrorType}).")]
        public static partial void PurgeFailed(ILogger logger, string errorType);
    }
}
