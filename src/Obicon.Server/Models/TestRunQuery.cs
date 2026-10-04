using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Models;

/// <summary>
/// A page request for finished and in-flight test runs: page window plus optional
/// server-side filters. Search matches the target, output, and error message.
/// </summary>
/// <param name="Limit">Page size, 1 to 500.</param>
/// <param name="Offset">Zero-based offset of the first run in the page.</param>
/// <param name="Status">Only runs with this status. Null for every status.</param>
/// <param name="NodeId">Only runs executed by this node. Null for every node.</param>
/// <param name="TestId">Only runs of this test. Null for every test.</param>
/// <param name="Search">Case-insensitive text matched against target, output, and error message. Null for no search.</param>
public record TestRunQuery(int Limit, int Offset, TestJobStatus? Status, Guid? NodeId, Guid? TestId, string? Search);
