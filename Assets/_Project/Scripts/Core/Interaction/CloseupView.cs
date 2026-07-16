// ============================================================================
//  CloseupView.cs —— 特写视图(契约 §10)  🟢
//  点抽屉/信件等"细看"物件时不换场景,弹出特写层,可继续点交互,点返回退出。
//  打开时 PlayerController 自动锁定(不响应行走点击),关闭后恢复。
//  实现:一个运行时创建的 UI 层,放入指定的特写预制体/子画面。
// ============================================================================

using UnityEngine;

namespace LostGoddess
{
    public static class CloseupView
    {
        static GameObject _root;      // 特写层根(带半透明背景 + 内容挂点)
        static Transform _content;    // 当前特写内容挂点
        static GameObject _currentInstance;

        public static bool IsOpen { get; private set; }

        static void EnsureRoot()
        {
            if (_root != null) return;

            _root = new GameObject("~CloseupView");
            Object.DontDestroyOnLoad(_root);

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500; // 在游戏之上、淡入淡出遮罩之下
            _root.AddComponent<UnityEngine.UI.CanvasScaler>();
            _root.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // 半透明黑底(挡住并可点空白返回)
            var bgGo = new GameObject("DimBackground");
            bgGo.transform.SetParent(_root.transform, false);
            var img = bgGo.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(0, 0, 0, 0.7f);
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var btn = bgGo.AddComponent<UnityEngine.UI.Button>();
            btn.transition = UnityEngine.UI.Selectable.Transition.None;
            btn.onClick.AddListener(Close); // 点空白返回

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(_root.transform, false);
            var crt = contentGo.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = Vector2.zero;
            _content = contentGo.transform;

            _root.SetActive(false);
        }

        /// <summary>按 id 从 Resources/Closeups 加载特写预制体并打开。</summary>
        public static void Open(string closeupId)
        {
            var prefab = Resources.Load<GameObject>("Closeups/" + closeupId);
            if (prefab == null)
            {
                Debug.LogWarning($"[CloseupView] 未找到特写预制体 'Closeups/{closeupId}'。");
                return;
            }
            Open(prefab);
        }

        public static void Open(GameObject closeupPrefab)
        {
            EnsureRoot();
            if (IsOpen) Close();

            _root.SetActive(true);
            IsOpen = true;

            if (closeupPrefab != null)
                _currentInstance = Object.Instantiate(closeupPrefab, _content);

            // 锁定老人行走
            if (PlayerController.Instance != null)
                PlayerController.Instance.SetControllable(false);
        }

        public static void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;

            if (_currentInstance != null) Object.Destroy(_currentInstance);
            _currentInstance = null;
            if (_root != null) _root.SetActive(false);

            if (PlayerController.Instance != null)
                PlayerController.Instance.SetControllable(true);
        }
    }
}
