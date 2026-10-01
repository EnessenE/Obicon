using Microsoft.Extensions.Options;
using Obicon.Node.Configuration;
using Obicon.Node.Services;
using Obicon.Shared.Models.Messages;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Obicon.Node.Tests;

/// <summary>
/// Unit tests for the log capture sink: metadata capture, level filtering and mapping,
/// feedback-loop protection, and queue behavior.
/// </summary>
public class NodeLogSinkTests : IDisposable
{
    private readonly string _identityPath;
    private readonly NodeIdentityStore _identityStore;

    public NodeLogSinkTests()
    {
        _identityPath = Path.Combine(Path.GetTempPath(), $"node-identity-test-{Guid.NewGuid():N}.json");
        _identityStore = new NodeIdentityStore(_identityPath);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try
        {
            File.Delete(_identityPath);
        }
        catch (IOException)
        {
            // best effort cleanup
        }
    }

    private NodeLogSink CreateSink(NodeSettings? settings = null)
    {
        settings ??= new NodeSettings();
        return new NodeLogSink(Options.Create(settings), _identityStore);
    }

    private static LogEvent CreateEvent(
        LogEventLevel level = LogEventLevel.Information,
        string message = "test message",
        Exception? exception = null,
        Dictionary<string, LogEventPropertyValue>? properties = null)
    {
        var template = new MessageTemplateParser().Parse(message);
        var eventProperties = (properties ?? new Dictionary<string, LogEventPropertyValue>())
            .Select(kv => new LogEventProperty(kv.Key, kv.Value))
            .ToList();
        return new LogEvent(DateTimeOffset.UtcNow, level, exception, template, eventProperties);
    }

    private static ScalarValue Scalar(object value) => new(value);

    [Fact]
    public void Emit_QueuesEntryWithFullMetadata()
    {
        var sink = CreateSink();
        var exception = new InvalidOperationException("boom");
        sink.Emit(CreateEvent(
            properties: new()
            {
                ["SourceContext"] = Scalar("Obicon.Node.Services.TestSource"),
                ["JobId"] = Scalar("44444444-4444-4444-4444-444444444444")
            },
            exception: exception));

        Assert.True(sink.Queue.Reader.TryRead(out var entry));
        Assert.Equal("test message", entry.Message);
        Assert.Equal("Information", entry.Level);
        Assert.Contains("boom", entry.Exception);
        Assert.Equal(NodeInfo.Version, entry.NodeVersion);
        Assert.Equal("Obicon.Node.Services.TestSource", entry.Properties!["SourceContext"]);
        Assert.Equal("44444444-4444-4444-4444-444444444444", entry.Properties!["JobId"]);
    }

    [Fact]
    public void Emit_UsesNodeName_OrMachineName()
    {
        var named = CreateSink(new NodeSettings { NodeName = "pi-1" });
        named.Emit(CreateEvent());
        Assert.True(named.Queue.Reader.TryRead(out var namedEntry));
        Assert.Equal("pi-1", namedEntry.NodeName);

        var unnamed = CreateSink();
        unnamed.Emit(CreateEvent());
        Assert.True(unnamed.Queue.Reader.TryRead(out var unnamedEntry));
        Assert.Equal(Environment.MachineName, unnamedEntry.NodeName);
    }

    [Fact]
    public void Emit_UsesEnrolledNodeId_WhenAvailable()
    {
        _identityStore.Save("6f2d7f6e-0000-0000-0000-000000000001", "token");
        var sink = CreateSink();
        sink.Emit(CreateEvent());
        Assert.True(sink.Queue.Reader.TryRead(out var entry));
        Assert.Equal("6f2d7f6e-0000-0000-0000-000000000001", entry.NodeId);
    }

    [Fact]
    public void Emit_LeavesNodeIdEmpty_BeforeEnrollment()
    {
        var sink = CreateSink();
        sink.Emit(CreateEvent());
        Assert.True(sink.Queue.Reader.TryRead(out var entry));
        Assert.Equal(string.Empty, entry.NodeId);
    }

    [Fact]
    public void Emit_DropsEntriesBelowMinimumLevel()
    {
        var sink = CreateSink(new NodeSettings { LogShippingMinLevel = "Warning" });

        sink.Emit(CreateEvent(LogEventLevel.Information));
        Assert.False(sink.Queue.Reader.TryRead(out _));

        sink.Emit(CreateEvent(LogEventLevel.Warning));
        Assert.True(sink.Queue.Reader.TryRead(out var entry));
        Assert.Equal("Warning", entry.Level);
    }

    [Theory]
    [InlineData(LogEventLevel.Verbose, "Debug")]
    [InlineData(LogEventLevel.Debug, "Debug")]
    [InlineData(LogEventLevel.Information, "Information")]
    [InlineData(LogEventLevel.Warning, "Warning")]
    [InlineData(LogEventLevel.Error, "Error")]
    [InlineData(LogEventLevel.Fatal, "Error")]
    public void Emit_MapsSerilogLevels_ToMessageLevels(LogEventLevel level, string expected)
    {
        var sink = CreateSink(new NodeSettings { LogShippingMinLevel = "Verbose" });
        sink.Emit(CreateEvent(level));
        Assert.True(sink.Queue.Reader.TryRead(out var entry));
        Assert.Equal(expected, entry.Level);
    }

    [Fact]
    public void Emit_SkipsShipperOwnLogs_ToAvoidFeedbackLoops()
    {
        var sink = CreateSink();

        sink.Emit(CreateEvent(properties: new()
        {
            ["SourceContext"] = Scalar(NodeLogSink.ShippingSourcePrefix)
        }));
        Assert.False(sink.Queue.Reader.TryRead(out _));

        // A different source still ships
        sink.Emit(CreateEvent(properties: new() { ["SourceContext"] = Scalar("Obicon.Node.Services.Other") }));
        Assert.True(sink.Queue.Reader.TryRead(out _));
    }

    [Fact]
    public void Emit_TruncatesLongPropertyValues()
    {
        var sink = CreateSink();
        var longValue = new string('x', 1000);

        sink.Emit(CreateEvent(properties: new() { ["Big"] = Scalar(longValue) }));

        Assert.True(sink.Queue.Reader.TryRead(out var entry));
        var value = entry.Properties!["Big"];
        // Capped around the 500-character limit (rendering quotes are trimmed) and marked truncated
        Assert.True(value.Length <= 501, $"value should be capped, was {value.Length}");
        Assert.StartsWith("x", value);
        Assert.EndsWith("…", value);
    }

    [Fact]
    public void Emit_RendersMessageTemplate_WithPlaceholders()
    {
        var sink = CreateSink();
        sink.Emit(CreateEvent(message: "Connected to {Host} after {Delay} seconds"));

        // The template itself is the rendered message; property values appear via RenderMessage
        Assert.True(sink.Queue.Reader.TryRead(out var entry));
        Assert.Equal("Connected to {Host} after {Delay} seconds", entry.Message);
    }

    [Fact]
    public void Emit_QueueIsBounded_AndDropsOldestWhenFull()
    {
        var sink = CreateSink();

        for (var i = 0; i < 1100; i++)
        {
            sink.Emit(CreateEvent(message: $"entry {i}"));
        }

        var entries = new List<NodeLogMessage>();
        while (sink.Queue.Reader.TryRead(out var entry))
        {
            entries.Add(entry);
        }

        Assert.Equal(1000, entries.Count);
        // The oldest entries were dropped, the newest kept
        Assert.Equal("entry 100", entries[0].Message);
        Assert.Equal("entry 1099", entries[^1].Message);
    }
}
