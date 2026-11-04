using System.Threading.Channels;

namespace Ags.Finance.Modules.Diagnostics.Application.RecordError;

/// <summary>Fila limitada entre quem loga e quem grava: logar nunca espera o banco. Cheia, descarta o que chega.</summary>
internal sealed class ErrorQueue
{
    private const int Capacity = 256;

    private readonly Channel<ErrorOccurrence> _channel = Channel.CreateBounded<ErrorOccurrence>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    public ChannelReader<ErrorOccurrence> Reader => _channel.Reader;

    public void Add(ErrorOccurrence occurrence) => _channel.Writer.TryWrite(occurrence);
}
