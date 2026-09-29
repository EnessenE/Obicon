using System.ComponentModel.DataAnnotations;

namespace Obicon.Server.Models.Requests;

/// <summary>
/// Renames a node pool.
/// </summary>
public class UpdatePoolRequest
{
    /// <summary>
    /// New human-readable name for the pool. Required, at least 1 character.
    /// </summary>
    [Required(AllowEmptyStrings = false), MinLength(1)]
    public string Name { get; set; } = string.Empty;
}
