# IP Whitelist Refactor — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor AutoRegisterLogin plugin from UUID-only auth to IP whitelist + UUID dual verification with admin approval workflow.

**Architecture:** Split the monolithic 275-line plugin into 6 focused files. Plugin is a thin shell that wires dependencies; AuthenticationService orchestrates the flow; IpWhitelist and PendingApprovalStore manage JSON-persisted data; AdminCommands exposes `/approveip`, `/ipwhitelist`, `/removeip`.

**Tech Stack:** .NET 9, TShock 6.1.0, Newtonsoft.Json, xUnit 2.9.3

---

### Task 1: Update PluginConfig to simplified 6-field config

**Files:**
- Modify: `AutoRegisterLogin/PluginConfig.cs` (entire file)

- [ ] **Step 1: Replace PluginConfig.cs with simplified version**

```csharp
using System.IO;
using Newtonsoft.Json;
using TShockAPI;

namespace AutoRegisterLogin;

public sealed class PluginConfig
{
    public bool Enabled { get; set; } = true;
    public bool AutoRegisterNewPlayers { get; set; } = true;
    public bool BindUuidOnRegister { get; set; } = true;
    public bool SendPlayerMessages { get; set; } = true;
    public string DefaultGroupName { get; set; } = "default";
    public int PendingApprovalTimeoutHours { get; set; } = 72;

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

        if (string.IsNullOrWhiteSpace(configFromFile.DefaultGroupName))
        {
            configFromFile.DefaultGroupName = "default";
        }

        if (configFromFile.PendingApprovalTimeoutHours < 1)
        {
            configFromFile.PendingApprovalTimeoutHours = 72;
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
```

- [ ] **Step 2: Verify compilation**

```bash
dotnet build AutoRegisterLogin/AutoRegisterLogin.csproj
```

Expected: Build succeeds (though main plugin will have broken references until later tasks).

- [ ] **Step 3: Commit**

```bash
git add AutoRegisterLogin/PluginConfig.cs
git commit -m "refactor: simplify PluginConfig to 6 fields, remove unsafe toggles"
```

---

### Task 2: Set up xUnit test project

**Files:**
- Create: `AutoRegisterLogin.Tests/AutoRegisterLogin.Tests.csproj`

- [ ] **Step 1: Create test project**

```bash
mkdir -p AutoRegisterLogin.Tests
```

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>AutoRegisterLogin.Tests</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.0.1" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\AutoRegisterLogin\AutoRegisterLogin.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Restore test packages**

```bash
dotnet restore AutoRegisterLogin.Tests/AutoRegisterLogin.Tests.csproj
```

Expected: Packages restored successfully.

- [ ] **Step 3: Verify empty test project builds**

```bash
dotnet build AutoRegisterLogin.Tests/AutoRegisterLogin.Tests.csproj
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add AutoRegisterLogin.Tests/AutoRegisterLogin.Tests.csproj
git commit -m "feat: add xUnit test project"
```

---

### Task 3: Create IpWhitelist with persistence

**Files:**
- Create: `AutoRegisterLogin/IpWhitelist.cs`
- Create: `AutoRegisterLogin.Tests/IpWhitelistTests.cs`

- [ ] **Step 1: Write failing test for IpWhitelist**

```csharp
using Xunit;

namespace AutoRegisterLogin.Tests;

public class IpWhitelistTests
{
    [Fact]
    public void IsWhitelisted_EmptyStore_ReturnsFalse()
    {
        var path = Path.GetTempFileName();
        try
        {
            var wl = new IpWhitelist(path);
            wl.Load();
            Assert.False(wl.IsWhitelisted("Player1", "192.168.1.1"));
        }
        finally { File.Delete(path); }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test AutoRegisterLogin.Tests/AutoRegisterLogin.Tests.csproj --filter "IsWhitelisted_EmptyStore_ReturnsFalse"
```

Expected: FAIL — `IpWhitelist` type not found.

- [ ] **Step 3: Create IpWhitelist.cs implementation**

```csharp
using System.IO;
using Newtonsoft.Json;

namespace AutoRegisterLogin;

public sealed class IpWhitelist
{
    private readonly string _filePath;
    private Dictionary<string, HashSet<string>> _entries = new(StringComparer.OrdinalIgnoreCase);

    public IpWhitelist(string filePath)
    {
        _filePath = filePath;
    }

    public void Load()
    {
        if (!File.Exists(_filePath))
        {
            _entries = new(StringComparer.OrdinalIgnoreCase);
            return;
        }

        var json = File.ReadAllText(_filePath);
        _entries = JsonConvert.DeserializeObject<Dictionary<string, HashSet<string>>>(json)
                   ?? new(StringComparer.OrdinalIgnoreCase);
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(_filePath, JsonConvert.SerializeObject(_entries, Formatting.Indented));
    }

    public bool IsWhitelisted(string playerName, string ip)
    {
        return _entries.TryGetValue(playerName, out var ips) && ips.Contains(ip);
    }

    public List<string> GetIps(string playerName)
    {
        return _entries.TryGetValue(playerName, out var ips) ? ips.ToList() : new List<string>();
    }

    public void AddIp(string playerName, string ip)
    {
        if (!_entries.TryGetValue(playerName, out var ips))
        {
            ips = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _entries[playerName] = ips;
        }

        ips.Add(ip);
    }

    public void RemoveIp(string playerName, string ip)
    {
        if (_entries.TryGetValue(playerName, out var ips))
        {
            ips.Remove(ip);
            if (ips.Count == 0)
            {
                _entries.Remove(playerName);
            }
        }
    }
}
```

- [ ] **Step 4: Run the first test to verify it passes**

```bash
dotnet test AutoRegisterLogin.Tests/AutoRegisterLogin.Tests.csproj --filter "IsWhitelisted_EmptyStore_ReturnsFalse"
```

Expected: PASS

- [ ] **Step 5: Add remaining tests to IpWhitelistTests.cs**

Append these test methods to the existing `IpWhitelistTests` class:

```csharp
    [Fact]
    public void AddIp_ThenIsWhitelisted_ReturnsTrue()
    {
        var path = Path.GetTempFileName();
        try
        {
            var wl = new IpWhitelist(path);
            wl.Load();
            wl.AddIp("Player1", "192.168.1.1");
            Assert.True(wl.IsWhitelisted("Player1", "192.168.1.1"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void AddMultipleIps_AllPresentInGetIps()
    {
        var path = Path.GetTempFileName();
        try
        {
            var wl = new IpWhitelist(path);
            wl.Load();
            wl.AddIp("Player1", "192.168.1.1");
            wl.AddIp("Player1", "10.0.0.1");
            var ips = wl.GetIps("Player1");
            Assert.Contains("192.168.1.1", ips);
            Assert.Contains("10.0.0.1", ips);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void RemoveIp_ThenIsWhitelisted_ReturnsFalse()
    {
        var path = Path.GetTempFileName();
        try
        {
            var wl = new IpWhitelist(path);
            wl.Load();
            wl.AddIp("Player1", "192.168.1.1");
            wl.RemoveIp("Player1", "192.168.1.1");
            Assert.False(wl.IsWhitelisted("Player1", "192.168.1.1"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void RemoveLastIp_GetIps_ReturnsEmpty()
    {
        var path = Path.GetTempFileName();
        try
        {
            var wl = new IpWhitelist(path);
            wl.Load();
            wl.AddIp("Player1", "192.168.1.1");
            wl.RemoveIp("Player1", "192.168.1.1");
            Assert.Empty(wl.GetIps("Player1"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void SaveAndLoad_PreservesData()
    {
        var path = Path.GetTempFileName();
        try
        {
            var wl1 = new IpWhitelist(path);
            wl1.Load();
            wl1.AddIp("Player1", "192.168.1.1");
            wl1.AddIp("Player2", "10.0.0.1");
            wl1.Save();

            var wl2 = new IpWhitelist(path);
            wl2.Load();
            Assert.True(wl2.IsWhitelisted("Player1", "192.168.1.1"));
            Assert.True(wl2.IsWhitelisted("Player2", "10.0.0.1"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void GetIps_NonexistentPlayer_ReturnsEmptyList()
    {
        var path = Path.GetTempFileName();
        try
        {
            var wl = new IpWhitelist(path);
            wl.Load();
            Assert.Empty(wl.GetIps("Nobody"));
        }
        finally { File.Delete(path); }
    }
```

- [ ] **Step 6: Run all IpWhitelist tests**

```bash
dotnet test AutoRegisterLogin.Tests/AutoRegisterLogin.Tests.csproj --filter "IpWhitelistTests"
```

Expected: All 7 tests PASS

- [ ] **Step 7: Commit**

```bash
git add AutoRegisterLogin/IpWhitelist.cs AutoRegisterLogin.Tests/IpWhitelistTests.cs
git commit -m "feat: add IpWhitelist with JSON persistence"
```

---

### Task 4: Create PendingApprovalStore with TTL

**Files:**
- Create: `AutoRegisterLogin/PendingApprovalStore.cs`
- Create: `AutoRegisterLogin.Tests/PendingApprovalStoreTests.cs`

- [ ] **Step 1: Write failing test for PendingApprovalStore**

```csharp
using Xunit;

namespace AutoRegisterLogin.Tests;

public class PendingApprovalStoreTests
{
    private const int TimeoutHours = 72;

    [Fact]
    public void Get_NonexistentPlayer_ReturnsNull()
    {
        var path = Path.GetTempFileName();
        try
        {
            var store = new PendingApprovalStore(path, TimeoutHours);
            store.Load();
            Assert.Null(store.Get("Nobody"));
        }
        finally { File.Delete(path); }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test AutoRegisterLogin.Tests/AutoRegisterLogin.Tests.csproj --filter "Get_NonexistentPlayer_ReturnsNull"
```

Expected: FAIL — `PendingApprovalStore` type not found.

- [ ] **Step 3: Create PendingApprovalStore.cs implementation**

```csharp
using System.IO;
using Newtonsoft.Json;

namespace AutoRegisterLogin;

public sealed class PendingApproval
{
    public string PlayerName { get; set; } = string.Empty;
    public List<string> ExistingIps { get; set; } = new();
    public string NewIp { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
}

public sealed class PendingApprovalStore
{
    private readonly string _filePath;
    private readonly int _timeoutHours;
    private List<PendingApproval> _entries = new();

    public PendingApprovalStore(string filePath, int timeoutHours)
    {
        _filePath = filePath;
        _timeoutHours = timeoutHours;
    }

    public void Load()
    {
        if (!File.Exists(_filePath))
        {
            _entries = new();
            return;
        }

        var json = File.ReadAllText(_filePath);
        _entries = JsonConvert.DeserializeObject<List<PendingApproval>>(json) ?? new();
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(_filePath, JsonConvert.SerializeObject(_entries, Formatting.Indented));
    }

    public void Add(PendingApproval approval)
    {
        _entries.RemoveAll(e =>
            string.Equals(e.PlayerName, approval.PlayerName, StringComparison.OrdinalIgnoreCase));
        _entries.Add(approval);
    }

    public PendingApproval? Get(string playerName)
    {
        return _entries.Find(e =>
            string.Equals(e.PlayerName, playerName, StringComparison.OrdinalIgnoreCase));
    }

    public void Remove(string playerName)
    {
        _entries.RemoveAll(e =>
            string.Equals(e.PlayerName, playerName, StringComparison.OrdinalIgnoreCase));
    }

    public void CleanExpired()
    {
        var cutoff = DateTime.UtcNow.AddHours(-_timeoutHours);
        _entries.RemoveAll(e => e.RequestedAt < cutoff);
    }
}
```

- [ ] **Step 4: Run the first test to verify it passes**

```bash
dotnet test AutoRegisterLogin.Tests/AutoRegisterLogin.Tests.csproj --filter "Get_NonexistentPlayer_ReturnsNull"
```

Expected: PASS

- [ ] **Step 5: Add remaining tests to PendingApprovalStoreTests.cs**

Append these test methods to the existing `PendingApprovalStoreTests` class:

```csharp
    [Fact]
    public void Add_ThenGet_ReturnsApproval()
    {
        var path = Path.GetTempFileName();
        try
        {
            var store = new PendingApprovalStore(path, TimeoutHours);
            store.Load();
            store.Add(new PendingApproval
            {
                PlayerName = "Player1",
                ExistingIps = new List<string> { "192.168.1.1" },
                NewIp = "10.0.0.1",
                RequestedAt = DateTime.UtcNow
            });

            var retrieved = store.Get("Player1");
            Assert.NotNull(retrieved);
            Assert.Equal("10.0.0.1", retrieved!.NewIp);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Add_DuplicatePlayer_ReplacesEntry()
    {
        var path = Path.GetTempFileName();
        try
        {
            var store = new PendingApprovalStore(path, TimeoutHours);
            store.Load();
            store.Add(new PendingApproval
            {
                PlayerName = "Player1",
                NewIp = "10.0.0.1",
                ExistingIps = new List<string> { "192.168.1.1" }
            });
            store.Add(new PendingApproval
            {
                PlayerName = "Player1",
                NewIp = "10.0.0.2",
                ExistingIps = new List<string> { "192.168.1.1" }
            });

            Assert.Equal("10.0.0.2", store.Get("Player1")!.NewIp);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Remove_ThenGet_ReturnsNull()
    {
        var path = Path.GetTempFileName();
        try
        {
            var store = new PendingApprovalStore(path, TimeoutHours);
            store.Load();
            store.Add(new PendingApproval
            {
                PlayerName = "Player1",
                NewIp = "10.0.0.1",
                ExistingIps = new List<string>()
            });
            store.Remove("Player1");
            Assert.Null(store.Get("Player1"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void CleanExpired_RemovesOldEntries_KeepsNewOnes()
    {
        var path = Path.GetTempFileName();
        try
        {
            var store = new PendingApprovalStore(path, 1);
            store.Load();
            store.Add(new PendingApproval
            {
                PlayerName = "OldPlayer",
                NewIp = "10.0.0.1",
                ExistingIps = new List<string>(),
                RequestedAt = DateTime.UtcNow.AddHours(-2)
            });
            store.Add(new PendingApproval
            {
                PlayerName = "NewPlayer",
                NewIp = "10.0.0.2",
                ExistingIps = new List<string>(),
                RequestedAt = DateTime.UtcNow
            });

            store.CleanExpired();
            Assert.Null(store.Get("OldPlayer"));
            Assert.NotNull(store.Get("NewPlayer"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void SaveAndLoad_PreservesData()
    {
        var path = Path.GetTempFileName();
        try
        {
            var store1 = new PendingApprovalStore(path, TimeoutHours);
            store1.Load();
            store1.Add(new PendingApproval
            {
                PlayerName = "Player1",
                NewIp = "10.0.0.1",
                ExistingIps = new List<string> { "192.168.1.1" },
                RequestedAt = DateTime.UtcNow
            });
            store1.Save();

            var store2 = new PendingApprovalStore(path, TimeoutHours);
            store2.Load();
            var retrieved = store2.Get("Player1");
            Assert.NotNull(retrieved);
            Assert.Equal("10.0.0.1", retrieved!.NewIp);
        }
        finally { File.Delete(path); }
    }
```

- [ ] **Step 6: Run all PendingApprovalStore tests**

```bash
dotnet test AutoRegisterLogin.Tests/AutoRegisterLogin.Tests.csproj --filter "PendingApprovalStoreTests"
```

Expected: All 6 tests PASS

- [ ] **Step 7: Commit**

```bash
git add AutoRegisterLogin/PendingApprovalStore.cs AutoRegisterLogin.Tests/PendingApprovalStoreTests.cs
git commit -m "feat: add PendingApprovalStore with TTL cleanup"
```

---

### Task 5: Create AuthenticationService

**Files:**
- Create: `AutoRegisterLogin/AuthenticationService.cs`

- [ ] **Step 1: Create AuthenticationService.cs**

```csharp
using System.Security.Cryptography;
using Microsoft.Xna.Framework;
using Terraria;
using TShockAPI;
using TShockAPI.DB;

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
```

- [ ] **Step 2: Verify compilation (may fail due to not-yet-updated plugin)**

```bash
dotnet build AutoRegisterLogin/AutoRegisterLogin.csproj
```

Expected: May fail with "AutoRegisterLoginPlugin.cs has errors" — that's OK, the plugin still references old types. The AuthenticationService.cs itself should compile cleanly. Task 6 replaces the plugin.

- [ ] **Step 3: Commit**

```bash
git add AutoRegisterLogin/AuthenticationService.cs
git commit -m "feat: add AuthenticationService with IP whitelist + UUID verification"
```

---

### Task 6: Create AdminCommands

**Files:**
- Create: `AutoRegisterLogin/AdminCommands.cs`

- [ ] **Step 1: Create AdminCommands.cs**

```csharp
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
```

- [ ] **Step 2: Commit**

```bash
git add AutoRegisterLogin/AdminCommands.cs
git commit -m "feat: add admin commands for IP whitelist management"
```

---

### Task 7: Rewrite AutoRegisterLoginPlugin as thin shell

**Files:**
- Modify: `AutoRegisterLogin/AutoRegisterLoginPlugin.cs` (entire file)

- [ ] **Step 1: Replace AutoRegisterLoginPlugin.cs**

```csharp
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
```

- [ ] **Step 2: Build to verify full compilation**

```bash
dotnet build AutoRegisterLogin/AutoRegisterLogin.csproj
```

Expected: Build succeeded with no errors.

- [ ] **Step 3: Commit**

```bash
git add AutoRegisterLogin/AutoRegisterLoginPlugin.cs
git commit -m "refactor: rewrite plugin as thin shell, wire IP whitelist modules"
```

---

### Task 8: Final verification

**Files:** None modified

- [ ] **Step 1: Run full test suite**

```bash
dotnet test AutoRegisterLogin.Tests/AutoRegisterLogin.Tests.csproj
```

Expected: All 13 tests PASS (7 IpWhitelist + 6 PendingApprovalStore)

- [ ] **Step 2: Release build**

```bash
dotnet build AutoRegisterLogin/AutoRegisterLogin.csproj -c Release
```

Expected: Build succeeded. Output DLL at `AutoRegisterLogin/bin/Release/net9.0/AutoRegisterLogin.dll`

- [ ] **Step 3: Final commit if any adjustments**

```bash
git status
```
