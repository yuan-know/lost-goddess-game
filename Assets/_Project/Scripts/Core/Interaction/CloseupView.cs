// ============================================================================
//  CloseupView.cs —— 特写视图(契约 §10)  🟢
//  点抽屉/信件等"细看"物件时不换场景,弹出特写层,可继续点交互,点返回退出。
//  打开时 PlayerController 自动锁定(不响应行走点击),关闭后恢复。
//  实现:一个运行时创建的 UI 层,放入指定的特写预制体/子画面。
// ============================================================================

using UnityEngine;
using UnityEngine.UI;

namespace LostGoddess
{
    public static class CloseupView
    {
        static GameObject _root;      // 特写层根(带半透明背景 + 内容挂点)
        static Transform _content;    // 当前特写内容挂点
        static GameObject _currentInstance;
        static System.Action _onClosed; // 特写关闭后的回调
        static Button _backgroundButton; // 背景按钮，可动态禁用点击

        public static bool IsOpen { get; private set; }
        public static bool CanClose { get; private set; } // 是否允许点击背景关闭

        static void EnsureRoot()
        {
            if (_root != null) return;

            // 确保场景中有EventSystem,否则UI点击事件不会触发
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var esGo = new GameObject("EventSystem");
                Object.DontDestroyOnLoad(esGo);
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Debug.Log("[CloseupView] 创建了EventSystem");
            }

            _root = new GameObject("~CloseupView");
            Object.DontDestroyOnLoad(_root);

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 2026-07-21 二修:sortingOrder 必须高于 LetterboxOverlay(1000),否则特写会被黑边遮住上下部。
            // 2026-07-22 修复:特写层级调低到 1050,让对话 UI(1100)和表情特写(1200)能显示在特写之上。
            //   需求:文案和表情要和特写同框,必须能被看到。
            canvas.sortingOrder = 1050;
            var scaler = _root.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            _root.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // 半透明黑底(挡住并可点空白返回)
            var bgGo = new GameObject("DimBackground");
            bgGo.transform.SetParent(_root.transform, false);
            var img = bgGo.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(0, 0, 0, 0.7f);
            img.raycastTarget = true;  // 确保可以接收点击
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            _backgroundButton = bgGo.AddComponent<UnityEngine.UI.Button>();
            _backgroundButton.transition = UnityEngine.UI.Selectable.Transition.None;
            _backgroundButton.targetGraphic = img;  // 明确设置按钮的图形目标
            _backgroundButton.onClick.AddListener(() => {
                if (CanClose)
                {
                    Debug.Log("[CloseupView] 背景按钮被点击,关闭特写");
                    Close();
                }
                else
                {
                    Debug.Log("[CloseupView] 背景点击被忽略 - CanClose=false，还在播放字幕");
                }
            });

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(_root.transform, false);
            var crt = contentGo.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = Vector2.zero;
            _content = contentGo.transform;

            _root.SetActive(false);
            Debug.Log("[CloseupView] CloseupView UI 已创建");
        }

        /// <summary>按 id 从 Resources/Closeups 加载特写预制体并打开。</summary>
        /// <param name="onClosed">用户点空白关闭特写后执行的回调(可选)。</param>
        public static void Open(string closeupId, System.Action onClosed = null)
        {
            var prefab = Resources.Load<GameObject>("Closeups/" + closeupId);
            if (prefab == null)
            {
                Debug.LogWarning($"[CloseupView] 未找到特写预制体 'Closeups/{closeupId}'。");
                return;
            }
            Open(prefab, onClosed);
        }

        /// <param name="onClosed">用户点空白关闭特写后执行的回调(可选)。</param>
        public static void Open(GameObject closeupPrefab, System.Action onClosed = null)
        {
            EnsureRoot();
            if (IsOpen) Close();

            _onClosed = onClosed;
            _root.SetActive(true);
            IsOpen = true;
            CanClose = true; // 默认允许关闭，保持向后兼容

            Debug.Log("[CloseupView] Open() 被调用, prefab=" + (closeupPrefab != null ? closeupPrefab.name : "null"));

            if (closeupPrefab != null)
                _currentInstance = Object.Instantiate(closeupPrefab, _content);

            // 锁定老人行走
            if (PlayerController.Instance != null)
                PlayerController.Instance.SetControllable(false);

            Debug.Log("[CloseupView] 特写已打开, _root.activeSelf=" + _root.activeSelf + ", IsOpen=" + IsOpen);
        }

        /// <summary>直接打开一个已构建好的 GameObject(不做 Instantiate)。
        /// 适用于运行时动态构建且带有匿名委托/事件回调的内容(Instantiate 会丢失匿名方法)。</summary>
        public static void OpenExisting(GameObject go, System.Action onClosed = null)
        {
            EnsureRoot();
            if (IsOpen) Close();

            _onClosed = onClosed;
            _root.SetActive(true);
            IsOpen = true;
            CanClose = true;

            Debug.Log("[CloseupView] OpenExisting() 被调用, go=" + (go != null ? go.name : "null"));

            if (go != null)
            {
                _currentInstance = go;
                go.transform.SetParent(_content, false);
            }

            if (PlayerController.Instance != null)
                PlayerController.Instance.SetControllable(false);

            Debug.Log("[CloseupView] 特写已打开(OpenExisting), IsOpen=" + IsOpen);
        }

        /// <summary>设置是否允许点击背景关闭特写。用于需要逐句播放字幕时禁用背景关闭。</summary>
        public static void SetCanClose(bool canClose)
        {
            CanClose = canClose;
            if (_backgroundButton != null)
                _backgroundButton.interactable = canClose;
        }

        public static void Close()
        {
            Debug.Log("[CloseupView] Close() 被调用, IsOpen=" + IsOpen);
            if (!IsOpen) return;
            IsOpen = false;

            if (_currentInstance != null) Object.Destroy(_currentInstance);
            _currentInstance = null;
            if (_root != null) _root.SetActive(false);

            if (PlayerController.Instance != null)
                PlayerController.Instance.SetControllable(true);

            var cb = _onClosed;
            _onClosed = null;
            Debug.Log("[CloseupView] 特写已关闭, 回调=" + (cb != null ? "有" : "无"));
            cb?.Invoke();
        }
    }
}
