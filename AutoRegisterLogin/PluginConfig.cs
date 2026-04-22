using System.IO;
using Newtonsoft.Json;
using TShockAPI;

namespace AutoRegisterLogin;

public sealed class PluginConfig
{
    public bool Enabled { get; set; } = true;
    public bool AutoRegisterNewPlayers { get; set; } = true;
    public bool AutoLoginExistingPlayers { get; set; } = true;
    public bool BindUuidOnRegister { get; set; } = true;
    public bool RequireMatchingUuidForExistingAccounts { get; set; } = true;
    public bool SendPlayerMessages { get; set; } = true;
    public string DefaultGroupName { get; set; } = "default";
    public int GeneratedPasswordBytes { get; set; } = 16;

    [JsonIgnore]
    public static string ConfigPath => Path.Combine(TShock.SavePath, "AutoRegisterLogin.json");

    public static PluginConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            var config = new PluginConfig();
            config.Save();
            return config;
        }

        var json = File.ReadAllText(ConfigPath);
        var configFromFile = JsonConvert.DeserializeObject<PluginConfig>(json) ?? new PluginConfig();
        configFromFile.GeneratedPasswordBytes = Math.Clamp(configFromFile.GeneratedPasswordBytes, 8, 64);

        if (string.IsNullOrWhiteSpace(configFromFile.DefaultGroupName))
        {
            configFromFile.DefaultGroupName = "default";
        }

        configFromFile.Save();
        return configFromFile;
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(this, Formatting.Indented));
    }
}
