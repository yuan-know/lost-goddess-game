// ============================================================================
//  ItemUseCloseup.cs —— 背包双击"使用道具"时的特写 + 文案展示(2026-07-25)
//   InventoryUI 双击道具 → ConfirmDialog 确认 → ItemUseCloseup.Show(itemId)。
//   复用 CloseupView 弹出道具特写图(居中大图),底部字幕显示该道具的文案介绍。
//   特写关闭方式与拾取特写一致:文案播完 SetCanClose(true),点空白背景关闭。
// ============================================================================

using System.Collections;
using UnityEngine;

namespace LostGoddess
{
    public static class ItemUseCloseup
    {
        /// <summary>道具 id → 特写图 Resources 路径(优先 v2 精修图)。</summary>
        static string GetCloseupIconPath(string itemId)
        {
            switch (itemId)
            {
                case "crowbar":     return "UI/Icons/crowbar_v2";
                case "wrench":      return "UI/Icons/wrench";
                case "focus_lens":  return "UI/Icons/focus_lens_v2";
                case "brass_base":  return "Closeups/brass_base_closeup";  // 3400×1200 底座特写
                case "gear_1":      return "UI/Icons/gear_small_v2";
                case "gear_2":      return "UI/Icons/gear_big_v2";
                case "pottery_key": return "UI/Icons/pottery_key";  // 500×500 铜钥匙图标
                case "lantern":     return "UI/Icons/hand_lamp";
                case "magnifier":   return "UI/Icons/magnifier";
                case "hex_wrench":  return "UI/Icons/hex_wrench";
                case "light_projector": return "Closeups/light_projector";
                default:            return null;
            }
        }

        /// <summary>道具 id → 文案介绍(与拾取时一致)。</summary>
        static string GetDescription(string itemId)
        {
            switch (itemId)
            {
                case "crowbar":     return "一根锈迹斑斑的铁撬棍。撬头扁平，底部弯折，可用于撬开被卡住或封死的东西。还可以当作武器使用。";
                case "wrench":      return "一把铸铁扳手。可以转动齿轮、螺栓或者某种机关。";
                case "focus_lens":  return "透镜，可组合使用或嵌入特定机械装置中。";
                case "brass_base":  return "黄铜质地的机械底座，表面有卡槽和接口，可与其他零件组合形成完整器械。";
                case "gear_1":      return "表面有锈痕的齿轮，可组合使用或嵌入特定机械装置中。";
                case "gear_2":      return "黄铜齿轮，可组合使用或嵌入特定机械装置中。";
                case "pottery_key": return "一枚覆满铜锈的钥匙。";
                case "lantern":     return "一盏还能点亮的旧油灯。";
                case "magnifier":   return "一枚老式放大镜，能看清墙壁上细小的刻痕。";
                case "hex_wrench":  return "一把六角扳手。";
                case "light_projector": return "组合完成的光幕投影仪。";
                default:            return "";
            }
        }

        static CloseupRunner _runner;

        public static void Show(string itemId)
        {
            string iconPath = GetCloseupIconPath(itemId);
            string desc = GetDescription(itemId);

            if (string.IsNullOrEmpty(iconPath))
            {
                // 没有特写图 → 只弹文案
                if (!string.IsNullOrEmpty(desc)) DialogueSystem.ShowText(desc);
                return;
            }

            var sprite = LoadSprite(iconPath);
            if (sprite == null)
            {
                Debug.LogWarning($"[ItemUseCloseup] 未找到特写资源 '{iconPath}',仅弹文案");
                if (!string.IsNullOrEmpty(desc)) DialogueSystem.ShowText(desc);
                return;
            }

            // 运行时组装特写 UI 对象(与拾取特写一致的做法)
            var go = new GameObject("_ItemUseCloseup");
            var rt = go.AddComponent<RectTransform>();
            // brass_base 是 3400×1200 宽幅图,给大一点的框;其余方形道具用 700×700
            bool wide = itemId == "brass_base";
            rt.sizeDelta = wide ? new Vector2(1100f, 500f) : new Vector2(700f, 700f);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;

            CloseupView.Open(go);
            CloseupView.SetCanClose(false);

            // 用一个常驻协程宿主播放"文案→允许关闭→等关闭→销毁"序列
            EnsureRunner();
            _runner.Play(go, desc);
        }

        static Sprite LoadSprite(string path)
        {
            var sprite = Resources.Load<Sprite>(path);
            if (sprite != null) return sprite;
            var tex = Resources.Load<Texture2D>(path);
            if (tex != null)
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return null;
        }

        static void EnsureRunner()
        {
            if (_runner != null) return;
            var go = new GameObject("~ItemUseCloseupRunner");
            Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<CloseupRunner>();
        }

        /// <summary>协程宿主:文案播完允许关闭,等玩家点空白关闭后销毁特写内容。</summary>
        class CloseupRunner : MonoBehaviour
        {
            public void Play(GameObject closeupGo, string desc)
            {
                StopAllCoroutines();
                StartCoroutine(Run(closeupGo, desc));
            }

            IEnumerator Run(GameObject closeupGo, string desc)
            {
                bool done = false;
                if (!string.IsNullOrEmpty(desc))
                    DialogueSystem.ShowText(desc, () => done = true);
                else
                    done = true;
                while (!done) yield return null;

                CloseupView.SetCanClose(true);
                yield return new WaitWhile(() => CloseupView.IsOpen);

                if (closeupGo != null) Object.Destroy(closeupGo);
            }
        }
    }
}
