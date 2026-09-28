using Microsoft.Extensions.Hosting;
using Obicon.Server.Models;
using Obicon.Server.Services;

namespace Obicon.Server.BackgroundServices;

public class TestQueueProcessor : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<TestQueueProcessor> _logger;

    public TestQueueProcessor(IServiceProvider services, ILogger<TestQueueProcessor> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TestQueueProcessor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var queueService = scope.ServiceProvider.GetRequiredService<ITestQueueService>();
                var nodeService = scope.ServiceProvider.GetRequiredService<INodeService>();
                var testService = scope.ServiceProvider.GetRequiredService<ITestService>();

                var queueLength = await queueService.GetQueueLengthAsync();
                _logger.LogDebug("Queue length: {QueueLength}", queueLength);

                // TODO: Assign jobs to available nodes
                // This will be implemented when WebSocket communication is set up
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TestQueueProcessor");
            }

            await Task.Delay(1000, stoppingToken);
        }

        _logger.LogInformation("TestQueueProcessor stopped");
    }
}
