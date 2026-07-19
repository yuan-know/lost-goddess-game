// ============================================================================
//  CutsceneSteps.cs —— Cutscene 的 9 个最小步骤实现(契约 §12)
//  Say / Wait / WalkPlayerTo / Fade / Screenshake / SwitchEra / SetFlag / GoToScene / AwaitClick
// ============================================================================

using System.Collections;
using UnityEngine;

namespace LostGoddess
{
    /// <summary>播一条对白,等玩家点击推进。</summary>
    public class SayStep : CutsceneStep
    {
        public string dialogueId;
        public SayStep(string id) { dialogueId = id; }
        public override IEnumerator Execute(Cutscene owner)
        {
            bool done = false;
            DialogueSystem.Show(dialogueId, () => done = true);
            while (!done) yield return null;
        }
    }

    /// <summary>直接显示一段文本(不走对白表)。</summary>
    public class SayTextStep : CutsceneStep
    {
        public string text;
        public SayTextStep(string t) { text = t; }
        public override IEnumerator Execute(Cutscene owner)
        {
            bool done = false;
            DialogueSystem.ShowText(text, () => done = true);
            while (!done) yield return null;
        }
    }

    /// <summary>停顿。</summary>
    public class WaitStep : CutsceneStep
    {
        public float seconds;
        public WaitStep(float s) { seconds = s; }
        public override IEnumerator Execute(Cutscene owner)
        {
            yield return new WaitForSeconds(seconds);
        }
    }

    /// <summary>老人走到指定世界 X,到位继续。</summary>
    public class WalkPlayerToStep : CutsceneStep
    {
        public float worldX;
        public WalkPlayerToStep(float x) { worldX = x; }
        public override IEnumerator Execute(Cutscene owner)
        {
            var pc = PlayerController.Instance;
            if (pc == null) yield break;

            // Cutscene 里强制移动:不开启玩家控制,避免玩家点击覆盖目标导致剧情卡住。
            pc.SetControllable(false);
            bool arrived = false;
            pc.ForceWalkTo(new Vector2(worldX, pc.transform.position.y), () => arrived = true);
            while (!arrived) yield return null;
        }
    }

    /// <summary>全屏色淡入/淡出(白闪或黑幕)。fadeType:In=从透明淡到色 / Out=从色淡到透明 / Flash=先入再出。</summary>
    public enum FadeType { In, Out, Flash }
    public class FadeStep : CutsceneStep
    {
        public Color color;
        public float duration;
        public FadeType type;
        public FadeStep(Color c, float d, FadeType t = FadeType.In) { color = c; duration = d; type = t; }

        public override IEnumerator Execute(Cutscene owner)
        {
            var overlay = FadeOverlayColored.Get();
            if (type == FadeType.In)
            {
                yield return overlay.FadeToColor(color, duration);
            }
            else if (type == FadeType.Out)
            {
                yield return overlay.FadeToClear(duration);
            }
            else // Flash
            {
                yield return overlay.FadeToColor(color, duration * 0.4f);
                yield return new WaitForSeconds(duration * 0.2f);
                yield return overlay.FadeToClear(duration * 0.4f);
            }
        }
    }

    /// <summary>相机抖动。</summary>
    public class ScreenshakeStep : CutsceneStep
    {
        public float intensity;
        public float duration;
        public ScreenshakeStep(float i, float d) { intensity = i; duration = d; }
        public override IEnumerator Execute(Cutscene owner)
        {
            var cam = Camera.main;
            if (cam == null) { yield return new WaitForSeconds(duration); yield break; }
            Vector3 origin = cam.transform.position;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / duration);  // 衰减
                float dx = (UnityEngine.Random.value - 0.5f) * 2f * intensity * k;
                float dy = (UnityEngine.Random.value - 0.5f) * 2f * intensity * k;
                cam.transform.position = origin + new Vector3(dx, dy, 0);
                yield return null;
            }
            cam.transform.position = origin;
        }
    }

    /// <summary>切换 Era + 播闪白动画。</summary>
    public class SwitchEraStep : CutsceneStep
    {
        public Era targetEra;
        public bool withFlash;
        public SwitchEraStep(Era e, bool flash = true) { targetEra = e; withFlash = flash; }
        public override IEnumerator Execute(Cutscene owner)
        {
            if (withFlash)
            {
                var overlay = FadeOverlayColored.Get();
                yield return overlay.FadeToColor(Color.white, 0.3f);
                GameState.UnlockEra(targetEra);
                GameState.SetEra(targetEra);
                yield return new WaitForSeconds(0.15f);
                yield return overlay.FadeToClear(0.5f);
            }
            else
            {
                GameState.UnlockEra(targetEra);
                GameState.SetEra(targetEra);
            }
        }
    }

    /// <summary>设置存档 Flag。</summary>
    public class SetFlagStep : CutsceneStep
    {
        public string key;
        public bool value;
        public SetFlagStep(string k, bool v) { key = k; value = v; }
        public override IEnumerator Execute(Cutscene owner)
        {
            GameState.SetFlag(key, value);
            yield break;
        }
    }

    /// <summary>换场景(带淡出,等 SceneLoader 完成)。</summary>
    public class GoToSceneStep : CutsceneStep
    {
        public string sceneName;
        public float fadeTime;
        public GoToSceneStep(string s, float f = 0.4f) { sceneName = s; fadeTime = f; }
        public override IEnumerator Execute(Cutscene owner)
        {
            SceneLoader.GoToRoom(sceneName, fadeTime);
            // 等 SceneLoader 完成(通过 OnAfterLoad 事件感知)
            bool loaded = false;
            System.Action<string> handler = (_) => loaded = true;
            SceneLoader.OnAfterLoad += handler;
            // 保险:超时 5 秒也继续
            float t = 0f;
            while (!loaded && t < 5f) { t += Time.deltaTime; yield return null; }
            SceneLoader.OnAfterLoad -= handler;
            // 关键:场景已切,新 Player 是新对象。旧 Cutscene 若继续跑末尾 SetControllable(true) 是无害的,
            // 但若 owner 已随旧场景根 Destroy,协程也就断了——两条路都不会污染新场景。
            // 但要防"旧 Cutscene 仍活着 + 后面还有 step"的情况:切场景应该视为 Cutscene 的终点,
            // 立即 Stop 掉,避免后续 step 作用到新 Player 上。
            if (owner != null) owner.Stop();
        }
    }

    /// <summary>等玩家点击任意处继续。</summary>
    public class AwaitClickStep : CutsceneStep
    {
        public override IEnumerator Execute(Cutscene owner)
        {
            // 忽略当帧按下(避免 Say 完立刻吃掉这个 click)
            yield return null;
            while (!Input.GetMouseButtonDown(0)) yield return null;
        }
    }

    /// <summary>等玩家按指定键(如剧情杀里"按 1 切青年"提示)。</summary>
    public class AwaitKeyStep : CutsceneStep
    {
        public KeyCode key;
        public AwaitKeyStep(KeyCode k) { key = k; }
        public override IEnumerator Execute(Cutscene owner)
        {
            yield return null;
            while (!Input.GetKeyDown(key)) yield return null;
        }
    }

    // ------------------------------------------------------------------------
    //  FadeOverlayColored —— Cutscene 专用彩色全屏 overlay(区别于 SceneLoader 的黑幕)
    //  单例,DontDestroyOnLoad;可 FadeToColor(白闪/红警等)+ FadeToClear。
    // ------------------------------------------------------------------------

    public class FadeOverlayColored : MonoBehaviour
    {
        static FadeOverlayColored _inst;
        UnityEngine.UI.Image _img;

        public static FadeOverlayColored Get()
        {
            if (_inst != null) return _inst;
            var go = new GameObject("~FadeOverlayColored");
            Object.DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9998;  // 在 FadeOverlay(9999) 之下、DialogueUI(400) 之上
            go.AddComponent<UnityEngine.UI.CanvasScaler>();
            var imgGo = new GameObject("Color");
            imgGo.transform.SetParent(go.transform, false);
            var img = imgGo.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(0, 0, 0, 0);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            _inst = go.AddComponent<FadeOverlayColored>();
            _inst._img = img;
            return _inst;
        }

        public IEnumerator FadeToColor(Color c, float dur)
        {
            Color from = _img.color;
            Color to = new Color(c.r, c.g, c.b, c.a > 0f ? c.a : 1f);
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                _img.color = Color.Lerp(from, to, Mathf.Clamp01(t / dur));
                yield return null;
            }
            _img.color = to;
        }

        public IEnumerator FadeToClear(float dur)
        {
            Color from = _img.color;
            Color to = new Color(from.r, from.g, from.b, 0f);
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                _img.color = Color.Lerp(from, to, Mathf.Clamp01(t / dur));
                yield return null;
            }
            _img.color = to;
        }
    }
}
