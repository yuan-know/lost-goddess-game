// ============================================================================
//  InventoryDebugBootstrap.cs —— 背包 UI 调试场景 (2026-09-14)
//
//  ★ 设计原则:调试场景里看到的**就是游戏里看到的**。
//    · 背景 = 真实房间美术,走 SceneRoomBuilder.Build(SceneDef),与正式房间同一份代码
//    · 相机 = 与 SandboxBootstrap 完全一致的设置(ortho 6 + letterbox rect)
//    · 背包 = 直接 new 出**正式的 InventoryUI**(GameManager 里那个),不是另写一套
//    · 道具尺寸/位置的算法与可调参数 = InventoryPanelLayout,游戏读的也是它
//      ⇒ HUD 里调出来的值就是游戏里生效的值,不存在"调试是好看、进游戏又不对"
//
//  挂在空场景里唯一的 GameObject 上,Play 后:
//    1) 建 GameManager(→ 建出正式 InventoryUI)+ 相机 + 信箱黑边
//    2) 用 SceneRoomBuilder.Build 建一个真实房间(默认 序幕·神庙门厅 TempleFoyer)+ 老人
//    3) 把现阶段**可获取的道具**灌进 GameState,打开正式的背包面板
//    4) 左上角 HUD(调试场景默认显示),实时显示/调整排版参数
//
//  生成场景:Unity 菜单 ▸ 失落的女神 ▸ 生成背包 UI 调试场景
//            (Assets/_Project/Scenes/InventoryDebug.unity,纯调试不进正式包)
//
//  操作:
//    Tab / Shift+Tab  → 选中下一件 / 上一件道具
//    方向键           → 微调选中道具位置(Shift = 0.25px 细调)
//    [ / ]            → 选中道具单独放大 / 缩小(SizeMul)
//    - / =            → 全部道具统一大小 ItemFill − / +
//    , / .            → 背包整体大小 PanelHeightRatio − / +
//    ; / '            → 遮罩 alpha − / +
//    T                → 格子框 + 中心点 显示/隐藏
//    A                → 追加未实装道具(提灯/放大镜)对比观感
//    B                → 背包面板 开/关(= 游戏里点背包按钮那个动作)
//    Y                → 换一个真实房间背景(前厅/大殿/餐厅/武器室/密室2)
//    P                → 隐藏 / 显示整个背包 UI 层(连按钮一起),看纯场景
//    L                → 把当前调参表打到 Console(定稿后照抄进 InventoryPanelLayout)
//    R                → 全部复位
// ============================================================================

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using LostGoddess.Content;

namespace LostGoddess
{
    public class InventoryDebugBootstrap : MonoBehaviour
    {
        // ── 真实房间候选(背景美术都已在 Resources/Scenes/ 里)──
        struct RoomOption { public string label; public SceneDef def; public float spawnX; }
        static readonly RoomOption[] kRooms =
        {
            new RoomOption { label = "序幕·神庙门厅", def = SceneRoomBuilder.TempleFoyer, spawnX = -3f },
            new RoomOption { label = "章一·大殿",     def = SceneRoomBuilder.MainHall,      spawnX = 0f },
            new RoomOption { label = "章一·餐厅",     def = SceneRoomBuilder.DiningHall,    spawnX = 0f },
            new RoomOption { label = "章一·武器室",   def = SceneRoomBuilder.WeaponsRoom,   spawnX = 0f },
            new RoomOption { label = "序幕·密室2",    def = SceneRoomBuilder.PrologueChamber2, spawnX = 0f },
        };

        GameObject _roomRoot;
        GameObject _player;
        Text _hud;
        int _roomIndex;
        int _selected;
        bool _showMarks;
        bool _showExtra;
        bool _panelOpen = true;
        List<string> _shown = new List<string>();

        void Start()
        {
            EnsureGameManager();                 // → GameManager.Awake → 正式 InventoryUI
            PlayerBuilder.EnableAutoRebuild();
            BuildCamera();
            LetterboxOverlay.Ensure();

            GameState.CurrentEra = Era.Old;
            RebuildRoom();

            GrantItems();
            var ui = InventoryUI.Instance;
            if (ui == null) { Debug.LogError("[InventoryDebug] 没拿到正式 InventoryUI(GameManager 没起来?)"); }
            else
            {
                ui.SetVisible(true);
                ui.SetOpen(true);
            }

            BuildHud();
            RefreshShownList();

            Debug.Log("[InventoryDebug] 背包 UI 调试场景:背景/相机/背包全是正式代码。"
                    + "Tab 选件,方向键微调,[ ] 单件缩放,- = 统一大小,, . 面板大小,; ' 遮罩,"
                    + "T 格子框,A 追加未实装道具,B 面板开关,Y 换房间,P 遮罩开关,L 打印参数,R 复位。");
        }

        void Update()
        {
            if (_hud != null) _hud.text = BuildHudText();

            // ── 选中 ──
            if (Input.GetKeyDown(KeyCode.Tab) && _shown.Count > 0)
            {
                int step = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? -1 : 1;
                _selected = ((_selected + step) % _shown.Count + _shown.Count) % _shown.Count;
            }

            string selId = (_selected >= 0 && _selected < _shown.Count) ? _shown[_selected] : null;
            float d = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? 0.25f : 1f;
            bool changed = false;

            if (selId != null)
            {
                Vector2 n = InventoryPanelLayout.Nudge(selId);
                if (Input.GetKeyDown(KeyCode.LeftArrow))  { n.x -= d; InventoryPanelLayout.SetNudge(selId, n); changed = true; }
                if (Input.GetKeyDown(KeyCode.RightArrow)) { n.x += d; InventoryPanelLayout.SetNudge(selId, n); changed = true; }
                if (Input.GetKeyDown(KeyCode.UpArrow))    { n.y += d; InventoryPanelLayout.SetNudge(selId, n); changed = true; }
                if (Input.GetKeyDown(KeyCode.DownArrow))  { n.y -= d; InventoryPanelLayout.SetNudge(selId, n); changed = true; }
                if (Input.GetKeyDown(KeyCode.LeftBracket))
                { InventoryPanelLayout.SetSizeMul(selId, Mathf.Max(0.3f, InventoryPanelLayout.SizeMul(selId) - 0.02f * d)); changed = true; }
                if (Input.GetKeyDown(KeyCode.RightBracket))
                { InventoryPanelLayout.SetSizeMul(selId, Mathf.Min(1.8f, InventoryPanelLayout.SizeMul(selId) + 0.02f * d)); changed = true; }
            }

            if (Input.GetKeyDown(KeyCode.Minus))
            { InventoryPanelLayout.ItemFill = Mathf.Max(0.30f, InventoryPanelLayout.ItemFill - 0.02f * d); changed = true; }
            if (Input.GetKeyDown(KeyCode.Equals))
            { InventoryPanelLayout.ItemFill = Mathf.Min(1.20f, InventoryPanelLayout.ItemFill + 0.02f * d); changed = true; }
            if (Input.GetKeyDown(KeyCode.Comma))
            { InventoryPanelLayout.PanelHeightRatio = Mathf.Max(0.30f, InventoryPanelLayout.PanelHeightRatio - 0.01f * d); changed = true; }
            if (Input.GetKeyDown(KeyCode.Period))
            { InventoryPanelLayout.PanelHeightRatio = Mathf.Min(1.40f, InventoryPanelLayout.PanelHeightRatio + 0.01f * d); changed = true; }
            if (Input.GetKeyDown(KeyCode.Semicolon))
            { InventoryPanelLayout.DimAlpha = Mathf.Max(0f, InventoryPanelLayout.DimAlpha - 0.02f * d); changed = true; }
            if (Input.GetKeyDown(KeyCode.Quote))
            { InventoryPanelLayout.DimAlpha = Mathf.Min(1f, InventoryPanelLayout.DimAlpha + 0.02f * d); changed = true; }

            if (changed && InventoryUI.Instance != null) InventoryUI.Instance.LayoutAll();

            // ── 开关 ──
            if (Input.GetKeyDown(KeyCode.T))
            {
                _showMarks = !_showMarks;
                if (InventoryUI.Instance != null) InventoryUI.Instance.SetSlotDebugOutline(_showMarks);
            }
            if (Input.GetKeyDown(KeyCode.A)) { _showExtra = !_showExtra; GrantItems(); ReloadItems(); }
            if (Input.GetKeyDown(KeyCode.B))
            {
                _panelOpen = !_panelOpen;
                if (InventoryUI.Instance != null) InventoryUI.Instance.SetOpen(_panelOpen);
            }
            if (Input.GetKeyDown(KeyCode.P))
            {
                if (InventoryUI.Instance != null)
                    InventoryUI.Instance.GetComponent<Canvas>().enabled = !InventoryUI.Instance.GetComponent<Canvas>().enabled;
            }
            if (Input.GetKeyDown(KeyCode.Y)) { _roomIndex = (_roomIndex + 1) % kRooms.Length; RebuildRoom(); }
            if (Input.GetKeyDown(KeyCode.R))
            {
                InventoryPanelLayout.ResetTuning();
                if (InventoryUI.Instance != null) InventoryUI.Instance.LayoutAll();
            }
            if (Input.GetKeyDown(KeyCode.L)) LogTuning();
        }

        // ── 场景 ──
        void EnsureGameManager()
        {
            if (GameManager.Instance == null)
            {
                var go = new GameObject("GameManager");
                go.AddComponent<GameManager>();   // Awake → InitSystems → InventoryUI.CreateAttached()
            }
        }

        void BuildCamera()
        {
            if (Camera.main == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                var c = go.AddComponent<Camera>();
                c.orthographic = true;
                c.orthographicSize = 6f;      // 与 SandboxBootstrap 一致:12 单位图完整落进中间条带
                c.transform.position = new Vector3(0, 0, -10);
                go.AddComponent<ClickInputManager>();
            }
            var cam = Camera.main;
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.08f);

            float barPct = LetterboxOverlay.BarHeightPct;
            cam.rect = new Rect(0f, barPct, 1f, 1f - 2f * barPct);

            if (cam.GetComponent<ClickInputManager>() == null)
                cam.gameObject.AddComponent<ClickInputManager>();
        }

        /// <summary>用一个真实 SceneDef 重建房间 + 老人。</summary>
        void RebuildRoom()
        {
            if (_roomRoot != null) Destroy(_roomRoot);
            if (_player != null) Destroy(_player);

            var opt = kRooms[_roomIndex];
            _roomRoot = SceneRoomBuilder.Build(opt.def);
            _roomRoot.name = "Room_" + opt.def.roomName;
            GameState.CurrentRoom = opt.def.roomName;

            _player = PlayerBuilder.Build(GameState.CurrentEra, opt.def.groundY, opt.spawnX);
        }

        // ── 道具 ──
        /// <summary>把"现阶段可获取的道具"灌进 GameState(与背包系统用的是同一条路)。</summary>
        void GrantItems()
        {
            foreach (var id in InventoryPanelLayout.ObtainableItems)
                if (!GameState.HasItem(id)) GameState.AddItem(id);

            // ★ if/else 的内嵌语句共用同一个声明空间,两个分支里不能出现同名局部变量 → 各加花括号
            if (_showExtra)
            {
                foreach (var eid in InventoryPanelLayout.ExtraItems)
                    if (!GameState.HasItem(eid)) GameState.AddItem(eid);
            }
            else
            {
                foreach (var xid in InventoryPanelLayout.ExtraItems)
                    if (GameState.HasItem(xid)) GameState.RemoveItem(xid);
            }
        }

        void ReloadItems()
        {
            RefreshShownList();
            if (_selected >= _shown.Count) _selected = _shown.Count - 1;
            if (InventoryUI.Instance != null) InventoryUI.Instance.LayoutAll();
            if (_showMarks && InventoryUI.Instance != null) InventoryUI.Instance.SetSlotDebugOutline(true);
        }

        void RefreshShownList()
        {
            _shown = new List<string>();
            foreach (var id in InventoryPanelLayout.ObtainableItems) _shown.Add(id);
            if (_showExtra)
                foreach (var id in InventoryPanelLayout.ExtraItems) _shown.Add(id);
        }

        // ── HUD ──
        void BuildHud()
        {
            var go = new GameObject("~InventoryDebugHUD");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 3000;      // 盖过 letterbox(1000)/对话(1100)/背包(300)
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            go.AddComponent<GraphicRaycaster>();

            var tgo = new GameObject("Text");
            tgo.transform.SetParent(go.transform, false);
            _hud = tgo.AddComponent<Text>();
            _hud.font = GameFonts.Primary;
            _hud.fontSize = 18;
            _hud.color = new Color(1f, 0.92f, 0.72f);
            _hud.alignment = TextAnchor.UpperLeft;
            _hud.horizontalOverflow = HorizontalWrapMode.Overflow;
            _hud.verticalOverflow = VerticalWrapMode.Overflow;
            _hud.raycastTarget = false;
            var rt = _hud.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -14f);
            rt.sizeDelta = new Vector2(1500f, 760f);

            var sh = tgo.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.9f);
            sh.effectDistance = new Vector2(1.5f, -1.5f);
        }

        string BuildHudText()
        {
            var sb = new StringBuilder(1600);
            float k = InventoryPanelLayout.ScaleFactor;
            Vector2 ps = InventoryPanelLayout.PanelDisplaySize;

            sb.AppendLine("背包 UI 调试 · 背景=真实房间 + 面板=正式 InventoryUI · 底图 bag_panel_v2.png 1121x908");
            sb.AppendLine($"房间 [{_roomIndex + 1}/{kRooms.Length}] {kRooms[_roomIndex].label}"
                        + $"  ({kRooms[_roomIndex].def.roomName})  [Y] 换房间");
            sb.AppendLine($"条带高 {InventoryPanelLayout.BandHeight:F0}px  (LetterboxOverlay.BarHeightPct="
                        + $"{LetterboxOverlay.BarHeightPct:F4};旧的 InventoryUI 硬编码 0.2018 已统一)");
            sb.AppendLine($"面板 显示 {ps.x:F0}x{ps.y:F0}px  缩放 k={k:F4}  高/条带={InventoryPanelLayout.PanelHeightRatio:F2}  [ , . ]");
            sb.AppendLine($"格子内框 {InventoryPanelLayout.TileW * k:F1}x{InventoryPanelLayout.TileH * k:F1}px  "
                        + $"道具 fill={InventoryPanelLayout.ItemFill:F2}  [ - = ]");
            sb.AppendLine($"遮罩 alpha={InventoryPanelLayout.DimAlpha:F2}(美术 PSD 图层1)  [ ; ' ]   "
                        + $"面板 {(_panelOpen ? "开" : "关")} [B]   P 隐藏整个背包层看纯场景");
            sb.AppendLine($"格子框 {(_showMarks ? "开" : "关")} [T]   未实装道具 {(_showExtra ? "开" : "关")} [A]");
            sb.AppendLine("道具(顺序=入包顺序=格位顺序):");
            for (int i = 0; i < _shown.Count; i++)
            {
                ItemIconGeom g;
                InventoryPanelLayout.TryGet(_shown[i], out g);
                float fill = InventoryPanelLayout.ItemFill * InventoryPanelLayout.SizeMul(_shown[i]);
                float s = Mathf.Min(InventoryPanelLayout.TileW * k * fill / Mathf.Max(g.ContentW, 1e-4f),
                                    InventoryPanelLayout.TileH * k * fill / Mathf.Max(g.ContentH, 1e-4f));
                Vector2 n = InventoryPanelLayout.Nudge(_shown[i]);
                sb.AppendLine($" {((i == _selected) ? "▶" : " ")}{i,2} r{i / 4}c{i % 4} {g.displayName,-14} "
                            + $"显示 {g.srcW * s,5:F0}x{g.srcH * s,-4:F0}(内容 {g.ContentW * s:F0}x{g.ContentH * s:F0})  "
                            + $"mul {InventoryPanelLayout.SizeMul(_shown[i]):F2} [ ]  nudge ({n.x:F1},{n.y:F1})");
            }
            sb.AppendLine("Tab 选件  方向键微调  [ ] 单件缩放  - = 统一大小  , . 面板大小  ; ' 遮罩  T 格子框  A 追加  B 面板  Y 换房间  P 藏背包层  L 打印  R 复位");
            return sb.ToString();
        }

        void LogTuning()
        {
            var sb = new StringBuilder();
            sb.AppendLine("// ── 背包排版定稿参数:照抄进 InventoryPanelLayout 的默认值 ──");
            sb.AppendLine($"//   PanelHeightRatio = {InventoryPanelLayout.PanelHeightRatio:F3}f;");
            sb.AppendLine($"//   ItemFill         = {InventoryPanelLayout.ItemFill:F3}f;");
            sb.AppendLine($"//   DimAlpha         = {InventoryPanelLayout.DimAlpha:F3}f;");
            for (int i = 0; i < _shown.Count; i++)
            {
                float m = InventoryPanelLayout.SizeMul(_shown[i]);
                Vector2 n = InventoryPanelLayout.Nudge(_shown[i]);
                if (Mathf.Abs(m - 1f) > 0.001f || n != Vector2.zero)
                    sb.AppendLine($"//   {_shown[i],-16} sizeMul={m:F3}f  nudge=({n.x:F1}f, {n.y:F1}f)");
            }
            Debug.Log(sb.ToString());
        }
    }
}
