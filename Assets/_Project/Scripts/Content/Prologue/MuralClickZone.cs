// ============================================================================
//  MuralClickZone.cs —— 壁画点击区域
//  挂在色差方块上，处理左/右壁画的点击
// ============================================================================

using UnityEngine;

namespace LostGoddess.Content
{
    public class MuralClickZone : MonoBehaviour
    {
        public string side;  // "left" 或 "right"
        public MuralPulseEffect pulseEffect;

        void OnMouseDown()
        {
            Debug.Log($"[MuralClickZone] 点击了{side}侧壁画");
            ShowMuralCloseup();
        }

        void ShowMuralCloseup()
        {
            // 加载壁画特写
            var tex = Resources.Load<Texture2D>("Closeups/mural_prologue");
            if (tex == null)
            {
                tex = Resources.Load<Texture2D>("Closeups/mural_prologue_v2");
            }

            if (tex == null)
            {
                Debug.LogWarning("[MuralClickZone] 未找到壁画特写资源");
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

            Debug.Log($"[MuralClickZone] 打开{side}侧壁画特写");

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
            if (pulseEffect != null)
            {
                pulseEffect.StopPulse();
                Debug.Log("[MuralClickZone] 关闭特写，停止脉冲效果");
            }
        }
    }
}
