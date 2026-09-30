using System.ComponentModel.DataAnnotations;

namespace Obicon.Server.Models.Requests;

/// <summary>
/// Creates a node pool.
/// </summary>
public class CreatePoolRequest
{
    /// <summary>
    /// Human-readable name of the pool. Required, at least 1 character.
    /// </summary>
    [Required(AllowEmptyStrings = false), MinLength(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Free-form description of the pool, shown in the UI. Optional. Default: empty string.
    /// </summary>
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
}
