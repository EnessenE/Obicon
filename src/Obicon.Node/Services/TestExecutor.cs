using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obicon.Node.Services.TestRunners;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using Obicon.Node.Configuration;
using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Messages;

namespace Obicon.Node.Services;

/// <summary>
/// Executes test assignments. Each assignment runs on its own task, capped at
/// MaxConcurrentTests concurrent executions. A test task destroys itself at
/// most [test timeout] + 5 seconds after it starts running.
/// </summary>
public class TestExecutor : ITestExecutor
{
    private const int HardKillGraceSeconds = 5;

    private readonly NodeSettings _settings;
    private readonly IServiceProvider _services;
    private readonly IReadOnlyDictionary<TestType, ITestRunner> _runners;
    private readonly ILogger<TestExecutor> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly SemaphoreSlim _slots;

    // Resolved lazily to break the construction cycle:
    // ServerConnection -> TestExecutor -> IServerConnection -> ServerConnection
    private IServerConnection ServerConnection => _services.GetRequiredService<IServerConnection>();

    public NodeStatistics Statistics { get; } = new();

    public TestExecutor(
        IOptions<NodeSettings> settings,
        IServiceProvider services,
        IEnumerable<ITestRunner> runners,
        ILogger<TestExecutor> logger,
        IHostApplicationLifetime lifetime)
    {
        _settings = settings.Value;
        _services = services;
        _runners = runners.ToDictionary(r => r.Type);
        _logger = logger;
        _lifetime = lifetime;
        _slots = new SemaphoreSlim(Math.Max(1, _settings.MaxConcurrentTests));
    }

    public Task ExecuteAssignmentAsync(TestAssignmentMessage assignment)
    {
        // Dedicated task per test; the executor only gates and tracks them
        _ = RunAssignmentAsync(assignment);
        return Task.CompletedTask;
    }

    private async Task RunAssignmentAsync(TestAssignmentMessage assignment)
    {
        var stats = Statistics;
        Interlocked.Increment(ref stats.Pending);
        try
        {
            await _slots.WaitAsync(_lifetime.ApplicationStopping);
        }
        catch (OperationCanceledException)
        {
            Interlocked.Decrement(ref stats.Pending);
            return;
        }

        Interlocked.Decrement(ref stats.Pending);
        Interlocked.Increment(ref stats.Running);
        var stopwatch = Stopwatch.StartNew();

        CancellationTokenSource? hardKill = null;
        CancellationTokenSource? testTimeout = null;
        try
        {
            var timeout = TimeSpan.FromSeconds(Math.Clamp(
                assignment.TimeoutSeconds > 0 ? assignment.TimeoutSeconds : _settings.DefaultTestTimeoutSeconds,
                1,
                Math.Max(1, _settings.MaxTestTimeoutSeconds)));

            // The task destroys itself at most [timeout] + 5 seconds after it starts
            hardKill = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.ApplicationStopping);
            hardKill.CancelAfter(timeout + TimeSpan.FromSeconds(HardKillGraceSeconds));
            testTimeout = CancellationTokenSource.CreateLinkedTokenSource(hardKill.Token);
            testTimeout.CancelAfter(timeout);

            await SendStatusUpdateAsync(assignment, TestJobStatus.Running, "Executing");

            var runner = _runners.GetValueOrDefault(assignment.TestType);
            if (runner == null)
            {
                _logger.LogWarning("No runner registered for test type {TestType}", assignment.TestType);
                await ReportAsync(assignment, success: false, stopwatch,
                    $"No runner for test type {assignment.TestType}", TestJobStatus.Failed);
                return;
            }

            var outcome = await runner.ExecuteAsync(assignment, timeout, testTimeout.Token);

            var finalStatus = outcome.Success ? TestJobStatus.Completed : TestJobStatus.Failed;
            if (testTimeout.IsCancellationRequested)
            {
                finalStatus = TestJobStatus.Timeout;
                outcome = new TestOutcome { Success = false, Output = $"Timed out after {timeout.TotalSeconds}s" };
            }

            await ReportAsync(assignment, outcome.Success, stopwatch, outcome.Output, finalStatus);
        }
        catch (OperationCanceledException) when (hardKill?.IsCancellationRequested == true
                                                && !_lifetime.ApplicationStopping.IsCancellationRequested)
        {
            _logger.LogWarning("Job {JobId} destroyed after exceeding its hard time limit", assignment.JobId);
            await ReportAsync(assignment, success: false, stopwatch,
                "Test task exceeded its hard time limit and was destroyed", TestJobStatus.Timeout);
        }
        catch (OperationCanceledException) when (!_lifetime.ApplicationStopping.IsCancellationRequested)
        {
            await ReportAsync(assignment, success: false, stopwatch,
                $"Timed out after {stopwatch.Elapsed.TotalSeconds:F1}s", TestJobStatus.Timeout);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed unexpectedly", assignment.JobId);
            await ReportAsync(assignment, success: false, stopwatch, $"Execution error: {ex.Message}", TestJobStatus.Failed);
        }
        finally
        {
            hardKill?.Dispose();
            testTimeout?.Dispose();
            Interlocked.Decrement(ref stats.Running);
            _slots.Release();
        }
    }

    private async Task ReportAsync(
        TestAssignmentMessage assignment,
        bool success,
        Stopwatch stopwatch,
        string output,
        TestJobStatus finalStatus)
    {
        var stats = Statistics;
        Metrics.NodeMetrics.TestExecuted(finalStatus.ToString(), assignment.TestType.ToString(), stopwatch.Elapsed.TotalMilliseconds);
        switch (finalStatus)
        {
            case TestJobStatus.Completed:
                Interlocked.Increment(ref stats.Completed);
                break;
            case TestJobStatus.Timeout:
                Interlocked.Increment(ref stats.TimedOut);
                break;
            default:
                Interlocked.Increment(ref stats.Failed);
                break;
        }

        try
        {
            await ServerConnection.SendAsync(new WebSocketMessage
            {
                Type = MessageType.TestResult,
                Data = new TestResultMessage
                {
                    JobId = assignment.JobId,
                    TestId = assignment.TestId,
                    NodeId = string.Empty,
                    Success = success,
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    Output = output
                }
            });

            await SendStatusUpdateAsync(assignment, finalStatus, output);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to report job {JobId} to server", assignment.JobId);
        }
    }

    private async Task SendStatusUpdateAsync(TestAssignmentMessage assignment, TestJobStatus status, string message)
    {
        await ServerConnection.SendAsync(new WebSocketMessage
        {
            Type = MessageType.TestStatusUpdate,
            Data = new TestStatusUpdateMessage
            {
                JobId = assignment.JobId,
                TestId = assignment.TestId,
                NodeId = string.Empty,
                Status = status,
                Message = message
            }
        });
    }
}
