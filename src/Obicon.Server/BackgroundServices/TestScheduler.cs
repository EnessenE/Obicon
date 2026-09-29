using Obicon.Server.Services;

namespace Obicon.Server.BackgroundServices;

/// <summary>
/// Runs active tests on their configured frequency. Scans every 5 seconds;
/// overdue tests (e.g. after server downtime) run once and resynchronize.
/// </summary>
public class TestScheduler : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TestScheduler> _logger;

    public TestScheduler(IServiceScopeFactory scopeFactory, ILogger<TestScheduler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TestScheduler started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var testService = scope.ServiceProvider.GetRequiredService<ITestService>();
                await testService.ScheduleDueTestsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TestScheduler");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("TestScheduler stopped");
    }
}
