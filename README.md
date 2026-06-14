# AutoRegisterLogin

中文说明：

`AutoRegisterLogin` 是一个 TShock 插件，用于在玩家首次进入服务器时自动注册账号，并通过 **IP 白名单 + UUID 双重验证** 确保账号安全。当玩家 IP 变更时需管理员审批，防止账号盗用。

## 仓库结构

- `AutoRegisterLogin/`：插件源码
- `AutoRegisterLogin.Tests/`：单元测试

## 功能概览

- 新玩家首次进服时自动注册并登录
- 注册时自动记录当前 IP 为初始白名单
- 已有账号需 **IP 在白名单中 + UUID 匹配** 才能自动登录
- IP 不在白名单时踢出玩家，等待管理员审批
- 管理员审批后新旧 IP 均加入白名单
- 待审批记录超时自动清理（默认 72 小时）
- 支持 `/reload` 重载配置

## 管理命令

| 命令 | 权限 | 说明 |
|------|------|------|
| `/approveip <玩家名>` | `autoregisterlogin.admin` | 批准 IP 变更，新旧 IP 加入白名单 |
| `/ipwhitelist <玩家名>` | `autoregisterlogin.admin` | 查看玩家的白名单 IP 列表 |
| `/removeip <玩家名> <ip>` | `autoregisterlogin.admin` | 移除白名单中的某个 IP |

## 配置

配置文件：`tshock/AutoRegisterLogin.json`

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

| 字段 | 默认值 | 说明 |
|------|--------|------|
| Enabled | true | 插件总开关 |
| AutoRegisterNewPlayers | true | 自动为新玩家创建账号 |
| BindUuidOnRegister | true | 注册时绑定玩家 UUID |
| SendPlayerMessages | true | 向玩家发送提示消息 |
| DefaultGroupName | "default" | 自动注册账号归属的权限组 |
| PendingApprovalTimeoutHours | 72 | 待审批请求超时小时数 |

## 安全模型

- **IP 白名单**：每个账号关联一组合法 IP，只有白名单内的 IP + 匹配的 UUID 才能自动登录
- **UUID 强制校验**：不可在配置中关闭
- **IP 变更审批**：新 IP 连接时踢出玩家，管理员审查后用 `/approveip` 批准
- **自动注册**：首次进服时 IP 自动加入白名单，无需管理员操作

## 构建

```powershell
dotnet build AutoRegisterLogin\AutoRegisterLogin.csproj -c Release
```

编译后的 DLL 位于 `AutoRegisterLogin/bin/Release/net9.0/`。

## 安装

将 `AutoRegisterLogin.dll` 复制到 TShock 服务器的 `ServerPlugins` 目录，重启服务器。

---

English:

`AutoRegisterLogin` is a TShock plugin that automatically registers first-time players and logs them in with **IP whitelist + UUID dual verification**. IP changes require admin approval to prevent account theft.

## Features

- Automatically creates a TShock account for new players on first join and logs them in
- Records first-join IP as the initial whitelist entry
- Existing accounts require **whitelisted IP + matching UUID** for auto-login
- Players with unrecognized IPs are disconnected and flagged for admin approval
- Admin approval whitelists both old and new IPs
- Pending approval requests expire automatically (default 72 hours)
- Supports `/reload` for config reloads

## Admin Commands

| Command | Permission | Description |
|---------|------------|-------------|
| `/approveip <player>` | `autoregisterlogin.admin` | Approve IP change, whitelist old and new IPs |
| `/ipwhitelist <player>` | `autoregisterlogin.admin` | List whitelisted IPs for a player |
| `/removeip <player> <ip>` | `autoregisterlogin.admin` | Remove an IP from whitelist |

## Config

Config file: `tshock/AutoRegisterLogin.json`

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
| Enabled | true | Master switch |
| AutoRegisterNewPlayers | true | Auto-create account for new players |
| BindUuidOnRegister | true | Bind player UUID during registration |
| SendPlayerMessages | true | Send informational messages to players |
| DefaultGroupName | "default" | Group for auto-registered accounts |
| PendingApprovalTimeoutHours | 72 | Hours before pending requests expire |

## Security Model

- **IP Whitelist**: Each account has a set of allowed IPs. Auto-login requires both whitelisted IP and matching UUID.
- **UUID check is mandatory** — not configurable.
- **IP change approval**: Unknown IPs trigger a disconnect with a clear message. Admins review and approve with `/approveip`.
- **First-join auto-whitelist**: No admin action needed for new players.

## Build

```powershell
dotnet build AutoRegisterLogin\AutoRegisterLogin.csproj -c Release
```

The compiled DLL is at `AutoRegisterLogin/bin/Release/net9.0/`.

## Install

Copy `AutoRegisterLogin.dll` into your TShock server's `ServerPlugins` directory and restart the server.
