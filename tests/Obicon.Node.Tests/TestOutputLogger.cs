using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Obicon.Node.Tests;

/// <summary>
/// ILogger that writes every entry to the xUnit test output, so services under test
/// can show their logs in the test console. Pass it to any constructor taking an
/// ILogger; entries land in the output of the test that ran the service.
/// </summary>
public sealed class TestOutputLogger : ILogger
{
    private readonly ITestOutputHelper _output;

    public TestOutputLogger(ITestOutputHelper output)
    {
        _output = output;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // Same level abbreviation as the node's own console template
        var level = logLevel.ToString()[..3].ToUpperInvariant();
        _output.WriteLine($"[{level}] {formatter(state, exception)}");
        if (exception is not null)
        {
            _output.WriteLine(exception.ToString());
        }
    }
}
