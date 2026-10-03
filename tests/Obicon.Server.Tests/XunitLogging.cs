using System.Globalization;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Xunit.Abstractions;

namespace Obicon.Server.Tests;

/// <summary>
/// Routes the in-memory server's log entries into the output of the test that is
/// currently running. A test activates it through <see cref="LoggedTest"/>; while no
/// scope is active, entries are dropped, so the factory stays silent on its own.
/// </summary>
public static class XunitLogging
{
    private static readonly AsyncLocal<ITestOutputHelper?> CurrentOutput = new();

    /// <summary>
    /// Makes server log entries written during the returned scope's lifetime go to the
    /// given test's console output. Default: entries are dropped until a scope begins.
    /// </summary>
    public static IDisposable Begin(ITestOutputHelper output)
    {
        var previous = CurrentOutput.Value;
        CurrentOutput.Value = output;
        return new RestoreScope(previous);
    }

    /// <summary>
    /// The output of the test that began the current scope. Default: null.
    /// </summary>
    public static ITestOutputHelper? Output => CurrentOutput.Value;

    private sealed class RestoreScope : IDisposable
    {
        private readonly ITestOutputHelper? _previous;

        public RestoreScope(ITestOutputHelper? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            CurrentOutput.Value = _previous;
        }
    }
}

/// <summary>
/// Serilog sink that hands the server's log entries to <see cref="XunitLogging"/> so
/// they end up in the running test's output: shown for failed tests, and for all
/// tests with detailed console verbosity. The test factory attaches it as the
/// "Xunit" sink; entries below Information are not forwarded.
/// </summary>
public sealed class XunitLoggingSink : ILogEventSink
{
    /// <summary>
    /// The single sink instance, attached through the "Xunit" sink name.
    /// </summary>
    public static readonly XunitLoggingSink Instance = new();

    private XunitLoggingSink()
    {
    }

    public void Emit(LogEvent logEvent)
    {
        var output = XunitLogging.Output;
        if (output is null || logEvent.Level < LogEventLevel.Information)
        {
            return;
        }

        var category = logEvent.Properties.TryGetValue("SourceContext", out var sourceContext)
            ? sourceContext.ToString().Trim('"')
            : string.Empty;
        var prefix = category.Length == 0 ? string.Empty : $"{category}: ";

        // Same level abbreviation as the server's own console template
        var level = logEvent.Level.ToString()[..3].ToUpperInvariant();
        output.WriteLine($"[{level}] {prefix}{logEvent.RenderMessage(CultureInfo.InvariantCulture)}");
        if (logEvent.Exception is not null)
        {
            output.WriteLine(logEvent.Exception.ToString());
        }
    }
}

/// <summary>
/// Registers <see cref="XunitLoggingSink"/> under the "Xunit" sink name, so the test
/// factory's Serilog configuration can enable it as a WriteTo entry.
/// </summary>
public static class XunitLoggingSinkExtensions
{
    public static LoggerConfiguration Xunit(this LoggerSinkConfiguration configuration)
    {
        return configuration.Sink(XunitLoggingSink.Instance);
    }
}

/// <summary>
/// Test base class that shows the server's logs in the running test's console output
/// for the test's whole lifetime. Derived classes with their own cleanup override
/// <see cref="DisposeManagedResources"/> instead of implementing IDisposable.
/// </summary>
public abstract class LoggedTest : IDisposable
{
    private readonly IDisposable _logScope;

    protected LoggedTest(ITestOutputHelper output)
    {
        _logScope = XunitLogging.Begin(output);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _logScope.Dispose();
        DisposeManagedResources();
    }

    /// <summary>
    /// Cleanup hook for derived classes; runs after the log scope has ended.
    /// </summary>
    protected virtual void DisposeManagedResources()
    {
    }
}
