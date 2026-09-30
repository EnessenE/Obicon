using Obicon.Node.Services;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Obicon.Node.Tests;

/// <summary>
/// Unit tests for the logging policy state: the node's override wins over the
/// server's default, a disabled policy mutes only test-related output, and
/// lifecycle logs are never muted.
/// </summary>
public class NodeLoggingStateTests
{
    private static LogEvent CreateEvent(string sourceContext)
    {
        var properties = sourceContext == null
            ? new List<LogEventProperty>()
            : [new("SourceContext", new ScalarValue(sourceContext))];
        return new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            new MessageTemplateParser().Parse("test"),
            properties);
    }

    [Theory]
    [InlineData(true, false, false)]   // node override wins over server default
    [InlineData(false, true, true)]   // override wins the other way too
    public void ApplyLocalLoggingPolicy_NodeOverrideWins(bool? nodeOverride, bool serverDefault, bool expectedTestsMuted)
    {
        var state = new NodeLoggingState();

        state.ApplyLocalLoggingPolicy(nodeOverride, serverDefault);

        Assert.Equal(!expectedTestsMuted, state.LastAppliedLocalLogging);
        Assert.Equal(expectedTestsMuted, state.TestsMuted);
    }

    [Theory]
    [InlineData(false, true)]  // follows the server: enabled
    [InlineData(true, false)]  // follows the server: muted
    public void ApplyLocalLoggingPolicy_NullOverrideFollowsServer(bool serverDefault, bool expectedTestsMuted)
    {
        var state = new NodeLoggingState();

        state.ApplyLocalLoggingPolicy(null, serverDefault);

        Assert.Equal(expectedTestsMuted, state.TestsMuted);
    }

    [Theory]
    [InlineData("Obicon.Node.Services.TestExecutor", true)]
    [InlineData("Obicon.Node.Services.TestRunners.HttpTestRunner", true)]
    [InlineData("Obicon.Node.Services.TestRunners.DnsTestRunner", true)]
    [InlineData("Obicon.Node.Services.MonitoringService", true)]
    [InlineData("Obicon.Node.Services.ServerConnection", false)]
    [InlineData("Obicon.Node.Services.NodeLogShipper", false)]
    [InlineData(null, false)]
    public void IsTestRelated_ClassifiesBySourceContext(string? sourceContext, bool expected)
    {
        var logEvent = CreateEvent(sourceContext!);

        Assert.Equal(expected, NodeLoggingState.IsTestRelated(logEvent));
    }

    [Fact]
    public void ShouldMuteLocally_OnlyMutesTestLogs_WhilePolicyIsDisabled()
    {
        var state = new NodeLoggingState();
        var testLog = CreateEvent("Obicon.Node.Services.TestExecutor");
        var lifecycleLog = CreateEvent("Obicon.Node.Services.ServerConnection");

        // Enabled policy: nothing is muted
        state.ApplyLocalLoggingPolicy(null, true);
        Assert.False(state.ShouldMuteLocally(testLog));
        Assert.False(state.ShouldMuteLocally(lifecycleLog));

        // Disabled policy: test logs mute, lifecycle logs never do
        state.ApplyLocalLoggingPolicy(null, false);
        Assert.True(state.ShouldMuteLocally(testLog));
        Assert.False(state.ShouldMuteLocally(lifecycleLog));
    }

    [Fact]
    public void IsTestRelated_HonorsTheTestActivityMarker()
    {
        // ServerConnection logs both lifecycle lines and job assignments; only the
        // assignment calls carry the marker and must classify as test activity
        var plain = CreateEvent("Obicon.Node.Services.ServerConnection");
        var marked = new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            new MessageTemplateParser().Parse("assigned"),
            [new LogEventProperty(NodeLoggingState.TestActivityProperty, new ScalarValue(true))]);

        Assert.False(NodeLoggingState.IsTestRelated(plain));
        Assert.True(NodeLoggingState.IsTestRelated(marked));
    }

    [Fact]
    public void PolicyStarts_Conservative()
    {
        var state = new NodeLoggingState();

        // No hello received yet: shipping is off until the server allows it,
        // and local logging is on by default
        Assert.False(state.ServerAllowsLogShipping);
        Assert.False(state.TestsMuted);
        Assert.True(state.ServerLocalLoggingEnabled);
    }
}
