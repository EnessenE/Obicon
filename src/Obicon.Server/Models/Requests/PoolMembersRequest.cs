namespace Obicon.Server.Models.Requests;

/// <summary>
/// Replaces the member list of a node pool.
/// </summary>
public class PoolMembersRequest
{
    /// <summary>
    /// IDs of the nodes that should be in the pool, replacing the current members.
    /// Node IDs that do not exist are rejected. Default: empty list.
    /// </summary>
    public List<Guid> NodeIds { get; set; } = new();
}
