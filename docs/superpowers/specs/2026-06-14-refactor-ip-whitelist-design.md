# AutoRegisterLogin Refactor — IP Whitelist Security Model

## Motivation

Original plugin auto-registers new players and logs them in via UUID matching. The UUID-only model is insufficient: if config is misconfigured (UUID check disabled), or UUID is missing/spoofed, any player can impersonate another by using the same name.

## Design: IP Whitelist + UUID Dual Verification

### Auth Flow

```
Player joins
  |
  +-- Account does not exist
  |     -> Auto-register (if enabled)
  |     -> Record current IP as initial whitelist entry
  |     -> Auto-login
  |
  +-- Account exists
        |
        +-- IP is whitelisted + UUID matches -> Auto-login
        |
        +-- IP is NOT whitelisted
              -> Record pending approval (name, old IP list, new IP)
              -> Kick player with message:
                 "Your IP has changed (old: X.X.X.X -> new: Y.Y.Y.Y).
                  Please contact an admin for approval."
              -> Admin runs /approveip <player>
                 -> Both old and new IPs added to whitelist
                 -> Player can reconnect and auto-login
```

- UUID check is **mandatory** — cannot be disabled via config.
- Password generation fixed at **8 bytes** (16 hex chars).
- First-registration IP is auto-whitelisted (no admin needed).

### Module Split

```
AutoRegisterLogin/
├── AutoRegisterLogin.csproj
├── AutoRegisterLoginPlugin.cs     -- Thin shell: hook registration + delegation
├── PluginConfig.cs                -- Config load/save/reload
├── IpWhitelist.cs                 -- IP whitelist data + persistence
├── AuthenticationService.cs       -- Auth orchestration: register -> IP check -> login
├── PendingApprovalStore.cs        -- Pending IP-change records with TTL
└── AdminCommands.cs               -- /approveip, /ipwhitelist, /removeip
```

| Module | Responsibility |
|--------|---------------|
| AutoRegisterLoginPlugin (~40 lines) | Register ServerJoin/NetGreetPlayer/Reload hooks, forward to AuthenticationService |
| PluginConfig (~35 lines) | 6 config fields, JSON read/write with validation |
| IpWhitelist (~80 lines) | `Dictionary<string, HashSet<string>>`, JSON persistence to `tshock/AutoRegisterLogin_whitelist.json` |
| PendingApprovalStore (~50 lines) | Store pending IP-change requests with TTL, auto-cleanup on expiry |
| AuthenticationService (~130 lines) | Orchestrate registration, IP check, UUID check, TShock login |
| AdminCommands (~55 lines) | `/approveip <name>`, `/ipwhitelist <name>`, `/removeip <name> <ip>` |

### Whitelist JSON Structure

```json
{
  "PlayerA": ["192.168.1.1", "10.0.0.5"],
  "PlayerB": ["172.16.0.3"]
}
```

### Pending Approvals JSON Structure

```json
[
  {
    "PlayerName": "PlayerC",
    "ExistingIps": ["192.168.1.1"],
    "NewIp": "10.0.0.8",
    "RequestedAt": "2026-06-14T12:00:00Z"
  }
]
```

### Config (Final)

```json
{
  "Enabled": true,
  "AutoRegisterNewPlayers": true,
  "BindUuidOnRegister": true,
  "SendPlayerMessages": true,
  "DefaultGroupName": "default",
  "PendingApprovalTimeoutHours": 72
}
```

| Field | Default | Description |
|-------|---------|-------------|
| Enabled | true | Master switch. When false, plugin does nothing. |
| AutoRegisterNewPlayers | true | Auto-create account for first-time players. |
| BindUuidOnRegister | true | Bind player UUID during registration. |
| SendPlayerMessages | true | Send informational messages to players. |
| DefaultGroupName | "default" | TShock group assigned to auto-registered accounts. |
| PendingApprovalTimeoutHours | 72 | Hours before pending IP-change requests are auto-cleaned. |

Removed from original config:
- `RequireMatchingUuidForExistingAccounts` — now mandatory, always on.
- `AutoLoginExistingPlayers` — replaced by IP whitelist model.
- `GeneratedPasswordBytes` — fixed at 8 bytes.

### Security Properties

| Threat | Mitigation |
|--------|-----------|
| Account theft (same name, different player) | IP must be whitelisted + UUID must match |
| UUID spoofing | UUID alone insufficient; IP must also be whitelisted |
| Config misconfiguration | UUID check and IP check are hardcoded, not toggleable |
| Admin forgets to approve | Pending requests auto-expire after configured timeout |
| Stale whitelist entries | `/removeip` command for manual cleanup |

### Error / Edge Cases

- **UUID empty (pirated client)**: Authentication rejected. Player must use `/login` manually.
- **IP missing (unusual proxy)**: Treated as "IP not in whitelist," triggers pending approval.
- **Player renamed**: TShock account stays with old name. New name = new registration flow.
- **Whitelist file missing**: Treated as empty, auto-created on first registration.
- **Config file missing**: Regenerated with defaults.
