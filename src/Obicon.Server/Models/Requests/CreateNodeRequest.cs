using System.ComponentModel.DataAnnotations;

namespace Obicon.Server.Models.Requests;

public class CreateNodeRequest
{
    /// <summary>
    /// Human-readable name of the node to create. Required, at least 1 character.
    /// </summary>
    [Required(AllowEmptyStrings = false), MinLength(1)]
    public string Name { get; set; } = string.Empty;
}
