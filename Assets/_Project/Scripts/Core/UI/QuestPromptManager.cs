// ============================================================================
//  QuestPromptManager.cs —— 左上角常驻任务指引工具(2026-07-27 v2)
//   任务指引 UI:左上白色圆点 + 文字,常驻显示当前任务目标。
//   首次调用 UpdateText 时自动创建(DontDestroyOnLoad),之后跨场景常驻。
//   本静态类封装"改文字/隐藏/显示/销毁"操作,供各场景/交互调用。
//
//   v2 改动:创建逻辑从 FoyerInteractables 移到这里,任何场景都能按需创建,
//   避免"先去女神像密室解谜再回前厅"时 QuestPrompt 还不存在的警告。
// ============================================================================

using UnityEngine;
using UnityEngine.UI;

namespace LostGoddess
{
    public static class QuestPromptManager
    {
        const string kQuestPromptName = "QuestPrompt";
        static GameObject _cachedGo;
        static Text _cachedText;

        /// <summary>更新任务指引文字。如果指引对象不存在,当场创建一个。</summary>
        public static void UpdateText(string text)
        {
            EnsureCreated();
            if (_cachedText == null)
            {
                Debug.LogWarning("[QuestPromptManager] QuestPrompt Text 组件找不到");
                return;
            }
            _cachedText.text = "●  " + text;
            Debug.Log($"[QuestPromptManager] 任务指引更新为: {text}");
        }

        /// <summary>隐藏任务指引(不销毁,SetActive(false),可再显示)。</summary>
        public static void Hide()
        {
            if (_cachedGo == null) EnsureCreated();
            if (_cachedGo != null) _cachedGo.SetActive(false);
        }

        /// <summary>显示任务指引(若已隐藏则再显示)。不存在则创建。</summary>
        public static void Show()
        {
            EnsureCreated();
            if (_cachedGo != null) _cachedGo.SetActive(true);
        }

        /// <summary>彻底销毁任务指引(例如开锁完成后不需要了)。</summary>
        public static void Destroy()
        {
            if (_cachedGo != null)
            {
                Object.Destroy(_cachedGo);
                _cachedGo = null;
                _cachedText = null;
            }
            else
            {
                var go = GameObject.Find(kQuestPromptName);
                if (go != null) Object.Destroy(go);
            }
        }

        /// <summary>确保 QuestPrompt 对象已创建。</summary>
        static void EnsureCreated()
        {
            if (_cachedGo != null) return;

            // 先看看场景里有没有(可能是 FoyerInteractables 旧代码创建的)
            var go = GameObject.Find(kQuestPromptName);
            if (go != null)
            {
                _cachedGo = go;
                _cachedText = go.GetComponentInChildren<Text>();
                return;
            }

            // 没有就创建一个
            CreateQuestPrompt();
        }

        /// <summary>创建任务指引 UI(左上角,白色圆点 + 文字)。</summary>
        static void CreateQuestPrompt()
        {
            var go = new GameObject(kQuestPromptName);
            Object.DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1300;  // 高于 CloseupView(1050)、Dialogue(1100)、Portrait(1200)
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();

            // 背景面板(左上角锚点)
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(go.transform, false);
            var panelRT = panelGo.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0f, 1f);
            panelRT.anchorMax = new Vector2(0f, 1f);
            panelRT.pivot = new Vector2(0f, 1f);
            panelRT.anchoredPosition = new Vector2(20f, -20f);

            // 白色实心圆点
            var dotGo = new GameObject("Dot");
            dotGo.transform.SetParent(panelGo.transform, false);
            var dotImg = dotGo.AddComponent<Image>();
            dotImg.color = Color.white;
            dotImg.raycastTarget = false;
            var dotRT = dotImg.rectTransform;
            dotRT.anchorMin = new Vector2(0f, 0.5f);
            dotRT.anchorMax = new Vector2(0f, 0.5f);
            dotRT.pivot = new Vector2(0.5f, 0.5f);
            dotRT.sizeDelta = new Vector2(16f, 16f);
            dotRT.anchoredPosition = new Vector2(8f, 0f);

            // 任务文字
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(panelGo.transform, false);
            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 28;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.text = "";
            // 黑色描边保证可读性
            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            var textRT = text.rectTransform;
            textRT.anchorMin = new Vector2(0f, 0.5f);
            textRT.anchorMax = new Vector2(0f, 0.5f);
            textRT.pivot = new Vector2(0f, 0.5f);
            textRT.anchoredPosition = new Vector2(24f, 0f);
            textRT.sizeDelta = new Vector2(600f, 36f);

            _cachedGo = go;
            _cachedText = text;

            Debug.Log("[QuestPromptManager] 任务指引 UI 已创建");
        }
    }
}
