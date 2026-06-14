# AutoRegisterLogin

中文说明：

`AutoRegisterLogin` 是一个 TShock 插件，通过 **IP 白名单 + UUID 双重验证** 实现安全自动登录。

## 功能

- 新玩家首次进服时自动创建 TShock 账号并登录
- 注册时记录当前 IP 为初始白名单，无需管理员操作
- 已有账号需 **IP 在白名单中 + UUID 匹配** 才能自动登录
- IP 不在白名单时踢出玩家，提示联系管理员审批
- 管理员用 `/approveip` 审批后，新旧 IP 均加入白名单
- 待审批记录在配置的超时后自动清理
- 配置支持 `/reload` 热重载

## 管理命令

需要权限 `autoregisterlogin.admin`：

- `/approveip <玩家名>` — 批准 IP 变更，将旧 IP 和新 IP 加入白名单
- `/ipwhitelist <玩家名>` — 查看某玩家所有白名单 IP
- `/removeip <玩家名> <ip>` — 从白名单移除指定 IP

## 安全设计

- **双重验证**：IP 白名单 + UUID 匹配，缺一不可
- **UUID 强制开启**：不可通过配置关闭
- **首次信任**：新玩家首次进服 IP 自动入白名单
- **变更审批**：IP 变更时旧 IP 保留，管理员审批后新旧均有效

## 默认行为

- 新玩家分配到 `default` 权限组
- 自动生成 16 位十六进制随机密码，仅存储 BCrypt 哈希
- 配置文件：`tshock/AutoRegisterLogin.json`
- 白名单文件：`tshock/AutoRegisterLogin_whitelist.json`
- 待审批文件：`tshock/AutoRegisterLogin_pending.json`

## 构建

```powershell
dotnet build AutoRegisterLogin.csproj -c Release
```

编译输出：`bin/Release/net9.0/AutoRegisterLogin.dll`

## 安装

将 `AutoRegisterLogin.dll` 复制到服务器的 `ServerPlugins` 目录后重启。

## 运行测试

```powershell
dotnet test ..\AutoRegisterLogin.Tests\AutoRegisterLogin.Tests.csproj
```

---

English:

`AutoRegisterLogin` is a TShock plugin providing secure automatic login via **IP whitelist + UUID dual verification**.

## Features

- Auto-creates TShock accounts for new players and logs them in
- First-join IP is auto-whitelisted (no admin action needed)
- Existing accounts require **whitelisted IP + matching UUID** for auto-login
- Unrecognized IPs are rejected with a clear message; admin approval required
- `/approveip` whitelists both old and new IPs
- Pending approvals auto-expire after configured timeout
- Configuration supports `/reload`

## Admin Commands

Requires `autoregisterlogin.admin` permission:

- `/approveip <player>` — Approve IP change, whitelist old and new IPs
- `/ipwhitelist <player>` — List all whitelisted IPs for a player
- `/removeip <player> <ip>` — Remove an IP from whitelist

## Security

- **Dual verification**: IP whitelist + UUID match, both required
- **UUID mandatory**: Cannot be disabled via config
- **First-join trust**: Initial IP auto-whitelisted
- **Change approval**: Old IPs preserved; admin approval adds new IPs alongside existing ones

## Defaults

- New players placed in `default` group
- 16-char hex random password, BCrypt hash only
- Config: `tshock/AutoRegisterLogin.json`
- Whitelist: `tshock/AutoRegisterLogin_whitelist.json`
- Pending: `tshock/AutoRegisterLogin_pending.json`

## Build

```powershell
dotnet build AutoRegisterLogin.csproj -c Release
```

Output: `bin/Release/net9.0/AutoRegisterLogin.dll`

## Install

Copy `AutoRegisterLogin.dll` to server's `ServerPlugins` directory and restart.

## Run Tests

```powershell
dotnet test ..\AutoRegisterLogin.Tests\AutoRegisterLogin.Tests.csproj
```
