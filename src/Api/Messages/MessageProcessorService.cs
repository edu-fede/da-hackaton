using Hackaton.Api.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hackaton.Api.Messages;

/// <summary>
/// Drains the <see cref="MessageQueue"/> and persists each <see cref="MessageWorkItem"/>
/// via <see cref="MessageAppender.AppendAsync"/>. Per CLAUDE.md §5 this hosted service lives
/// inside the API process. Failures are logged and skipped (no retry, no dead-letter); the
/// watermark resync path (Story 1.13) is responsible for closing any resulting client-side gap.
/// </summary>
public sealed class MessageProcessorService(
    MessageQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<MessageProcessorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MessageProcessorService started; draining MessageQueue.");

        try
        {
            await foreach (var item in queue.Reader.ReadAllAsync(stoppingToken))
            {
                await ProcessItemAsync(item, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("MessageProcessorService stopping; cancellation requested.");
        }
    }

    private async Task ProcessItemAsync(MessageWorkItem item, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await MessageAppender.AppendAsync(
                db,
                item.RoomId,
                item.SenderId,
                item.Text,
                item.ReplyToMessageId,
                id: item.Id,
                createdAt: item.CreatedAt,
                ct: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to persist MessageWorkItem {MessageId} in room {RoomId}; dropping and continuing.",
                item.Id,
                item.RoomId);
        }
    }
}
