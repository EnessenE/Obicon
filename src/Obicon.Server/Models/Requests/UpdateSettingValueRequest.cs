namespace Obicon.Server.Models.Requests;

/// <summary>
/// New value for a server setting.
/// </summary>
public class UpdateSettingValueRequest
{
    /// <summary>
    /// New value as string; converted to the setting's type. Required.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
