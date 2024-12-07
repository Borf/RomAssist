using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace RomAssistant.Models;
public class Config
{
    IConfiguration configuration;

    public Config(IConfiguration configurationManager)
    {
        this.configuration = configurationManager;
        configuration.Bind(this);
    }

    private ulong _regLogChannel = 0;
    public ulong RegLogChannel { get => _regLogChannel; set => setProperty(value, nameof(RegLogChannel), ref _regLogChannel); }



    private void setProperty<T>(T value, string key, ref T property)
    {
        property = value;
        configuration[key] = value?.ToString();
        Save();
    }

    public void Save()
    {
        Program.Log(Module.Config, "Saving custom config");
        File.WriteAllText(Path.Combine("data", "CustomizableSettings.json"), JsonSerializer.Serialize(this));
    }
}
