# Sprocket Spawn Menu

适用于 **Sprocket 0.2.55.5** 的 BepInEx 插件：在沙盒模式中用准星生成坦克，方便测试 AI、武器和散布。

## 下载

- [`Sprocket.SpawnMenu.dll`](release/Sprocket.SpawnMenu.dll) — 单文件，放入 `BepInEx/plugins/` 即可
- [`Sprocket.SpawnMenu-0.6.10.zip`](release/Sprocket.SpawnMenu-0.6.10.zip) — 同样内容的打包版
- [`SHA256SUMS.txt`](release/SHA256SUMS.txt) — 校验值

> 目前尚未发布到 GitHub Releases；仓库 `release/` 下的文件就是当前构建产物。

## 功能

- **仅沙盒可用**：主菜单、任务场景、自定义战斗中禁用热键、蓝图面板、生成和探针。
- **准星生成**：从视角中心发射射线，坦克生成在你瞄准的位置；射线会跳过自己的车。
  没命中时退回玩家前方 150 米。
- **落地与物理**：射线找地面，按车辆包围盒对齐车底，生成后启用物理，不会悬空。
- **朝向玩家**：车头转向你，方便当靶子。
- **默认敌方**：`Team 2` / `Group 1`，可配置。
- **蓝图面板**：`B` 显示/隐藏，`[` / `]` 切换游戏内所有蓝图（包括 Mod 蓝图）。
- **自动清理**：回坦克编辑器、重置战局、离开对局时清理本模组生成的车。

## 安装

1. 安装 **BepInEx 6 IL2CPP（be.788）**，以及 Hans21223 的兼容补丁。
2. 把 `Sprocket.SpawnMenu.dll` 放入游戏的 `BepInEx/plugins/` 目录。
3. 启动游戏，`BepInEx/LogOutput.log` 出现 `Sprocket Spawn Menu 0.6.10 loaded` 即加载成功。

## 使用

只在沙盒模式生效：

| 按键 | 作用 |
| --- | --- |
| `B` | 显示/隐藏蓝图面板 |
| `[` / `]` | 上一辆 / 下一辆蓝图 |
| `Z` / `F10` | 在准星位置生成 |
| `F9` | 输出诊断日志 |

进入主菜单或任务场景后按键无效，日志会显示：

```
[gate] mode=Sandbox; spawn menu=enabled
[gate] mode=Mission; spawn menu=disabled
```

## 配置

配置文件：`BepInEx/config/sprocket.spawn.menu.cfg`

常用项：

- `SpawnKey` / `SpawnFallbackKey`：生成键，默认 `Z` / `F10`
- `UiKey`：面板键，默认 `B`
- `NextKey` / `PrevKey`：蓝图切换，默认 `]` / `[`
- `Team` / `Group`：生成队伍，默认 `2` / `1`
- `SpawnAtAim` / `MaxAimDistance`：准星生成及最大距离
- `Distance` / `Height` / `GroundSnap` / `FacePlayer`：回退位置、高度、贴地、朝向
- `ClearOnEditorReturn` / `CleanupOnMatchExit`：回编辑器 / 离场清理

## 从源码构建

需要 .NET 6 SDK 和游戏的 IL2CPP interop 程序集：

```bash
export DOTNET_ROOT=/path/to/dotnet6
export PATH="$DOTNET_ROOT:$PATH"
./build.sh    # 输出 release/Sprocket.SpawnMenu.dll
./pack.sh     # 同时生成 zip 和校验值
```

`InteropDir` 指向游戏的 `BepInEx/interop`，默认路径在 `SprocketSpawnMenu.csproj` 中。

## 已知限制

- 只在沙盒模式可用，这是刻意限制。
- 0.6.x 使用手动 `Spawn + Register` 生成；AI 不保证生成后立刻行动，通常要等重置战局。
- 重置战局一两次后，游戏特效可能消失；原生 `StageVehicleSpawnEvent` 方案尚未合并。

## 许可证

[MIT](LICENSE)
