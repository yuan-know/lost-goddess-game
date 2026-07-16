// ============================================================================
//  InteractableBase.cs —— 可交互物件基类(契约 §2)  🟢
//  B 的所有可点击物继承它,只写 OnClick() 里"到位后发生什么"。
//  交互流程:点击命中 → 若 walkToBeforeInteract,老人先走到 interactPoint →
//            到位后回调 OnClick();否则立即 OnClick()。行走调度由本基类处理,B 不用管。
//  能力钩子:requiredEra —— 分章固定形态下,某交互可能要求特定形态才可用。
// ============================================================================

using UnityEngine;

namespace LostGoddess
{
    [RequireComponent(typeof(Collider2D))]
    public abstract class InteractableBase : MonoBehaviour
    {
        [Header("交互(契约 §2)")]
        [Tooltip("老人交互前该站的位置(空物体);为空则站原地")]
        public Transform interactPoint;
        [Tooltip("是否需要先走过去;false=点了立即触发")]
        public bool walkToBeforeInteract = true;

        [Header("能力要求(可选,分章固定形态)")]
        [Tooltip("是否限定某形态才能交互")]
        public bool eraRestricted = false;
        public Era requiredEra = Era.Old;

        [Header("高亮(可空)")]
        public SpriteRenderer highlightTarget;
        public Color highlightColor = new Color(1f, 1f, 0.6f);

        Color _baseColor;
        bool _hasHighlight;

        protected virtual void Awake()
        {
            if (highlightTarget != null)
            {
                _baseColor = highlightTarget.color;
                _hasHighlight = true;
            }
        }

        /// <summary>由 ClickInputManager 调用:玩家点了这个物件。处理走近 → 触发。</summary>
        public void RequestInteract()
        {
            if (eraRestricted && GameState.CurrentEra != requiredEra)
            {
                OnEraMismatch();
                return;
            }

            if (walkToBeforeInteract && PlayerController.Instance != null)
            {
                Transform dest = interactPoint != null ? interactPoint : transform;
                PlayerController.Instance.WalkTo(dest, OnClick);
            }
            else
            {
                OnClick();
            }
        }

        /// <summary>必须实现:老人到位后发生什么(开抽屉、进特写、捡道具…)。</summary>
        public abstract void OnClick();

        /// <summary>形态不满足时的默认反馈(子类可覆盖,如播一句旁白)。</summary>
        protected virtual void OnEraMismatch()
        {
            Debug.Log($"[Interactable] {name}: 当前形态({GameState.CurrentEra})无法与之交互(需要 {requiredEra})。");
        }

        // ── 受保护辅助(契约 §2)──

        /// <summary>是否持有某道具(检查背包)。</summary>
        protected bool RequireItem(string id) => InventorySystem.Has(id);

        /// <summary>是否"手上正选中"某道具(选中→点目标模式,契约 §9)。</summary>
        protected bool IsHolding(string id) => InventorySystem.SelectedItem == id;

        protected void PlaySfx(string clip) => AudioManager.PlaySfx(clip);

        /// <summary>鼠标悬停高亮(由 ClickInputManager 调用)。</summary>
        public virtual void Highlight(bool on)
        {
            if (!_hasHighlight) return;
            highlightTarget.color = on ? highlightColor : _baseColor;
        }
    }
}
