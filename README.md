# Sprocket Spawn Menu

**在沙盒模式里生成坦克。**

**注意！！**
***第三人称生成坦克的距离存在问题，想要精准的选择坦克生成位置，请使用瞄准镜瞄准***

***本模组只经过简单测试，还处于早期版本***
***使用deepseek开发，是个AI项目***

*A BepInEx plugin that restores sandbox vehicle spawning in Sprocket: aim, press a key, get a tank.*

## 下载

- **[`Sprocket.SpawnMenu.dll`](release/Sprocket.SpawnMenu.dll)** ← 单文件，Mod Manager 里 **Add mod** 直接选它
- [`Sprocket.SpawnMenu-0.6.10.zip`](release/Sprocket.SpawnMenu-0.6.10.zip) ← 同样内容，打包形式
- 校验值：[`release/SHA256SUMS.txt`](release/SHA256SUMS.txt)

**直接下载：**
[`Sprocket.SpawnMenu.dll`](https://github.com/hhosj/sprocket-spawn-menu/raw/main/release/Sprocket.SpawnMenu.dll) ·
[`Sprocket.SpawnMenu-0.6.10.zip`](https://github.com/hhosj/sprocket-spawn-menu/raw/main/release/Sprocket.SpawnMenu-0.6.10.zip)

> 仓库里的这两个文件就是当前构建产物；版本发布时会同步到右侧 **Releases**。


## 安装（Windows，推荐）

1. 下载 **Sprocket Mod Manager**（Hans21223 的 Sprocket-Mod-Loader 项目 Release 里的 `Sprocket-Mod-Manager-1.4.0.zip`），解压到任意固定位置。
2. 双击 **`Mod Manager.exe`**（自带 Python，不需要安装别的东西）。
3. 点 **Install mod loader**，等到底部出现 `Installed and verified … loader files`。
   - 国内下载失败时：先用浏览器把 **BepInEx be.788** 和 **Sprocket 兼容补丁** 两个原始 zip 下好放进“下载”文件夹，再点一次按钮（Mod Manager 会自动采用并校验）。
4. 点 **Add mod** → 选上一步下载的 `Sprocket.SpawnMenu.dll`（或 `.zip`）→ 在列表里**双击**启用。
5. 点 **Launch**。首次启动慢 1~3 分钟（在生成 interop），之后正常。

## 安装（Linux / Steam Deck）

Mod Manager 是 Windows 专用程序，Linux 只能手动装：

```bash
GAME="$HOME/.local/share/Steam/steamapps/common/Sprocket"; cd "$GAME"

# 1) 加载器：be.788 + 兼容补丁（两行校验必须都 OK）
curl -fL -o bepinex.zip 'https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788%2B5b766a3.zip'
curl -fL -o patch.zip  'https://github.com/Hans21223/Sprocket-Mod-Loader/releases/download/v1.4.0/Sprocket-Mod-Loader-1.2.3.zip'
sha256sum -c <<'SUMS'
f4cc496bd098a0df4164b81e3737297707f13a47c2478dba2f60eefab784817a  bepinex.zip
e54f09a863913b96dc2eb15402ee1a8a91ba46d60548c3be04c6e672ec43fe15  patch.zip
SUMS
unzip -oq bepinex.zip
unzip -oq patch.zip 'Sprocket-Mod-Loader-1.2.3/Patch/*' -d t && cp -r t/Sprocket-Mod-Loader-1.2.3/Patch/BepInEx . && rm -rf t bepinex.zip patch.zip

# 2) 预置 3MB 的 Unity 基础库（国内容易卡死，建议先放好）
mkdir -p BepInEx/unity-libs
curl -fL -o BepInEx/unity-libs/6000.3.21.zip 'https://unity.bepinex.dev/libraries/6000.3.21.zip'

# 3) 本模组
unzip -o ~/Downloads/Sprocket.SpawnMenu-0.6.10.zip -d BepInEx/plugins/
```

然后在 Steam → Sprocket → 属性 → 启动选项加上（**必须**，否则 Proton 不会加载模组）：

```
WINEDLLOVERRIDES="winhttp=n,b" %command%
```

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

编辑 `BepInEx/config/sprocket.spawn.menu.cfg`，改完**重启游戏**生效：

| 项 | 说明 |
| --- | --- |
| `SpawnKey` / `SpawnFallbackKey` | 生成键，默认 `Z` / `F10` |
| `UiKey` | 面板键，默认 `B` |
| `NextKey` / `PrevKey` | 蓝图切换，默认 `]` / `[` |
| `Team` / `Group` | 生成队伍，默认 `2` / `1` |
| `SpawnAtAim` / `MaxAimDistance` | 准星生成及最大距离，默认开启 / `800` |
| `Distance` / `Height` | 回退位置距离 / 初始高度，默认 `150` / `2` |
| `GroundSnap` / `FacePlayer` | 贴地 / 车头朝向玩家，默认开启 |
| `ClearOnEditorReturn` / `CleanupOnMatchExit` | 回编辑器 / 离场清理，默认开启 |

## 卸载

删掉 `BepInEx/plugins/Sprocket.SpawnMenu.dll`。
想连加载器一起清掉：再删 `winhttp.dll`、`doorstop_config.ini`、`.doorstop_version`、`BepInEx/`、`dotnet/`。

## 注意

- **只在沙盒模式可用** —— 主菜单、任务场景、自定义战斗中被刻意禁用。
- 生成车辆由本模组手动 `Spawn + Register`；AI 不保证生成后立刻行动，通常要等重置战局。
- 重置战局一两次后，游戏特效可能消失（原生 `StageVehicleSpawnEvent` 方案尚未合并）。
- 游戏大版本更新后可能失效：本模组会写日志并停用对应功能，不会让游戏崩溃。
- 适用于 **Sprocket 0.2.55.5**；同时装其他改生成/AI 的模组可能冲突，排查时只留一个。

## 许可 / 致谢

本模组：MIT，见 `LICENSE`。

加载器是第三方项目，本仓库**不包含**它们：

- [BepInEx](https://github.com/BepInEx/BepInEx)
- [Hans21223/Sprocket-Mod-Loader](https://github.com/Hans21223/Sprocket-Mod-Loader) —— Sprocket 专用兼容补丁 + Mod Manager（LGPL-3.0）
- [Neemogudmen/SprocketSDK](https://github.com/Neemogudmen/SprocketSDK) —— 其发布包提供可直接下载的 be.788 底座（MIT）

开发与逆向笔记见 [`docs/开发笔记.md`](docs/开发笔记.md)。
