// ============================================================================
//  EyeCorridorLaserPuzzle.cs —— S05 千眼回廊 · 青年线 PZ_04「躲光束」
//
//  玩法(2026-09-14 按用户要求整体推翻重做;同日第二轮修订):
//    · 墙上挂一只黄铜眼,同时射出【两片锥形光区】(顶点都在铜眼、底边都贴地、
//      每片宽约三个拱门),一副镜像姿态在走廊里左右反复扫荡,永不停歇。
//      两片的相位差 0.5 → 永远一片偏左一片偏右,像两只眼各扫半边。
//    · 青年从【最左端】出发,必须走到【最右端场外】。他 3.0 单位/秒,光带 11.0 单位/秒。
//    · 被任何一片光照到 = 立刻失败:白光一闪 + 人闪回最左端重来。
//    · 石像之间的 8 个【拱门暗影】(美术 bg_gate_shadow.png)是安全点:
//      光束扫过也不算被照到;人躲进去会被同步调暗,和暗影融为一体。
//    · 出生点故意放在最左端安全区之外 —— 站着不动就会被扫到,必须马上动。
//    · 全程没有引导 UI;只有**死亡判词**(D_02「目困莽夫,身陷焦土。」/
//      「见影即止,避瞳而行。」)保留,走底部对白条。← 用户明确要求不要丢
//
//  几何来源(bg_full.png / bg_gate_shadow.png 都是 3400×1200 / PPU100,
//           世界X = px/100 - 17,世界Y = 6 - py/100,世界宽 34、groundY = -5.76):
//    · 铜眼 = 美术图层 bg_brass_eye.png(与背景同格)直接贴,中心 (-0.075, 4.40)
//    · 8 个拱门安全区(实测自 bg_gate_shadow.png 的 alpha 区间)中心依次:
//      -15.085 / -10.795 / -6.460 / -2.180 / +2.120 / +6.420 / +10.725 / +15.085,间距 4.31
//      (它们落在石像**之间**,石像在 -12.96 / -8.56 / -4.30 / 0 / 4.34 / 8.61 / 12.95)
//
//  为什么用 Mesh 画光束:光锥顶点固定在铜眼、底边必须贴地且宽度恒定,
//  所以是**斜三角形**,等腰 sprite 拼不出来;用 3 顶点 Mesh 每帧重算即可。
//  「散光」质感靠三件事(2026-09-14 第二轮,用户嫌"太亮像一束小激光"):
//    ① 拿掉原来的高亮内核(旧版还有一层 alpha 0.68 的芯 → 过曝)
//    ② 横向柔边 + 顶点压暗 + 颗粒噪点(程序化贴图 + 顶点色,见 ConeTex/BuildBeam)
//    ③ 整体亮度降到 additive alpha 0.16 左右
//
//  离线可玩性校验:tools/check_pz04_beam.py(改常量后跑一次,出图 + 模拟)
// ============================================================================

using System.Collections;
using UnityEngine;

namespace LostGoddess.Content
{
    public enum PZ04Phase { Intro, Playing, Reward, Done }

    /// <summary>
    /// 千眼回廊 PZ_04:青年在铜眼的两片锥形光区之间躲到走廊另一端。
    /// 被照到立刻回退到最左端重来;拱门暗影内是安全点(且角色会被同步调暗)。
    /// </summary>
    public class EyeCorridorLaserPuzzle : MonoBehaviour
    {
        public static EyeCorridorLaserPuzzle Instance { get; private set; }

        // ── 走廊几何(与 SceneRoomBuilder.EyeCorridor 对齐) ──
        public const float CorridorHalfWidth = 17f;      // 3400px / PPU100 / 2
        public const float GroundY           = -5.76f;   // 已实机调过的地平线

        /// <summary>出生点 = 走廊最左端。★故意放在第一个拱门安全区(-16.28)之外,
        /// 所以开局站着不动会被光束扫到 —— 这是设计要求(玩家必须立刻做出反应)。</summary>
        public static float SpawnX = -16.45f;
        /// <summary>走到这里 = 出右侧场外 → 通关(最后一个安全区右缘 16.34 之外)。</summary>
        public static float ExitX  =  16.45f;

        // ── 铜眼(静态美术层,不做眼珠转动/眼睑开合) ──
        /// <summary>铜眼内容中心的世界坐标(与最初那版 bg_brass_eye 实测包围盒一致)。</summary>
        public static readonly Vector2 EyeCenterWorld = new Vector2(-0.075f, 4.400f);
        /// <summary>光束顶点:略低于眼睛中心,看着像从瞳仁里打出来。</summary>
        public static float BeamOriginY = 4.30f;

        // ── 拱门安全区(实测自 bg_gate_shadow.png 的 alpha 列区间) ──
        public static readonly float[] GateMinX = { -16.280f, -12.080f, -7.700f, -3.420f, 0.830f, 5.170f,  9.420f, 13.830f };
        public static readonly float[] GateMaxX = { -13.890f,  -9.510f, -5.220f, -0.940f, 3.410f, 7.670f, 12.030f, 16.340f };

        // ── 光束参数(调试场景可实时改) ──
        [Header("光束")]
        /// <summary>同时存在几片光区。★用户要求 2(参照图里画了两片区域)。</summary>
        public static int   BeamCount          = 2;
        /// <summary>相邻两片的相位差(0.5 = 永远左右镜像对开;改成 0.25 就会前后跟跑)。</summary>
        public static float BeamPhaseStep      = 0.5f;
        /// <summary>地面光带中心移动速度(单位/秒)。★用户要求"比旧版快一倍":旧 5.5 → 11。</summary>
        public static float BeamGroundSpeed    = 11.0f;
        /// <summary>地面危险带半宽(总宽 7.5 ≈ 三个拱门宽,拱门 2.5 单位)。</summary>
        public static float BeamGroundHalfWidth = 3.75f;
        /// <summary>光带中心左右扫到的范围(走廊两端)。</summary>
        public static float BeamTrackMin = -17.0f;
        public static float BeamTrackMax =  17.0f;
        /// <summary>★复位时两束所在的地面位置(±对称)。9.3 ⇒ 出生点还有约 2.0 秒反应窗口。
        /// 若设成 17(贴着两端),镜像的另一束会正好盖在出生点上,一开局就死。</summary>
        public static float BeamResetX  = 9.3f;
        public static bool  BeamPaused  = false;

        [Header("散光观感")]
        /// <summary>光雾整体亮度(additive 材质的 alpha)。旧版 0.30 + 0.68 内核 → 中心过曝。</summary>
        public static float BeamAlpha     = 0.11f;
        /// <summary>顶点(靠铜眼那头)的额外压暗,避免光锥在眼睛处糊成白团。</summary>
        public static float BeamApexFade  = 0.50f;
        /// <summary>散光颗粒感强度(0=纯平滑)。</summary>
        public static float BeamGrain     = 0.14f;
        /// <summary>地面亮条亮度(危险边界的读数就靠它)。</summary>
        public static float BeamSpotAlpha = 0.22f;
        /// <summary>地面亮条比危险带多出来的比例。★只能放一点点给柔降用 —— 宽出去多少,
        /// 地面就多亮多少"没被光吹到的地方"。见 GroundBarSprite() 的说明。</summary>
        const float BarBleed = 1.06f;
        /// <summary>铜眼光晕亮度。</summary>
        public static float EyeGlowAlpha  = 0.30f;

        [Header("暗影调光")]
        /// <summary>★躲进拱门暗影时角色的亮度倍率(遮罩 alpha≈100/255 ⇒ 场景被压到 0.61,角色跟着压才"融"进去)。</summary>
        public static float ShadowDim       = 0.60f;
        /// <summary>调光过渡速度(越大越快)。</summary>
        public static float ShadowDimSpeed  = 7.0f;

        [Header("被抓 / 演出")]
        /// <summary>被抓到时的白光强度(0=不闪,直接回退)。</summary>
        public static float CaughtFlashAlpha = 0.85f;
        /// <summary>★是否播死亡判词(D_02)。用户明确要求不要丢掉。</summary>
        public static bool  ShowDeathVerdict  = true;
        /// <summary>判词是否每次死亡都播。false(默认)= 首次死播完整判词,之后的失败只播一句短引导,
        /// 免得每次重来都被 5 秒字幕卡住。</summary>
        public static bool  VerdictOnEveryDeath = false;
        /// <summary>调试:画出安全区(绿)与危险带(红)。**默认关** —— 正式观感不该有这些。</summary>
        public static bool  DebugZones = false;

        // ── 运行时状态 ──
        public PZ04Phase Phase { get; private set; } = PZ04Phase.Intro;
        public float BeamX     => BeamCount > 0 ? BeamXAt(0) : 0f;
        public float BeamDir   => BeamCount > 0 ? BeamDirAt(0) : 1f;
        public float PlayerX   => _player != null ? _player.transform.position.x : 0f;
        /// <summary>玩家此刻是否站在拱门暗影里(安全)。</summary>
        public bool  PlayerInGate { get; private set; }
        /// <summary>玩家此刻是否被光束照着(含安全区判定之后的结果)。</summary>
        public bool  PlayerLit    { get; private set; }
        /// <summary>角色的暗影调光系数(1=原亮度)。</summary>
        public float ShadowFactor => _dimFactor;
        /// <summary>被抓到的累计次数(调试用)。</summary>
        public int   CaughtCount  { get; private set; }

        const float ZOrderEye        = 30f;     // 铜眼贴图层
        const float ZOrderBeam       = 600f;    // 光束盖在角色(576)之上,保证读得清
        const float ZOrderDebugSafe  = 4f;
        const float ZOrderDebugDanger = 5f;

        // ── 内部 ──
        Transform _root;
        PlayerController _player;

        SpriteRenderer _eye;
        SpriteRenderer _eyeGlow;

        // 多片光区:每片一份 mesh + 地面光斑;材质共用一份(外观一样)
        Mesh[]            _beamMesh;
        MeshRenderer[]    _beamMr;
        Transform[]       _spot;
        SpriteRenderer[]  _spotSr;
        Material          _beamMat;
        Vector3[][]       _triVerts;             // 每片 3 个顶点(顶点/左底/右底)

        Transform _debugRoot;
        SpriteRenderer[] _debugSafe, _debugDanger;

        float _phase;                            // 0..1 三角波相位(0=最左,0.5=最右)
        bool  _busy;                             // 演出中(开场/被抓/通关)时不判定
        float _apexFadeApplied = -1f;            // 已经写进顶点色的顶点压暗系数

        // 角色暗影调光
        SpriteRenderer[] _prSr;
        Color[]          _prBase;
        Transform        _prRoot;
        float            _dimFactor = 1f;

        static Sprite _sGlow, _sSoftBox, _sGroundBar;
        static Texture2D _texCone;
        static Material _matShared;

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

            BuildEye();
            BuildBeam();
            BuildDebugZones();
            ResetRun();

            if (skipIntro) Phase = PZ04Phase.Playing;
        }

        void Start()
        {
            if (Phase == PZ04Phase.Intro) StartCoroutine(IntroRoutine());
        }

        void OnDestroy()
        {
            if (_beamMesh != null)
                for (int i = 0; i < _beamMesh.Length; i++)
                    if (_beamMesh[i] != null) Destroy(_beamMesh[i]);
            if (_beamMat != null && _beamMat != _matShared) Destroy(_beamMat);
            if (Instance == this) Instance = null;
        }

        // ── 铜眼:静态贴图(最初那版 bg_brass_eye,3400×1200 与 bg_full 同格 → 位置天然正确) ──
        void BuildEye()
        {
            var sp = Resources.Load<Sprite>("Scenes/ChaseCorridor/bg_brass_eye");
            if (sp == null) sp = Resources.Load<Sprite>("Scenes/ChaseCorridor/bg_eye_shell");
            if (sp == null)
            {
                Debug.LogWarning("[PZ04] 找不到 Scenes/ChaseCorridor/bg_brass_eye,铜眼不显示。");
            }
            else
            {
                var go = new GameObject("bg_brass_eye");
                go.transform.SetParent(_root, false);
                _eye = go.AddComponent<SpriteRenderer>();
                _eye.sprite = sp;
                _eye.sortingOrder = (int)ZOrderEye;
                if (sp.bounds.size.x > 30f)
                {
                    // 3400×1200 同格图:让"图中心"落在世界 (0,0),眼睛内容就回到 (-0.075, 4.40)
                    go.transform.position = new Vector3(0f, -sp.bounds.center.y, 0f);
                }
                else
                {
                    // 分层素材(525×380 裁剪框 / BottomCenter)直接按内容中心摆
                    go.transform.position = new Vector3(EyeCenterWorld.x, EyeCenterWorld.y, 0f);
                }
            }

            // 铜眼光晕(让静态贴图有个"在发亮"的观感)
            var glow = new GameObject("~eye_glow");
            glow.transform.SetParent(_root, false);
            _eyeGlow = glow.AddComponent<SpriteRenderer>();
            _eyeGlow.sprite = GlowSprite();
            _eyeGlow.material = SharedAddMat();
            _eyeGlow.color = new Color(1f, 0.93f, 0.72f, EyeGlowAlpha);
            _eyeGlow.sortingOrder = (int)ZOrderEye + 1;
            glow.transform.position = new Vector3(EyeCenterWorld.x, EyeCenterWorld.y + 0.05f, 0f);
            glow.transform.localScale = new Vector3(2.2f, 2.2f, 1f);
        }

        // ── 光束:每片一个 3 顶点斜三角(顶点=铜眼,底边贴地、宽 2×BeamGroundHalfWidth) ──
        void BuildBeam()
        {
            int n = Mathf.Clamp(BeamCount, 1, 4);
            _beamMesh   = new Mesh[n];
            _beamMr     = new MeshRenderer[n];
            _triVerts   = new Vector3[n][];
            _spot       = new Transform[n];
            _spotSr     = new SpriteRenderer[n];

            // ★ 光束单独一份材质实例 —— 不能和精灵共用:每帧要写 _TintColor,
            //   共用会把铜眼光晕/地面光斑/调试色块一起改掉。
            var bsh = AddShader();
            _beamMat = bsh != null ? new Material(bsh) : SharedAddMat();
            if (_beamMat != null) _beamMat.mainTexture = ConeTex();

            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("~beam" + i);
                go.transform.SetParent(_root, false);
                _beamMesh[i] = NewTriMesh("beam" + i);
                _beamMr[i] = go.AddComponent<MeshRenderer>();
                go.AddComponent<MeshFilter>().mesh = _beamMesh[i];
                _beamMr[i].material = _beamMat;
                _beamMr[i].sortingOrder = (int)ZOrderBeam;
                _triVerts[i] = new Vector3[3];

                // 每片一块贴地光斑(危险带最直观的读数)
                var spotGo = new GameObject("~beam_spot" + i);
                spotGo.transform.SetParent(_root, false);
                _spot[i] = spotGo.transform;
                _spotSr[i] = spotGo.AddComponent<SpriteRenderer>();
                _spotSr[i].sprite = GroundBarSprite();
                _spotSr[i].material = SharedAddMat();
                _spotSr[i].color = new Color(1f, 1f, 1f, BeamSpotAlpha);
                _spotSr[i].sortingOrder = (int)ZOrderBeam - 1;
            }
        }

        static Mesh NewTriMesh(string name)
        {
            var m = new Mesh { name = name };
            m.MarkDynamic();
            m.vertices = new Vector3[3];
            m.uv = new[] { new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f) };
            // 顶点色:顶点(靠铜眼)压暗一点,底边(地面)最实 → 光锥不会在眼睛处糊成白团。
            // 加色 shader 会把顶点色乘进去;不给颜色时取值不保证是白,显式写一遍。
            m.colors = new[] { new Color(1f, 1f, 1f, 0.6f), Color.white, Color.white };
            m.triangles = new[] { 0, 1, 2 };
            return m;
        }

        /// <summary>材质颜色:加色 shader 用 _TintColor(且默认值 0.5 代表 1x),Sprites/Default 用 _Color。</summary>
        static void SetMatColor(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_TintColor"))
                m.SetColor("_TintColor", new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, c.a));
        }

        void BuildDebugZones()
        {
            _debugRoot = new GameObject("~pz04_debug").transform;
            _debugRoot.SetParent(_root, false);
            _debugSafe = new SpriteRenderer[GateMinX.Length];
            for (int i = 0; i < GateMinX.Length; i++)
            {
                var go = new GameObject("safe" + i);
                go.transform.SetParent(_debugRoot, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SoftBoxSprite();
                sr.material = SharedAddMat();
                sr.color = new Color(0.35f, 1f, 0.55f, 0.22f);
                sr.sortingOrder = (int)ZOrderDebugSafe;
                float w = GateMaxX[i] - GateMinX[i];
                go.transform.position = new Vector3((GateMinX[i] + GateMaxX[i]) * 0.5f, GroundY + 2.4f, 0f);
                go.transform.localScale = new Vector3(w, 4.8f, 1f);
                _debugSafe[i] = sr;
            }
            int dn = Mathf.Max(1, BeamCount);
            _debugDanger = new SpriteRenderer[dn];
            for (int i = 0; i < dn; i++)
            {
                var dgo = new GameObject("danger" + i);
                dgo.transform.SetParent(_debugRoot, false);
                var dsr = dgo.AddComponent<SpriteRenderer>();
                dsr.sprite = SoftBoxSprite();
                dsr.material = SharedAddMat();
                dsr.color = new Color(1f, 0.30f, 0.25f, 0.20f);
                dsr.sortingOrder = (int)ZOrderDebugDanger;
                dgo.transform.position = new Vector3(0f, GroundY + 2.4f, 0f);
                _debugDanger[i] = dsr;
            }
            _debugRoot.gameObject.SetActive(DebugZones);
        }

        // ====================================================================
        //  主循环
        // ====================================================================
        void Update()
        {
            float dt = Time.deltaTime;
            _player = PlayerController.Instance;

            if (!_busy && Phase == PZ04Phase.Playing && !DialogueSystem.IsPlaying) AdvanceBeam(dt);
            SyncBeamVisual();

            PlayerInGate = IsInSafeGate(PlayerX);
            PlayerLit    = IsLit(PlayerX);
            ApplyPlayerDim(dt);

            if (!_busy && Phase == PZ04Phase.Playing && !DialogueSystem.IsPlaying)
            {
                if (PlayerLit) { StartCoroutine(CaughtRoutine()); return; }
                if (PlayerX >= ExitX) { StartCoroutine(WinRoutine()); return; }
            }

            // 调试可视化
            if (_debugRoot != null && _debugRoot.gameObject.activeSelf)
            {
                for (int i = 0; i < _debugDanger.Length; i++)
                {
                    var d = _debugDanger[i];
                    if (d == null) continue;
                    d.transform.position = new Vector3(BeamXAt(i), GroundY + 2.4f, 0f);
                    d.transform.localScale = new Vector3(BeamGroundHalfWidth * 2f, 4.8f, 1f);
                    var c = d.color;
                    c.a = PlayerLit ? 0.42f : 0.18f;
                    d.color = c;
                }
                for (int i = 0; i < _debugSafe.Length; i++)
                {
                    var sc = _debugSafe[i].color;
                    sc.a = (PlayerInGate && PlayerX >= GateMinX[i] && PlayerX <= GateMaxX[i]) ? 0.55f : 0.20f;
                    _debugSafe[i].color = sc;
                }
            }
        }

        // ── 相位 ↔ 位置 ──
        static float BeamSpan => BeamTrackMax - BeamTrackMin;
        static float BeamPeriod => 2f * BeamSpan / Mathf.Max(0.05f, BeamGroundSpeed);

        /// <summary>三角波:0→最左,0.5→最右,1→回到最左。</summary>
        static float TriValue(float ph)
        {
            ph = Mathf.Repeat(ph, 1f);
            return ph < 0.5f ? ph * 2f : (1f - ph) * 2f;
        }

        public static float BeamXAt(int i)
        {
            if (Instance == null) return 0f;
            return Mathf.Lerp(BeamTrackMin, BeamTrackMax,
                              TriValue(Instance._phase + i * Mathf.Max(0.0001f, BeamPhaseStep)));
        }

        public static float BeamDirAt(int i)
        {
            if (Instance == null) return 1f;
            return Mathf.Repeat(Instance._phase + i * Mathf.Max(0.0001f, BeamPhaseStep), 1f) < 0.5f ? 1f : -1f;
        }

        /// <summary>某个地面位置此刻是否会被任一片光照到(不考虑安全区)。</summary>
        public static bool IsLitByAnyBeam(float x)
        {
            int n = Instance != null ? Mathf.Clamp(BeamCount, 1, 4) : 0;
            for (int i = 0; i < n; i++)
                if (Mathf.Abs(x - BeamXAt(i)) <= BeamGroundHalfWidth) return true;
            return false;
        }

        /// <summary>给定时相下,让第 0 片落在地面 x 处、且朝左走。</summary>
        static float PhaseForX(float x)
        {
            float u = Mathf.Clamp01((x - BeamTrackMin) / Mathf.Max(0.01f, BeamSpan));
            return 1f - u * 0.5f;                 // 三角波的下降段 = 朝左走
        }

        /// <summary>光带中心按相位匀速推进(两端不停顿 —— 用户要求"不会消失并且一直移动")。</summary>
        void AdvanceBeam(float dt)
        {
            if (BeamPaused) return;
            _phase = Mathf.Repeat(_phase + dt / BeamPeriod, 1f);
        }

        /// <summary>光锥形状:顶点固定在铜眼;底边贴地、以各自的光带中心为准、半宽固定。</summary>
        void SyncBeamVisual()
        {
            float pulse = 0.94f + 0.06f * Mathf.Sin(Time.time * 6f);
            // ★ 光是纯白的(不再带暖黄):颜色只由 alpha 与颗粒决定,不掺色调
            SetMatColor(_beamMat, new Color(1f, 1f, 1f, Mathf.Clamp01(BeamAlpha) * pulse));

            float barW = BeamGroundHalfWidth * 2f * BarBleed;
            for (int i = 0; i < _beamMesh.Length; i++)
            {
                if (_beamMesh[i] == null) continue;
                float bx = BeamXAt(i);

                _triVerts[i][0] = new Vector3(EyeCenterWorld.x, BeamOriginY, 0f);
                _triVerts[i][1] = new Vector3(bx - BeamGroundHalfWidth, GroundY, 0f);
                _triVerts[i][2] = new Vector3(bx + BeamGroundHalfWidth, GroundY, 0f);
                _beamMesh[i].vertices = _triVerts[i];
                _beamMesh[i].RecalculateBounds();

                if (_spot[i] != null)
                {
                    // 地面亮条:中间一段是平台、两端柔降 —— 危险边界全靠它读
                    _spot[i].position = new Vector3(bx, GroundY + 0.22f, 0f);
                    _spot[i].localScale = new Vector3(barW, 1.30f, 1f);
                    _spotSr[i].color = new Color(1f, 1f, 1f, Mathf.Clamp01(BeamSpotAlpha) * pulse);
                }
            }

            if (_eyeGlow != null)
                _eyeGlow.color = new Color(1f, 0.93f, 0.72f,
                                           Mathf.Clamp01(EyeGlowAlpha) * (0.9f + 0.1f * pulse));

            // 顶点(靠铜眼那头)的压暗系数 —— 参数改了立刻重写顶点色
            float apex = Mathf.Clamp(BeamApexFade, 0.05f, 1f);
            if (Mathf.Abs(apex - _apexFadeApplied) > 0.005f)
            {
                _apexFadeApplied = apex;
                var vc = new[] { new Color(1f, 1f, 1f, apex), Color.white, Color.white };
                for (int i = 0; i < _beamMesh.Length; i++)
                    if (_beamMesh[i] != null) _beamMesh[i].colors = vc;
            }
        }

        /// <summary>玩家 X 是否落在某个拱门暗影里(安全点)。</summary>
        public static bool IsInSafeGate(float x)
        {
            for (int i = 0; i < GateMinX.Length; i++)
                if (x >= GateMinX[i] && x <= GateMaxX[i]) return true;
            return false;
        }

        /// <summary>是否被照到(= 在任一片光里 且 不在安全区)。</summary>
        public static bool IsLit(float x)
        {
            return !IsInSafeGate(x) && IsLitByAnyBeam(x);
        }

        /// <summary>离 x 最近的安全区中心(调试跳段用)。</summary>
        public static float NearestGateCenter(float x)
        {
            float best = GateMinX[0]; float bd = float.MaxValue;
            for (int i = 0; i < GateMinX.Length; i++)
            {
                float c = (GateMinX[i] + GateMaxX[i]) * 0.5f;
                float d = Mathf.Abs(c - x);
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        // ====================================================================
        //  角色暗影调光:躲进拱门暗影(遮罩区)时把角色一起压暗,看着才"融进去"
        // ====================================================================
        void EnsurePlayerRenderers()
        {
            if (_player == null) return;
            if (_prRoot == _player.transform && _prSr != null && _prSr.Length > 0 && _prSr[0] != null)
                return;

            // 角色可能被 PlayerBuilder 重建(换形态),重新抓一遍并把"已施加的压暗"除回去
            _prRoot = _player.transform;
            _prSr = _player.GetComponentsInChildren<SpriteRenderer>(true);
            _prBase = new Color[_prSr.Length];
            float inv = _dimFactor > 0.001f ? 1f / _dimFactor : 1f;
            for (int i = 0; i < _prSr.Length; i++)
            {
                var c = _prSr[i].color;
                _prBase[i] = new Color(Mathf.Clamp01(c.r * inv), Mathf.Clamp01(c.g * inv),
                                       Mathf.Clamp01(c.b * inv), c.a);
            }
        }

        void ApplyPlayerDim(float dt)
        {
            EnsurePlayerRenderers();
            if (_prSr == null || _prSr.Length == 0) return;

            float target = PlayerInGate ? Mathf.Clamp01(ShadowDim) : 1f;
            float k = 1f - Mathf.Exp(-Mathf.Max(0.1f, ShadowDimSpeed) * dt);
            _dimFactor += (target - _dimFactor) * k;
            if (Mathf.Abs(_dimFactor - target) < 0.002f) _dimFactor = target;

            for (int i = 0; i < _prSr.Length; i++)
            {
                if (_prSr[i] == null) continue;
                var b = _prBase[i];
                _prSr[i].color = new Color(b.r * _dimFactor, b.g * _dimFactor, b.b * _dimFactor, b.a);
            }
        }

        // ====================================================================
        //  演出:开场 / 被抓 / 通关
        // ====================================================================
        IEnumerator IntroRoutine()
        {
            Phase = PZ04Phase.Intro;
            _busy = true;
            LockPlayer(true);
            ResetRun();
            yield return new WaitForSeconds(0.4f);

            yield return Say(Dialogues.ch1_eye_enter);
            yield return Say(Dialogues.ch1_eye_monk_appear);
            yield return Say(Dialogues.ch1_eye_monk_01);
            // ★ 用户要求:青年这句之后**直接进游戏**,不再播任何引导/说明
            yield return Say(Dialogues.ch1_eye_tir_01);      // 「装神弄鬼。不就是躲个光吗?」

            GameState.SetFlag(Flags.Ch1_EyeIntroDone, true);
            ResetRun();
            Phase = PZ04Phase.Playing;
            LockPlayer(false);
            _busy = false;
        }

        /// <summary>被光束照到 —— 立刻失败:闪一下白光 + 人回最左端重来,再播 D_02 判词。
        /// ★ 判词是用户明确要求保留的(之前整段死亡演出被砍,连判词一起没了)。</summary>
        IEnumerator CaughtRoutine()
        {
            _busy = true;
            CaughtCount++;
            LockPlayer(true);
            AudioManager.PlaySfx(Sfx.eye_burn);

            var fade = FadeOverlayColored.Get();
            float a = Mathf.Clamp01(CaughtFlashAlpha);
            if (fade != null && a > 0.01f)
            {
                yield return fade.FadeToColor(new Color(1f, 0.96f, 0.88f, a), 0.05f);
                if (_player != null) _player.Teleport(new Vector2(SpawnX, GroundY));
                ResetRun();
                yield return fade.FadeToClear(0.18f);
            }
            else
            {
                if (_player != null) _player.Teleport(new Vector2(SpawnX, GroundY));
                ResetRun();
            }

            // ── 死亡判词(D_02) ──
            if (ShowDeathVerdict)
            {
                bool first = CaughtCount == 1;
                if (first || VerdictOnEveryDeath)
                    yield return Say(Dialogues.ch1_eye_death);     // 目困莽夫,身陷焦土。
                yield return Say(Dialogues.ch1_eye_respawn);       // 见影即止,避瞳而行。
            }

            Phase = PZ04Phase.Playing;
            LockPlayer(false);
            _busy = false;
        }

        IEnumerator WinRoutine()
        {
            _busy = true;
            Phase = PZ04Phase.Reward;
            LockPlayer(true);
            GameState.SetFlag(Flags.Ch1_EyePZ04Solved, true);

            yield return new WaitForSeconds(0.4f);
            yield return Say(Dialogues.ch1_eye_monk_02);
            AudioManager.PlaySfx(Sfx.eye_rune);
            yield return Say(Dialogues.ch1_eye_reward);      // 符文【ᚱ Raidho · 旅程】
            yield return Say(Dialogues.ch1_eye_tir_02);

            Phase = PZ04Phase.Done;
            LockPlayer(false);
            _busy = false;
        }

        /// <summary>把一次尝试复位:人回最左端、两片光束回到 ±BeamResetX(朝外走)。
        /// ★ 不能把光束丢在两端 —— 镜像是成对的,一束在 -17 另一束就在 +17,正好盖住出生点,
        ///   开局没反应过来就死。放在 ±9.3 时出生点有约 2.0 秒缓冲。</summary>
        void ResetRun()
        {
            _phase = PhaseForX(BeamResetX);
            if (_player == null) _player = PlayerController.Instance;
            if (_player != null) _player.Teleport(new Vector2(SpawnX, GroundY));
            SyncBeamVisual();
        }

        void LockPlayer(bool locked)
        {
            if (_player == null) _player = PlayerController.Instance;
            if (_player != null) _player.SetControllable(!locked);
        }

        IEnumerator Say(string dialogueId)
        {
            bool done = false;
            DialogueSystem.ShowMonologue(DialogueSystem.GetText(dialogueId), null, () => done = true);
            while (!done) yield return null;
            yield return new WaitForSeconds(0.1f);
        }

        // ====================================================================
        //  调试接口
        // ====================================================================
        public void DebugReset()
        {
            StopAllCoroutines();
            _busy = false;
            CaughtCount = 0;
            ResetRun();
            Phase = PZ04Phase.Playing;
            LockPlayer(false);
        }

        public void DebugRestartIntro()
        {
            StopAllCoroutines();
            StartCoroutine(IntroRoutine());
        }

        /// <summary>强制被抓一次(测回退 + 判词)。</summary>
        public void DebugCatch()
        {
            if (Phase != PZ04Phase.Playing || _busy) return;
            StartCoroutine(CaughtRoutine());
        }

        /// <summary>把玩家瞬移到第 i 个拱门安全区中心(0-based)。</summary>
        public void DebugJumpToGate(int i)
        {
            if (i < 0 || i >= GateMinX.Length) return;
            float c = (GateMinX[i] + GateMaxX[i]) * 0.5f;
            if (_player == null) _player = PlayerController.Instance;
            if (_player != null) _player.Teleport(new Vector2(c, GroundY));
        }

        public void DebugSetZones(bool on)
        {
            DebugZones = on;
            if (_debugRoot != null) _debugRoot.gameObject.SetActive(on);
        }

        // ====================================================================
        //  程序化贴图(全部运行时生成,不依赖美术文件)
        // ====================================================================
        static Sprite MakeSprite(Texture2D tex, Vector2 pivot, float ppu)
        {
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, ppu);
        }

        static float Hash01(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7fffffff) / (float)0x7fffffff;
            }
        }

        /// <summary>平滑值噪声(散光颗粒用)。</summary>
        static float ValueNoise(float fx, float fy)
        {
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float a = Hash01(x0, y0),     b = Hash01(x0 + 1, y0);
            float c = Hash01(x0, y0 + 1), d = Hash01(x0 + 1, y0 + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        /// <summary>光锥贴图:纯柔边(中间最亮、两端渐隐) + 颗粒噪点 —— 一层"散光",不是一块板。
        /// 纵向明暗交给 mesh 顶点色(顶点=靠铜眼,压暗)。UV:顶点 (0.5,1) / 底边 (0/1,0)。</summary>
        static Texture2D ConeTex()
        {
            if (_texCone != null) return _texCone;
            const int S = 256;
            float grain = Mathf.Clamp01(BeamGrain);
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
            {
                float v = (float)y / (S - 1);            // 0=底(地面) 1=顶(铜眼)
                for (int x = 0; x < S; x++)
                {
                    float u = (float)x / (S - 1);
                    float e = Mathf.Min(u, 1f - u) * 2f;                 // 0=边缘 1=中线
                    float lat = Mathf.SmoothStep(0f, 1f, e);             // 纯柔边,没有平台
                    // 轻微颗粒:让它是"一片散光"而不是一块半透明塑料板
                    float n = ValueNoise(u * 11f, v * 17f) * 0.65f + ValueNoise(u * 29f, v * 37f) * 0.35f;
                    float a = Mathf.Clamp01(lat * (1f - grain + grain * n));
                    px[y * S + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            _texCone = tex;
            return _texCone;
        }

        /// <summary>地面亮条(**必须正好 1×1 世界单位**):横向 = 中间平台 + 两端极窄柔降;
        /// 纵向 = 中间最亮、向上下渐隐。危险边界就靠它读(光雾是散的,边界不能靠光雾)。
        ///
        /// ★★尺寸不能乱改:BuildBeam/SyncBeamVisual 里是直接 `localScale = (barW, 1.30)` 当世界尺寸用的。
        ///   2026-09-14 修掉的 bug:原来是 256×128 贴图 + ppu=Hh(128) ⇒ 精灵实际 **2×1 单位**,
        ///   乘上 barW(7.95) 后实机渲染成 **15.9 单位宽(1590px)**,而危险带只有 7.5 单位(750px)。
        ///   再叠上当时 BeamBarEdge=0.14 的宽柔降(平台占半宽的 72%)⇒ 平台(最亮那段)宽 11.45 单位,
        ///   **比危险带每侧多出 1.97 单位(197px)的满亮地面** —— 玩家站在"亮得很明显"的地面上
        ///   却是安全的,而那截地板其实没被光锥照到。这就是"非光照区域也有泛光效果"。
        ///   现在改成方形 S×S + ppu=S ⇒ 严格 1×1 单位,纵向尺寸不变(仍是 1 单位 × localScale 1.30)。
        /// ★平台(alpha=1)的边界严格落在 ±BeamGroundHalfWidth = 危险判定线上,平台以外只剩
        ///   BarBleed 那 6% 的余量做柔降(≈22px),所以"看得到的亮" = "会死的地方",不再骗人。</summary>
        static Sprite GroundBarSprite()
        {
            if (_sGroundBar != null) return _sGroundBar;
            const int S = 256;
            // 平台占半宽的比例:1/1.06 = 0.943 ⇒ 平台边缘正好压在危险边界上
            float plateau = Mathf.Clamp01(1f / BarBleed);
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
            {
                float fy = Mathf.Abs(y - (S - 1) * 0.5f) / ((S - 1) * 0.5f);
                float vert = Mathf.Pow(Mathf.Clamp01(1f - fy), 1.4f);
                for (int x = 0; x < S; x++)
                {
                    float fx = (float)x / (S - 1) * 2f - 1f;             // -1..1
                    float e = Mathf.Min(1f - fx, 1f + fx);               // 0=两端 1=中间
                    float lat = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / plateau));
                    px[y * S + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(lat * vert) * 255f));
                }
            }
            tex.SetPixels32(px);
            _sGroundBar = MakeSprite(tex, new Vector2(0.5f, 0.5f), S);   // ppu=S ⇒ 1×1 单位
            return _sGroundBar;
        }

        /// <summary>柔光球(2×2 单位):铜眼光晕。</summary>
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
            _sGlow = MakeSprite(tex, new Vector2(0.5f, 0.5f), S / 2);
            return _sGlow;
        }

        /// <summary>柔边方块(1×1 单位):地面光斑 / 调试安全区。</summary>
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
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - Mathf.Max(fx, fy)));
                    a *= a;
                    px[y * S + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px);
            _sSoftBox = MakeSprite(tex, new Vector2(0.5f, 0.5f), S);
            return _sSoftBox;
        }

        static Shader AddShader()
        {
            var sh = Shader.Find("Legacy Shaders/Particles/Additive");
            if (sh == null) sh = Shader.Find("Mobile/Particles/Additive");
            if (sh == null) sh = Shader.Find("Particles/Additive");
            if (sh == null)
            {
                sh = Shader.Find("Sprites/Default");
                Debug.LogWarning("[PZ04] 找不到加色混合 shader,退回 Sprites/Default(光束会偏灰)。");
            }
            if (sh == null) sh = Shader.Find("Unlit/Transparent");
            if (sh == null) sh = Shader.Find("Standard");
            return sh;
        }

        /// <summary>精灵共用的加色材质(铜眼光晕 / 地面光斑 / 调试色块)。</summary>
        static Material SharedAddMat()
        {
            if (_matShared != null) return _matShared;
            var sh = AddShader();
            if (sh == null)
            {
                Debug.LogError("[PZ04] 一个可用 shader 都找不到,光效将不显示。");
                return null;
            }
            _matShared = new Material(sh);
            return _matShared;
        }
    }
}
