# AutoRegisterLogin

中文说明：

`AutoRegisterLogin` 是一个 TShock 插件，会在玩家第一次进入服务器时自动创建账号并登录。对于已经存在的账号，插件可以在 UUID 匹配时自动登录，避免玩家每次手动输入 `/register` 或 `/login`。

## 功能

- 新玩家首次进服时自动创建 TShock 账号
- 注册后立即自动登录
- 注册时绑定玩家 UUID
- 已有账号在 UUID 匹配时自动登录
- 支持 `/reload`，并在配置文件缺失时自动重新生成

## 默认行为

- 如果玩家名还没有对应账号，插件会把账号创建到 `default` 组
- 插件会随机生成密码，只保存 BCrypt 哈希，不保存明文
- 默认仅在 UUID 匹配时自动登录已有账号
- 配置文件生成在 `tshock/AutoRegisterLogin.json`

## 配置

```json
{
  "Enabled": true,
  "AutoRegisterNewPlayers": true,
  "AutoLoginExistingPlayers": true,
  "BindUuidOnRegister": true,
  "RequireMatchingUuidForExistingAccounts": true,
  "SendPlayerMessages": true,
  "DefaultGroupName": "default",
  "GeneratedPasswordBytes": 16
}
```

## 构建

```powershell
dotnet build AutoRegisterLogin\AutoRegisterLogin.csproj -c Release
```

编译后的文件位于 `AutoRegisterLogin/bin/Release/net9.0/`。

## 安装

将 `AutoRegisterLogin.dll` 复制到服务器的 `ServerPlugins` 目录，然后重启服务器。

---

English:

`AutoRegisterLogin` is a TShock plugin that automatically registers first-time players and logs them in. Returning players can also be logged in automatically when their UUID matches the stored account.

## Features

- Automatically creates a TShock account for a new player on first join
- Automatically logs the player in after registration
- Binds the player's UUID on registration
- Automatically logs in existing players when the UUID matches
- Supports `/reload` and recreates its config file when missing

## Default Behavior

- If a player's account does not exist yet, the plugin creates one in the `default` group
- The plugin generates a random password and stores only the BCrypt hash
- Existing accounts are only auto-logged in when the UUID matches by default
- The config file is created at `tshock/AutoRegisterLogin.json`

## Config

```json
{
  "Enabled": true,
  "AutoRegisterNewPlayers": true,
  "AutoLoginExistingPlayers": true,
  "BindUuidOnRegister": true,
  "RequireMatchingUuidForExistingAccounts": true,
  "SendPlayerMessages": true,
  "DefaultGroupName": "default",
  "GeneratedPasswordBytes": 16
}
```

## Build

```powershell
dotnet build AutoRegisterLogin\AutoRegisterLogin.csproj -c Release
```

The compiled plugin will be generated under `AutoRegisterLogin/bin/Release/net9.0/`.

## Install

Copy `AutoRegisterLogin.dll` into your server's `ServerPlugins` directory and restart the server.
