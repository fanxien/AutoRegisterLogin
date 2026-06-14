using TShockAPI;

namespace AutoRegisterLogin;

public static class AdminCommands
{
    private static IpWhitelist? _whitelist;
    private static PendingApprovalStore? _pendingStore;

    public static void Register(IpWhitelist whitelist, PendingApprovalStore pendingStore)
    {
        _whitelist = whitelist;
        _pendingStore = pendingStore;

        Commands.ChatCommands.Add(
            new Command("autoregisterlogin.admin", ApproveIp, "approveip"));
        Commands.ChatCommands.Add(
            new Command("autoregisterlogin.admin", ListIps, "ipwhitelist"));
        Commands.ChatCommands.Add(
            new Command("autoregisterlogin.admin", RemoveIp, "removeip"));
    }

    private static void ApproveIp(CommandArgs args)
    {
        if (_whitelist == null || _pendingStore == null) return;

        if (args.Parameters.Count < 1)
        {
            args.Player.SendErrorMessage("Usage: /approveip <player name>");
            return;
        }

        var playerName = args.Parameters[0];
        var pending = _pendingStore.Get(playerName);

        if (pending == null)
        {
            args.Player.SendErrorMessage(
                $"[AutoRegisterLogin] No pending approval for '{playerName}'.");
            return;
        }

        foreach (var ip in pending.ExistingIps)
        {
            _whitelist.AddIp(playerName, ip);
        }

        _whitelist.AddIp(playerName, pending.NewIp);
        _whitelist.Save();
        _pendingStore.Remove(playerName);
        _pendingStore.Save();

        args.Player.SendSuccessMessage(
            $"[AutoRegisterLogin] Approved IP change for '{playerName}'. " +
            "Old and new IPs whitelisted.");

        TShock.Log.ConsoleInfo(
            $"[AutoRegisterLogin] {args.Player.Name} approved IP change for {playerName}.");
    }

    private static void ListIps(CommandArgs args)
    {
        if (_whitelist == null) return;

        if (args.Parameters.Count < 1)
        {
            args.Player.SendErrorMessage("Usage: /ipwhitelist <player name>");
            return;
        }

        var playerName = args.Parameters[0];
        var ips = _whitelist.GetIps(playerName);

        if (ips.Count == 0)
        {
            args.Player.SendInfoMessage(
                $"[AutoRegisterLogin] No whitelisted IPs for '{playerName}'.");
            return;
        }

        args.Player.SendInfoMessage(
            $"[AutoRegisterLogin] Whitelisted IPs for '{playerName}': {string.Join(", ", ips)}");
    }

    private static void RemoveIp(CommandArgs args)
    {
        if (_whitelist == null) return;

        if (args.Parameters.Count < 2)
        {
            args.Player.SendErrorMessage("Usage: /removeip <player name> <ip>");
            return;
        }

        var playerName = args.Parameters[0];
        var ip = args.Parameters[1];

        _whitelist.RemoveIp(playerName, ip);
        _whitelist.Save();

        args.Player.SendSuccessMessage(
            $"[AutoRegisterLogin] Removed IP '{ip}' from '{playerName}'s whitelist.");

        TShock.Log.ConsoleInfo(
            $"[AutoRegisterLogin] {args.Player.Name} removed IP {ip} from {playerName}.");
    }
}
