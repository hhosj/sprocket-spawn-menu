# Sprocket Spawn Menu

**在沙盒里用准星直接生成敌方坦克，方便测试 AI、散布和武器。**

*A BepInEx plugin for Sprocket 0.2 that restores sandbox vehicle spawning: aim, press a key, get an AI tank.*

## 下载

- **[`Sprocket.SpawnMenu.dll`](release/Sprocket.SpawnMenu.dll)** ← 单文件，Mod Manager 里 **Add mod** 直接选它
- [`Sprocket.SpawnMenu-0.6.0.zip`](release/Sprocket.SpawnMenu-0.5.0.zip) ← 同样内容，打包形式
- 校验值：[`release/SHA256SUMS.txt`](release/SHA256SUMS.txt)

> 仓库里的这两个文件就是最新构建产物；以后发版会同步到右侧 **Releases**。

## 它做了什么

- **准星生成**：从当前视角中心发一条射线，坦克出现在你瞄准的位置；射线会跳过你自己的车。
  没命中（看天空）时退回玩家前方 150 米。
- **落在地面上**：用射线找到地面，再用车辆渲染包围盒把车底对齐；生成后启用物理，不会悬空。
- **面朝玩家**：车头转向你，方便当靶子。
- **默认敌方**：队伍 `Team 2`，生成后自动注册进游戏载具列表并启用 AI 行为。
- **蓝图列表**：按 `B` 显示，`[` / `]` 切换（游戏里所有蓝图，包括 Mod 蓝图）。
- **自动清场**：按空格回坦克编辑器时，本模组生成过的坦克会被销毁，不会留到下一轮。

## 0.6.0：回编辑器清理 + 生成即启用 AI

- `ClearOnEditorReturn` 默认重新打开：回编辑器时按已验证的 `Dispose → Deregister` 顺序清理；
- 生成完成后在静态 `AIRegister` 里找到本车的 `CommanderAI`，把 `Enabled = true`，让 AI 立即开始
  （之前要等重置战局才会被激活）。日志会打 `[spawn] AI enabled for N commander(s)`。

## 0.5.9：重置战局前清理

反馈：自定义对局里生成的车在**重置战局**后不会消失，而且开始有 AI 行为，重置几次后无法操控。
重置走的是 `DeathmatchGameMode.ResetGame`（`Retry` / `ExitGameInternal` 调用），不是离场也不是编辑器。

0.5.9 给 `ResetGame` 加前缀：重置**之前**对本模组生成的车执行
`VehicleSpawn.Dispose()` → `VehicleRegister.Deregister()`，让它们不参与重置后的 AI 重建。

## 0.5.8：离场时清空静态 AIRegister

0.5.7 的 `[cleanup] match exit` 确实执行了，但异常变成 `FlankTargetTask.Update → AIRegister.Update`：
离开对局时，静态 `AIRegister` 里还留着 AI（包括地图靶车），它们的 target 已随场景销毁，
下一局每帧 NRE。

0.5.8 在 `CleanupForSceneExit`（`GameController.Release`，场景卸载前）里，除了 Dispose 本模组
生成的车，还调用 `AIRegister.Clear()` 并按 `AvoidanceEntityRegister.nextID` 清空 avoidance 槽位。
离开一局清空是安全的，新一局会重新注册。

## 0.5.7：只在离场时清理

反馈指出：沙盒 → 对局正常，**对局 → 主菜单 → 沙盒**才坏。也就是说问题出在离开一局时：
生成的坦克随场景卸载，但静态注册表（AI/avoidance/RotationAxisPhysics）没清，进沙盒后继续 tick
已销毁对象。

0.5.7 的策略：

- `CleanupOnMatchExit = true`（默认）：在 `GameController.Release` 里、场景卸载前
  `VehicleSpawn.Dispose()` → `VehicleRegister.Deregister()`，清掉静态注册项；
- `ClearOnEditorReturn = false`（保持关闭）：编辑器往返仍不销毁，避免 0.5.2 的重建问题；
- 代价：回编辑器时生成的坦克会留在原地，直到你离开这一局。

## 0.5.6：自动清理默认关闭（止损）

0.5.4 的异常变成 `LayingDriveBehaviour.Apply → RotationAxisPhysicsRegister.Update`，说明静态注册表
不止 AI 一处；而 0.5.2 的 `Dispose` 又会在编辑器重建时弄脏 `VehicleRegister`。这条“手动清理”路线
目前风险太高。

0.5.6 把 `ClearOnEditorReturn` / `CleanupOnMatchExit` 默认改为 **false**：

- 生成的坦克在回编辑器/切换对局后**会留在原地**（旧行为）；
- 但不会再因为清理而破坏 AI/物理静态注册表，切换对局应该恢复正常；
- 两个清理开关保留为实验性选项，想测试可以手动打开。

## 0.5.5：Dispose → Deregister

0.5.4 的兜底清空了 `AIRegister` 和 avoidance，但异常换成了
`LayingDriveBehaviour.Apply → RotationAxisPhysicsRegister.Update`——又是一个静态注册表。
只做“摘 AI + 隐藏”不可能清完所有静态系统；只有游戏自己的 `VehicleSpawn.Dispose()`
（`factory.Destroy`）会释放它们。

但 0.5.2 的“只 Dispose”会在 `VehicleRegister` 里留下脏项，导致 `ExitEditor` 报
`VehicleRegister.Register` NRE / `Already enabled`。所以正确顺序是：

1. `spawn.Dispose()` —— 释放所有静态注册表；
2. 再 `VehicleRegister.Deregister(vehicle)` —— 清掉可能残留的注册项。

（0.5.0 是反过来的：先 Deregister，让 Dispose 跳过了清理。）

## 0.5.4：AI 匹配失败时的兜底

0.5.3 的日志里清场没有打出 `removed N AI(s)`——按车辆根节点匹配 `CommanderAI` 没匹配上，
AI 还留在静态 `AIRegister`，而 `SetActive(false)` 清掉了它的 avoidance 实体，于是下一帧
`Spherecast` 用旧 casterID 继续 NRE。

0.5.4 在匹配失败时兜底调用 `AIRegister.Clear()`，并按 `AvoidanceEntityRegister.nextID`
清掉 avoidance 槽位。代价是这一次切换会停掉**所有** AI（包括场景里原有的靶车），
但保证不会再把静态注册表留脏；新一局会重新注册。

## 0.5.3：清场改为“摘 AI + 注销 + 隐藏”

0.5.2 的日志显示 `AIs 1 -> 0`，`Dispose` 确实清了静态 AI；但紧接着 `ExitEditor` 里出现
`VehicleRegister.Register` NRE 和 `Failed to enable vehicle behaviour: Already enabled`——
在编辑器里销毁载具把 VehicleRegister 弄脏了，并影响了玩家设计的重建。

0.5.3 不再调用工厂销毁，而是按游戏 `VehicleAIAssemblyStage.Release()` 的顺序手工清理：

1. 从静态 `AIRegister` 里找到属于本车的 `CommanderAI`，先
   `AvoidanceEntityRegister.Deregister(avoidanceID)`，再 `AIRegister.Deregister(ai)`，
   最后调用 `CommanderAI.Release()`；
2. `VehicleRegister.Deregister(vehicle)`；
3. `gameObject.SetActive(false)`。

完全不碰工厂/共享 baked mesh，也不在编辑器重建期间销毁任何东西。

## 0.5.2：修复“下一局履带消失、无法操控”

0.5.1 的日志里，清场后每帧都在 `AIRegister.Update → DriverAI.Step → AvoidanceEntityRegister.Spherecast`
抛 `NullReferenceException`（6 万行日志里 1.3 万次）。`AIRegister` 是**静态**的，我们只把车隐藏并从
`VehicleRegister` 注销，AI 实体没从静态 `AIRegister`/`AvoidanceEntityRegister` 移除，下一局还在 tick
已经销毁的对象，于是履带不更新、操控失效。

0.5.2 改为**只调用游戏自己的 `VehicleSpawn.Dispose()`**（`StageVehicleSpawnEvent` 的清理路径），
不再先手动 `Deregister`、也不再 `SetActive(false)`；这条路径会走
`VehicleAIAssemblyStage.Release()` 把 AI 从 `AIRegister` 里移除。日志会打
`[cleanup] done (AIs N -> M)`，可以直接确认 AI 数量有没有降下去。

## 0.5.1：修复“下一局履带消失、无法操控”

0.5.0 回编辑器时用 `VehicleSpawn.Dispose()`（内部 `factory.Destroy`）清场。这条路径会释放
**baked mesh**，而 Sprocket 的履带等网格是跨载具共享的，结果下一局玩家坦克的履带可能消失、无法驾驶。

0.5.1 改成：

- 只做 `VehicleRegister.Deregister(vehicle)` + `gameObject.SetActive(false)`，**不再调用**工厂销毁/资源卸载；
- 增加 `GameController.Release` 前缀，在离开一局之前就把本模组生成的车注销并隐藏，避免它们参与场景拆除；
- 切场景时只忘掉列表，不触碰已销毁对象。

## 前置

- Sprocket **0.2.55.5**
- **BepInEx be.788 + Hans21223 兼容补丁**（原版 BepInEx 会黑屏）。
  Windows 上用 **Sprocket Mod Manager** 安装最省事。

## 安装（Windows，推荐）

1. 下载 **Sprocket Mod Manager**（[Hans21223/Sprocket-Mod-Loader](https://github.com/Hans21223/Sprocket-Mod-Loader) 的 Release，
   `Sprocket-Mod-Manager-1.4.0.zip`），解压到固定位置。
2. 打开 `Mod Manager.exe` → **Install mod loader**，等到底部显示安装完成。
3. **Add mod** → 选 `Sprocket.SpawnMenu.dll` → 在列表里双击启用。
4. **Launch**。首次启动慢 1~3 分钟（生成 interop），之后正常。

> ⚠️ 不要另外再装一套官方 BepInEx。Sprocket 必须用 be.788 + 社区兼容补丁，Mod Manager 会处理。

## 安装（Linux / Steam Deck）

Mod Manager 是 Windows 专用；Linux 手动装：

1. 已经有加载器（BepInEx be.788 + 兼容补丁）的话，把 `Sprocket.SpawnMenu.dll`
   放进 `~/.local/share/Steam/steamapps/common/Sprocket/BepInEx/plugins/`。
2. Steam → Sprocket → 属性 → 启动选项加：
   ```
   WINEDLLOVERRIDES="winhttp=n,b" %command%
   ```
3. 没有加载器时，按 [Sprocket-Mod-Loader](https://github.com/Hans21223/Sprocket-Mod-Loader)
   的说明安装 be.788 底座和兼容补丁。

日志：`BepInEx/LogOutput.log`。

## 使用

| 按键 | 作用 |
| --- | --- |
| `B` | 显示 / 隐藏蓝图面板 |
| `[` / `]` | 上一辆 / 下一辆蓝图 |
| `Z` 或 `F10` | 在准星位置生成当前蓝图 |
| `F9` | 打印探针日志 |

生成后日志里会出现 `[spawn]`、`[track]`；如果生成失败，会有 `[spawn] ... failed` 和原因。

> 笔记本上 `F10` 可能是静音/媒体键，收不到就按 `Z`。

## 配置

文件：`BepInEx/config/sprocket.spawn.menu.cfg`，改完**重启游戏**生效。

| 项 | 默认 | 说明 |
| --- | --- | --- |
| `[Spawn] Team` | `2` | 生成载具的队伍（1–8），2 = 敌方 |
| `[Spawn] Group` | `1` | 组（0 = 不指定） |
| `[Spawn] SpawnAtAim` | `true` | 生成在准星命中点 |
| `[Spawn] MaxAimDistance` | `800` | 准星射线最大距离（米） |
| `[Spawn] Distance` | `150` | 准星没命中时的水平距离（米） |
| `[Spawn] Height` | `2` | 初始离地高度（随后按包围盒贴地） |
| `[Spawn] GroundSnap` | `true` | 生成点向下找地面 |
| `[Spawn] FacePlayer` | `true` | 车头朝向玩家 |
| `[Spawn] FacingOffsetDegrees` | `0` | 车头反了设 `180` |
| `[Spawn] CreateSpawnerIfMissing` | `true` | 场景没有生成点时自建 |
| `[Spawn] ClearOnEditorReturn` | `false` | 实验性：回编辑器时清理（可能破坏静态注册表） |
| `[Spawn] CleanupOnMatchExit` | `true` | 离场（回主菜单）时清理生成的坦克 |
| `[Keys] UiToggleKey` | `B` | 面板显示/隐藏 |
| `[Keys] NextBlueprintKey` | `]` | 下一辆 |
| `[Keys] PrevBlueprintKey` | `[` | 上一辆 |
| `[Keys] SpawnKey` | `F10` | 备用生成键 |
| `[Keys] SpawnFallbackKey` | `Z` | 备用生成键 |
| `[Keys] ProbeKey` | `F9` | 探针日志 |

> 键位都避开游戏自带绑定：`J` 是弃车、`K` 是设计师 Dissolve，所以没用 J/K。

## 已知限制

- 实验性模组，只在沙盒/有 `GameController` 的场景里工作。
- 生成的 AI 行为取决于游戏本身的 AI；蓝图必须有效（损坏的蓝图会生成失败）。
- 面板是只读列表：这个游戏的 IL2CPP 构建裁掉了 IMGUI 的 Button/Window 等 API，
  所以只能用键盘选，鼠标点不了。
- 游戏大版本更新后可能失效；失败时只写日志，不会让游戏崩溃。

## 从源码构建

需要 .NET SDK 6+，以及游戏跑过一次后生成的 `BepInEx/interop`：

```bash
export DOTNET_ROOT=/path/to/dotnet6
export PATH="$DOTNET_ROOT:$PATH"
./build.sh        # 产出 release/Sprocket.SpawnMenu.dll
./pack.sh         # 产出 release/ 下的 dll + zip + sha256
```

interop 目录默认 `$HOME/.local/share/Steam/steamapps/common/Sprocket/BepInEx/interop`，
可用 `INTEROP_DIR` 覆盖。

## 许可 / 致谢

本模组：MIT，见 [`LICENSE`](LICENSE)。

加载器是第三方项目，本仓库**不包含**它们：

- [BepInEx](https://github.com/BepInEx/BepInEx)
- [Hans21223/Sprocket-Mod-Loader](https://github.com/Hans21223/Sprocket-Mod-Loader) — Sprocket 兼容补丁 + Mod Manager

开发与逆向笔记见 [`docs/开发笔记.md`](docs/开发笔记.md)。
