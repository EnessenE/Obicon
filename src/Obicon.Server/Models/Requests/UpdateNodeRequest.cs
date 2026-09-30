using System.ComponentModel.DataAnnotations;

namespace Obicon.Server.Models.Requests;

/// <summary>
/// Updates a node: rename, set labels, and optionally regenerate the auth token.
/// </summary>
public class UpdateNodeRequest
{
    /// <summary>
    /// New human-readable name for the node. Required, at least 1 character.
    /// </summary>
    [Required(AllowEmptyStrings = false), MinLength(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// New set of labels for the node, replacing the current ones. Default: empty list.
    /// </summary>
    public List<string> Labels { get; set; } = new();

    /// <summary>
    /// If true, generates a new auth token; the old token expires immediately
    /// and any live connection using it is closed. Default: false.
    /// </summary>
    public bool RegenerateToken { get; set; }
}
