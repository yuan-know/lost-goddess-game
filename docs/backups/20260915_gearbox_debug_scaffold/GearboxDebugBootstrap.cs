// ============================================================================
//  GearboxDebugBootstrap.cs —— 齿轮箱「内部旋转」独立调试场景
//  (2026-09-15 从零重做:旧版精密操作玩法 + 千眼回廊背景 已整包弃用)
//
//  ★ 搭建基准 = 真实游戏 Sandbox(SandboxBootstrap.cs)+ 千眼回廊调试场景
//    (EyeCorridorLaserDebugBootstrap.cs)。三件事,与它们逐字对齐:
//      1) 相机:正交 orthographicSize=6 + **cam.rect 裁进信箱条带** + ClickInputManager
//         (不裁的话房间会被放大 1.59 倍、两侧被切 —— 旧版就是栽在这)
//      2) 背景:**真实武器房** SceneRoomBuilder.Build(WeaponsRoom)
//         room.name = "Room_" + Rooms.Chapter1_WeaponsRoom(与 WeaponsRoomScene 同名)
//      3) 玩家:PlayerBuilder.Build(era, groundY, x, yOffsetOverride: GetChapter1YOffset)
//      4) 齿轮箱走**真实特写层通道** CloseupView(OpenExisting),不是世界空间假特写
//
//  玩法(用户 2026-09-15 口述 + 六修定稿):
//    · 点右侧**大传动轴**     → 播放齿轮箱内部旋转序列帧 → 通关
//    · 点齿轮箱内**其他零件** → 烟雾特效一遍(开头一次红闪)→ 弹出失败刻痕
//                              → 停留 → 刻痕淡出消失 → 重新开始小游戏
//    ★ 没有文字提示(用户要求删掉箱体上方的系统提示语句)。
//
//  操作:
//    左键点击    → 判定命中(大传动轴 = 通关 / 其他零件 = 蒸汽 + 失败刻痕)
//    F           → 切换**烟雾摆位模式**(开启后:左键拖/滚轮缩放/右键旋转,
//                  蒸汽自动常驻循环、点击被全屏手柄吃掉、玩法暂停;
//                  关闭则蒸汽回到"点错零件才喷"、交互正常)
//    K           → 切换**热区标定模式**(点击只打印 u,v,不触发玩法)
//    R           → 重置 → 可直接重开
//    N           → 重新打开特写(关掉后用)
//    Esc         → 关闭特写(调试用;正式流程由通关回调接管)
//    G / H       → 唤出 / 隐藏 调试 HUD(默认隐藏,正式观感不该有调试文字)
//    1/2/3       → 切换形态 青年/中年/老年(重建玩家)
//
//  ★★ 2026-09-15 四修(用户口述):
//    "这次直接使用这套序列帧图的初始帧作为齿轮箱的常驻图…然后你然烟雾特效不断重播
//     并给它加一个可拖动和缩放的组件(我亲自来摆放位置和调整尺寸),交互逻辑不变但是
//     等我调整好烟雾特效后你再让小游戏交互生效"
//    → 常驻图 = 首帧 / 蒸汽常驻循环 / 摆位手柄 / 交互默认关闭待用户验收后开启。
//
//  ★★★ 2026-09-15 五修(用户:"我调整好位置和尺寸了,你固定一下,然后进入正常的
//    小游戏交互"):
//    → 蒸汽摆位已从 Console 日志抄回常量并**固化**(见 GearboxSpinCloseup 常量区
//      SteamAnchored / SteamSize / SteamRotZ);SteamLayoutEditable 默认 false
//      (不再创建全屏手柄);InteractionEnabled = true,**玩法正式生效**。
//    → 蒸汽显示时机随模式走:摆位模式常驻循环;正常玩法 = 点错零件才喷一整轮。
//
//  ★★★ 2026-09-15 六修(用户:"把齿轮箱上方的系统提示语句删去,玩家点击错误先播放
//    一遍烟雾特效然后弹出刻痕并淡出消失重新开始小游戏"):
//    → **Toast 提示条已整块删除**(节点 + ShowToast/ToastRoutine + 4 处调用),
//      通关/失败的反馈全部由画面承担。
//    → 失败流程新顺序:烟雾一遍(开头红闪保留)→ 刻痕弹出 → 停留 0.7s → 淡出 → 重开。
//    → 烟雾**一次失败只播一遍**,不循环重复。
//
//  ★★★ 2026-09-15 七修(用户:"动画播放时把初始帧隐藏(有留影)…播完立刻关闭特写…
//    真实游戏通关后要关特写并继续剧情,齿轮箱也要留一个交互接口"):
//    → GearboxSpinCloseup 侧:动画期间 `staticImg.enabled = false`;播完不回落到常驻图;
//      新增 `CloseOnSolved`(默认 true)在播完后关特写,**关闭之后**才触发 onSolved。
//    → **交互接口 = `Content/EyeCorridor/Interact_Gearbox.cs`**(继承 InteractableBase,
//      Inspector 填 onSolved 接剧情)。**正式关卡用那个类,不要用本调试场景**。
//    → 本调试场景显式设 `CloseOnSolved = false`(特写留着反复看)+ `SetCanClose(true)`。
//
//  ★★★ 2026-09-15 八修(用户:"你把正确的交互区域给我画出来,我看一下调整一下"):
//    → `tools/gearbox_hitzone_viz.py` 把 HitZone() 的判定**逐条照抄**渲染成
//      `docs/gearbox/_hitzones.png`(面板 + 热区叠加 + 0.1 归一化网格 + 编号)。
//    → 新增**热区标定模式** `GearboxSpinCloseup.HitZoneCalibration`(本场景按 **K**):
//      开启后点击只打印 `[HitZoneCalib] u=… v=… → 面板像素(…)`,不触发玩法。
//      标定流程:点零件的左上角/右下角(或圆心)→ 抄 Console 里的 u,v 进 HitZone()。
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using LostGoddess.Content;   // SceneRoomBuilder 在这个命名空间下

namespace LostGoddess
{
    public class GearboxDebugBootstrap : MonoBehaviour
    {
        Text _hud;
        bool _showHud = false;                 // 默认隐藏(照 EyeCorridorLaserDebugBootstrap)
        GearboxSpinCloseup.SpinBehaviour _beh;
        bool _solved;

        void Start()
        {
            EnsureGameManager();
            PlayerBuilder.EnableAutoRebuild();
            AudioManager.Init(this);           // 没音频文件时 AudioManager 自己静默

            GameState.CurrentRoom = Rooms.Chapter1_WeaponsRoom;
            GameState.CurrentEra = Era.Middle; // 齿轮箱是 S05 中年线

            BuildCamera();                     // 正交 + cam.rect 裁条带
            LetterboxOverlay.Ensure();         // 电影级信箱黑边
            BuildRoomAndPlayer();              // ★ 真实武器房 + 玩家(照 WeaponsRoomScene)

            OpenPuzzle();
            BuildHud();
                Debug.Log("[GearboxDebug] ★ 玩法已生效:点右侧大传动轴 → 内部旋转 → 通关;"
                    + "点其他零件 → 烟雾一遍 → 刻痕弹出 → 淡出 → 重开。"
                    + "蒸汽摆位已固化 " + GearboxSpinCloseup.SteamLayoutText() + "。"
                    + "F 切摆位模式,G/H 开关 HUD,R 重置,N 重开特写。");
        }

        void OpenPuzzle()
        {
            _solved = false;
            // ★★ 调试专用:通关后**不**自动关闭特写(留在特写里反复看动画 / 按 R 重播)。
            //    正式游戏里这个值是 true —— 播完动画就收起特写,并把回调放出去续剧情。
            GearboxSpinCloseup.CloseOnSolved = false;
            GearboxSpinCloseup.Show(() =>
            {
                _solved = true;
                Debug.Log("[GearboxDebug] ★ 齿轮箱内部旋转播放完毕 —— 通关"
                        + "(调试模式:特写保留。按 R 重来。正式游戏此处会关闭特写并继续剧情)。");
            });
            // 调试场景允许点背景 / Esc 关闭(正式游戏由 CloseOnSolved 自动禁掉)
            CloseupView.SetCanClose(true);
            _beh = Object.FindObjectOfType<GearboxSpinCloseup.SpinBehaviour>();
        }

        // ── 系统 ─────────────────────────────────────────────────────────
        void EnsureGameManager()
        {
            if (GameManager.Instance == null)
            {
                var go = new GameObject("GameManager");
                go.AddComponent<GameManager>();
            }
        }

        /// <summary>相机:照抄 SandboxBootstrap.BuildCamera + EyeCorridorLaserDebugBootstrap.BuildCamera。
        /// orthoSize=6 → 世界高 12 单位;cam.rect 裁进中间条带后,世界宽 = 12×(1920/678) ≈ 34,
        /// 正好等于 3400×1200 房间图 1:1 铺满 1920×678 条带。</summary>
        void BuildCamera()
        {
            if (Camera.main == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                var newCam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
                newCam.orthographic = true;
                newCam.orthographicSize = 6f;
                newCam.transform.position = new Vector3(0f, 0f, -10f);
            }

            var cam = Camera.main;
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.06f);   // 近黑
            cam.transform.position = new Vector3(0f, 0f, -10f);

            // ★★ 关键:视口裁进信箱条带。漏了这步 → 房间被放大 1.59 倍且两侧被切,
            //    用户原话"特写图背后的场景显示明显不对"。
            float barPct = LetterboxOverlay.BarHeightPct;
            cam.rect = new Rect(0f, barPct, 1f, 1f - 2f * barPct);

            if (cam.GetComponent<ClickInputManager>() == null)
                cam.gameObject.AddComponent<ClickInputManager>();
        }

        /// <summary>真实武器房 + 玩家。逐条照 WeaponsRoomScene.Build 的搭建方式。
        /// ★ 必须用 SceneRoomBuilder.Build(SceneRoomBuilder.WeaponsRoom):图层、排序、
        ///   地平线(groundY=-5.66)、bg_near 前景全在里面。
        /// ⚠ 不要在这里挂 ScenePortal:调试场景不该能切走。</summary>
        void BuildRoomAndPlayer()
        {
            var room = SceneRoomBuilder.Build(SceneRoomBuilder.WeaponsRoom);
            room.name = "Room_" + Rooms.Chapter1_WeaponsRoom;
            float groundY = SceneRoomBuilder.WeaponsRoom.groundY;

            var player = PlayerBuilder.Build(GameState.CurrentEra, groundY, 0f,
                yOffsetOverride: PlayerBuilder.GetChapter1YOffset(GameState.CurrentEra));
            if (player != null)
            {
                var pc = player.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.spriteFacesRight = false;
                    var s = player.transform.localScale;
                    s.x = Mathf.Abs(s.x) * -1f;   // 立绘默认朝左
                    player.transform.localScale = s;
                }
            }
        }

        // ── HUD(与旧版同款,只换文案)─────────────────────────────────────
        void BuildHud()
        {
            var canvasGo = new GameObject("GearboxDebugHud");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;              // 压在 CloseupView(1050) 之上
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var hudGo = new GameObject("Hud");
            hudGo.transform.SetParent(canvasGo.transform, false);
            _hud = hudGo.AddComponent<Text>();
            _hud.font = GameFonts.Primary;
            _hud.fontSize = 24;
            _hud.alignment = TextAnchor.UpperLeft;
            _hud.raycastTarget = false;              // ⚠ 必须关,否则挡住点击
            _hud.horizontalOverflow = HorizontalWrapMode.Overflow;
            var rt = _hud.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(14f, -10f);
            rt.sizeDelta = new Vector2(980f, 580f);
            _hud.enabled = _showHud;                 // 默认隐藏
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.G) || Input.GetKeyDown(KeyCode.H))
            {
                _showHud = !_showHud;
                if (_hud != null) _hud.enabled = _showHud;
            }

            // ★ K = 切换**热区标定模式**(点击只打印归一化坐标 u,v,不触发玩法)
            if (Input.GetKeyDown(KeyCode.K))
            {
                GearboxSpinCloseup.HitZoneCalibration = !GearboxSpinCloseup.HitZoneCalibration;
                Debug.Log("[GearboxDebug] 热区标定模式 = "
                        + (GearboxSpinCloseup.HitZoneCalibration
                           ? "★ 开 —— 现在点零件只会打印 u,v(点左上角/右下角/圆心各一次)"
                           : "关 —— 玩法恢复正常"));
            }

            if (Input.GetKeyDown(KeyCode.N)) OpenPuzzle();
            if (Input.GetKeyDown(KeyCode.Escape)) CloseupView.Close();

            // ★ F = 切换**烟雾摆位模式**(2026-09-15 定稿后:默认关闭,交互正常)
            if (Input.GetKeyDown(KeyCode.F))
            {
                GearboxSpinCloseup.SteamLayoutEditable = !GearboxSpinCloseup.SteamLayoutEditable;
                Debug.Log("[GearboxDebug] 烟雾摆位模式 = "
                        + (GearboxSpinCloseup.SteamLayoutEditable
                           ? "★ 开启(左键拖/滚轮缩放/右键旋转;蒸汽常驻循环;玩法暂停)"
                           : "关闭(玩法交互正常;蒸汽改为点错零件才喷)"));
                OpenPuzzle();   // 重建 UI 才能增删手柄
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                if (_beh != null) _beh.ResetRun();
                else OpenPuzzle();
            }

            if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchEra(Era.Young);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchEra(Era.Middle);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchEra(Era.Old);

            if (_beh == null) _beh = Object.FindObjectOfType<GearboxSpinCloseup.SpinBehaviour>();

            if (_showHud && _hud != null)
            {
                string st = _beh == null ? "(特写未打开)"
                    : (_beh.State == GearboxSpinCloseup.SpinBehaviour.Phase.Solved ? "★ 已通关"
                    : (_beh.State == GearboxSpinCloseup.SpinBehaviour.Phase.Penalizing ? "⚠ 惩罚中"
                    : (_beh.State == GearboxSpinCloseup.SpinBehaviour.Phase.Spinning ? "旋转中…" : "待操作")));
                var handle = Object.FindObjectOfType<GearboxSpinCloseup.SteamLayoutHandle>();
                string layout = GearboxSpinCloseup.SteamLayoutText();
                _hud.text =
                    "── 齿轮箱 · 内部旋转(特写层) · 调试 ──\n"
                    + string.Format("状态 {0}    失败次数 {1}\n", st, _beh != null ? _beh.PenaltyCount : 0)
                    + string.Format("通关回调 {0}\n", _solved ? "★ 已触发" : "未触发")
                    + string.Format("游戏交互 {0}\n",
                        GearboxSpinCloseup.InteractionEnabled ? "★ 开启" : "关闭")
                    + string.Format("烟雾摆位 {0}\n", layout)
                    + string.Format("摆位手柄 {0}\n", handle != null ? "存在(点击被吃掉)" : "未创建")
                    + string.Format("热区标定 {0}\n",
                        GearboxSpinCloseup.HitZoneCalibration ? "★ 开(点击只报 u,v)" : "关")
                    + "背景:武器房 SceneRoomBuilder.WeaponsRoom(groundY=-5.66)\n"
                    + "特写:CloseupView sortingOrder=1050 + DimBackground 0.7\n"
                    + "常驻图 = 序列帧首帧 gearbox_static.png(575×620)\n"
                    + "动画帧表 161 帧 · 7列×6行×4张 · 原生分辨率\n\n"
                    + "玩法:点**右侧大传动轴** → 齿轮箱内部旋转 → 通关\n"
                    + "      点其他零件        → 烟雾一遍(红闪) → 刻痕弹出 → 淡出 → 重开\n\n"
                    + "快捷键: F 切摆位模式  K 热区标定  R 重置  N 重开特写  Esc 关闭\n"
                    + "        1/2/3 青年/中年/老年  G/H 本面板";
            }
        }

        void SwitchEra(Era era)
        {
            if (GameState.CurrentEra == era) return;
            GameState.SetEra(era);   // 触发 OnEraChanged → PlayerBuilder 重建
            Debug.Log("[GearboxDebug] 切换形态: " + era);
        }
    }
}
