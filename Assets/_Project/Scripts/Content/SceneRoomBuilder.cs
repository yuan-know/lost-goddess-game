// ============================================================================
//  RoomBuilder_Scene.cs —— 通用"美术给三层背景 + 老人接入"的房间构建器
//  美术把 bg_far / bg_mid / bg_near 三张全画布 PNG 丢在 Resources/Scenes/<房间名>/
//  本脚本读一份 SceneDef,按定义构建:
//    · 三层 SpriteRenderer(远/中/近),排序层从后到前
//    · 地平线锚点算好,让画上的地面 Y = 老人脚底 Y(与 SandboxBootstrap.GroundY 一致)
//    · WalkableArea 按背景宽度自动圈边界
//    · 相机加 CameraFollow,跟老人 X,clamp 到背景两端
//    · 老人由 SandboxBootstrap.BuildPlayer 之前已放好,这里不管
//
//  【只做场景,不放交互物】——交互物等策划给清单再摆。
// ============================================================================
using System.Collections;
using UnityEngine;

namespace LostGoddess.Content
{
    /// <summary>一个场景房间的定义:图片路径+地平线百分比+视差/世界大小/摄像机可动区间。</summary>
    public class SceneDef
    {
        public string roomName;         // 房间标识(等于 Resources/Scenes/{roomName}/ 目录)
        public float bgPixelWidth;      // 背景图像素宽(4250 / 3400 / …)
        public float bgPixelHeight;     // 背景图像素高(通常 1200)
        public float bgPPU;             // 与 BackgroundImporter 保持一致(100)
        public float groundFromBottom;  // 画上地平线离图底的比例(0..1),0.25 / 0.18…
        public float groundY;           // 老人脚底所在世界 Y(与 SandboxBootstrap.GroundY 一致)
        public float parallaxFar;       // 远层 factor
        public float parallaxMid;
        public float parallaxNear;
        public float leftPadding = 0.5f;  // 老人可走线两端各留一点边(不让走出背景)
        public float rightPadding = 0.5f;

        // 覆盖三层的文件名(为空则默认 bg_far/bg_mid/bg_near)。
        //   用途:美术给的是"整张全景 bg_unlit_full.png"而不是三层分层图时,
        //   把 bgFarSprite = "bg_unlit_full" 就能一张图当远景铺满,其他两层留空(缺图不 warn)。
        public string bgFarSprite;
        public string bgMidSprite;
        public string bgNearSprite;

        // 覆盖 Resources 目录名(为空则用 roomName)。
        //   用途:两个逻辑房间共享同一美术目录时(如 StatueRoom 用 Chamber2 图),
        //   把 resourceFolderOverride = "Chamber2" 即可,roomName 仍保持逻辑区分。
        public string resourceFolderOverride;
    }

    public static class SceneRoomBuilder
    {
        /// <summary>最近一次 Build 使用的 SceneDef，供运行时地平线校准工具读取。</summary>
        public static SceneDef LastBuiltDef { get; private set; }

        public static readonly SceneDef DarkForest = new SceneDef
        {
            roomName = "DarkForest",
            bgPixelWidth = 4250f,
            bgPixelHeight = 1200f,
            bgPPU = 100f,
            // 目标:背景图**底边**贴相机视口底(Y=-5),画上地平线在图底往上 13%
            //   imageBottomY = groundY - worldHeight * groundFromBottom
            //   要 imageBottomY = -5,   worldHeight = 12,   groundFromBottom = 0.13
            //   → groundY = -5 + 12*0.13 = -3.44
            // 老人脚底跟着落到 -3.44,与画上地面对齐
            groundFromBottom = 0.13f,
            groundY = -3.44f,
            // 【占位阶段:三层视差全部关掉】
            //   原因:美术还没按"这一层画什么、那一层画什么"严格分工,占位期把三层
            //   全画满会导致占位物(世界固定)和 mid/far 层视差滑动不同步,视觉错位。
            //   等真美术给出各层分工再放开(远层 0.20 / 中层 0.55 / 近层 0)。
            parallaxFar = 0.00f,
            parallaxMid = 0.00f,
            parallaxNear = 0.00f,
        };

        public static readonly SceneDef TempleEntry = new SceneDef
        {
            roomName = "TempleEntry",
            bgPixelWidth = 3400f,
            bgPixelHeight = 1200f,
            bgPPU = 100f,
            // 同上:图底贴视口底 Y=-5,老人脚底 -3.44
            groundFromBottom = 0.13f,
            groundY = -3.44f,
            // 占位阶段视差全关(同 DarkForest 注释)
            parallaxFar = 0.00f,
            parallaxMid = 0.00f,
            parallaxNear = 0.00f,
        };

        // 【神庙一楼密室】—— Chamber3 洗礼池场景专用美术
        //  美术:Scenes/TempleChamber1F/{bg_far, prop_pottery, bg_full}
        //   · bg_far.png(3400×1200)= 洗礼池全景背景
        //   · prop_pottery.png(3400×1200)= 陶罐层(全画布位置,与背景对齐)
        //  共 2 层美术:bg_far 作远景 / prop_pottery 作前景 near / mid 空跳过
        public static readonly SceneDef TempleChamber1F = new SceneDef
        {
            roomName = "TempleChamber1F",
            bgPixelWidth = 3400f,
            bgPixelHeight = 1200f,
            bgPPU = 100f,
            // 2026-07-19 修正:按公式统一 groundY;原 -3.44 与 0.28 不一致
            // 2026-07-19 二修:截图显示老人仍轻微浮空于石台,再下调 0.11
            groundFromBottom = 0.271f,      // 洗礼池石台顶约图底 27.1% 处(运行时按 P 键精校)
            groundY = -1.75f,               // = -5 + 12*0.271
            parallaxFar = 0.00f,
            parallaxMid = 0.00f,
            parallaxNear = 0.00f,
            bgFarSprite = "bg_far",
            bgMidSprite = "",                // 无中层
            bgNearSprite = "prop_pottery",   // 陶罐当近景层
        };

        // 【前厅二楼坍塌的回廊】—— UpperHall 铁笼齿轮箱场景专用美术
        //  美术:Scenes/UpperHall/{bg_far, bg_near, bg_full}
        //   · bg_far.png(2544×912)= 回廊背景(比舞台窄 —— 用 bg_full 3400×1200 作远景铺满,避免露黑边)
        //   · bg_near.png(3400×1200)= 前景(铁笼齿轮箱等)
        //  2026-07-19 修:原来用 bg_far,舞台是 3400 → 左右两边各 4.28 单位空白露黑底,
        //    改用 bg_full 作远景(3400×1200 铺满舞台),bg_near 依然作近景剪影层。
        public static readonly SceneDef PrologueUpperHall = new SceneDef
        {
            roomName = "UpperHall",
            bgPixelWidth = 3400f,
            bgPixelHeight = 1200f,
            bgPPU = 100f,
            // 2026-07-19 修正:按公式统一 groundY
            groundFromBottom = 0.12f,       // 实测 bg_full 地平线在图底 12% 处(运行时按 P 键校准)
            groundY = -3.56f,               // = -5 + 12*0.12
            parallaxFar = 0.00f,
            parallaxMid = 0.00f,
            parallaxNear = 0.00f,
            bgFarSprite = "bg_full",         // 用 3400×1200 全景当远景铺满,不再用 2544 的 bg_far
            bgMidSprite = "",                // 无中层
            bgNearSprite = "bg_near",
        };

        // 【神庙入口】—— 第 0 幕结尾 Prologue_Gate 外景(石拱门,纯过场)
        //  剧本序列:黑暗森林 → **神庙入口 TempleGate**(石拱门下)→ 点击石拱门 → 神庙门厅内景(TempleFoyer)
        //  ⚠ 这里**没有任何交互物**,老人走到石拱门位置自动切场景。
        //  美术:Scenes/TempleGate/{bg_far, bg_temple, bg_near}
        //   · bg_far.png(3400×1200)= 远景山影
        //   · bg_temple.png(3400×1200)= 神庙建筑本体(当中层)
        //   · bg_near.png(3400×1200)= 近景前景遮挡
        //   · bg_full.png = 三层合成全景(备用,不用)
        public static readonly SceneDef TempleGate = new SceneDef
        {
            roomName = "TempleGate",
            bgPixelWidth = 3400f,
            bgPixelHeight = 1200f,
            bgPPU = 100f,
            groundFromBottom = 0.13f,
            groundY = -3.44f,
            parallaxFar = 0.00f,
            parallaxMid = 0.00f,
            parallaxNear = 0.00f,
            bgFarSprite = "bg_far",
            bgMidSprite = "bg_temple",       // 神庙建筑本体作中层
            bgNearSprite = "bg_near",
        };

        // 【神庙门厅】—— 序幕第一幕 Prologue_Foyer(内景,黄铜石门+展台+楼梯+浮雕墙)
        //  剧本第一幕就是**这里**:老人被石门锁死,展台上有凹槽,用岁月洞察看壁画图腾,
        //   楼梯废墟老年爬不上去,进密室拾遗失物切青年 → 二楼获取透镜/齿轮箱 → 中年组装 →
        //   放到展台上转圆盘解锁 → 石门开 → 第四幕黑雾。
        //  美术:Scenes/TempleFoyer/
        //   · bg_unlit_full.png(3400×1200)= 关灯全景(阴森初进 = 默认)
        //   · bg_lit_full.png / bg_lit_bg.png = 开灯版(待策划定触发条件,可能是放投影仪之后)
        //   · prop_podium_lit.png / prop_podium_unlit.png = 展台单件(全画布定位)
        //  第一阶段:只用关灯全景当远景,展台/开灯逻辑等策划答复后再加。
        public static readonly SceneDef TempleFoyer = new SceneDef
        {
            roomName = "TempleFoyer",
            bgPixelWidth = 3400f,
            bgPixelHeight = 1200f,
            bgPPU = 100f,
            groundFromBottom = 0.13f,       // 保守值:图底刚好贴相机底(orthoSize=5 时 bottomY=-5.0)
                                            //   Foyer 图底 6% 位置是地砖反光突变行,不是真地平线 → 沿用 0.13
            groundY = -3.44f,
            parallaxFar = 0.00f,
            parallaxMid = 0.00f,
            parallaxNear = 0.00f,
            bgFarSprite = "bg_unlit_full",   // 关灯版全景
            bgMidSprite = "",
            bgNearSprite = "",               // 暂无前景,前景元素等策划确认(展台位置/亮灯逻辑)
        };

        // 【密室 2 · 棺材小游戏】—— Prologue_Chamber2
        //  美术:Scenes/Chamber2/{bg_far, bg_near, bg_full}
        //   · bg_far.png = 6656×2304 巨图,但仍按 3400×1200 舞台居中显示(超出部分被相机 clamp 挡住)
        //   · bg_near.png(3400×1200)= 前景
        //  策划稿:青年推入密室 2 → 棺材小游戏(D7 未实现)→ 解开切中年。
        //  这里只把图接进来,交互物(棺材 puzzle)等 D7 补。
        public static readonly SceneDef PrologueChamber2 = new SceneDef
        {
            roomName = "Chamber2",
            bgPixelWidth = 3400f,           // 舞台按 3400 算(bg_far 巨图溢出,靠相机 clamp 裁掉)
            bgPixelHeight = 1200f,
            bgPPU = 100f,
            // 2026-07-19 修正:与 StatueRoom 共用 Chamber2 美术,地面同在最下一排壁龛底(约图底 8%)
            groundFromBottom = 0.08f,       // 实测 Chamber2/bg_full 地平线在图底 8% 处
            groundY = -4.04f,               // = -5 + 12*0.08
            parallaxFar = 0.00f,
            parallaxMid = 0.00f,
            parallaxNear = 0.00f,
            bgFarSprite = "bg_full",         // 用 bg_full 3400×1200 铺满舞台,避免 bg_far 6656 露出诡异构图
            bgMidSprite = "",
            bgNearSprite = "",               // 暂不加 bg_near(前景剪影可能挡视觉)
        };

        // ── 2026-07-19 策划场景切换图新增 3 张真实美术接入 ──

        // 【梯子密室】—— Prologue_LadderChamber(前厅左 2,策划图"朝左走回到梯子密室")
        //  美术:Scenes/LadderChamber/{bg_far, bg_full, prop_ladder}
        //   · bg_far.png(3400×1200)= 石墙 + 中央祭台的房间
        //   · prop_ladder.png(3400×1200)= 木梯单件(像素 bbox x∈[758,1008] → 世界 x ≈ -8.17)
        //   · 无 bg_near/bg_mid,ladder 作近景层用于视觉遮挡
        //  2026-07-19 修正:原 0.26 使老人踩在祭台上方浮空,实际地面更靠近图底。
        public static readonly SceneDef LadderChamber = new SceneDef
        {
            roomName = "LadderChamber",
            bgPixelWidth = 3400f,
            bgPixelHeight = 1200f,
            bgPPU = 100f,
            groundFromBottom = 0.10f,       // 祭台底/地面约图底 10% 处(初估值,运行时按 P 键校准)
            groundY = -3.80f,               // = -5 + 12*0.10
            parallaxFar = 0.00f,
            parallaxMid = 0.00f,
            parallaxNear = 0.00f,
            bgFarSprite = "bg_far",
            bgMidSprite = "",
            bgNearSprite = "prop_ladder",   // 木梯当近景剪影层
        };

        // 【齿轮骨骸间】—— Prologue_GearRoom(前厅右 1,策划图"以神庙前厅为中心 朝右走所切换的密室"第 1 间)
        //  美术:Scenes/Chamber1/{bg_far, bg_near, bg_full}
        //   · bg_far.png(3400×1200)= 石墙 + 齿轮/管道/骨骸背景
        //   · bg_near.png(3400×1200)= 前景骨骸/管道剪影
        public static readonly SceneDef GearRoom = new SceneDef
        {
            roomName = "GearRoom",
            resourceFolderOverride = "Chamber1",
            bgPixelWidth = 3400f,
            bgPixelHeight = 1200f,
            bgPPU = 100f,
            // 2026-07-19 修正:按公式统一 groundY
            groundFromBottom = 0.16f,       // 实测 Chamber1 地平线在图底 16% 处(运行时按 P 键校准)
            groundY = -3.08f,               // = -5 + 12*0.16
            parallaxFar = 0.00f,
            parallaxMid = 0.00f,
            parallaxNear = 0.00f,
            bgFarSprite = "bg_full",         // 全景当远景铺满
            bgMidSprite = "",
            bgNearSprite = "",               // bg_near 前景暂不接(骨骸剪影可能挡老人)
        };

        // 【石雕室】—— Prologue_StatueRoom(前厅右 2,策划图 "方格墙 + 中央人形石雕像")
        //  美术直接复用 Chamber2 目录(bg_full 3400×1200,里面有大型无头长袍立像 + 8 个小雕像 + 壁龛墙)
        //  2026-07-19 二修:用户截图老人踩在"最下一排壁龛顶"而非"雕像基座底" → 真地平线在图底 8%,
        //   不是原来算错的 25%(25% 是壁龛顶装饰线被梯度法误判)。
        //   基座底在图底 8% → pct=0.08,同步把 groundY 从 -3.44 下调到 -4.04
        //   (公式:groundY = -5 + 12*pct 保证图底贴相机底 -5,不露黑相机背景)
        public static readonly SceneDef StatueRoom = new SceneDef
        {
            roomName = "StatueRoom",
            resourceFolderOverride = "Chamber2",
            bgPixelWidth = 3400f,
            bgPixelHeight = 1200f,
            bgPPU = 100f,
            groundFromBottom = 0.08f,       // 雕像基座底 ≈ 图底 8%
            groundY = -4.04f,               // 老人脚 = 基座底,占屏幕下方约 10%
            parallaxFar = 0.00f,
            parallaxMid = 0.00f,
            parallaxNear = 0.00f,
            bgFarSprite = "bg_full",
            bgMidSprite = "",
            bgNearSprite = "",
        };

        /// <summary>按定义构建场景:三层背景 + WalkableArea + 相机跟随。返回根节点。</summary>
        public static GameObject Build(SceneDef def)
        {
            LastBuiltDef = def;
            var root = new GameObject("Room_" + def.roomName);
            float worldWidth = def.bgPixelWidth / def.bgPPU;    // 42.5 / 34.0
            float worldHeight = def.bgPixelHeight / def.bgPPU;  // 12.0
            float halfW = worldWidth * 0.5f;

            // 图底 Y(BottomCenter 锚点下,SpriteRenderer 的 transform.y 就是图底 Y)
            // 我们要画上地平线落在 def.groundY:
            //   image.bottomY = def.groundY - worldHeight * def.groundFromBottom
            // 已在 SceneDef 里调好参数使 imageBottomY = -5(贴相机视口底),
            //   → 底部不再露出灰底,BottomExtender 已废除。
            float imageBottomY = def.groundY - worldHeight * def.groundFromBottom;

            // 三层背景(排序:远最靠后 / 中间层 / 近层是"半透明前景遮挡")
            //   bg_near sortingOrder=60 恢复"在老人之前"(前景遮挡感),
            //   但把 alpha 降到 0.55 → 老人被前景剪影"薄薄挡住"而不是完全吞掉,
            //   同时前景剪影的形体依然清晰(黑影层次感)。等美术出小型前景元素
            //   (柱子/草丛)再单独用 alpha=1 摆真前景遮挡。
            //   若 def 里指定了 bgXxxSprite,用指定文件名代替默认 bg_far/mid/near。
            string farName  = string.IsNullOrEmpty(def.bgFarSprite)  ? "bg_far"  : def.bgFarSprite;
            string midName  = string.IsNullOrEmpty(def.bgMidSprite)  ? "bg_mid"  : def.bgMidSprite;
            string nearName = string.IsNullOrEmpty(def.bgNearSprite) ? "bg_near" : def.bgNearSprite;
            BuildLayer(root.transform, def, farName,  imageBottomY, def.parallaxFar,  sortingOrder: -30, alpha: 1.0f);
            BuildLayer(root.transform, def, midName,  imageBottomY, def.parallaxMid,  sortingOrder: -20, alpha: 1.0f);
            BuildLayer(root.transform, def, nearName, imageBottomY, def.parallaxNear, sortingOrder:  60, alpha: 0.55f);

            // 可走区:X 边界按背景宽度(相机跟随时,人物走到边缘停下,不出背景)
            // 注意:老人不能走到贴边,预留 padding。
            var walkGo = new GameObject("WalkableArea");
            walkGo.transform.SetParent(root.transform, false);
            walkGo.transform.position = new Vector3(0f, def.groundY, 0f);
            var wa = walkGo.AddComponent<WalkableArea>();
            wa.useTransformY = true;
            wa.minX = -halfW + def.leftPadding;
            wa.maxX =  halfW - def.rightPadding;

            // 相机跟随:X 可动区间 = 相机相对背景两端不能出界
            // 相机可动 X ∈ [-halfW + orthoHalfW, halfW - orthoHalfW]
            var cam = Camera.main;
            if (cam != null)
            {
                var follow = cam.GetComponent<CameraFollow>();
                if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
                float orthoHalfW = cam.orthographicSize * cam.aspect;
                follow.minX = -halfW + orthoHalfW;
                follow.maxX =  halfW - orthoHalfW;
                follow.fixedY = 0f;
                follow.smoothTime = 0.15f;
                follow.target = null;   // 强制下一帧重新 Find "Player"(切场景后旧 target 已失效)
                // 相机瞬移到新场景的可动区中点(避免上一场景的 X 位置让 SmoothDamp 慢慢滑,导致 Player 短暂在视野外)
                cam.transform.position = new Vector3(0f, follow.fixedY, cam.transform.position.z);
            }

            return root;
        }

        static void BuildLayer(Transform parent, SceneDef def, string spriteName, float bottomY, float factor, int sortingOrder, float alpha = 1f)
        {
            // 空名字 = 显式跳过这一层(不 warn),用于只有 1~2 层美术的场景
            if (string.IsNullOrEmpty(spriteName)) return;
            // resourceFolderOverride 允许多个逻辑房间共享同一份美术目录(如 StatueRoom → Chamber2)
            string folder = string.IsNullOrEmpty(def.resourceFolderOverride) ? def.roomName : def.resourceFolderOverride;
            var sp = Resources.Load<Sprite>($"Scenes/{folder}/{spriteName}");
            if (sp == null)
            {
                Debug.LogWarning($"[SceneRoomBuilder] 找不到背景图 Resources/Scenes/{folder}/{spriteName}");
                return;
            }
            var go = new GameObject(spriteName);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, bottomY, 0f);   // 图底 Y
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.sortingOrder = sortingOrder;
            if (alpha < 0.999f) sr.color = new Color(1f, 1f, 1f, alpha);
            var px = go.AddComponent<ParallaxLayer>();
            px.factor = factor;
        }

        /// <summary>底部延展面板:填补背景图底到相机视口底之间的空隙。
        /// 挂在相机子物体上跟随水平位移,足够宽/高即可,颜色贴近背景近层地面色。</summary>
        static void BuildBottomExtender(Transform parent, SceneDef def)
        {
            var cam = Camera.main;
            if (cam == null) return;

            var go = new GameObject("_BottomExtender");
            go.transform.SetParent(parent, false);
            // 使用 SpriteRenderer 画一块纯色矩形(与 bg_near 同一 sortingOrder 层,但更靠下用 sortingOrder 数值区分)
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidWhiteSprite();
            // 冷灰蓝,与暗森林 / 神庙入口下沿石地都协调;略暗防止"过亮出戏"
            sr.color = new Color(0.11f, 0.13f, 0.15f);
            // near 层前景是 sortingOrder=60,我们要挡住 skybox 但不挡背景 → 设 -100(最远)
            sr.sortingOrder = -100;

            // 面板尺寸:宽=舞台 + 屏幕(保险),高=相机视口全高
            //  · 世界宽 def.bgPixelWidth/def.bgPPU + 相机屏幕宽(cam.orthographicSize * 2 * cam.aspect)
            //  · 高:相机视口全高(cam.orthographicSize * 2 = 10 单位)
            float sceneWidth = def.bgPixelWidth / def.bgPPU;
            float screenWidth = cam.orthographicSize * 2f * cam.aspect;
            float w = sceneWidth + screenWidth + 4f;  // 富余 4
            float h = cam.orthographicSize * 2f + 2f; // 富余 2

            // SolidWhiteSprite PPU=1(1 像素 = 1 单位),localScale 直接等于世界大小
            go.transform.localScale = new Vector3(w, h, 1f);
            // 让面板顶边正好卡在图底 Y(bg 底) → 中心 Y = bg底 - h/2
            //  这样面板正好托住背景,不会盖住任何背景画面
            float imageBottomY = def.groundY - (def.bgPixelHeight / def.bgPPU) * def.groundFromBottom;
            go.transform.position = new Vector3(0f, imageBottomY - h * 0.5f, 0f);
        }

        static Sprite _solid;
        static Sprite SolidWhiteSprite()
        {
            if (_solid != null) return _solid;
            var tex = new Texture2D(2, 2);
            var px = new Color[] { Color.white, Color.white, Color.white, Color.white };
            tex.SetPixels(px); tex.Apply();
            // PPU=2:2px 纹理 = 1 单位,localScale 直接等于世界尺寸(便于用世界单位算)
            _solid = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            return _solid;
        }
    }
}
