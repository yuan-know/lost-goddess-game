# 齿轮箱调试脚手架备份(2026-09-15)

用户 2026-09-15 19:4x:"把调试用的部分删去,然后把这一整个小游戏实现的资源打包推送到 github"

删除的东西(本目录即备份,正式仓库里**不再有**这些):

| 文件 | 原位置 | 作用 |
|---|---|---|
| `GearboxDebugBootstrap.cs` | `Assets/_Project/Scripts/Utils/` | 齿轮箱独立调试场景的启动器(相机裁条带 + 武器房 + 玩家 + 特写 + HUD) |
| `GearboxDebug.unity` | `Assets/_Project/Scenes/` | 对应的手写场景(GUID `fd5b49b1e00048d48e0198a5197b297d`) |

同时从 `ProjectSettings/EditorBuildSettings.asset` 里移除了 `GearboxDebug.unity` 条目。

另外从 `Core/UI/GearboxSpinCloseup.cs` 里删掉的调试开关:

- `InteractionEnabled`        —— 调试期交互总闸
- `SteamLayoutEditable`       —— 烟雾摆位模式(会创建全屏拖拽手柄)
- `HitZoneCalibration`        —— 热区标定模式(点击只打印 u,v)
- `SteamLayoutHandle`(类)     —— 烟雾拖动/缩放/旋转手柄
- `SteamLayoutText()`         —— 给 HUD 显示摆位参数
- `ResetRun()` / `ResetSolvedFlag()` —— 调试场景的重置 API

## 怎么恢复(以后要再调齿轮箱)

1. 把本目录的 4 个文件拷回原位(同名同 GUID,覆盖即可)
2. 在 `ProjectSettings/EditorBuildSettings.asset` 的 `m_Scenes` 里加回:
   ```
     - enabled: 1
       path: Assets/_Project/Scenes/GearboxDebug.unity
       guid: fd5b49b1e00048d48e0198a5197b297d
   ```
   (该 guid 就是本目录 `GearboxDebug.unity.meta` 里的那个)
3. 从 `GearboxSpinCloseup.cs` 的历史版本(git)里把上面那些开关加回来

⚠ 恢复后 Unity 需要重新编译。
