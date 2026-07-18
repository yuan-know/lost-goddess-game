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
    }

    public static class SceneRoomBuilder
    {
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
        //  当前只有 2 层,SceneRoomBuilder 找不到 bg_mid/bg_near 会 warn(可忽略);
        //  把 prop_pottery 借用 "bg_near" 通道让它落在前景层(sorting 60,alpha=0.55)。
        //  D5 待办:等美术补 bg_mid/bg_near 或用 SceneRoomBuilder 改造成"任意层名"。
        public static readonly SceneDef TempleChamber1F = new SceneDef
        {
            roomName = "TempleChamber1F",
            bgPixelWidth = 3400f,
            bgPixelHeight = 1200f,
            bgPPU = 100f,
            groundFromBottom = 0.13f,
            groundY = -3.44f,
            parallaxFar = 0.00f,
            parallaxMid = 0.00f,
            parallaxNear = 0.00f,
        };

        // 【前厅二楼坍塌的回廊】—— UpperHall 铁笼齿轮箱场景专用美术
        //  美术:Scenes/UpperHall/{bg_far, bg_near, bg_full}
        //   · bg_far.png(2544×912)= 回廊背景(比舞台窄,居中显示)
        //   · bg_near.png(3400×1200)= 前景(铁笼齿轮箱等)
        //  bg_far 尺寸偏小,视差 factor 保持 0 让它跟随相机居中,避免露边。
        public static readonly SceneDef PrologueUpperHall = new SceneDef
        {
            roomName = "UpperHall",
            bgPixelWidth = 3400f,
            bgPixelHeight = 1200f,
            bgPPU = 100f,
            groundFromBottom = 0.13f,
            groundY = -3.44f,
            parallaxFar = 0.00f,
            parallaxMid = 0.00f,
            parallaxNear = 0.00f,
        };

        /// <summary>按定义构建场景:三层背景 + WalkableArea + 相机跟随。返回根节点。</summary>
        public static GameObject Build(SceneDef def)
        {
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
            BuildLayer(root.transform, def, "bg_far",  imageBottomY, def.parallaxFar,  sortingOrder: -30, alpha: 1.0f);
            BuildLayer(root.transform, def, "bg_mid",  imageBottomY, def.parallaxMid,  sortingOrder: -20, alpha: 1.0f);
            BuildLayer(root.transform, def, "bg_near", imageBottomY, def.parallaxNear, sortingOrder:  60, alpha: 0.55f);

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
            var sp = Resources.Load<Sprite>($"Scenes/{def.roomName}/{spriteName}");
            if (sp == null)
            {
                Debug.LogWarning($"[SceneRoomBuilder] 找不到背景图 Resources/Scenes/{def.roomName}/{spriteName}");
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
