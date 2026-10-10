using Microsoft.Extensions.Logging;
using Obicon.Server.Services;
using Obicon.Shared.Models.Messages;
using OpenTelemetry.Logs;
using Xunit;

namespace Obicon.Server.Tests;

/// <summary>
/// Tests the node log funnel's attribute contract: a forwarded entry must surface
/// the node's identity, the source context, and the entry's own properties (JobId,
/// TestId) as first-class fields on the exported record - if a field disappears in
/// some bridge or serializer, that is a bug in the funnel, not a config problem.
/// </summary>
public class NodeLogFunnelTests
{
    private static (NodeLogFunnel Funnel, List<LogRecord> Exported) CreateFunnel()
    {
        var exported = new List<LogRecord>();
        var factory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddOpenTelemetry(options => options.AddInMemoryExporter(exported)));
        return (new NodeLogFunnel(factory), exported);
    }

    private static NodeLogMessage CreateEntry()
    {
        return new NodeLogMessage
        {
            NodeName = "fra-1",
            NodeVersion = "0.5.0",
            Level = "Warning",
            Message = "Job finished with a slow response {JobId}",
            Exception = "System.TimeoutException: took too long",
            Properties = new Dictionary<string, string>
            {
                ["SourceContext"] = "\"Obicon.Node.Services.TestExecutor\"",
                ["JobId"] = "44444444-4444-4444-4444-444444444444",
                ["TestId"] = "22222222-2222-2222-2222-222222222222"
            }
        };
    }

    private static Dictionary<string, object?> AttributeMap(LogRecord record)
    {
        return record.Attributes!.ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    [Fact]
    public void Forward_CarriesEveryNodeAttribute()
    {
        var (funnel, exported) = CreateFunnel();
        var entry = CreateEntry();

        funnel.Forward("11111111-1111-1111-1111-111111111111", entry);

        var record = Assert.Single(exported);
        var attributes = AttributeMap(record);
        Assert.Equal("11111111-1111-1111-1111-111111111111", attributes["node_id"]);
        Assert.Equal("fra-1", attributes["node_name"]);
        Assert.Equal("0.5.0", attributes["node_version"]);
        Assert.Equal("Obicon.Node.Services.TestExecutor", attributes["source_context"]);
        Assert.Equal("44444444-4444-4444-4444-444444444444", attributes["JobId"]);
        Assert.Equal("22222222-2222-2222-2222-222222222222", attributes["TestId"]);
    }

    [Fact]
    public void Forward_CarriesMessageBodyLevelAndException()
    {
        var (funnel, exported) = CreateFunnel();
        var entry = CreateEntry();

        funnel.Forward("11111111-1111-1111-1111-111111111111", entry);

        var record = Assert.Single(exported);
        var attributes = AttributeMap(record);
        Assert.Equal(NodeLogFunnel.LoggerCategory, record.CategoryName);
        Assert.Equal(LogLevel.Warning, record.LogLevel);
        Assert.Equal("Job finished with a slow response {JobId}", record.Body);
        Assert.Equal("System.TimeoutException: took too long", attributes["exception"]);
    }

    [Fact]
    public void Forward_MapsEveryLevel()
    {
        var (funnel, exported) = CreateFunnel();
        var entry = CreateEntry();

        funnel.Forward("11111111-1111-1111-1111-111111111111", entry);
        entry.Level = "Error";
        funnel.Forward("11111111-1111-1111-1111-111111111111", entry);
        entry.Level = "Debug";
        funnel.Forward("11111111-1111-1111-1111-111111111111", entry);
        entry.Level = "Information";
        funnel.Forward("11111111-1111-1111-1111-111111111111", entry);
        // Unknown levels fall back to Information
        entry.Level = "Critical";
        funnel.Forward("11111111-1111-1111-1111-111111111111", entry);

        Assert.Equal(
            [LogLevel.Warning, LogLevel.Error, LogLevel.Debug, LogLevel.Information, LogLevel.Information],
            exported.Select(r => r.LogLevel).ToArray());
    }

    [Fact]
    public void Forward_WithoutProperties_StillCarriesNodeIdentity()
    {
        var (funnel, exported) = CreateFunnel();
        var entry = new NodeLogMessage
        {
            NodeName = "bare-node",
            Level = "Information",
            Message = "hello",
            Properties = null
        };

        funnel.Forward("11111111-1111-1111-1111-111111111111", entry);

        var record = Assert.Single(exported);
        var attributes = AttributeMap(record);
        Assert.Equal("11111111-1111-1111-1111-111111111111", attributes["node_id"]);
        Assert.Equal("bare-node", attributes["node_name"]);
        Assert.Equal("unknown", attributes["source_context"]);
    }
}
