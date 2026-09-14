// ============================================================================
//  EyeCorridorLaserPuzzle.cs —— S05 千眼回廊 · 青年线 PZ_04「躲避激光」  (2026-09-14)
//
//  脚本依据:《章一_三线整合程序脚本表_v0_3》
//    · 01_场景表 S05:盲眼僧侣守关。青年靠走位躲避(其余两形态不在本脚本范围内)
//    · 03_交互物表:青年用【兜帽石像】跑图
//    · 04_谜题表 PZ_04:光柱扫来前点击兜帽石像瞬移入阴影,走完全程
//                      失败 = 3 秒未躲入阴影 → 灼烧死亡回滚
//    · 05_死亡表 D_02:判词「目困莽夫,身陷焦土」,重生引导「见影即止,避瞳而行」,
//                      回该密室入口,可重试(惩罚性死亡,不是剧情杀)
//    · 02_流程节点表 N05-Y-010 ~ N05-Y-090(台词原文见 DialogueSystem 表)
//
//  玩法(可在调试 HUD 里实时改):
//    头顶黄铜眼扫出一条光柱,沿地面左右横扫。玩家走位躲它;
//    被光柱照到 → 「锁定」3 秒倒计时(默认:离开光柱也不会清零,只有躲进石像阴影才清零)
//      → 3 秒内没进阴影 = 灼烧死亡,回密室入口重试。
//    点兜帽石像 = 瞬移进它的阴影(青年专属);石像阴影里光柱扫过安全。
//    走进石像阴影 = 石眼暂时闭上(黄铜眼瞳孔收成缝,背景不换图、不变暗)。
//    走到长廊尽头 = 走完全程 → 盲僧赠符文【ᚱ Raidho · 旅程】。
//
//  占位说明(美术/音频未到位,全部程序化生成,不依赖任何 PNG):
//    · 光柱本体 / 地面光斑 / 石像阴影 / 石像可用标记 / 出口标记 —— 运行时生成贴图
//    · 黄铜眼本体用美术给的独立图层 Resources/Scenes/ChaseCorridor/bg_brass_eye.png
//    · 音效只调 AudioManager.PlaySfx(文件缺失时它只打一行提示,不报错)
//
//  几何来源(bg_full.png 3400×1200 / PPU100 / 世界宽 34,世界X = px/100 - 17):
//    兜帽石像躯干中心实测 px 403.5/843.5/1269.5/1699.5/2133.5/2561.5/2995.5
//    黄铜眼图层 alpha 包围盒 px(1641,133)-(1737,188) → 中心世界 (-0.11, 4.40)
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LostGoddess.Content
{
    public enum PZ04Phase { Intro, Playing, Dying, Reward, Done }

    public class EyeCorridorLaserPuzzle : MonoBehaviour
    {
        public static EyeCorridorLaserPuzzle Instance { get; private set; }

        // ── 场景几何(与 SceneRoomBuilder.EyeCorridor 严格对齐) ──
        public const float CorridorHalfWidth = 17f;      // 3400px / PPU100 / 2
        public const float GroundY           = -5.76f;   // 已实机调过的地平线
        public const float SpawnX            = -15.4f;   // 入口(左端)
        public const float ExitX             =  15.4f;   // 走完全程(右端)

        // ── 黄铜眼分层(源:黄金瞳.psd,导出为 bg_eye_shell / bg_eye_pupil,同一裁剪框,与 bg 同格对齐)──
        //   裁剪框 px (1437,112)-(1962,492) = 525×380,PPU100,锚点 BottomCenter
        //   → 锚点世界坐标 = ((1437+1962)/2/100-17, 6-492/100) = (-0.005, 1.08)
        public static readonly Vector2 EyeAnchor      = new Vector2(-0.005f, 1.080f);
        /// <summary>瞳孔球中心相对锚点的偏移(px -10, +205.5 → 单位 -0.10, +2.055)。</summary>
        public static readonly Vector2 EyePupilOffset = new Vector2(-0.100f, 2.055f);

        // ── 眼睑遮挡几何(与 tools/eye_lid_mask.py 同一口径,全部实测自 bg_eye_shell.png)──
        //   bg_eye_lid = 外壳但「眼窝开口内」抠空 → 叠在瞳孔之上。
        //   这里的开口是**真正的眼睑内缘**(眼窝与眼睑亮带的跃变线),不是最外圈的金属包边。
        public const float EyeOpeningLeftPx  = 116f;    // 眼窝开口左端(裁剪框内 px)
        public const float EyeOpeningRightPx = 414f;    // 眼窝开口右端
        public const float PupilCenterInBoxX = 252.5f;  // 瞳孔默认中心 x(裁剪框内 px,实测)
        public const float PupilRadiusInBox  = 38f;     // 瞳孔半径 px
        /// <summary>瞳孔中心移到这个摆幅就刚好贴住眼角;再往外,超出部分会被眼睑压住。</summary>
        public static readonly float PupilEdgeSwingLeft  = (PupilCenterInBoxX - (EyeOpeningLeftPx  + PupilRadiusInBox)) / 100f;
        public static readonly float PupilEdgeSwingRight = ((EyeOpeningRightPx - PupilRadiusInBox) - PupilCenterInBoxX) / 100f;

        /// <summary>黄铜眼所在世界点(发光点/光柱发源)。</summary>
        public static readonly Vector2 EyeWorldPos    = new Vector2(EyeAnchor.x + EyePupilOffset.x,
                                                                    EyeAnchor.y + EyePupilOffset.y);

        /// <summary>7 尊完整可见的兜帽石像 X(实测自 bg_full.png);两端各有一尊被画布切掉一半,不在可走区内,不作庇护点。</summary>
        public static readonly float[] StatueXs = { -12.96f, -8.56f, -4.30f, 0.00f, 4.34f, 8.61f, 12.95f };

        // ── 可调参数(调试 HUD 实时改) ──
        [Header("光柱")]
        public static float BeamSpeed     = 5.5f;    // 横扫速度(单位/秒);青年 3.0 单位/秒 → 光柱追得上人
        public static float BeamHalfWidth = 2.2f;    // 地面光斑半宽(危险区)
        public static float BeamTurnPause = 0.35f;   // 两端折返停顿
        public static float BeamTrackMin  = -15.6f;
        public static float BeamTrackMax  =  15.6f;
        public static bool  BeamPaused    = false;

        [Header("暴露/死亡判定")]
        public static bool  UseGraceMode     = true;   // true=被照到后 3 秒保命窗口(直读脚本"3秒未躲入阴影")
                                                      // false=累计暴露 3 秒(离开光柱按 ExposureDecay 回退)
        public static float GraceSeconds     = 3.0f;   // ★脚本硬性数值,不要乱改
        public static bool  GraceResetOnLeave= false;  // false=被锁定后离开光柱也不清零(只有进阴影才清零)
        public static float ExposureDecay    = 1.5f;   // 累计模式的回退倍率

        [Header("石像/阴影")]
        public static float ShadowHalfWidth  = 1.30f;  // 阴影安全区半宽
        public static float StatueReach      = 0f;     // 瞬移最大距离,0=不限
        public static float StatueCooldown   = 0f;     // 用过的石像冷却(秒),StatueOnce=false 时生效
        public static bool  StatueOnce       = true;   // 每尊石像只能用一次(脚本里石眼只"暂时"闭上)
        public static bool  RespawnAtEntrance= true;   // true=回密室入口(脚本原文) / false=回最近用过的石像

        [Header("呈现")]
        public static bool  UsePortrait   = false;     // true=角色台词带立绘特写(需美术立绘到位)
        public static bool  DebugZones    = true;      // 画危险区/安全区/石像锚点

        [Header("黄铜眼(眼珠跟随光柱)")]
        public static bool  PupilFollow   = true;      // 眼珠是否跟随光柱转动
        // 摆幅由「眼窝开口(真正的眼睑内缘)」的实测位置反推:滚到眼角时超出开口的部分会被 bg_eye_lid 压住。
        // 眼窝相对瞳孔中心不对称(瞳孔默认偏左 12.5px)→ 左右摆幅分开给,保证两侧极限都有遮挡。
        public static float PupilSwingLeft  = 1.10f;   // 向左摆幅(单位);±1.10 ≈ 瞳孔被眼角压掉 15%
        public static float PupilSwingRight = 1.30f;   // 向右摆幅(单位);+1.30 ≈ 被压掉 10%
        public static float PupilSwingY   = 0.16f;     // 瞳孔垂直摆幅
        public static float PupilSmooth   = 9f;        // 瞳孔跟随的平滑速度(越大越"黏")
        public static float PupilEdgeSquash = 0.10f;   // 滚到眼角时横向压缩量(球面转动感);0=不压

        [Header("闭眼(躲入阴影)表现")]
        public static bool  DimBeamWhenShielded = true; // 闭眼时光柱变淡(但继续横扫,不影响玩法)
        public static float ClosedBeamAlpha     = 0.35f;// 闭眼时光柱 alpha 倍率
        public static float ClosedPupilScaleY   = 0.26f;// 闭眼时瞳孔压成横缝的 Y 缩放
        public static float ClosedPupilScaleX   = 1.18f;// 闭眼时瞳孔略拉宽

        // ── 运行时状态 ──
        public PZ04Phase Phase { get; private set; } = PZ04Phase.Intro;
        public float BeamX      => _beamX;
        public float PlayerX    => _player != null ? _player.transform.position.x : 0f;
        public float LockLeft   => _graceLeft;          // 保命窗口剩余(秒)
        public float Exposure   => _exposure;           // 累计暴露(秒)
        public bool  IsShielded { get; private set; }
        public bool  IsExposed  { get; private set; }
        public int   StatuesLeft { get; private set; }
        public int   StatueCount => _statues.Count;
        public float BeamDir    => _beamDir;
        public float EyeOpen    => _eyeOpen;            // 1=黄铜眼睁开 0=闭上(调试 HUD 用)
        public string LastToast { get; private set; } = "";

        const float ZOrderEye      = 30f;
        const float ZOrderShadow   = 5f;
        const float ZOrderMarker   = 6f;
        const float ZOrderExit     = 5f;
        const float ZOrderBeam     = 600f;   // 光柱盖在角色(576)之上,保证读得清
        const float BeamTexBaseW   = 0.176f; // 光柱贴图底边宽度(单位,scale=1 时)

        // ── 内部 ──
        Transform _root;
        PlayerController _player;
        SpriteRenderer _eyeShell, _eyePupil, _eyeLid;
        Transform _eyePupilTr;
        SpriteRenderer _eyeGlow;
        Transform _beam;                       // 光柱根:位置=瞳孔,朝落点旋转
        SpriteRenderer _beamOuter, _beamCore;  // 双层(外晕 + 内核)
        Transform _spot;
        bool _wantEyesClosed;
        float _eyeOpen = 1f;                   // 1=睁开 0=闭上(平滑驱动:pupil/glow/beam 都读它)
        float _pupilX, _pupilY;                // 平滑后的瞳孔摆动(世界单位)
        bool _beamLocked;                      // 死亡演出:光柱锁死在玩家身上

        class StatueEntry
        {
            public float x;
            public GameObject go;
            public SpriteRenderer shadow;
            public SpriteRenderer marker;
            public HoodedStatueInteract interact;
            public bool used;
            public float cooldown;
        }
        readonly List<StatueEntry> _statues = new List<StatueEntry>();

        float _beamX, _beamDir = 1f, _turnTimer;
        float _graceLeft, _exposure;
        bool _graceLocked;
        int _lastShelterIndex = -1;

        Image _vignette;
        Text _toast;
        CanvasGroup _toastCg;
        float _toastTimer;

        // 程序化贴图缓存
        static Sprite _sBeam, _sGlow, _sShadow, _sSoftBox, _sSpot;
        static Material _matAdd, _matAlpha;

        // ====================================================================
        //  构建
        // ====================================================================
        public static EyeCorridorLaserPuzzle Build(Transform roomRoot, bool skipIntro = false)
        {
            var go = new GameObject("~EyeCorridorPuzzle");
            if (roomRoot != null) go.transform.SetParent(roomRoot, false);
            var p = go.AddComponent<EyeCorridorLaserPuzzle>();
            p.Init(skipIntro);
            return p;
        }

        void Init(bool skipIntro)
        {
            Instance = this;
            _root = transform;
            _graceLeft = GraceSeconds;

            BuildEyeLayers();
            BuildBeam();
            BuildStatues();
            BuildExitMarker();
            BuildUi();

            if (skipIntro) Phase = PZ04Phase.Playing;   // 调试:HUD 里可跳过剧情
        }

        void Start()
        {
            if (Phase == PZ04Phase.Intro) StartCoroutine(IntroRoutine());
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── 黄铜眼分层:外壳(bg_eye_shell) + 瞳孔球(bg_eye_pupil) + 眼睑遮挡(bg_eye_lid) ──
        //   三张图同一裁剪框,共用 EyeAnchor。
        //   lid = 外壳但「眼窝开口内」抠空 → 叠在瞳孔之上,瞳孔滚到眼角时超出开口的部分被眼睑压住。
        //   详见 tools/eye_lid_mask.py(眼窝开口由逐列梯度法自动识别,不用最外圈金属包边)。
        void BuildEyeLayers()
        {
            var shellSp = Resources.Load<Sprite>("Scenes/ChaseCorridor/bg_eye_shell");
            var pupilSp = Resources.Load<Sprite>("Scenes/ChaseCorridor/bg_eye_pupil");
            var lidSp   = Resources.Load<Sprite>("Scenes/ChaseCorridor/bg_eye_lid");

            if (shellSp != null && pupilSp != null)
            {
                var so = new GameObject("bg_eye_shell");
                so.transform.SetParent(_root, false);
                _eyeShell = so.AddComponent<SpriteRenderer>();
                _eyeShell.sprite = shellSp;
                _eyeShell.sortingOrder = (int)ZOrderEye;
                so.transform.position = new Vector3(EyeAnchor.x, EyeAnchor.y, 0f);

                var po = new GameObject("bg_eye_pupil");
                po.transform.SetParent(_root, false);
                _eyePupilTr = po.transform;
                _eyePupil = po.AddComponent<SpriteRenderer>();
                _eyePupil.sprite = pupilSp;
                _eyePupil.sortingOrder = (int)ZOrderEye + 1;
                _eyePupilTr.position = new Vector3(EyeAnchor.x, EyeAnchor.y, 0f);

                if (lidSp != null)
                {
                    var lo = new GameObject("bg_eye_lid");
                    lo.transform.SetParent(_root, false);
                    _eyeLid = lo.AddComponent<SpriteRenderer>();
                    _eyeLid.sprite = lidSp;
                    _eyeLid.sortingOrder = (int)ZOrderEye + 2;   // 压在瞳孔之上
                    lo.transform.position = new Vector3(EyeAnchor.x, EyeAnchor.y, 0f);
                }
                else
                {
                    Debug.LogWarning("[PZ04] 缺 Scenes/ChaseCorridor/bg_eye_lid(眼睑遮挡层),瞳孔不会在眼角被压住。" +
                                     "跑 tools/eye_lid_mask.py 生成。");
                }
            }
            else
            {
                Debug.LogWarning("[PZ04] 缺 Scenes/ChaseCorridor/bg_eye_shell 或 bg_eye_pupil" +
                                 "(从 黄金瞳.psd 导出的分层),退回单片 bg_brass_eye —— 瞳孔不会转动。");
                var sp = Resources.Load<Sprite>("Scenes/ChaseCorridor/bg_brass_eye");
                if (sp != null)
                {
                    var go = new GameObject("bg_brass_eye");
                    go.transform.SetParent(_root, false);
                    var eyeSr = go.AddComponent<SpriteRenderer>();
                    eyeSr.sprite = sp;
                    eyeSr.sortingOrder = (int)ZOrderEye;
                    // 与 SceneRoomBuilder.BuildLayer 同一套 pivot 数学:让"图中心"落在 y=0
                    go.transform.position = new Vector3(0f, -sp.bounds.center.y, 0f);
                }
            }

            // 眼睛位置的柔光(光柱发源点)
            var glow = new GameObject("~eye_glow");
            glow.transform.SetParent(_root, false);
            _eyeGlow = glow.AddComponent<SpriteRenderer>();
            _eyeGlow.sprite = GlowSprite();
            _eyeGlow.material = AddMat();
            _eyeGlow.color = new Color(0.95f, 0.9f, 0.75f, 0.42f);
            _eyeGlow.sortingOrder = (int)ZOrderEye + 3;      // 高于 lid
            glow.transform.position = EyeWorldPos;
            glow.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
        }

        // ── 光柱(双层:外晕 + 内核) + 地面光斑 ──
        void BuildBeam()
        {
            var beamGo = new GameObject("~beam");
            beamGo.transform.SetParent(_root, false);
            _beam = beamGo.transform;

            // 外晕:从瞳孔射向落点的柔光锥(pivot=顶中,贴图高 1 单位 → scale.y 即长度)
            var outerGo = new GameObject("outer");
            outerGo.transform.SetParent(_beam, false);
            _beamOuter = outerGo.AddComponent<SpriteRenderer>();
            _beamOuter.sprite = BeamSprite();
            _beamOuter.material = AddMat();
            _beamOuter.color = new Color(0.70f, 0.83f, 1f, 0.20f);
            _beamOuter.sortingOrder = (int)ZOrderBeam;

            // 内核:更窄更亮,做出"烧穿空气"的观感
            var coreGo = new GameObject("core");
            coreGo.transform.SetParent(_beam, false);
            _beamCore = coreGo.AddComponent<SpriteRenderer>();
            _beamCore.sprite = BeamSprite();
            _beamCore.material = AddMat();
            _beamCore.color = new Color(0.97f, 0.99f, 1f, 0.52f);
            _beamCore.sortingOrder = (int)ZOrderBeam + 1;

            var spotGo = new GameObject("~beam_spot");
            spotGo.transform.SetParent(_root, false);
            _spot = spotGo.transform;
            var spotSr = spotGo.AddComponent<SpriteRenderer>();
            spotSr.sprite = SpotSprite();          // 2×1 单位
            spotSr.material = AddMat();
            spotSr.color = new Color(1f, 0.96f, 0.82f, 0.42f);
            spotSr.sortingOrder = (int)ZOrderBeam;
        }

        // ── 兜帽石像(可点击件 + 阴影安全区 + 可用标记)──
        void BuildStatues()
        {
            foreach (float x in StatueXs)
            {
                var e = new StatueEntry { x = x };

                e.go = new GameObject("Statue_x" + x.ToString("0.00"));
                e.go.transform.SetParent(_root, false);
                e.go.transform.position = new Vector3(x, 0f, 0f);

                // 点击区:覆盖画上的石像躯干(实测袍身约 y -3.3 ~ +0.9,宽约 1.45)
                var col = e.go.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.offset = new Vector2(0f, -1.20f);
                col.size = new Vector2(1.70f, 4.30f);

                e.interact = e.go.AddComponent<HoodedStatueInteract>();

                // 阴影安全区(贴地)
                var sh = new GameObject("shadow");
                sh.transform.SetParent(e.go.transform, false);
                e.shadow = sh.AddComponent<SpriteRenderer>();
                e.shadow.sprite = ShadowSprite();
                e.shadow.material = AlphaMat();
                e.shadow.color = new Color(0f, 0f, 0f, 0.55f);
                e.shadow.sortingOrder = (int)ZOrderShadow;
                sh.transform.position = new Vector3(x, GroundY + 0.05f, 0f);

                // 可用标记(占位:柔光方块,替代"石像高亮"美术件)
                var mk = new GameObject("marker");
                mk.transform.SetParent(e.go.transform, false);
                e.marker = mk.AddComponent<SpriteRenderer>();
                e.marker.sprite = SoftBoxSprite();
                e.marker.material = AddMat();
                e.marker.color = new Color(0.55f, 0.85f, 1f, 0.30f);
                e.marker.sortingOrder = (int)ZOrderMarker;
                mk.transform.position = new Vector3(x, -1.20f, 0f);
                mk.transform.localScale = new Vector3(2.1f, 4.6f, 1f);

                e.interact.puzzle = this;
                e.interact.index = _statues.Count;
                e.interact.walkToBeforeInteract = false;   // 点石像 = 立刻瞬移,不走过去
                e.interact.marker = e.marker;
                e.interact.highlightColor = new Color(1f, 1f, 0.75f, 0.85f);
                _statues.Add(e);
            }
            StatuesLeft = _statues.Count;
        }

        void BuildExitMarker()
        {
            var go = new GameObject("~exit_marker");
            go.transform.SetParent(_root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SoftBoxSprite();
            sr.material = AddMat();
            sr.color = new Color(0.7f, 1f, 0.85f, 0.20f);
            sr.sortingOrder = (int)ZOrderExit;
            go.transform.position = new Vector3(ExitX, GroundY + 1.4f, 0f);
            go.transform.localScale = new Vector3(0.6f, 2.8f, 1f);
        }

        void BuildUi()
        {
            // 危险红晕(排序在信纸黑边之下,只铺满游戏画面)
            var vig = new GameObject("~PZ04Vignette");
            vig.transform.SetParent(_root, false);
            var c = vig.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 900;
            vig.AddComponent<CanvasScaler>();
            var imgGo = new GameObject("img");
            imgGo.transform.SetParent(vig.transform, false);
            _vignette = imgGo.AddComponent<Image>();
            _vignette.color = new Color(0.75f, 0.10f, 0.06f, 0f);
            _vignette.raycastTarget = false;
            var rt = _vignette.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            // 系统提示条(toast)
            var tgo = new GameObject("~PZ04Toast");
            tgo.transform.SetParent(vig.transform, false);
            _toastCg = tgo.AddComponent<CanvasGroup>();
            _toastCg.alpha = 0f;
            _toast = tgo.AddComponent<Text>();
            _toast.font = GameFonts.Primary;
            _toast.fontSize = 26;
            _toast.color = new Color(1f, 0.93f, 0.78f, 1f);
            _toast.alignment = TextAnchor.MiddleCenter;
            _toast.raycastTarget = false;
            var trt = _toast.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = new Vector2(0f, 150f);
            trt.sizeDelta = new Vector2(1400f, 80f);
            var sh = tgo.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.9f);
            sh.effectDistance = new Vector2(2f, -2f);
        }

        // ====================================================================
        //  主循环
        // ====================================================================
        void Update()
        {
            float dt = Time.deltaTime;
            _player = PlayerController.Instance;

            bool frozen = DialogueSystem.IsPlaying || BeamPaused || Phase != PZ04Phase.Playing;
            if (_beamLocked) _beamX = PlayerX;          // 死亡演出:光柱咬住玩家
            else if (!frozen) AdvanceBeam(dt);

            if (Phase == PZ04Phase.Playing && !DialogueSystem.IsPlaying) TickDanger(dt);
            else if (Phase != PZ04Phase.Playing) { IsShielded = false; IsExposed = false; }

            SmoothEyeOpen(dt);      // 先平滑"睁/闭",眼睛与光柱都读它
            SyncBeamVisual();
            UpdateStatueVisuals(dt);
            UpdateUi(dt);
            CheckExit();
        }

        void AdvanceBeam(float dt)
        {
            if (_turnTimer > 0f) { _turnTimer -= dt; return; }
            _beamX += _beamDir * BeamSpeed * dt;
            if (_beamX >= BeamTrackMax) { _beamX = BeamTrackMax; _beamDir = -1f; _turnTimer = BeamTurnPause; }
            else if (_beamX <= BeamTrackMin) { _beamX = BeamTrackMin; _beamDir = 1f; _turnTimer = BeamTurnPause; }
        }

        void SyncBeamVisual()
        {
            float dt = Time.deltaTime;

            // ① 眼珠跟随光柱:横向位置归一化 → 瞳孔摆动(光柱在左,眼珠看左)
            float t = Mathf.InverseLerp(BeamTrackMin, BeamTrackMax, _beamX);        // 0=最左 1=最右
            float t2 = t * 2f - 1f;                                                 // -1=最左 +1=最右
            float mid = 1f - Mathf.Abs(t2);                                         // 1=正中 0=两端
            // 左右摆幅分开:瞳孔默认位置偏左于眼窝中心,两侧到眼角(眼睑内缘)的距离不同
            float tx = PupilFollow ? (t2 < 0f ? t2 * PupilSwingLeft : t2 * PupilSwingRight) : 0f;
            float ty = PupilFollow ? -PupilSwingY * mid : 0f;                       // 扫到正中时微微俯视
            float k = 1f - Mathf.Exp(-Mathf.Max(0.01f, PupilSmooth) * dt);
            _pupilX += (tx - _pupilX) * k;
            _pupilY += (ty - _pupilY) * k;

            // ② 瞳孔:以"瞳孔中心"为支点做缩放(贴图 pivot 在框底中心,先抵消 offset 再摆)
            //   滚到眼角时横向压一点(球面转动感);闭眼压成横缝时不受影响
            float squash = 1f - PupilEdgeSquash * Mathf.Abs(t2) * _eyeOpen;
            float sx = Mathf.Lerp(ClosedPupilScaleX, 1f, _eyeOpen) * squash;
            float sy = Mathf.Lerp(ClosedPupilScaleY, 1f, _eyeOpen);
            Vector2 pupilCenter = new Vector2(EyeAnchor.x + _pupilX,
                                              EyeAnchor.y + EyePupilOffset.y + _pupilY);
            if (_eyePupilTr != null)
            {
                _eyePupilTr.localScale = new Vector3(sx, sy, 1f);
                _eyePupilTr.position = new Vector3(pupilCenter.x - EyePupilOffset.x * sx,
                                                   pupilCenter.y - EyePupilOffset.y * sy, 0f);
            }
            // 闭眼收光时三层一起收,否则 lid 会盖着 shell 显得没变化
            float eyeLit     = Mathf.Lerp(0.80f, 1f, _eyeOpen);
            float pupilLit   = Mathf.Lerp(0.42f, 1f, _eyeOpen);
            if (_eyePupil != null) _eyePupil.color = new Color(pupilLit, pupilLit, pupilLit, 1f);
            if (_eyeShell != null) _eyeShell.color = new Color(eyeLit, eyeLit, eyeLit, 1f);
            if (_eyeLid   != null) _eyeLid.color   = new Color(eyeLit, eyeLit, eyeLit, 1f);

            // ③ 光柱:从瞳孔射出 → 落点(眼珠看向哪,光就打到哪)
            Vector2 origin = _eyePupilTr != null ? pupilCenter : EyeWorldPos;
            Vector2 target = new Vector2(_beamX, GroundY);
            Vector2 d = target - origin;
            float dist = d.magnitude;
            float ang = Mathf.Atan2(d.x, -d.y) * Mathf.Rad2Deg;   // 精灵局部 -Y 指向目标
            float beamK = DimBeamWhenShielded ? Mathf.Lerp(ClosedBeamAlpha, 1f, _eyeOpen) : 1f;
            float pulse = 0.93f + 0.07f * Mathf.Sin(Time.time * 9f);

            if (_beam != null)
            {
                _beam.position = origin;
                _beam.rotation = Quaternion.Euler(0f, 0f, ang);
            }
            if (_beamOuter != null)
            {
                _beamOuter.transform.localScale = new Vector3(BeamHalfWidth * 1.9f / BeamTexBaseW, dist, 1f);
                var c = _beamOuter.color; c.a = 0.20f * beamK * pulse; _beamOuter.color = c;
            }
            if (_beamCore != null)
            {
                _beamCore.transform.localScale = new Vector3(BeamHalfWidth * 0.62f / BeamTexBaseW, dist, 1f);
                var c = _beamCore.color; c.a = 0.52f * beamK * pulse; _beamCore.color = c;
            }
            if (_spot != null)
            {
                _spot.position = new Vector3(_beamX, GroundY + 0.06f, 0f);
                _spot.localScale = new Vector3(BeamHalfWidth * (0.62f + 0.38f * beamK), 0.55f, 1f);
            }
            if (_eyeGlow != null)
            {
                float g = (0.34f + 0.10f * Mathf.Sin(Time.time * 6f)) * Mathf.Lerp(0.10f, 1f, _eyeOpen);
                _eyeGlow.color = new Color(0.95f, 0.9f, 0.75f, g);
                _eyeGlow.transform.position = new Vector3(pupilCenter.x, pupilCenter.y, 0f);  // 柔光跟着瞳孔走
            }
        }

        // ── 危险判定 ──
        void TickDanger(float dt)
        {
            float px = PlayerX;
            IsExposed = Mathf.Abs(px - _beamX) <= BeamHalfWidth;
            IsShielded = IsShieldedAt(px);

            if (IsShielded)
            {
                _graceLocked = false;
                _graceLeft = GraceSeconds;
                _exposure = 0f;
                WantEyesClosed(true);
                return;
            }

            WantEyesClosed(false);

            if (UseGraceMode)
            {
                if (IsExposed && !_graceLocked)
                {
                    _graceLocked = true;                 // 被光柱照到 → 开始 3 秒保命倒计时
                    AudioManager.PlaySfx(Sfx.eye_lock_warn);
                }
                if (_graceLocked)
                {
                    // 默认(GraceResetOnLeave=false):离开光柱也不清零,只有躲进阴影才清零
                    if (IsExposed || !GraceResetOnLeave) _graceLeft -= dt;
                    else _graceLeft = GraceSeconds;
                }
                if (_graceLeft <= 0f) { _graceLeft = 0f; Die(); }
            }
            else
            {
                if (IsExposed) _exposure += dt;
                else _exposure = Mathf.Max(0f, _exposure - dt * ExposureDecay);
                if (_exposure >= GraceSeconds) { _exposure = GraceSeconds; Die(); }
            }
        }

        bool IsShieldedAt(float px)
        {
            for (int i = 0; i < _statues.Count; i++)
            {
                var s = _statues[i];
                if (!IsStatueAvailable(s)) continue;
                if (Mathf.Abs(px - s.x) <= ShadowHalfWidth) return true;
            }
            return false;
        }

        static bool IsStatueAvailable(StatueEntry s)
        {
            if (StatueOnce) return !s.used;
            return s.cooldown <= 0f;
        }

        void UpdateStatueVisuals(float dt)
        {
            int left = 0;
            for (int i = 0; i < _statues.Count; i++)
            {
                var s = _statues[i];
                if (!StatueOnce && s.cooldown > 0f) s.cooldown = Mathf.Max(0f, s.cooldown - dt);
                bool ok = IsStatueAvailable(s);
                if (ok) left++;

                // 可用:阴影浓 + 标记亮;已失效:都淡掉
                float shadowTarget = ok ? 0.55f : 0.06f;
                var sc = s.shadow.color;
                sc.a = Mathf.Lerp(sc.a, shadowTarget, dt * 6f);
                s.shadow.color = sc;

                // 标记:可用时轻微呼吸(调试模式更亮,方便核对安全区);失效后几乎看不见
                bool hover = s.interact != null && s.interact.IsHovered;
                float markTarget = ok ? (DebugZones ? 0.34f + 0.08f * Mathf.Sin(Time.time * 3f + i) : 0.20f)
                                      : 0.02f;
                if (hover) markTarget = Mathf.Max(markTarget, 0.70f);
                Color want = hover ? new Color(1f, 0.95f, 0.70f) : new Color(0.55f, 0.85f, 1f);
                var mc = s.marker.color;
                mc.r = Mathf.Lerp(mc.r, want.r, dt * 9f);
                mc.g = Mathf.Lerp(mc.g, want.g, dt * 9f);
                mc.b = Mathf.Lerp(mc.b, want.b, dt * 9f);
                mc.a = Mathf.Lerp(mc.a, markTarget, dt * 6f);
                s.marker.color = mc;
            }
            StatuesLeft = left;
        }

        void WantEyesClosed(bool closed) { _wantEyesClosed = closed; }

        /// <summary>平滑"睁 / 闭"。躲进石像阴影 → 瞳孔收成一条横缝(石眼闭上);离开 → 睁开。
        /// 【重要】不再切换整张底图:bg_closed.png 实测就是 bg_full 的整体压暗版(≈×0.399),
        /// 没有任何"闭眼"的结构差异 —— 一切换整个画面就变暗、看起来像缩了一圈,
        /// 所以改成只动黄铜眼自己(瞳孔/光泽/光柱强度)。</summary>
        void SmoothEyeOpen(float dt)
        {
            float target = _wantEyesClosed ? 0f : 1f;
            float k = 1f - Mathf.Exp(-8f * dt);
            _eyeOpen += (target - _eyeOpen) * k;
            if (Mathf.Abs(target - _eyeOpen) < 0.002f) _eyeOpen = target;
        }

        void UpdateUi(float dt)
        {
            // 红晕强度:被锁定时随倒计时逼近而变浓;累计模式按累计量
            float danger = 0f;
            if (Phase == PZ04Phase.Playing)
            {
                danger = UseGraceMode
                    ? (_graceLocked ? 1f - Mathf.Clamp01(_graceLeft / Mathf.Max(0.01f, GraceSeconds)) : 0f)
                    : Mathf.Clamp01(_exposure / Mathf.Max(0.01f, GraceSeconds));
            }
            if (_vignette != null)
            {
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 9f);
                var c = _vignette.color;
                c.a = Mathf.Lerp(c.a, danger * 0.34f * pulse, dt * 8f);
                _vignette.color = c;
            }

            if (_toastTimer > 0f)
            {
                _toastTimer -= dt;
                if (_toastCg != null) _toastCg.alpha = Mathf.Clamp01(_toastTimer / 0.4f);
            }
        }

        void CheckExit()
        {
            if (Phase != PZ04Phase.Playing) return;
            if (PlayerX >= ExitX) StartCoroutine(WinRoutine());
        }

        public void Toast(string msg, float dur = 1.6f)
        {
            LastToast = msg;
            if (_toast != null) _toast.text = msg;
            _toastTimer = dur;
            if (_toastCg != null) _toastCg.alpha = 1f;
        }

        // ====================================================================
        //  石像瞬移
        // ====================================================================
        /// <summary>点石像 → 瞬移进它的阴影。返回是否成功。</summary>
        public bool TryShelter(int index)
        {
            if (index < 0 || index >= _statues.Count) return false;
            var s = _statues[index];

            if (Phase != PZ04Phase.Playing)
            {
                if (Phase == PZ04Phase.Done) Toast("已经穿过回廊了。");
                else if (Phase == PZ04Phase.Intro) Toast("(先听完盲僧的话。)");
                return false;
            }
            if (!IsStatueAvailable(s))
            {
                Toast(StatueOnce ? "这尊神像的目光已经醒了,阴影不再庇护你。" : "这尊神像还没缓过来。");
                return false;
            }
            if (StatueReach > 0f && Mathf.Abs(PlayerX - s.x) > StatueReach)
            {
                Toast("太远了,够不到这尊神像。");
                return false;
            }

            // 瞬移:原位置与落点各留一团闪光(占位表演)
            Vector2 from = _player != null ? (Vector2)_player.transform.position : Vector2.zero;
            Vector2 to = new Vector2(s.x, GroundY);
            SpawnFlash(from);
            if (_player != null) _player.Teleport(to);
            else Debug.LogWarning("[PZ04] 找不到玩家(PlayerController.Instance),瞬移失败。");
            SpawnFlash(to);

            if (StatueOnce) s.used = true;
            else s.cooldown = StatueCooldown;

            _graceLocked = false;
            _graceLeft = GraceSeconds;
            _exposure = 0f;
            _lastShelterIndex = index;
            AudioManager.PlaySfx(Sfx.eye_shadow);
            return true;
        }

        void SpawnFlash(Vector2 pos)
        {
            var go = new GameObject("~flash");
            go.transform.SetParent(_root, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GlowSprite();
            sr.material = AddMat();
            sr.color = new Color(0.8f, 0.92f, 1f, 0.55f);
            sr.sortingOrder = (int)ZOrderBeam + 1;
            go.transform.localScale = new Vector3(1.8f, 1.8f, 1f);
            StartCoroutine(FadeAndKill(sr, 0.28f));
        }

        IEnumerator FadeAndKill(SpriteRenderer sr, float dur)
        {
            float t = 0f;
            Color c0 = sr.color;
            while (t < dur && sr != null)
            {
                t += Time.deltaTime;
                var c = c0; c.a = Mathf.Lerp(c0.a, 0f, t / dur);
                sr.color = c;
                sr.transform.localScale = Vector3.Lerp(new Vector3(1.8f, 1.8f, 1f), new Vector3(3.2f, 3.2f, 1f), t / dur);
                yield return null;
            }
            if (sr != null) Destroy(sr.gameObject);
        }

        // ====================================================================
        //  演出:进场 / 死亡 / 通关
        // ====================================================================
        IEnumerator IntroRoutine()
        {
            Phase = PZ04Phase.Intro;
            LockPlayer(true);
            yield return new WaitForSeconds(0.5f);

            yield return Say(Dialogues.ch1_eye_enter);
            yield return Say(Dialogues.ch1_eye_monk_appear);
            yield return Say(Dialogues.ch1_eye_monk_01);
            yield return Say(Dialogues.ch1_eye_tir_01);
            yield return Say(Dialogues.ch1_eye_start_hint);

            GameState.SetFlag(Flags.Ch1_EyeIntroDone, true);
            Phase = PZ04Phase.Playing;
            LockPlayer(false);
            _graceLeft = GraceSeconds;
            _exposure = 0f;
            _graceLocked = false;
        }

        void Die()
        {
            if (Phase != PZ04Phase.Playing) return;
            Phase = PZ04Phase.Dying;
            StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine()
        {
            LockPlayer(true);
            _beamLocked = true;                      // 光柱咬住玩家(被逮住的视觉)
            _wantEyesClosed = false;                 // 别在这时候闭眼
            AudioManager.PlaySfx(Sfx.eye_burn);

            var fade = FadeOverlayColored.Get();

            // ① 灼烧瞬间:白光连闪两下 —— 不用整屏红色大图,只让画面"闪"
            for (int i = 0; i < 2; i++)
            {
                if (fade != null) yield return fade.FadeToColor(new Color(1f, 0.95f, 0.86f, 0.80f), 0.045f);
                yield return new WaitForSeconds(0.035f);
                if (fade != null) yield return fade.FadeToClear(0.055f);
                yield return new WaitForSeconds(0.030f);
            }

            // ② 闪黑:焦热扭曲→惨叫倒地,判词压在黑幕上
            if (fade != null) yield return fade.FadeToColor(new Color(0f, 0f, 0f, 0.94f), 0.16f);
            yield return new WaitForSeconds(0.15f);
            yield return Say(Dialogues.ch1_eye_death);     // 判词:目困莽夫,身陷焦土
            yield return Say(Dialogues.ch1_eye_respawn);   // 重生引导:见影即止,避瞳而行

            // ③ 回退(惩罚性死亡:回密室入口,可重试)
            if (fade != null) yield return fade.FadeToClear(0.40f);
            Respawn();
        }

        void Respawn()
        {
            // 脚本:回该密室入口,可重试(惩罚性死亡)
            float x = RespawnAtEntrance ? SpawnX
                   : (_lastShelterIndex >= 0 ? _statues[_lastShelterIndex].x : SpawnX);
            if (_player != null) _player.Teleport(new Vector2(x, GroundY));

            // 新一次尝试:石像全部恢复庇护
            for (int i = 0; i < _statues.Count; i++) { _statues[i].used = false; _statues[i].cooldown = 0f; }

            _graceLocked = false;
            _graceLeft = GraceSeconds;
            _exposure = 0f;
            _beamLocked = false;
            _beamX = BeamTrackMin;
            _beamDir = 1f;
            _turnTimer = 0.6f;
            WantEyesClosed(false);
            _eyeOpen = 1f;
            _vignette.color = new Color(0.75f, 0.10f, 0.06f, 0f);

            Phase = PZ04Phase.Playing;
            LockPlayer(false);
            Toast("被灼伤致死 —— 已退回密室入口,可重试。");
        }

        IEnumerator WinRoutine()
        {
            Phase = PZ04Phase.Reward;
            LockPlayer(true);
            WantEyesClosed(true);
            _eyeOpen = 0f;           // 走完全程 = 石眼彻底闭上(不再扫射)
            if (_beam != null) _beam.gameObject.SetActive(false);
            if (_spot != null) _spot.gameObject.SetActive(false);

            GameState.SetFlag(Flags.Ch1_EyePZ04Solved, true);
            Toast("走完了千眼回廊。");

            yield return new WaitForSeconds(0.5f);
            yield return Say(Dialogues.ch1_eye_monk_02);
            AudioManager.PlaySfx(Sfx.eye_rune);
            yield return Say(Dialogues.ch1_eye_reward);   // 获得符文【ᚱ Raidho · 旅程】(占位:文本)
            yield return Say(Dialogues.ch1_eye_tir_02);

            Phase = PZ04Phase.Done;
            LockPlayer(false);
            Toast("谜题完成(调试场景:不切场景)。按 R 重来。");
        }

        // ====================================================================
        //  工具
        // ====================================================================
        void LockPlayer(bool locked)
        {
            if (_player != null) _player.SetControllable(!locked);
        }

        IEnumerator Say(string dialogueId)
        {
            bool done = false;
            if (UsePortrait)
                DialogueSystem.ShowMonologue(DialogueSystem.GetText(dialogueId), null, () => done = true);
            else
                DialogueSystem.ShowNarration(dialogueId, () => done = true);
            while (!done) yield return null;
        }

        // ── 调试入口(HUD 用)──
        public void DebugKill() { if (Phase == PZ04Phase.Playing) Die(); }

        public void DebugReset()
        {
            StopAllCoroutines();
            for (int i = 0; i < _statues.Count; i++) { _statues[i].used = false; _statues[i].cooldown = 0f; }
            _graceLocked = false; _graceLeft = GraceSeconds; _exposure = 0f;
            _beamLocked = false;
            _beamX = BeamTrackMin; _beamDir = 1f; _turnTimer = 0.6f;
            _pupilX = 0f; _pupilY = 0f;
            if (_beam != null) _beam.gameObject.SetActive(true);
            if (_spot != null) _spot.gameObject.SetActive(true);
            WantEyesClosed(false); _eyeOpen = 1f;
            if (_player != null) _player.Teleport(new Vector2(SpawnX, GroundY));
            Phase = PZ04Phase.Playing;
            LockPlayer(false);
            Toast("已重置。");
        }

        public void DebugJumpToStatue(int index)
        {
            if (index < 0 || index >= _statues.Count || _player == null) return;
            _player.Teleport(new Vector2(_statues[index].x, GroundY));
        }

        public void DebugRestartIntro() { StopAllCoroutines(); StartCoroutine(IntroRoutine()); }

        public void DebugSetEyesClosed(bool closed)
        {
            _wantEyesClosed = closed;
            _eyeOpen = closed ? 0f : 1f;      // 直接到位,方便美术核对睁眼/闭眼两种状态
        }

        // ====================================================================
        //  程序化占位贴图(全部运行时生成,不依赖美术文件)
        // ====================================================================
        static Sprite MakeSprite(Texture2D tex, Vector2 pivot, float ppu)
        {
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, ppu);
        }

        /// <summary>光柱:上窄下宽的锥形,纵向渐隐;pivot=顶中,高 1 单位。</summary>
        static Sprite BeamSprite()
        {
            if (_sBeam != null) return _sBeam;
            const int W = 96, H = 512;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                float v = 1f - (float)y / (H - 1);          // 0=底 1=顶
                float halfW = Mathf.Lerp(45f, 4f, v);       // 底宽顶窄
                float vFade = Mathf.Lerp(0.55f, 1f, v) * Mathf.Lerp(0.35f, 1f, Mathf.SmoothStep(0f, 0.55f, v));
                for (int x = 0; x < W; x++)
                {
                    float d = Mathf.Abs(x - (W - 1) * 0.5f) / Mathf.Max(1f, halfW);
                    float a = Mathf.Clamp01(1f - d * d);
                    a *= a * vFade;
                    byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255);
                    px[y * W + x] = new Color32(255, 255, 255, b);
                }
            }
            tex.SetPixels32(px);
            _sBeam = MakeSprite(tex, new Vector2(0.5f, 1f), H);  // PPU=H → 高 1 单位
            return _sBeam;
        }

        /// <summary>柔光球(2×2 单位):眼睛发光 / 瞬移闪光。</summary>
        static Sprite GlowSprite()
        {
            if (_sGlow != null) return _sGlow;
            const int S = 256;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var px = new Color32[S * S];
            float c = (S - 1) * 0.5f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * a;
                    px[y * S + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px);
            _sGlow = MakeSprite(tex, new Vector2(0.5f, 0.5f), S / 2);   // 2×2 单位
            return _sGlow;
        }

        /// <summary>贴地阴影椭圆(2×0.75 单位)。</summary>
        static Sprite ShadowSprite()
        {
            if (_sShadow != null) return _sShadow;
            const int W = 256, H = 96;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = (x - (W - 1) * 0.5f) / ((W - 1) * 0.5f);
                    float dy = (y - (H - 1) * 0.5f) / ((H - 1) * 0.5f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = Mathf.SmoothStep(0f, 1f, a);
                    px[y * W + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px);
            _sShadow = MakeSprite(tex, new Vector2(0.5f, 0.5f), (W / 2));
            return _sShadow;
        }

        /// <summary>柔边方块(1×1 单位):石像标记 / 出口标记 / 危险区可视化。</summary>
        static Sprite SoftBoxSprite()
        {
            if (_sSoftBox != null) return _sSoftBox;
            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float fx = Mathf.Abs(x - (S - 1) * 0.5f) / ((S - 1) * 0.5f);
                    float fy = Mathf.Abs(y - (S - 1) * 0.5f) / ((S - 1) * 0.5f);
                    float edge = Mathf.Max(fx, fy);
                    float a = Mathf.Clamp01(1f - edge);
                    a = Mathf.SmoothStep(0f, 1f, a);
                    a *= a;
                    px[y * S + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px);
            _sSoftBox = MakeSprite(tex, new Vector2(0.5f, 0.5f), S);
            return _sSoftBox;
        }

        /// <summary>地面光斑(2×1 单位,横向椭圆)。</summary>
        static Sprite SpotSprite()
        {
            if (_sSpot != null) return _sSpot;
            const int W = 256, H = 128;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = (x - (W - 1) * 0.5f) / ((W - 1) * 0.5f);
                    float dy = (y - (H - 1) * 0.5f) / ((H - 1) * 0.5f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy * 1.1f);
                    float a = Mathf.Clamp01(1f - d);
                    a = Mathf.SmoothStep(0f, 1f, a);
                    px[y * W + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px);
            _sSpot = MakeSprite(tex, new Vector2(0.5f, 0.5f), W / 2);
            return _sSpot;
        }

        static Material AddMat()
        {
            if (_matAdd != null) return _matAdd;
            var sh = Shader.Find("Legacy Shaders/Particles/Additive");
            if (sh == null) sh = Shader.Find("Mobile/Particles/Additive");
            if (sh == null)
            {
                sh = Shader.Find("Sprites/Default");
                Debug.LogWarning("[PZ04] 找不到加色混合 shader,退回 Sprites/Default(光柱会偏灰)。");
            }
            if (sh == null)
            {
                Debug.LogError("[PZ04] 连 Sprites/Default 都找不到,占位光效将不显示。");
                return null;
            }
            _matAdd = new Material(sh);
            return _matAdd;
        }

        static Material AlphaMat()
        {
            if (_matAlpha != null) return _matAlpha;
            var sh = Shader.Find("Sprites/Default");
            if (sh == null)
            {
                Debug.LogError("[PZ04] 找不到 Sprites/Default,占位阴影将不显示。");
                return null;
            }
            _matAlpha = new Material(sh);
            return _matAlpha;
        }
    }
}
