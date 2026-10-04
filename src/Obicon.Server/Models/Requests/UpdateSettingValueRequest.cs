using System.Text.Json;

namespace Obicon.Server.Models.Requests;

/// <summary>
/// New value for a server setting.
/// </summary>
public class UpdateSettingValueRequest
{
    /// <summary>
    /// New value as raw JSON: a string for scalar settings, a native array for
    /// collection settings. Converted to the setting's type. Required.
    /// </summary>
    public JsonElement Value { get; set; }
}
