// ============================================================================
//  ConfirmDialog.cs —— 通用"是/否"二选一确认弹窗(2026-07-25)
//   用法:ConfirmDialog.Show("是否使用该道具？", onYes, onNo);
//   · 全屏半透明遮罩 + 居中问题文字 + 【是】【否】两个按钮。
//   · sortingOrder 高于特写(1050)/对话(1100)/表情(1200)/任务指引(1300),保证盖在最上层。
//   · 纯代码创建,零预制体。单例:同一时刻只允许一个确认框。
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;

namespace LostGoddess
{
    public static class ConfirmDialog
    {
        static GameObject _root;
        static Text _questionLabel;
        static Action _onYes;
        static Action _onNo;
        static bool _isOpen;

        public static bool IsOpen => _isOpen;

        public static void Show(string question, Action onYes, Action onNo = null)
        {
            EnsureRoot();
            _onYes = onYes;
            _onNo = onNo;
            _questionLabel.text = question;
            _root.SetActive(true);
            _isOpen = true;
        }

        static void Hide()
        {
            _isOpen = false;
            if (_root != null) _root.SetActive(false);
        }

        static void EnsureRoot()
        {
            if (_root != null) return;

            // 确保有 EventSystem(与 CloseupView/InventoryUI 一致的兜底)
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var esGo = new GameObject("EventSystem");
                UnityEngine.Object.DontDestroyOnLoad(esGo);
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            _root = new GameObject("~ConfirmDialog");
            UnityEngine.Object.DontDestroyOnLoad(_root);

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1400;   // 高于任务指引(1300)/表情(1200)/对话(1100)/特写(1050)
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            _root.AddComponent<GraphicRaycaster>();

            // 全屏半透明遮罩(拦截点击穿透)
            var dimGo = new GameObject("Dim");
            dimGo.transform.SetParent(_root.transform, false);
            var dimImg = dimGo.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.6f);
            dimImg.raycastTarget = true;
            var dimRt = dimImg.rectTransform;
            dimRt.anchorMin = Vector2.zero; dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = Vector2.zero; dimRt.offsetMax = Vector2.zero;

            // 中央面板
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(_root.transform, false);
            var panelImg = panelGo.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.10f, 0.08f, 0.96f);  // 深棕近黑
            var panelRt = panelImg.rectTransform;
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(760, 340);
            panelRt.anchoredPosition = Vector2.zero;

            // 面板描边
            var outline = panelGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.7f, 0.55f, 0.3f, 0.9f);  // 暖金描边
            outline.effectDistance = new Vector2(2.5f, -2.5f);

            // 问题文字
            var qGo = new GameObject("Question");
            qGo.transform.SetParent(panelGo.transform, false);
            _questionLabel = qGo.AddComponent<Text>();
            _questionLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _questionLabel.fontSize = 40;
            _questionLabel.color = new Color(1f, 0.96f, 0.9f);
            _questionLabel.alignment = TextAnchor.MiddleCenter;
            _questionLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _questionLabel.verticalOverflow = VerticalWrapMode.Overflow;
            var qRt = _questionLabel.rectTransform;
            qRt.anchorMin = new Vector2(0, 1); qRt.anchorMax = new Vector2(1, 1);
            qRt.pivot = new Vector2(0.5f, 1f);
            qRt.offsetMin = new Vector2(40, 0);
            qRt.offsetMax = new Vector2(-40, 0);
            qRt.sizeDelta = new Vector2(qRt.sizeDelta.x, 150);
            qRt.anchoredPosition = new Vector2(0, -50);

            // 【是】按钮(左)
            MakeButton(panelGo.transform, "YesButton", "是",
                new Vector2(-160, 70), new Color(0.35f, 0.5f, 0.28f),
                () => { var cb = _onYes; Hide(); cb?.Invoke(); });

            // 【否】按钮(右)
            MakeButton(panelGo.transform, "NoButton", "否",
                new Vector2(160, 70), new Color(0.5f, 0.3f, 0.28f),
                () => { var cb = _onNo; Hide(); cb?.Invoke(); });

            _root.SetActive(false);
        }

        static void MakeButton(Transform parent, string name, string label,
                               Vector2 sizeAndAnchorHelper, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            // sizeAndAnchorHelper.x = 相对面板中心的 X 偏移;.y = 按钮高度基准(仅用于计算)
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = bg;
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(220, 90);
            rt.anchoredPosition = new Vector2(sizeAndAnchorHelper.x, 40);

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.6f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            var txt = txtGo.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 40;
            txt.fontStyle = FontStyle.Bold;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = label;
            var trt = txt.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
        }
    }
}
