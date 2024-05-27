using Cryptique.Logic;

namespace Cryptique.Api.HostedServices;

public class MessageCleanupService(IMessageService messageService, ILogger<MessageCleanupService> logger)
    : IHostedService
{
    private CancellationTokenSource? _cancellationTokenSource;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task.Run(() => CleanupExpiredMessages(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
        return Task.CompletedTask;
    }

    private async Task CleanupExpiredMessages(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Call your cleanup method here
                var deletedMessages = await messageService.CleanupExpiredMessages();

                logger.LogInformation("Cleanup completed, {DeletedMessages} messages deleted.", deletedMessages);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred during cleanup.");
            }

            // Wait for an hour before the next cleanup
            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource?.Cancel();
        return Task.CompletedTask;
    }
}
