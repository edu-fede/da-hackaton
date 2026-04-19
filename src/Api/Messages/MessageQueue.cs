using System.Threading.Channels;

namespace Hackaton.Api.Messages;

/// <summary>
/// In-process fan-out buffer between the SignalR Hub (writer) and the BackgroundService
/// consumer (reader, Story 1.12). Single-writer, single-reader, unbounded. Registered as
/// a singleton so both sides share the same underlying <see cref="Channel{T}"/>.
/// </summary>
public sealed class MessageQueue
{
    private readonly Channel<MessageWorkItem> _channel = Channel.CreateUnbounded<MessageWorkItem>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    public ChannelWriter<MessageWorkItem> Writer => _channel.Writer;

    public ChannelReader<MessageWorkItem> Reader => _channel.Reader;
}
