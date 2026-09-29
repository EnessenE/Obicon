using System.ComponentModel.DataAnnotations;

namespace Obicon.Server.Models.Requests;

/// <summary>
/// A node registering itself on the server using an enroll token.
/// Requires the NodeAutoEnrollmentEnabled server setting.
/// </summary>
public class EnrollRequest
{
    /// <summary>
    /// Plain enroll token issued via POST /v1/enroll-tokens. Required.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string EnrollToken { get; set; } = string.Empty;

    /// <summary>
    /// ID of an already enrolled node, to update its information instead of creating a new one. Optional.
    /// </summary>
    public Guid? NodeId { get; set; }

    /// <summary>
    /// Human-readable name of the node. Required, at least 1 character.
    /// </summary>
    [Required(AllowEmptyStrings = false), MinLength(1)]
    public string NodeName { get; set; } = string.Empty;

    /// <summary>
    /// Labels the node attaches to itself. Default: empty list.
    /// </summary>
    public List<string> Labels { get; set; } = new();

    /// <summary>
    /// Names of pools the node puts itself into; missing pools are created. Default: empty list.
    /// </summary>
    public List<string> Pools { get; set; } = new();
}
