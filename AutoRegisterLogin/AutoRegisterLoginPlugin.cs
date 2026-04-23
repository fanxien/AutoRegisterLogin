using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Xna.Framework;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;

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

    public override string Description => "Automatically registers new players and logs them in with UUID-aware checks.";

    public override Version Version => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);

    public override void Initialize()
    {
        LoadConfig();
        ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
        ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreetPlayer);
        GeneralHooks.ReloadEvent += OnReload;
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
        args.Player?.SendSuccessMessage("[AutoRegisterLogin] Configuration reloaded.");
    }

    private void LoadConfig()
    {
        _config = PluginConfig.Load();
    }

    private void OnJoin(JoinEventArgs args)
    {
        TryAutoAuthenticate(args.Who, "join");
    }

    private void OnGreetPlayer(GreetPlayerEventArgs args)
    {
        TryAutoAuthenticate(args.Who, "greet");
    }

    private void TryAutoAuthenticate(int who, string stage)
    {
        if (!_config.Enabled || who < 0 || who >= TShock.Players.Length)
        {
            return;
        }

        var player = TShock.Players[who];
        if (player == null || player.IsLoggedIn)
        {
            return;
        }

        if (!CanAttemptAutoLogin(player))
        {
            return;
        }

        try
        {
            var account = TShock.UserAccounts.GetUserAccountByName(player.Name);
            var createdNow = false;

            if (account == null && _config.AutoRegisterNewPlayers)
            {
                account = RegisterAccountFor(player);
                createdNow = account != null;

                if (account == null)
                {
                    return;
                }
            }

            if (account == null || (!createdNow && !_config.AutoLoginExistingPlayers))
            {
                return;
            }

            if (!createdNow && !CanUseExistingAccount(player, account))
            {
                return;
            }

            if (ApplyLogin(player, account))
            {
                if (_config.SendPlayerMessages && stage == "greet")
                {
                    if (createdNow)
                    {
                        player.SendSuccessMessage("[AutoRegisterLogin] Your account has been created and logged in automatically.");
                    }
                    else
                    {
                        player.SendSuccessMessage("[AutoRegisterLogin] You have been logged in automatically.");
                    }
                }

                TShock.Log.ConsoleInfo($"[AutoRegisterLogin] {player.Name} auto-authenticated successfully during {stage}.");
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[AutoRegisterLogin] Failed to process player '{player.Name}': {ex}");
        }
    }

    private static bool CanAttemptAutoLogin(TSPlayer player)
    {
        if (player.TPlayer.dead)
        {
            return false;
        }

        if (player.TPlayer.itemTime > 0 || player.TPlayer.itemAnimation > 0)
        {
            return false;
        }

        if (player.TPlayer.CCed && Main.ServerSideCharacter)
        {
            return false;
        }

        return true;
    }

    private UserAccount? RegisterAccountFor(TSPlayer player)
    {
        var groupName = string.IsNullOrWhiteSpace(_config.DefaultGroupName) ? "default" : _config.DefaultGroupName.Trim();
        var group = TShock.Groups.GetGroupByName(groupName);
        if (group == null)
        {
            TShock.Log.ConsoleError($"[AutoRegisterLogin] Group '{groupName}' does not exist. Cannot auto-register {player.Name}.");
            return null;
        }

        var account = new UserAccount
        {
            Name = player.Name,
            Group = group.Name,
            UUID = _config.BindUuidOnRegister ? player.UUID ?? string.Empty : string.Empty
        };

        account.CreateBCryptHash(GeneratePassword());
        TShock.UserAccounts.AddUserAccount(account);

        var storedAccount = TShock.UserAccounts.GetUserAccountByName(player.Name);
        if (storedAccount == null)
        {
            TShock.Log.ConsoleError($"[AutoRegisterLogin] Account for {player.Name} was created but could not be reloaded.");
            return null;
        }

        TShock.Log.ConsoleInfo($"[AutoRegisterLogin] Registered new account for {player.Name} in group '{group.Name}'.");
        return storedAccount;
    }

    private bool CanUseExistingAccount(TSPlayer player, UserAccount account)
    {
        if (!_config.RequireMatchingUuidForExistingAccounts)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(player.UUID) || string.IsNullOrWhiteSpace(account.UUID))
        {
            return false;
        }

        return string.Equals(player.UUID, account.UUID, StringComparison.Ordinal);
    }

    private static bool ApplyLogin(TSPlayer player, UserAccount account)
    {
        if (PlayerHooks.OnPlayerPreLogin(player, account.Name, string.Empty))
        {
            return false;
        }

        var group = TShock.Groups.GetGroupByName(account.Group);
        if (!TShock.Groups.AssertGroupValid(player, group, false))
        {
            return false;
        }

        player.PlayerData = TShock.CharacterDB.GetPlayerData(player, account.ID);
        if (Main.ServerSideCharacter && TShock.CharacterDB.IsSeededAppearanceMissing(player.PlayerData))
        {
            TShock.CharacterDB.SyncSeededAppearance(account, player);
            player.PlayerData = TShock.CharacterDB.GetPlayerData(player, account.ID);
        }

        player.Group = group;
        player.tempGroup = null;
        player.Account = account;
        player.IsLoggedIn = true;
        player.IsDisabledForSSC = false;
        player.LoginFailsBySsi = false;
        player.LoginHarassed = false;

        if (Main.ServerSideCharacter)
        {
            if (player.HasPermission(Permissions.bypassssc))
            {
                player.PlayerData.CopyCharacter(player);
                TShock.CharacterDB.InsertPlayerData(player);
            }

            player.PlayerData.RestoreCharacter(player);
        }

        if (player.HasPermission(Permissions.ignorestackhackdetection))
        {
            player.IsDisabledForStackDetection = false;
        }

        if (player.HasPermission(Permissions.usebanneditem))
        {
            player.IsDisabledForBannedWearable = false;
        }

        if (!string.IsNullOrWhiteSpace(player.UUID))
        {
            TShock.UserAccounts.SetUserAccountUUID(account, player.UUID);
            account.UUID = player.UUID;
        }

        if (TShock.Config.Settings.RememberLeavePos && TShock.RememberedPos.GetLeavePos(player.Name, player.IP) != Vector2.Zero)
        {
            var pos = TShock.RememberedPos.GetLeavePos(player.Name, player.IP);
            player.Teleport((int)pos.X * 16, (int)pos.Y * 16);
        }

        PlayerHooks.OnPlayerPostLogin(player);
        return true;
    }

    private string GeneratePassword()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(_config.GeneratedPasswordBytes));
    }
}
