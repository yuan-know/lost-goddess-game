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

            Vector2 worldPoint = cam.ScreenToWorldPoint(Input.mousePosition);
            var hit = Physics2D.OverlapPoint(worldPoint, interactableMask);
            InteractableBase target = hit != null ? hit.GetComponentInParent<InteractableBase>() : null;

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
