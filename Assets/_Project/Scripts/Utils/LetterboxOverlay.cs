// ============================================================================
//  LetterboxOverlay.cs —— 电影级信箱式黑边(2026-07-20)
//
//  严格复刻策划参考图比例:
//   · 参考图整体 840×570,中间游戏画面 840×341,上下黑边各 ~115px
//   · 中间画面高度占比 = 341/570 ≈ 0.5982
//   · 上下黑边各占比    = 115/570 ≈ 0.2018
//   · 中间画面宽高比    = 840/341 ≈ 2.463:1(接近 CinemaScope 2.39:1)
//
//  实现:独立 ScreenSpaceOverlay Canvas,sortingOrder=1000(盖住所有游戏UI/对话/表情特写),
//  上下两个纯黑 Image,anchor 分别锚定屏幕上/下边,高度按屏幕高度百分比,自动适配任意分辨率。
//
//  由 SandboxBootstrap.Start() 挂载,整个游戏运行期间常驻。
// ============================================================================

using UnityEngine;
using UnityEngine.UI;

namespace LostGoddess
{
    public class LetterboxOverlay : MonoBehaviour
    {
        // 2026-07-21 三次校准:与"相机可见世界宽/高"精确联动。
        //   相机 orthoSize=6,rect=(0, p, 1, 1-2p),rect 内世界高 = 2×orthoSize = 12(= 图高完整装下)。
        //   要让 rect 内世界宽 = 图宽 34,rect 像素宽高比 = 34/12 = 2.833,
        //     → 1920 / (1080×(1-2p)) = 2.833 → p = 0.1863。
        //   这个 p 下,场景背景(3400×1200,12 单位高)在中间条带里像素-世界一比一完美贴合,
        //   不需要任何 fit-scale,原图完整呈现,人物 5 单位高 : 场景 12 单位高 = 5:12 严格满足。
        public const float BarHeightPct = 0.1863f;

        // Canvas sortingOrder:必须高于 DialogueSystem 的 400,盖住所有游戏 UI
        const int LetterboxSortingOrder = 1000;

        Canvas _canvas;
        Image _topBar;
        Image _bottomBar;

        /// <summary>入口:在场景里挂一个常驻黑边遮罩(重复调用只建一次)。</summary>
        public static LetterboxOverlay Ensure()
        {
            var existing = FindObjectOfType<LetterboxOverlay>();
            if (existing != null) return existing;

            var go = new GameObject("LetterboxOverlay");
            DontDestroyOnLoad(go);
            return go.AddComponent<LetterboxOverlay>();
        }

        void Awake()
        {
            BuildCanvas();
            BuildBars();
            ApplyBarHeights();
        }

        void BuildCanvas()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = LetterboxSortingOrder;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            // 黑边按高度百分比布局,所以按屏幕高度匹配,横向拉伸不影响
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;   // 完全按高度匹配

            // 不接受点击(黑边不吃鼠标事件,防止挡住底部一小段可点区域——虽然理论上底部黑边下没有可交互物)
            var raycaster = gameObject.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false;
        }

        void BuildBars()
        {
            _topBar = BuildBar("TopBar", topAnchored: true);
            _bottomBar = BuildBar("BottomBar", topAnchored: false);
        }

        Image BuildBar(string name, bool topAnchored)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var img = go.AddComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;   // 不吃事件

            var rt = img.rectTransform;
            if (topAnchored)
            {
                // 锚定屏幕上边:anchor min=(0,1) max=(1,1),pivot=(0.5,1)
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot     = new Vector2(0.5f, 1f);
            }
            else
            {
                // 锚定屏幕下边:anchor min=(0,0) max=(1,0),pivot=(0.5,0)
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot     = new Vector2(0.5f, 0f);
            }
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;   // 高度先设 0,ApplyBarHeights 里按屏幕高动态更新

            return img;
        }

        int _lastScreenH = -1;
        void Update()
        {
            // 分辨率变化时(全屏切窗口等)重算高度
            if (Screen.height != _lastScreenH)
                ApplyBarHeights();
        }

        void ApplyBarHeights()
        {
            _lastScreenH = Screen.height;

            // 参考分辨率高度基准:1080,黑边像素高 = 1080 * 0.2018 ≈ 218px
            //   CanvasScaler(ScaleWithScreenSize + matchWidthOrHeight=1)会把参考坐标按屏幕高等比缩放,
            //   所以直接写"参考坐标"即可,不需要自己乘 Screen.height。
            float refBarHeight = 1080f * BarHeightPct;

            SetBarHeight(_topBar,    refBarHeight);
            SetBarHeight(_bottomBar, refBarHeight);
        }

        static void SetBarHeight(Image bar, float height)
        {
            if (bar == null) return;
            var rt = bar.rectTransform;
            var s = rt.sizeDelta;
            s.y = height;
            rt.sizeDelta = s;
        }
    }
}
