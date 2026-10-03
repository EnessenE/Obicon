using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Obicon.Node.Configuration;
using Obicon.Node.Services.TestRunners;
using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Messages;

namespace Obicon.Node.Services;

/// <summary>
/// Executes test assignments. Each assignment runs on its own task, capped at
/// MaxConcurrentTests concurrent executions. A test task destroys itself at
/// most [test timeout] + 5 seconds after it starts running.
/// </summary>
public partial class TestExecutor : ITestExecutor, IDisposable
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

    public async Task ExecuteAssignmentAsync(TestAssignmentMessage assignment)
    {
        // Acknowledge immediately so the server knows the run arrived here,
        // even while waiting for a free execution slot
        await SendStatusUpdateAsync(assignment, TestJobStatus.Assigned, "Job accepted by node");

        // Dedicated task per test; the executor only gates and tracks them
        _ = RunAssignmentAsync(assignment);
    }

    private async Task RunAssignmentAsync(TestAssignmentMessage assignment)
    {
        // Everything logged inside this scope carries the job's identifiers
        using var _ = _logger.BeginScope(new Dictionary<string, object>
        {
            ["JobId"] = assignment.JobId,
            ["TestId"] = assignment.TestId,
            ["TestType"] = assignment.TestType.ToString(),
            ["Target"] = assignment.Target,
            ["IpVersion"] = assignment.IpVersion.ToString()
        });
        LogStarting(assignment.TestType, assignment.Target, assignment.TimeoutSeconds);

        var stats = Statistics;
        stats.IncrementPending();
        try
        {
            await _slots.WaitAsync(_lifetime.ApplicationStopping);
        }
        catch (OperationCanceledException)
        {
            stats.DecrementPending();
            return;
        }

        stats.DecrementPending();
        stats.IncrementRunning();
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
                LogNoRunnerRegistered(assignment.TestType);
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

            await ReportAsync(assignment, outcome.Success, stopwatch, outcome.Output, finalStatus, outcome.Metrics);
        }
        catch (OperationCanceledException) when (hardKill?.IsCancellationRequested == true
                                                && !_lifetime.ApplicationStopping.IsCancellationRequested)
        {
            LogHardKilled(assignment.JobId);
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
            LogJobFailed(assignment.JobId, ex);
            await ReportAsync(assignment, success: false, stopwatch, $"Execution error: {ex.Message}", TestJobStatus.Failed);
        }
        finally
        {
            hardKill?.Dispose();
            testTimeout?.Dispose();
            stats.DecrementRunning();
            _slots.Release();
        }
    }

    private async Task ReportAsync(
        TestAssignmentMessage assignment,
        bool success,
        Stopwatch stopwatch,
        string output,
        TestJobStatus finalStatus,
        Dictionary<string, object>? metrics = null)
    {
        var stats = Statistics;
        Metrics.NodeMetrics.TestExecuted(finalStatus.ToString(), assignment.TestType.ToString(), stopwatch.Elapsed.TotalMilliseconds);
        switch (finalStatus)
        {
            case TestJobStatus.Completed:
                stats.IncrementCompleted();
                break;
            case TestJobStatus.Timeout:
                stats.IncrementTimedOut();
                break;
            default:
                stats.IncrementFailed();
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
                    Output = output,
                    Metrics = metrics
                }
            });

            await SendStatusUpdateAsync(assignment, finalStatus, output);
        }
        catch (Exception ex)
        {
            LogReportFailed(assignment.JobId, ex);
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

    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _slots.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting {TestType} test against {Target} (timeout {TimeoutSeconds}s)")]
    private partial void LogStarting(TestType testType, string target, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No runner registered for test type {TestType}")]
    private partial void LogNoRunnerRegistered(TestType testType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Job {JobId} destroyed after exceeding its hard time limit")]
    private partial void LogHardKilled(string jobId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Job {JobId} failed unexpectedly")]
    private partial void LogJobFailed(string jobId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to report job {JobId} to server")]
    private partial void LogReportFailed(string jobId, Exception exception);
}
