// ============================================================================
//  Interact_EyeConsole.cs —— 黄金瞳控制台交互物（正式版）
//
//  挂在控制台的 Collider2D 物体上（InteractableBase 要求），玩家点击 →
//  老人走到 interactPoint → 打开黄金瞳特写。
//
//  纯交互：solution 保持空（各字段都是 NaN）即可，玩家随便摆弄不判定。
//  当谜题用：在 Inspector 里填 solution（0~1 归一化，NaN 项不参与判定），
//  三个控件都摆到位并稳定约 0.35 秒后触发 onSolved，特写自动收起。
// ============================================================================

using LostGoddess;
using UnityEngine;
using UnityEngine.Events;

namespace LostGoddess.Content
{
    public class Interact_EyeConsole : InteractableBase
    {
        [Header("黄金瞳谜题")]
        [Tooltip("谜题目标值。留空(NaN) = 纯交互不判定")]
        public EyeConsoleSolution solution;

        [Tooltip("解谜成功后触发（特写自动收起前）")]
        public UnityEvent onSolved;

        public override void OnClick()
        {
            EyeConsoleCloseup.Show(null, solution, () => onSolved?.Invoke());
        }
    }
}
