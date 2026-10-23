# Changelog

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
