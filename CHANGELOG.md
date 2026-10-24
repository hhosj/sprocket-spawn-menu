# Changelog

## 0.6.10
- 生成菜单限制为仅沙盒模式：主菜单、任务场景禁用热键/UI/生成/探针。
- 用 `GameController.setupContext.Mode` 判断，主菜单无 GameController 即拒绝。


## 0.6.0
- `ClearOnEditorReturn` 默认开启（Dispose → Deregister，与重置战局相同的已验证顺序）。
- 生成后把本车 CommanderAI 的 `Enabled` 设为 true，AI 立即行动，不再等重置战局。


## 0.5.9
- 给 `DeathmatchGameMode.ResetGame` 加前缀：重置战局前 Dispose + Deregister 本模组生成的车，
  避免它们留到重置后并获得 AI 行为、最终破坏操控。


## 0.5.8
- 离场清理时额外 `AIRegister.Clear()` + 清 avoidance 槽位，移除静态 AI 任务（FlankTargetTask 等）
  对已销毁目标的引用，修复“对局→主菜单→沙盒”后的每帧 NRE。


## 0.5.7
- `CleanupOnMatchExit` 默认开启：在 `GameController.Release`（离场、场景卸载前）
  `Dispose` + `Deregister`，清理静态 AI/avoidance/RotationAxisPhysics 注册项。
- 编辑器清理保持关闭，避免编辑器重建时弄脏 VehicleRegister。


## 0.5.6
- 自动清理（回编辑器/离场）默认关闭：清理会触碰 Sprocket 的静态注册表，
  曾导致切换对局后无法操控。生成的坦克会留在场景里，但不再破坏游戏状态。
- 清理开关保留为实验性配置。


## 0.5.5
- 清场顺序改为 `VehicleSpawn.Dispose()` → `VehicleRegister.Deregister()`：
  前者释放 AIRegister/Avoidance/RotationAxisPhysics 等静态注册表，后者清掉残留的载具注册项。
- 去掉手工 AI 摘除/兜底清空（会误伤其它 AI，且清不干净静态物理注册表）。


## 0.5.4
- AI 按根节点匹配失败时兜底 `AIRegister.Clear()` + 清 avoidance 槽位，避免静态 AI 每帧 NRE。
- 代价：该次切换会停掉所有 AI；新一局重新注册。


## 0.5.3
- 清场不再调用 `VehicleSpawn.Dispose()`（会在编辑器重建玩家设计时弄脏 VehicleRegister）。
- 改为手工：从静态 `AIRegister`/`AvoidanceEntityRegister` 摘除本车 AI → `VehicleRegister.Deregister`
  → `SetActive(false)`；顺序与 `VehicleAIAssemblyStage.Release()` 一致。


## 0.5.2
- 修复清场后 `AIRegister.Update` 每帧 NRE（下一局履带消失、无法操控）：
  改为只走游戏自己的 `VehicleSpawn.Dispose()`，让 `VehicleAIAssemblyStage.Release()` 把 AI
  从静态 `AIRegister` 移除；不再手动 `Deregister` 或 `SetActive(false)`。
- 清场日志增加 AI 数量变化。


## 0.5.1
- 修复下一局履带消失/无法操控：清场不再调用 `VehicleSpawn.Dispose()`/`factory.Destroy()`
  （会释放跨载具共享的 baked mesh），改为 `Deregister` + `SetActive(false)`。
- 在 `GameController.Release` 时清理本模组生成的车，避免参与场景拆除。


## 0.5.0
- 回坦克编辑器（空格）时自动清除本模组生成的所有坦克（`VehicleSpawn.Dispose`）。
- 切场景时清空内部列表，不重复销毁。

## 0.4.3
- 蓝图面板默认隐藏（B 显示）。
- 前后切换改为 `[` / `]`；避开游戏的 J（弃车）、K（Dissolve）等绑定。

## 0.4.2
- 改用 `GUI.Box` + `GUI.Label` 绘制面板（游戏的 IL2CPP 构建裁掉了 Button/Window/BeginArea）。

## 0.4.1
- 蓝图面板改为固定 Box，绕开 `GUILayout.Window` 的 interop 构造问题。

## 0.4.0
- 准星落点生成（从玩家当前视角中心射线，跳过自己的车）。
- 初步蓝图可视化面板（B）。

## 0.3.2
- 用渲染包围盒把车底对齐地面；选择主车体 Rigidbody 启用物理。
- 修复 interop 接口 `as` 转换失败。

## 0.3.1
- 生成后设置 `Rigidbody.isKinematic = false`，让坦克落地。
- 地面射线忽略 trigger。

## 0.3.0
- 地面射线忽略 trigger，避免生成在半空后掉出世界。
- `Height` 改为相对命中表面的初始高度。

## 0.2.0
- 默认 150 米、贴地、面朝玩家。

## 0.1.x
- 探针、生成链路打通、`CancellationToken` / `Il2CppObject` 判空修复、Z 备用键。
