using System.ComponentModel.DataAnnotations;

namespace Obicon.Server.Models.Requests;

/// <summary>
/// Partial update of a test: the fields present are applied, everything else stays.
/// </summary>
public class PatchTestRequest
{
    /// <summary>
    /// New active state of the test. Required; inactive tests are not run by the scheduler.
    /// </summary>
    [Required]
    public bool? IsActive { get; set; }
}
