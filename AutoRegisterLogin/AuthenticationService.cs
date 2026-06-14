using System.Security.Cryptography;
using Microsoft.Xna.Framework;
using Terraria;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;

namespace AutoRegisterLogin;

public sealed class AuthenticationService
{
    private readonly PluginConfig _config;
    private readonly IpWhitelist _whitelist;
    private readonly PendingApprovalStore _pendingStore;

    public AuthenticationService(
        PluginConfig config,
        IpWhitelist whitelist,
        PendingApprovalStore pendingStore)
    {
        _config = config;
        _whitelist = whitelist;
        _pendingStore = pendingStore;
    }

    public void TryAuthenticate(TSPlayer player, string stage)
    {
        if (!_config.Enabled || player.IsLoggedIn)
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

            if (account == null)
            {
                HandleNewPlayer(player, stage);
            }
            else
            {
                HandleExistingPlayer(player, account, stage);
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError(
                $"[AutoRegisterLogin] Failed to process player '{player.Name}': {ex}");
        }
    }

    private void HandleNewPlayer(TSPlayer player, string stage)
    {
        if (!_config.AutoRegisterNewPlayers)
        {
            return;
        }

        var account = RegisterAccount(player);
        if (account == null)
        {
            return;
        }

        _whitelist.AddIp(player.Name, player.IP);
        _whitelist.Save();

        if (ApplyLogin(player, account))
        {
            if (_config.SendPlayerMessages && stage == "greet")
            {
                player.SendSuccessMessage(
                    "[AutoRegisterLogin] Your account has been created and logged in automatically.");
            }

            TShock.Log.ConsoleInfo(
                $"[AutoRegisterLogin] {player.Name} registered and auto-authenticated.");
        }
    }

    private void HandleExistingPlayer(TSPlayer player, UserAccount account, string stage)
    {
        if (_whitelist.IsWhitelisted(player.Name, player.IP))
        {
            if (TryUuidMatch(player, account) && ApplyLogin(player, account))
            {
                if (_config.SendPlayerMessages && stage == "greet")
                {
                    player.SendSuccessMessage(
                        "[AutoRegisterLogin] You have been logged in automatically.");
                }

                TShock.Log.ConsoleInfo(
                    $"[AutoRegisterLogin] {player.Name} auto-authenticated.");
            }

            return;
        }

        _pendingStore.CleanExpired();
        _pendingStore.Add(new PendingApproval
        {
            PlayerName = player.Name,
            ExistingIps = _whitelist.GetIps(player.Name),
            NewIp = player.IP,
            RequestedAt = DateTime.UtcNow
        });
        _pendingStore.Save();

        var oldIps = string.Join(", ", _whitelist.GetIps(player.Name));
        player.Disconnect(
            $"[AutoRegisterLogin] Your IP has changed. Old: {oldIps} -> New: {player.IP}. " +
            "Please contact an admin for approval.");

        TShock.Log.ConsoleInfo(
            $"[AutoRegisterLogin] {player.Name} blocked: IP {player.IP} not whitelisted. Pending approval.");
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

    private static bool TryUuidMatch(TSPlayer player, UserAccount account)
    {
        if (string.IsNullOrWhiteSpace(player.UUID) || string.IsNullOrWhiteSpace(account.UUID))
        {
            return false;
        }

        return string.Equals(player.UUID, account.UUID, StringComparison.Ordinal);
    }

    private UserAccount? RegisterAccount(TSPlayer player)
    {
        var groupName = string.IsNullOrWhiteSpace(_config.DefaultGroupName)
            ? "default"
            : _config.DefaultGroupName.Trim();

        var group = TShock.Groups.GetGroupByName(groupName);
        if (group == null)
        {
            TShock.Log.ConsoleError(
                $"[AutoRegisterLogin] Group '{groupName}' does not exist. " +
                $"Cannot auto-register {player.Name}.");
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
            TShock.Log.ConsoleError(
                $"[AutoRegisterLogin] Account for {player.Name} was created but could not be reloaded.");
            return null;
        }

        TShock.Log.ConsoleInfo(
            $"[AutoRegisterLogin] Registered new account for {player.Name} in group '{group.Name}'.");
        return storedAccount;
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

        if (Main.ServerSideCharacter &&
            TShock.CharacterDB.IsSeededAppearanceMissing(player.PlayerData))
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
        }

        if (TShock.Config.Settings.RememberLeavePos &&
            TShock.RememberedPos.GetLeavePos(player.Name, player.IP) != Vector2.Zero)
        {
            var pos = TShock.RememberedPos.GetLeavePos(player.Name, player.IP);
            player.Teleport((int)pos.X * 16, (int)pos.Y * 16);
        }

        PlayerHooks.OnPlayerPostLogin(player);
        return true;
    }

    private static string GeneratePassword()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
    }
}
