# Changelog

## 0.6.8
- 重置战局清理改为非销毁式（Deregister + SetActive(false) + 清 AIRegister），
  不再 `Dispose`/`factory.Destroy`，修复重置一两次后特效消失。


## 0.6.7
- 只启用/命令本次生成新增的 AI，不再唤醒 AIRegister 里原有的其它 AI（特效异常的剩余嫌疑）。


## 0.6.6
- 修复 `EnableAIFor` 永远返回 0 导致的重试不退出、`AttackOrder` 每 0.5 秒重复下发。
- 现在每次生成只启用/命令一次。


## 0.6.5
- 移除每次生成后的 `VehicleRegister.RebuildRegister()`（疑似导致生成延迟和特效消失）。
- AI 启用重试间隔 1s -> 0.5s。


## 0.6.4
- 生成后对新加入静态 AIRegister 的 CommanderAI 下发 AttackOrder（目标为玩家载具 EntityID），
  解决“AI 已启用但待机”的问题。


## 0.6.3
- AI 启用改为遍历整个静态 AIRegister，把所有 AI 的 Enabled 设为 true；
  按车辆/队伍匹配不可靠（日志 count=4 但 matched=0）。


## 0.6.2
- 生成并注册后调用 `VehicleRegister.RebuildRegister()`，模拟重置战局对载具/AI 的重建，
  让生成的车立即获得 AI 行为。
- 补充 AIRegister 列表形状诊断日志。


## 0.6.1
- 生成后每秒重试启用本车 CommanderAI（最多 10 次），并打印 AIRegister 实际数量，
  诊断“AI 延迟注册”还是“匹配失败”。


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
