// ============================================================================
//  Interact_Gearbox.cs —— 齿轮箱交互物(特写层小游戏的**入口**)
//
//  2026-09-15 新增,按项目既有范式(见 Content/Prologue/Interact_EyeConsole.cs)
//  继承 InteractableBase:
//    玩家点击 → 老人走到 interactPoint → OnClick()
//      → GearboxSpinCloseup.Show() 弹出特写 + 开始小游戏
//
//  玩法(见 GearboxSpinCloseup.cs 文件头):
//    点右侧大传动轴 → 齿轮箱内部旋转动画播完 → 通关
//    点其他零件     → 烟雾一遍 → 刻痕弹出 → 淡出 → 重开
//
//  ★ 关卡接入须知:
//    · 挂在齿轮箱的 Collider2D 物体上(基类有 [RequireComponent(typeof(Collider2D))])。
//    · **剧情推进接在 onSolved 上**(UnityEvent,Inspector 里拖)。
//      触发时机 = 旋转动画播完 → **特写自动收起** → 然后才触发,
//      所以可以直接在里面切场景 / 播对白 / 开门,不会有特写挡在上面。
//    · 形态限制用基类的 allowedEras[](齿轮箱是 S05 中年线 → 填 Middle)。
//      不匹配时播 wrongEraDialogueId。
//    · interactPoint 建议摆在齿轮箱正前方,老人走过去再开特写。
//
//  ⚠ 想在编辑器里单独看这个特写:GearboxSpinCloseup.Show() 直接调即可。
//    (原先有个专门的调试场景 GearboxDebug.unity + GearboxDebugBootstrap.cs,
//     2026-09-15 交付时已删掉,备份在 docs/backups/20260915_gearbox_debug_scaffold/。)
// ============================================================================

using LostGoddess;
using UnityEngine;
using UnityEngine.Events;

namespace LostGoddess.Content
{
    public class Interact_Gearbox : InteractableBase
    {
        [Header("齿轮箱小游戏")]
        [Tooltip("通关触发:旋转动画播完 → 特写自动收起 → 然后触发这里。接剧情推进 / 切场景 / 开门等。")]
        public UnityEvent onSolved;

        public override void OnClick()
        {
            // ★ 确保通关后收起特写并把回调放出去续剧情(CloseOnSolved 默认就是 true)
            GearboxSpinCloseup.CloseOnSolved = true;
            GearboxSpinCloseup.Show(() => onSolved?.Invoke());
        }
    }
}
