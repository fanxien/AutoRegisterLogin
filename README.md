# AutoRegisterLogin

中文说明：

`AutoRegisterLogin` 是一个 TShock 插件，用来在玩家首次进入服务器时自动注册账号，并在满足条件时自动登录。对于已存在的账号，插件默认会在 UUID 匹配时执行自动登录。

## 仓库结构

- `AutoRegisterLogin/`：插件源码
- `_refs/`：开发时用到的本地参考源码目录，已被 `.gitignore` 忽略

## 功能概览

- 新玩家首次进服时自动注册
- 注册完成后自动登录
- 注册时自动绑定玩家 UUID
- 已有账号在 UUID 匹配时自动登录
- 支持 `/reload` 重载配置

## 构建

```powershell
dotnet build AutoRegisterLogin\AutoRegisterLogin.csproj -c Release
```

## 输出文件

编译后的插件位于：

`AutoRegisterLogin/bin/Release/net9.0/AutoRegisterLogin.dll`

## 安装方法

将编译得到的 `AutoRegisterLogin.dll` 复制到 TShock 服务器的 `ServerPlugins` 目录，然后重启服务器。

---

English:

`AutoRegisterLogin` is a TShock plugin that automatically registers first-time players and logs them in. Returning players can also be logged in automatically when their UUID matches the stored account.

## Repository Layout

- `AutoRegisterLogin/`: plugin source code
- `_refs/`: local reference clones used during development, ignored by `.gitignore`

## Highlights

- Automatically registers new players on first join
- Automatically logs players in after registration
- Binds player UUIDs during registration
- Automatically logs in existing players when the UUID matches
- Supports `/reload` for config reloads

## Build

```powershell
dotnet build AutoRegisterLogin\AutoRegisterLogin.csproj -c Release
```

## Output

The compiled plugin is generated at:

`AutoRegisterLogin/bin/Release/net9.0/AutoRegisterLogin.dll`

## Install

Copy `AutoRegisterLogin.dll` into your TShock server's `ServerPlugins` directory and restart the server.

