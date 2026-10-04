using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Obicon.Server.Tests;

/// <summary>
/// Server factory forcing list settings as native arrays through configuration,
/// exactly like appsettings would, so collection settings never depend on
/// JSON-in-string configuration forms.
/// </summary>
public class ForcedListSettingsServerFactory : ObiconServerFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((context, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ServerSettings:EnabledTestTypes:0"] = "Ping",
            ["ServerSettings:EnabledTestTypes:1"] = "Dns",
            ["ServerSettings:FrequencyPresetsSeconds:0"] = "15",
            ["ServerSettings:FrequencyPresetsSeconds:1"] = "45"
        }));
    }
}
