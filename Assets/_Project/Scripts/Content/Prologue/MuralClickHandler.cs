// ============================================================================
//  MuralClickHandler.cs —— 壁画图层点击处理
//  不使用碰撞体，通过壁画bounds手动判断点击区域
// ============================================================================

using UnityEngine;

namespace LostGoddess.Content
{
    public class MuralClickHandler : MonoBehaviour
    {
        public Bounds muralBounds;           // 壁画图层的bounds
        public Vector3 muralPosition;        // 壁画图层的position

        private MuralPulseEffect _pulseEffect;

        void Start()
        {
            _pulseEffect = GetComponent<MuralPulseEffect>();
            Debug.Log($"[MuralClickHandler] 壁画bounds: size=({muralBounds.size.x:F2},{muralBounds.size.y:F2}), center=({muralBounds.center.x:F2},{muralBounds.center.y:F2})");
            Debug.Log($"[MuralClickHandler] 壁画position: ({muralPosition.x:F2},{muralPosition.y:F2})");
        }

        void Update()
        {
            // 对话/特写打开时不响应点击
            if (DialogueSystem.IsPlaying || CloseupView.IsOpen) return;

            // 手动检测点击
            if (Input.GetMouseButtonDown(0))
            {
                Vector3 mousePos = Input.mousePosition;
                Vector3 worldPos = Camera.main.ScreenToWorldPoint(mousePos);

                // 计算壁画在世界空间中的实际bounds
                Bounds worldBounds = new Bounds(
                    muralPosition + muralBounds.center,
                    muralBounds.size
                );

                // 检查点击是否在壁画范围内
                if (worldBounds.Contains(new Vector3(worldPos.x, worldPos.y, worldBounds.center.z)))
                {
                    Debug.Log($"[MuralClickHandler] 点击在壁画范围内，世界坐标: ({worldPos.x:F2}, {worldPos.y:F2})");
                    OnMuralClicked(worldPos);
                }
            }
        }

        void OnMuralClicked(Vector3 clickPos)
        {
            // 根据 X 坐标判断点击的是左侧还是右侧壁画
            if (clickPos.x < 0)
            {
                Debug.Log("[MuralClickHandler] 点击了左侧壁画");
                ShowMuralCloseup("left");
            }
            else
            {
                Debug.Log("[MuralClickHandler] 点击了右侧壁画");
                ShowMuralCloseup("right");
            }
        }

        void ShowMuralCloseup(string side)
        {
            // 加载壁画特写
            var tex = Resources.Load<Texture2D>("Closeups/mural_prologue");
            if (tex == null)
            {
                tex = Resources.Load<Texture2D>("Closeups/mural_prologue_v2");
            }

            if (tex == null)
            {
                Debug.LogWarning("[MuralClickHandler] 未找到壁画特写资源");
                DialogueSystem.ShowText("古老的壁画，刻画着神秘的图腾和符号。");
                return;
            }

            // 创建Sprite
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

            // 创建特写UI
            var go = new GameObject("_MuralCloseupTemplate");
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1920, 678);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.preserveAspect = false;
            img.raycastTarget = false;

            Debug.Log($"[MuralClickHandler] 打开{side}侧壁画特写, texture={tex.width}x{tex.height}");

            // 锁定玩家控制
            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);

            CloseupView.Open(go, null);
            StartCoroutine(PlayMuralSubtitleSequence(go, pc));
        }

        System.Collections.IEnumerator PlayMuralSubtitleSequence(GameObject closeupGo, PlayerController pc)
        {
            bool done = false;

            CloseupView.SetCanClose(false);

            DialogueSystem.ShowText("古老的壁画，似乎在讲述一个久远的故事……", () => done = true);
            while (!done) yield return null;

            CloseupView.SetCanClose(true);

            // 等待用户关闭特写
            while (closeupGo != null) yield return null;

            // 关闭后恢复
            if (pc != null) pc.SetControllable(true);

            // 停止脉冲效果
            if (_pulseEffect != null)
            {
                _pulseEffect.StopPulse();
                Debug.Log("[MuralClickHandler] 关闭特写，停止脉冲效果");
            }
        }
    }
}
