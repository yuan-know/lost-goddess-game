// ============================================================================
//  SceneLoader.cs —— 场景/房间切换(含协程淡入淡出)  🟢  契约 §4
//  B 只调用 GoToRoom(name)。系统内部处理淡入淡出与加载。
//
//  加载策略(两条路,自动选择):
//   1) 若该房间名在 Build Settings 里有对应 .unity 场景 → SceneManager.LoadScene。
//   2) 否则若注册了"程序化房间构建器"(验证阶段无美术、无 .unity 时用)→ 调用它重建内容。
//  美术/正式场景就绪后走 (1),验证期走 (2),接口对 B 一致。
// ============================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostGoddess
{
    public static class SceneLoader
    {
        static MonoBehaviour _host;          // 协程宿主(GameManager)
        static FadeOverlay _fade;            // 运行时创建的淡入淡出遮罩
        static bool _busy;

        /// <summary>程序化房间构建器:房间名 → 在当前场景重建该房间内容。
        /// 验证期由 Bootstrap 注册。为 null 时走标准 LoadScene。</summary>
        public static Func<string, IEnumerator> ProceduralRoomBuilder;

        public const float DefaultFade = 0.4f;

        public static void Init(MonoBehaviour host)
        {
            _host = host;
            _fade = FadeOverlay.CreateAttached();
        }

        public static void GoToRoom(string sceneName) => GoToRoom(sceneName, DefaultFade);

        public static void GoToRoom(string sceneName, float fadeTime)
        {
            if (_host == null)
            {
                Debug.LogError("[SceneLoader] 未初始化(GameManager 未运行?)");
                return;
            }
            if (_busy) { Debug.LogWarning("[SceneLoader] 正在切换,忽略重复请求。"); return; }
            _host.StartCoroutine(Transition(sceneName, fadeTime));
        }

        static IEnumerator Transition(string sceneName, float fadeTime)
        {
            _busy = true;
            GameState.CurrentRoom = sceneName;

            yield return _fade.FadeOut(fadeTime);

            bool sceneExistsInBuild = CanLoadScene(sceneName);
            if (sceneExistsInBuild)
            {
                var op = SceneManager.LoadSceneAsync(sceneName);
                while (op != null && !op.isDone) yield return null;
            }
            else if (ProceduralRoomBuilder != null)
            {
                yield return ProceduralRoomBuilder(sceneName);
            }
            else
            {
                Debug.LogWarning($"[SceneLoader] 房间 '{sceneName}' 既不在 Build Settings,也无程序化构建器。");
            }

            yield return _fade.FadeIn(fadeTime);
            _busy = false;
        }

        static bool CanLoadScene(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (name == sceneName) return true;
            }
            return false;
        }
    }

    /// <summary>运行时创建的全屏淡入淡出遮罩(纯代码,零预制体依赖)。</summary>
    public class FadeOverlay : MonoBehaviour
    {
        CanvasGroup _cg;

        public static FadeOverlay CreateAttached()
        {
            var go = new GameObject("~FadeOverlay");
            DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999; // 盖住一切
            go.AddComponent<UnityEngine.UI.CanvasScaler>();

            var imgGo = new GameObject("Black");
            imgGo.transform.SetParent(go.transform, false);
            var img = imgGo.AddComponent<UnityEngine.UI.Image>();
            img.color = Color.black;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var fade = go.AddComponent<FadeOverlay>();
            fade._cg = go.AddComponent<CanvasGroup>();
            fade._cg.alpha = 0f;
            fade._cg.blocksRaycasts = false;
            return fade;
        }

        public IEnumerator FadeOut(float t) => FadeTo(1f, t);
        public IEnumerator FadeIn(float t)  => FadeTo(0f, t);

        IEnumerator FadeTo(float target, float dur)
        {
            _cg.blocksRaycasts = true;
            float start = _cg.alpha, e = 0f;
            if (dur <= 0f) { _cg.alpha = target; }
            else
            {
                while (e < dur)
                {
                    e += Time.unscaledDeltaTime;
                    _cg.alpha = Mathf.Lerp(start, target, e / dur);
                    yield return null;
                }
                _cg.alpha = target;
            }
            _cg.blocksRaycasts = target > 0.5f;
        }
    }
}
