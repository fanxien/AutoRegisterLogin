using System.Reflection;
using System.IO;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace AutoRegisterLogin;

[ApiVersion(2, 1)]
public sealed class AutoRegisterLoginPlugin : TerrariaPlugin
{
    private PluginConfig _config = new();
    private IpWhitelist _whitelist = null!;
    private PendingApprovalStore _pendingStore = null!;
    private AuthenticationService _authService = null!;

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
        LoadConfig();

        var whitelistPath = Path.Combine(TShock.SavePath, "AutoRegisterLogin_whitelist.json");
        _whitelist = new IpWhitelist(whitelistPath);
        _whitelist.Load();

        var pendingPath = Path.Combine(TShock.SavePath, "AutoRegisterLogin_pending.json");
        _pendingStore = new PendingApprovalStore(
            pendingPath, _config.PendingApprovalTimeoutHours);
        _pendingStore.Load();

        _authService = new AuthenticationService(_config, _whitelist, _pendingStore);

        ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
        ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreetPlayer);
        GeneralHooks.ReloadEvent += OnReload;

        AdminCommands.Register(_whitelist, _pendingStore);

        TShock.Log.ConsoleInfo("[AutoRegisterLogin] Plugin initialized.");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
            ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreetPlayer);
            GeneralHooks.ReloadEvent -= OnReload;
        }

        base.Dispose(disposing);
    }

    private void OnReload(ReloadEventArgs args)
    {
        LoadConfig();

        var pendingPath = Path.Combine(TShock.SavePath, "AutoRegisterLogin_pending.json");
        _pendingStore = new PendingApprovalStore(
            pendingPath, _config.PendingApprovalTimeoutHours);
        _pendingStore.Load();

        _authService = new AuthenticationService(_config, _whitelist, _pendingStore);

        args.Player?.SendSuccessMessage("[AutoRegisterLogin] Configuration reloaded.");
    }

    private void LoadConfig()
    {
        _config = PluginConfig.Load();
    }

    private void OnJoin(JoinEventArgs args)
    {
        TryAuthenticate(args.Who, "join");
    }

    private void OnGreetPlayer(GreetPlayerEventArgs args)
    {
        TryAuthenticate(args.Who, "greet");
    }

    private void TryAuthenticate(int who, string stage)
    {
        if (who < 0 || who >= TShock.Players.Length)
        {
            return;
        }

        var player = TShock.Players[who];
        if (player == null)
        {
            return;
        }

        _authService.TryAuthenticate(player, stage);
    }
}
