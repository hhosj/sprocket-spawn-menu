# Sprocket Spawn Menu — 项目规则（最高优先级）

## 代码基线

- **唯一基线：git tag `v0.6.0`。** 后续任何修改都从这棵树开始，不要从 0.6.1–0.6.9 的实验代码继续。
- 已应用用户明确要求的变更：0.6.10 的“仅沙盒模式”门禁；除此之外保持 0.6.0 行为。
- `main` 上的代码必须保持 0.6.0 的行为；实验性改动开 `experiment/*` 分支，验证通过前不得合并。
- 0.6.0 的 dll 哈希：`e16084ab7f82c685863f7c6f32d2d0a6396d67fc8b22556de29fa63ccea2f9f9`。
- 0.6.10（沙盒门禁）的 dll 哈希：`dad3d77f5bd28409a5da05a5551cce353e5ad8877e066cd3a5b945f090de1e2c`。

## 硬性底线

**切换对局（沙盒 ↔ 自定义战斗 ↔ 主菜单）后，玩家坦克必须可以正常操控。**
任何清理、AI、特效、性能改动只要破坏这一条，直接回退，不做例外。

## 已确认的取舍（不要再重复试错）

- 手动 `VehicleSpawner.Spawn` + `VehicleRegister.Register` 生成的载具不在游戏的生命周期里。
- `VehicleSpawn.Dispose()` → `factory.Destroy()`：静态注册表（AIRegister / AvoidanceEntityRegister /
  RotationAxisPhysicsRegister）能清干净、操控正常；但会释放共享特效资源，重置一两次后特效可能消失。
- 不 `Dispose`、只 Deregister + SetActive(false)：特效保留；但静态物理/AI 注册表留脏，
  重置或切对局后每帧 NRE、坦克无法操控。
- **要同时解决特效和操控，必须改走游戏自己的 `StageVehicleSpawnEvent` 生成流程（0.7 方向），
  不要再在手动 Spawn 路径上做清理实验。**

## 0.6.0 的已知行为

- 回编辑器清理默认开启（`ClearOnEditorReturn = true`）。
- 离场/重置清理使用 `Dispose → Deregister`。
- 生成后只做 `SetOurAIEnabled`，没有 `AttackOrder`；日志里通常 `AI enabled for 0 commander(s)`，
  生成的车一般要等重置战局才会行动。
- 2026-10-24 用户要求回退到 0.6.0；`main` 提交 `13bd59d` 的代码树即 0.6.0。
