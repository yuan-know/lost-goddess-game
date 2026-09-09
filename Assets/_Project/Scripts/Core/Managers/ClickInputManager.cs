// ============================================================================
//  ClickInputManager.cs —— 点击输入调度  🟢
//  统一处理鼠标:
//   · 悬停可交互物 → 高亮
//   · 点中可交互物 → RequestInteract()(内部走近→触发 OnClick)
//   · 点中空地     → 老人 WalkTo(clamp 进可走区)
//   · 特写打开 / 老人不可控时 → 不驱动行走(交给特写层自己处理)
//  输入用旧版 Input + Physics2D(2D 射线),零 InputSystem 配置。
//  注意:UI 上的点击(EventSystem)不触发世界点击。
// ============================================================================

using UnityEngine;
using UnityEngine.EventSystems;

namespace LostGoddess
{
    public class ClickInputManager : MonoBehaviour
    {
        [Tooltip("场景主相机;留空自动取 Camera.main")]
        public Camera cam;

        [Tooltip("可交互物所在层(留空则检测所有层)")]
        public LayerMask interactableMask = ~0;

        InteractableBase _hovered;

        void Awake()
        {
            if (cam == null) cam = Camera.main;
        }

        void Update()
        {
            if (cam == null) { cam = Camera.main; if (cam == null) return; }

            // 特写打开时,世界点击交给特写层,这里不处理行走/交互
            if (CloseupView.IsOpen) { ClearHover(); return; }

            // 对话播放时,全屏点击只用于推进对话,不触发世界交互/移动
            if (DialogueSystem.IsPlaying) { ClearHover(); return; }

            Vector2 worldPoint = cam.ScreenToWorldPoint(Input.mousePosition);
            // Physics2D.OverlapPoint 默认 ignoreTriggers = true → 忽略所有 isTrigger = true 的碰撞体
            // 我们所有交互物都是 trigger，所以必须设置 contactFilter.useTriggers = true 才能检测到！
            ContactFilter2D contactFilter = new ContactFilter2D();
            contactFilter.layerMask = interactableMask;
            contactFilter.useTriggers = true;
            contactFilter.useLayerMask = true;

            // 取该点下【全部】重叠碰撞体,而不是只取一个:
            // 角色自身的 trigger BoxCollider 可能盖在交互物上(逐帧版角色碰撞体近全身大小,
            // 不像骨骼版被 0.25 根缩放缩到很小)。只取第一个会随机命中角色碰撞体 →
            // 拿不到 InteractableBase → 误判成"点空地",表现为人物挡住时点不了交互。
            // 这里跳过任何不带 InteractableBase 的碰撞体(角色/装饰),挑出真正的交互物。
            var hits = new Collider2D[16];
            int count = Physics2D.OverlapPoint(worldPoint, contactFilter, hits);
            InteractableBase target = null;
            for (int i = 0; i < count; i++)
            {
                var t = hits[i] != null ? hits[i].GetComponentInParent<InteractableBase>() : null;
                if (t != null) { target = t; break; }
            }

            UpdateHover(target);

            if (Input.GetMouseButtonDown(0))
            {
                // 点在 UI 上(背包槽、对白条等)→ 让 UI 处理,不驱动世界
                if (IsPointerOverUI()) return;

                if (target != null)
                    target.RequestInteract();
                else
                    WalkToPoint(worldPoint);
            }
        }

        void WalkToPoint(Vector2 worldPoint)
        {
            if (PlayerController.Instance == null) return;
            PlayerController.Instance.WalkTo(worldPoint);
        }

        void UpdateHover(InteractableBase target)
        {
            if (target == _hovered) return;
            if (_hovered != null) _hovered.Highlight(false);
            _hovered = target;
            if (_hovered != null) _hovered.Highlight(true);
        }

        void ClearHover()
        {
            if (_hovered != null) { _hovered.Highlight(false); _hovered = null; }
        }

        static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
