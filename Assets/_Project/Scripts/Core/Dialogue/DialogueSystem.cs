// ============================================================================
//  DialogueSystem.cs —— 对白/旁白(契约 §5)  🟢
//  Show(id) 按 id 从对白表播放。验证期用运行时创建的底部文本条 + 内置示例文本;
//  正式期文案表可换成 ScriptableObject / json,接口对 B 不变。
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LostGoddess
{
    public static class DialogueSystem
    {
        // 简单内置对白表(验证用)。正式期从 Data/Dialogues 资源加载。
        static readonly Dictionary<string, string> _table = new Dictionary<string, string>
        {
            { Dialogues.demo_intro,  "……又是这样一扇门。我这把老骨头,还能推开它吗?" },
            { Dialogues.demo_locked, "锁着的。得先找到点亮它的办法。" },
        };

        static DialogueUI _ui;

        static void EnsureUI()
        {
            if (_ui != null) return;
            _ui = DialogueUI.CreateAttached();
        }

        public static void Show(string dialogueId) => Show(dialogueId, null);

        public static void Show(string dialogueId, Action onFinish)
        {
            EnsureUI();
            string text = _table.TryGetValue(dialogueId, out var t) ? t : dialogueId; // 找不到就直接显示 id 文本
            _ui.Play(text, onFinish);
        }

        /// <summary>直接显示一段文本(不走对白表)。</summary>
        public static void ShowText(string text, Action onFinish = null)
        {
            EnsureUI();
            _ui.Play(text, onFinish);
        }
    }

    /// <summary>运行时创建的底部对白条(纯代码,零预制体)。点击/等待后消失。</summary>
    public class DialogueUI : MonoBehaviour
    {
        Text _label;
        CanvasGroup _cg;
        Action _onFinish;
        Coroutine _routine;

        public static DialogueUI CreateAttached()
        {
            var go = new GameObject("~DialogueUI");
            DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();

            // 底部半透明条
            var barGo = new GameObject("Bar");
            barGo.transform.SetParent(go.transform, false);
            var barImg = barGo.AddComponent<Image>();
            barImg.color = new Color(0, 0, 0, 0.75f);
            var brt = barImg.rectTransform;
            brt.anchorMin = new Vector2(0, 0);
            brt.anchorMax = new Vector2(1, 0);
            brt.pivot = new Vector2(0.5f, 0);
            brt.sizeDelta = new Vector2(0, 220);
            brt.anchoredPosition = Vector2.zero;

            // 文本
            var txtGo = new GameObject("Label");
            txtGo.transform.SetParent(barGo.transform, false);
            var label = txtGo.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 34;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(80, 30);
            lrt.offsetMax = new Vector2(-80, -30);

            var ui = go.AddComponent<DialogueUI>();
            ui._label = label;
            ui._cg = go.AddComponent<CanvasGroup>();
            ui._cg.alpha = 0f;
            ui._cg.blocksRaycasts = false;
            return ui;
        }

        public void Play(string text, Action onFinish)
        {
            if (_routine != null) StopCoroutine(_routine);
            _onFinish = onFinish;
            _label.text = text;
            _routine = StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            _cg.alpha = 1f;
            _cg.blocksRaycasts = true;

            // 等待点击或超时(按文本长度)
            float minShow = 0.4f;
            float t = 0f;
            while (t < minShow) { t += Time.deltaTime; yield return null; }

            while (!Input.GetMouseButtonDown(0)) yield return null;

            _cg.alpha = 0f;
            _cg.blocksRaycasts = false;

            var cb = _onFinish;
            _onFinish = null;
            _routine = null;
            cb?.Invoke();
        }
    }
}
