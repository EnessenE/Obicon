namespace Obicon.Server.Models.Requests;

public class CreateNodeRequest
{
    /// <summary>
    /// Human-readable name of the node to create. Default: empty string.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
