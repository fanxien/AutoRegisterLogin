using System.Reflection;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace AutoRegisterLogin;

[ApiVersion(2, 1)]
public sealed class AutoRegisterLoginPlugin : TerrariaPlugin
{
    private PluginConfig _config = new();

    public AutoRegisterLoginPlugin(Main game)
        : base(game)
    {
        Order = 1000;
    }

    public override string Name => "AutoRegisterLogin";

    public override string Author => "槐序二七";

    public override string Description =>
        "Automatically registers new players and logs them in with IP whitelist + UUID verification.";

    public override Version Version =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);

    public override void Initialize()
    {
        _config = PluginConfig.Load();
        TShock.Log.ConsoleInfo("[AutoRegisterLogin] Plugin initialized.");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}
