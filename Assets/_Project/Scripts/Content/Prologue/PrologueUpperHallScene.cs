// ============================================================================
//  PrologueUpperHallScene.cs —— 第二幕【二楼密室】(原命名错误为二楼回廊,D6)
//
//  剧本(docs/序幕落地清单.md §2.4):
//    青年从楼梯废墟爬上来 → 铁笼(撬开拿透镜) + 齿轮箱(拽底座) + 齿轮机关(拨动切中年)
//
//  D6 简化:
//    · 未持撬棍时,场景左侧放一个"铁撬棍"占位物,先让青年拿到工具
//    · 铁笼 + 齿轮箱 摆在中偏右
//    · 齿轮机关 摆在最右,触发就切场景到 Chamber2(棺材小游戏 D7 补)
//    · 楼梯口 Portal 回门厅
// ============================================================================

using UnityEngine;

namespace LostGoddess.Content
{
    public static class PrologueUpperHallScene
    {
        public const float SpawnX = -12f;   // 从楼梯口出生

        public static void Build()
        {
            // 场景:美术已给「前厅二楼坍塌的回廊」专用图,SceneRoomBuilder 会自动加载
            //   Resources/Scenes/UpperHall/{bg_full, bg_near}。
            //   2026-07-19 修:改用 bg_full(3400×1200)当远景铺满,不再用 bg_far(2544×912)避免左右露黑边。
            var room = SceneRoomBuilder.Build(SceneRoomBuilder.PrologueUpperHall);
            room.name = "Room_" + Rooms.Prologue_UpperChamber;  // 命名修正:实际上这是二楼密室
            float groundY = SceneRoomBuilder.PrologueUpperHall.groundY;

            // 2026-07-24 出生位置根据进入方向:
            //   · 从下方(爬木梯上来,EnterDirection="up") → 出生在左边楼梯口 SpawnX = -12
            //   · 从右侧(二楼回廊回来,EnterDirection="right") → 出生在右边 x=+12
            //   · 其他情况(默认/首次) → 保留原有 SpawnX = -12
            string enterDir = SceneLoader.EnterDirection;
            float spawnX = SpawnX;
            if (enterDir == "right") spawnX = 12f;

            // 玩家:从楼梯爬上来的必然是青年 —— 但保守起见,不强制切 Era,
            //       只按当前 Era 建 Player。剧情里必然是 Young。
            PlayerBuilder.Build(GameState.CurrentEra, groundY, spawnX);

            // ── 环境交互物 (2026-07-24 用户红色涂鸦精准像素定位) ─────────────
            //  用户在 bg_full(3400×1200, PPU=100, pivot=Center) 上用红色涂鸦框出 9 个物品的
            //  可交互范围,像素→世界换算公式: wx=(px-1700)/100, wy=(600-py)/100。
            //  每块区域用半透明色差方块显示,点击弹对应文案。严格按用户框定的宽高布置。
            BuildLoreProp(room.transform, "大陶罐(左)",         new Vector2(-12.81f, -2.39f), new Vector2(1.72f, 2.26f),
                new Color(1f, 0.5f, 0.2f, 0.35f),
                "一口造型古朴的大型陶罐半掩在碎石中，罐口散发着淡淡的霉味。");
            BuildLoreProp(room.transform, "青花古瓷瓶",         new Vector2( -8.21f, -0.44f), new Vector2(0.71f, 1.38f),
                new Color(0.2f, 0.6f, 1f, 0.35f),
                "瓶身上绘着精致而诡异的花纹，在幽暗的密室中泛着微弱的幽光。拿起来轻轻晃动，似乎能听到内部有细沙流动的声音。");
            BuildLoreProp(room.transform, "三个长枪",           new Vector2( -5.62f,  0.82f), new Vector2(1.20f, 3.19f),
                new Color(1f, 1f, 0.2f, 0.35f),
                "传说永恒之枪投出就可以必中对手。");
            BuildLoreProp(room.transform, "燃尽的火把",         new Vector2(  0.51f,  0.43f), new Vector2(0.43f, 2.14f),
                new Color(1f, 0.3f, 0.1f, 0.35f),
                "残破的巨石耸立在密室中央，上面刻着犹如心脏脉络般的神秘纹路。插在上面的火把早已熄灭，残存的焦炭表明曾经有人造访过。");
            BuildLoreProp(room.transform, "石碑前小蜡烛",       new Vector2(  1.81f, -3.45f), new Vector2(1.43f, 1.54f),
                new Color(1f, 1f, 0.7f, 0.35f),
                "几根参差不齐的蜡烛静静立在石碑前，蜡油早已凝固成冰冷的白泪。这似乎是某种祈祷或献祭仪式后留下的残迹。");
            BuildLoreProp(room.transform, "墙上神秘符号",       new Vector2(  3.88f,  0.36f), new Vector2(1.43f, 1.95f),
                new Color(0.7f, 0.2f, 1f, 0.35f),
                "石墙上刻满了规则而古怪的线条与交叉记号");
            BuildLoreProp(room.transform, "彩绘陶瓶",           new Vector2(  5.41f, -2.02f), new Vector2(1.38f, 2.33f),
                new Color(0.2f, 1f, 0.4f, 0.35f),
                "罐身涂抹着鲜艳的红绿彩绘，图案宛如盘旋的古蛇。这些色彩依然诡异地保持着鲜亮。");
            // 2026-07-24 倚靠石板独立图层(严格复刻神庙前厅左右壁画的覆盖形式,尺寸与场景完全一致)。
            //   先铺图层(带 Q 键脉冲),再放透明点击块 —— 二者解耦:图层负责显示+发光,点击块负责交互。
            BuildStoneSlabLayer(room.transform);
            // 2026-07-24 楔形文字石板:多阶段交互(纯透明点击块,发光脉冲已移到独立石板图层)
            BuildStoneSlabs(room.transform, new Vector2(  7.81f, -1.86f), new Vector2(3.59f, 2.60f));
            BuildLoreProp(room.transform, "大陶罐(右)",         new Vector2( 11.37f, -2.69f), new Vector2(1.44f, 2.09f),
                new Color(1f, 0.5f, 0.2f, 0.35f),
                "一口造型古朴的大型陶罐半掩在碎石中，罐口散发着淡淡的霉味。");
            BuildGearRags(room.transform, new Vector2( 13.82f, -1.60f), new Vector2(2.27f, 2.01f));

            // ── 楼梯口 Portal(朝左 → 返回梯子密室) ──
            //  从 LadderChamber 的木梯爬上来,原路返回。
            BuildStairsPortal(room.transform, new Vector2(-15f, groundY + 1.5f), groundY);

            // ── 2026-07-24 右边缘隐形 Portal(朝右 → 二楼回廊) ──
            //   走入触发,与旗帜完全解耦:随时能走过去切场景。
            //   旗帜只是石板破解后的"视觉指引",不承担切场景职责。
            BuildRightPortalToUpperCorridor(room.transform, new Vector2(16.2f, groundY + 1.5f), groundY);

            // 青年首次进入二楼密室:头顶感叹号 → 3 句对话(一次性)
            if (GameState.CurrentEra == Era.Young &&
                !GameState.GetFlag("upper_chamber_first_arrival_shown"))
            {
                room.AddComponent<PrologueUpperChamberArrivalDirector>();
            }

            room.AddComponent<PrologueUpperHallDirector>();
        }

        // ── helpers ─────────────────────────────────────────────────

        static void BuildCrowbarPickup(Transform parent, Vector2 pos, float groundY)
        {
            var go = MakeBlock(parent, "Interact_CrowbarPickup", pos, new Vector2(1.0f, 0.3f),
                new Color(0.55f, 0.35f, 0.2f, 0.95f));
            var it = go.AddComponent<Interact_CrowbarPickup>();
            it.highlightTarget = go.GetComponent<SpriteRenderer>();
            it.interactPoint = MakePoint(go.transform, new Vector2(pos.x, groundY));
        }

        static void BuildIronCage(Transform parent, Vector2 pos, float groundY)
        {
            var go = MakeBlock(parent, "Interact_IronCage", pos, new Vector2(1.4f, 2.0f),
                new Color(0.28f, 0.28f, 0.32f, 0.9f));
            var it = go.AddComponent<Interact_IronCage>();
            it.highlightTarget = go.GetComponent<SpriteRenderer>();
            it.interactPoint = MakePoint(go.transform, new Vector2(pos.x - 1.2f, groundY));
        }

        static void BuildGearBox(Transform parent, Vector2 pos, float groundY)
        {
            var go = MakeBlock(parent, "Interact_GearBox", pos, new Vector2(1.6f, 1.6f),
                new Color(0.4f, 0.32f, 0.18f, 0.9f));
            var it = go.AddComponent<Interact_GearBox>();
            it.highlightTarget = go.GetComponent<SpriteRenderer>();
            it.interactPoint = MakePoint(go.transform, new Vector2(pos.x - 1.4f, groundY));
        }

        static void BuildGearsMechanism(Transform parent, Vector2 pos, float groundY)
        {
            var go = MakeBlock(parent, "Interact_Gears", pos, new Vector2(1.2f, 2.8f),
                new Color(0.5f, 0.42f, 0.25f, 0.9f));
            var it = go.AddComponent<Interact_Gears>();
            it.highlightTarget = go.GetComponent<SpriteRenderer>();
            it.interactPoint = MakePoint(go.transform, new Vector2(pos.x - 1.2f, groundY));
        }

        static void BuildStairsPortal(Transform parent, Vector2 pos, float groundY)
        {
            // UpperHall 朝左走 = 沿木梯返回 LadderChamber。
            var go = MakeBlock(parent, "Portal_BackToLadderChamber", pos, new Vector2(1.2f, 3.0f),
                new Color(0.15f, 0.15f, 0.25f, 0.5f));
            var portal = go.AddComponent<ScenePortal>();
            portal.targetRoom = Rooms.Prologue_LadderChamber;
            portal.highlightTarget = go.GetComponent<SpriteRenderer>();
            portal.interactPoint = MakePoint(go.transform, new Vector2(pos.x + 1.5f, groundY));
        }

        /// <summary>
        /// 2026-07-24 右边缘走入触发 Portal → 二楼回廊(UpperCorridor 真实美术)。
        ///   与旗帜完全解耦:玩家随时能走过去切场景,旗帜只是石板破解后的视觉指引。
        ///   参考 PrologueLinkRoomBuilder.BuildEdgePortal 的隐形 Portal 做法。
        /// </summary>
        static void BuildRightPortalToUpperCorridor(Transform parent, Vector2 pos, float groundY)
        {
            // 2026-07-25 收窄判定区:原版 pos=15 宽 3.0 → 左边界 x=13.5,
            //   与最右侧"破旧布匹"(x≈13.82, 宽 2.27 → 右边界≈14.96)重叠,
            //   导致点破旧布匹时先命中 Portal 直接切场景。改 pos=16 宽 1.2,
            //   覆盖 x∈[15.4,16.6],让破旧布匹(14.96 为止)完全在 Portal 左侧。
            const float w = 1.2f, h = 5.5f;
            float centerY = groundY + h * 0.5f;

            var go = new GameObject("Portal_Right_ToUpperCorridor");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, centerY, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            sr.color = new Color(1f, 1f, 1f, 0f);  // 完全透明
            sr.sortingOrder = 70;
            go.transform.localScale = new Vector3(w, h, 1f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var portal = go.AddComponent<ScenePortal>();
            portal.targetRoom = Rooms.Prologue_UpperHall;   // 二楼回廊(名字保留,语义=回廊)
            portal.successDialogueId = "";
            portal.highlightTarget = sr;
            portal.fadeTime = 0.6f;
            portal.triggerOnEnter = true;
            portal.enterDirection = "left";   // 从二楼回廊的左侧进入(老人在回廊左端出生,朝右)

            var ip = new GameObject("interactPoint").transform;
            ip.SetParent(go.transform, false);
            ip.position = new Vector3(pos.x - w * 0.6f, groundY, 0f);  // 站在 Portal 内侧稍远一点
            portal.interactPoint = ip;

            // 2026-07-24 走入触发后彻底销毁旗帜(玩家已看过一次指引,无需再显示)
            go.AddComponent<UpperChamberFlagCleanupOnEnter>();
        }

        /// <summary>
        /// 2026-07-24 破旧布匹杂物箱:两阶段交互(第一次弹环境文案 / 第二次揭开布匹展示齿轮特写 → 拿走)。
        ///   参考 Chamber3 陶罐→钥匙的分阶段模式。
        /// </summary>
        static void BuildGearRags(Transform parent, Vector2 center, Vector2 size)
        {
            var go = new GameObject("Interact_破旧布匹杂物箱");
            go.transform.SetParent(parent, false);
            go.transform.position = center;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            sr.color = new Color(0.6f, 0.6f, 0.6f, 0f);   // 与其他 LoreProp 一致的半透明色差
            sr.sortingOrder = 30;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var it = go.AddComponent<Interact_GearRags>();
            it.highlightTarget = sr;

            float groundY = SceneRoomBuilder.PrologueUpperHall.groundY;
            var ip = new GameObject("interactPoint").transform;
            ip.SetParent(go.transform, worldPositionStays: true);
            ip.position = new Vector3(center.x, groundY, 0f);
            it.interactPoint = ip;
        }

        /// <summary>
        /// 2026-07-24 楔形文字石板:两阶段点击 + 老年 Q 键岁月洞察脉冲。
        ///   · 第 1 次点击 → 弹环境文案(石板碎裂)
        ///   · 第 2 次点击 → 弹"请使用工具破开石板"→ 玩家头顶感叹号(4 句青年独白) + 右侧旗帜出现
        ///   · 老年形态按 Q → 石板亮暗脉冲(复用 MuralPulseEffect)
        /// </summary>
        static void BuildStoneSlabs(Transform parent, Vector2 center, Vector2 size)
        {
            var go = new GameObject("Interact_楔形石板");
            go.transform.SetParent(parent, false);
            go.transform.position = center;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            // 2026-07-24 用户要求:删掉点击块自身的发光脉冲。脉冲改由独立石板图层(BuildStoneSlabLayer)
            //   承担 —— 严格复刻神庙前厅左右壁画的图层覆盖形式。这里只保留点击碰撞功能,
            //   sprite 设为全透明(alpha=0),不再做任何色差/发光。
            sr.color = new Color(1f, 1f, 1f, 0f);
            sr.sortingOrder = 30;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var it = go.AddComponent<Interact_StoneSlabs>();
            it.highlightTarget = sr;
            // 脉冲不再挂在点击块上:it.pulseEffect 留空,Q 键脉冲由石板图层的 MuralInsightController 处理。

            float groundY = SceneRoomBuilder.PrologueUpperHall.groundY;
            var ip = new GameObject("interactPoint").transform;
            ip.SetParent(go.transform, worldPositionStays: true);
            ip.position = new Vector3(center.x, groundY, 0f);
            it.interactPoint = ip;
        }

        /// <summary>
        /// 2026-07-24 倚靠石板独立图层 —— 严格复刻神庙前厅左右壁画(BuildMuralHighlight)的图层覆盖形式。
        ///   · 美术单独给倚靠石板做了 3400×1200 的 RGBA 图层(Scenes/UpperHall/slab_leaning),
        ///     尺寸和场景背景完全一致 → 用与 SceneRoomBuilder.BuildLayer 相同的位置计算,原样覆盖。
        ///   · MuralPulseEffect + MuralInsightController 直接挂在图层上,按 Q 触发亮暗脉冲发光,
        ///     不需要任何单独定位(和前厅壁画一模一样的逻辑)。
        /// </summary>
        static GameObject BuildStoneSlabLayer(Transform parent)
        {
            var slabSprite = Resources.Load<Sprite>("Scenes/UpperHall/slab_leaning");
            if (slabSprite == null)
            {
                Debug.LogWarning("[PrologueUpperHallScene] 未找到倚靠石板图层资源 'Scenes/UpperHall/slab_leaning'");
                return null;
            }

            var artGo = new GameObject("Layer_StoneSlab");
            artGo.transform.SetParent(parent, false);

            var sr = artGo.AddComponent<SpriteRenderer>();
            sr.sprite = slabSprite;
            sr.color = Color.white;
            // 石板图层盖在背景之上、人物之下(和前厅壁画一样用负 sortingOrder)。
            //   bg_full 远景 = -30,bg_near 前景 = 60。石板是靠墙的背景装饰,放 -15(远景之上、人物之下)。
            sr.sortingOrder = -15;

            // 【关键】与 SceneRoomBuilder.BuildLayer / 前厅壁画完全相同的位置计算:
            //   imageCenterY = 0(背景图中心在世界原点);transform.y = centerY - pivotOffsetY。
            float imageCenterY = 0f;
            float pivotOffsetY = slabSprite.bounds.center.y;
            artGo.transform.position = new Vector3(0f, imageCenterY - pivotOffsetY, 0f);
            artGo.transform.localScale = new Vector3(1f, 1f, 1f);

            // 脉冲效果 + Q 键监听,直接挂在图层本身(和前厅壁画一模一样,零定位)。
            var pulse = artGo.AddComponent<MuralPulseEffect>();
            pulse.targetRenderer = sr;

            var controller = artGo.AddComponent<MuralInsightController>();
            controller.pulseEffect = pulse;

            Debug.Log($"[PrologueUpperHallScene] ✓ 倚靠石板图层已加载: {slabSprite.name}, pivotOffset={pivotOffsetY:F2}, position=({artGo.transform.position.x:F2},{artGo.transform.position.y:F2})");

            return artGo;
        }

        static GameObject MakeBlock(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            // 2026-07-19 打包Demo：占位方块设为透明
            sr.color = new Color(color.r, color.g, color.b, 0f);
            sr.sortingOrder = 15;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            return go;
        }

        /// <summary>
        /// 2026-07-24 环境交互物(点击弹文案):严格按用户红色涂鸦的像素框构建色差方块。
        ///   · 方块尺寸=红色涂鸦的宽×高(世界单位),位置=涂鸦中心(世界坐标)。
        ///   · 半透明彩色底色,便于策划期看效果;打包时把 alpha 改 0 即可隐形。
        ///   · 点击 → InteractableBase 走近 → 弹一段文案。走近点=方块底部对齐地面。
        /// </summary>
        static void BuildLoreProp(Transform parent, string name, Vector2 center, Vector2 size, Color color, string loreText)
        {
            var go = new GameObject("Interact_" + name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            sr.color = new Color(color.r, color.g, color.b, 0f);  // 正式版隐藏调试色差方块
            sr.sortingOrder = 30;                    // 盖在背景/前景之上,便于点击命中
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var it = go.AddComponent<Interact_LoreProp>();
            it.loreText = loreText;
            it.highlightTarget = sr;

            // 老人走近点:方块底部中央,y 落在地面上
            float groundY = SceneRoomBuilder.PrologueUpperHall.groundY;
            var ip = new GameObject("interactPoint").transform;
            ip.SetParent(go.transform, worldPositionStays: true);
            ip.position = new Vector3(center.x, groundY, 0f);
            it.interactPoint = ip;
        }

        static Transform MakePoint(Transform parent, Vector2 worldPos)
        {
            var p = new GameObject("interactPoint").transform;
            p.SetParent(parent, worldPositionStays: true);
            p.position = worldPos;
            return p;
        }

        static Sprite _solid;
        static Sprite SolidSprite()
        {
            if (_solid != null) return _solid;
            var tex = new Texture2D(2, 2);
            var px = new Color[] { Color.white, Color.white, Color.white, Color.white };
            tex.SetPixels(px); tex.Apply();
            _solid = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            return _solid;
        }
    }

    /// <summary>
    /// 2026-07-24 二楼密室环境交互物:点击弹一段文案,无其他效果。
    ///   走近点由 BuildLoreProp 挂 interactPoint,复用 InteractableBase 走近流程。
    /// </summary>
    public class Interact_LoreProp : InteractableBase
    {
        public string loreText;

        public override void OnClick()
        {
            if (!string.IsNullOrEmpty(loreText))
                DialogueSystem.ShowText(loreText);
        }
    }

    /// <summary>
    /// 2026-07-24 破旧布匹杂物箱:两阶段交互。
    ///   · 第一次点击 → 弹环境文案(布匹遮住东西)
    ///   · 第二次点击 → 揭开布匹 → gear_big 特写 + 文案"黄铜齿轮..." → 关闭特写 → 收入背包(Items.Gear2)
    ///   · 已拾取后 → 点击提示已经拿走
    ///   参考 Interact_Pottery 的分阶段模式,复用 CloseupView。
    /// </summary>
    public class Interact_GearRags : InteractableBase
    {
        const string kIntroText  = "一块沾满尘土的灰白色破布随意搭在废木箱上，遮挡住了下面的东西。";
        const string kCloseupText = "黄铜齿轮，可组合使用或嵌入特定机械装置中。";

        int _clickCount = 0;      // 0=未点 / 1=已弹环境文案 / 2=已拿走
        bool _busy = false;       // 特写中禁止重复点

        public override void OnClick()
        {
            if (_busy) return;

            if (_clickCount == 0)
            {
                // 第一次:环境文案
                _clickCount = 1;
                DialogueSystem.ShowText(kIntroText);
                return;
            }

            if (_clickCount == 1)
            {
                // 第二次:揭开布匹,弹特写
                _busy = true;
                ShowGearCloseup();
                return;
            }

            // _clickCount >= 2:已拿走
            DialogueSystem.ShowText("破布下面已经空了。");
        }

        void ShowGearCloseup()
        {
            // gear_big.png 导入类型是 Texture,和 key_reveal 一样用 Texture2D 加载
            var texture = Resources.Load<Texture2D>("UI/Icons/gear_big");
            if (texture == null)
            {
                Debug.LogWarning("[Interact_GearRags] 未找到特写资源 'UI/Icons/gear_big',直接给道具兜底");
                GiveGear();
                return;
            }

            var sprite = Sprite.Create(texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));

            var go = new GameObject("_GearBigCloseupTemplate");
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(800f, 600f);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);

            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;   // 图片本身不拦点击,背景按钮能关闭

            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);

            CloseupView.Open(go);
            CloseupView.SetCanClose(false);
            StartCoroutine(PlayGearCloseupSequence(go, pc));
        }

        System.Collections.IEnumerator PlayGearCloseupSequence(GameObject closeupGo, PlayerController pc)
        {
            bool done = false;
            DialogueSystem.ShowText(kCloseupText, () => done = true);
            while (!done) yield return null;

            // 2026-07-24 【入包修复】文案读完立即入包(和其它道具统一时序)
            GiveGear();
            _clickCount = 2;
            // 视觉表示已拿走:方块淡到几乎不可见
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) { var c = sr.color; c.a = 0f; sr.color = c; }

            CloseupView.SetCanClose(true);
            yield return new WaitWhile(() => CloseupView.IsOpen);

            Object.Destroy(closeupGo);
            if (pc != null) pc.SetControllable(true);
            _busy = false;
        }

        void GiveGear()
        {
            if (!InventorySystem.Has(Items.Gear2))
                InventorySystem.Add(Items.Gear2);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Interact_StoneSlabs —— 楔形文字石板(2026-07-24)
    //   · 第 1 次点击 → 环境文案(什么形态都能触发)
    //   · 第 2 次点击 → "请使用工具破开石板" + 感叹号(青年 4 句独白) + 右侧旗帜出现
    //   · 老年 Q 键 → 石板脉冲(复用 MuralPulseEffect,和神庙前厅壁画一模一样)
    // ─────────────────────────────────────────────────────────────────────
    public class Interact_StoneSlabs : InteractableBase
    {
        const string kIntroText  = "几块刻满楔形文字与图案的石板随意靠在墙边，边缘已经碎裂。";
        const string kToolHint   = "请使用工具破开石板";
        const string kFlagClicked1 = "prologue_stone_slabs_clicked_once";     // 已弹过环境文案
        const string kFlagAfter2   = "prologue_stone_slabs_clicked_twice";    // 已经点满 2 次 → 旗帜/感叹号已出现

        public MuralPulseEffect pulseEffect;   // 2026-07-24 已废弃:脉冲移到独立石板图层(Layer_StoneSlab),此字段保留仅为兼容旧引用
        int _clickCount = 0;

        void Start()
        {
            // 场景重建后(切场景返回时)恢复点击次数和旗帜状态
            if (GameState.GetFlag(kFlagAfter2))
            {
                _clickCount = 2;
                SpawnEdgeFlagIfMissing();
            }
            else if (GameState.GetFlag(kFlagClicked1))
            {
                _clickCount = 1;
            }

            // 2026-07-24 用户要求:点击块不再自己起脉冲。Q 键脉冲由石板图层(Layer_StoneSlab)上的
            //   MuralInsightController 处理 —— 和神庙前厅左右壁画完全一致的逻辑,这里不再挂 StoneSlabsQGate。
        }

        public override void OnClick()
        {
            // 2026-07-25 撬棍破石板流程(优先级最高):
            //   青年在背包双击撬棍并"使用"后(置位 item_used_crowbar)→ 点击石板 → 确认框"是否撬开石板"
            //   → 是 → 底座特写 + 文案 → 收入背包(Items.BrassBase)。
            if (GameState.GetFlag("prologue_slab_pried"))
            {
                // 已经撬开过
                DialogueSystem.ShowText("石板已经被撬开了，底座已经取走。");
                return;
            }
            if (GameState.GetFlag("item_used_crowbar") && !InventorySystem.Has(Items.BrassBase))
            {
                if (ConfirmDialog.IsOpen || CloseupView.IsOpen) return;
                ConfirmDialog.Show("是否撬开石板？",
                    onYes: PryOpenSlab,
                    onNo: null);
                return;
            }

            if (_clickCount == 0)
            {
                _clickCount = 1;
                GameState.SetFlag(kFlagClicked1, true);
                DialogueSystem.ShowText(kIntroText);
                return;
            }

            if (_clickCount == 1)
            {
                _clickCount = 2;
                GameState.SetFlag(kFlagAfter2, true);

                // 2026-07-24 卡死修复:第 2 次点击后立即禁用 collider,防止连锁点击打断对话。
                //   之前用协程 while(!done) 等 DialogueSystem.ShowText 回调 → 玩家推进对话时
                //   ClickInputManager 同一帧还在石板 collider 上重复触发 OnClick →
                //   DialogueUI.Play 打断旧协程但不调回调 → while(!done) 死等 → 卡死。
                //   现在:同步禁用 collider + 直接弹 ShowText,再挂感叹号(玩家看完文案点击感叹号触发独白)。
                //   感叹号自己会 SetControllable(false) 处理独白流程,不需要 Interact_StoneSlabs 再管。
                var col = GetComponent<Collider2D>();
                if (col != null) col.enabled = false;

                // 环境提示文案(不用等它完成 —— 感叹号也已经挂上,玩家推进 ShowText 后点感叹号触发独白)
                DialogueSystem.ShowText(kToolHint);

                // 感叹号(青年独白 4 句,带表情特写)—— 玩家点击感叹号触发 PlaySequence
                var pc = PlayerController.Instance;
                var player = pc != null ? pc.gameObject : null;
                if (player != null)
                {
                    var talk = PlayerTalkPrompt.Attach(player);
                    talk.SetDialogue(
                        new PlayerTalkPrompt.Line("怎么还得找工具？",           "frown"),  // 烦躁 → 皱眉
                        new PlayerTalkPrompt.Line("真想一拳就砸开这石板......", "frown"),  // 烦躁 → 皱眉
                        new PlayerTalkPrompt.Line("怎么样？有找到可以使用的工具吗？", "think"),  // 询问 → 思考
                        new PlayerTalkPrompt.Line("没有的话只能回一楼找了",     "shy")     // 无奈 → 害羞
                    );
                    talk.Show();
                }

                // 右侧场景边缘立即出现旗帜 → 走到边缘切换到二楼回廊
                SpawnEdgeFlagIfMissing();
                return;
            }

            // 后续再点:提示还是需要工具(暂无工具消耗逻辑,预留)
            DialogueSystem.ShowText(kToolHint);
        }

        /// <summary>2026-07-25 撬开石板 → 底座特写 + 文案 → 收入背包(Items.BrassBase)。</summary>
        void PryOpenSlab()
        {
            GameState.SetFlag("prologue_slab_pried", true);

            // 禁用石板点击,防止撬开动画/特写期间连点
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            PlaySfx(Sfx.mechanism_click);

            // 底座特写图(投影仪底座 3400×1200)
            var texture = Resources.Load<Texture2D>("Closeups/brass_base_closeup");
            if (texture == null)
            {
                Debug.LogWarning("[Interact_StoneSlabs] 未找到底座特写 'Closeups/brass_base_closeup',直接兜底给道具");
                GiveBrassBase();
                return;
            }

            var sprite = Sprite.Create(texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));

            var go = new GameObject("_BrassBaseCloseupTemplate");
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1100f, 500f);   // 宽幅底座图
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);

            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;

            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);

            CloseupView.Open(go);
            CloseupView.SetCanClose(false);
            StartCoroutine(PlayBaseCloseupSequence(go, pc));
        }

        System.Collections.IEnumerator PlayBaseCloseupSequence(GameObject closeupGo, PlayerController pc)
        {
            bool done = false;
            DialogueSystem.ShowText("黄铜质地的机械底座，表面有卡槽和接口，可与其他零件组合形成完整器械。", () => done = true);
            while (!done) yield return null;

            // 文案读完立即入包(与其它道具统一时序)
            GiveBrassBase();

            CloseupView.SetCanClose(true);
            yield return new WaitWhile(() => CloseupView.IsOpen);

            if (closeupGo != null) Object.Destroy(closeupGo);
            if (pc != null) pc.SetControllable(true);
        }

        void GiveBrassBase()
        {
            if (!InventorySystem.Has(Items.BrassBase))
                InventorySystem.Add(Items.BrassBase);
        }

        void SpawnEdgeFlagIfMissing()
        {
            // 场景根 = 石板的 parent(Room_Prologue_UpperChamber)
            if (transform.parent == null) return;            // 2026-07-24 旗帜"一次性视觉指引":玩家第一次成功从右侧走入二楼回廊后,
            //   BuildRightPortalToUpperCorridor 会 SetFlag("edge_flag_shown_to_upper_corridor"),
            //   之后再进入二楼密室就不再刷旗帜(彻底销毁)。
            if (GameState.GetFlag("edge_flag_shown_to_upper_corridor")) return;

            // 避免重复生成
            var existing = transform.parent.Find("EdgeGuideFlag_Triangle_ToUpperCorridor");
            if (existing != null) return;

            float groundY = SceneRoomBuilder.PrologueUpperHall.groundY;
            // 用户反馈:文件名和内容搞反了 → magnifying_glass.png 实际存的是旗帜(与黑森林一致)
            var sprite = Resources.Load<Sprite>("UI/Icons/magnifying_glass");
            if (sprite == null)
            {
                Debug.LogError("[Interact_StoneSlabs] 找不到旗帜资源 Resources/UI/Icons/magnifying_glass");
                return;
            }

            var go = new GameObject("EdgeGuideFlag_Triangle_ToUpperCorridor");
            go.transform.SetParent(transform.parent, false);
            // 位置:场景右侧靠近边缘 Portal(x=15)前一点,做视觉引导
            const float flagX = 13.5f;
            go.transform.position = new Vector3(flagX, groundY + 1.2f, 0f);
            go.layer = 2;              // Ignore Raycast:不阻挡下方点击

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 50;
            go.transform.localScale = Vector3.one * 1.5f;

            // Unity 自动生成的 PolygonCollider2D 会挡点击,去掉
            var autoCols = go.GetComponents<Collider2D>();
            foreach (var c in autoCols) UnityEngine.Object.Destroy(c);

            // 浮动动画(与黑森林完全一致)
            var floatAnim = go.AddComponent<FloatingIndicator>();
            floatAnim.amplitude = 0.18f;
            floatAnim.speed = 2.2f;

            // 2026-07-24 【重要修改】旗帜只做视觉指引,不再挂 EdgeFlagPortal 承担切场景。
            //   切场景由 BuildRightPortalToUpperCorridor 的隐形 ScenePortal(triggerOnEnter) 负责。
            //   这样即使旗帜未出现(未破解石板),走到右侧也能自然切场景。
        }
    }

    /// <summary>撬棍拾取物(D6 简版:青年点击就拿到,不需要预先条件)。</summary>
    public class Interact_CrowbarPickup : InteractableBase
    {
        bool _taken;

        public override void OnClick()
        {
            if (_taken) return;
            if (GameState.CurrentEra != Era.Young)
            {
                DialogueSystem.ShowText("地上有根铁棍……但这把老骨头搬不动。");
                return;
            }
            _taken = true;
            InventorySystem.Add(Items.Crowbar);
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) { var c = sr.color; c.a = 0f; sr.color = c; }
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
            DialogueSystem.Show(Dialogues.prologue_2_got_crowbar);
        }
    }

    /// <summary>UpperHall 首帧引路(仅首次进入)。</summary>
    public class PrologueUpperHallDirector : MonoBehaviour
    {
        void Start()
        {
            const string kFlag = "prologue_entered_upperhall";
            if (GameState.GetFlag(kFlag)) return;
            GameState.SetFlag(kFlag, true);

            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);
            var cs = gameObject.AddComponent<Cutscene>();
            cs.Add(new WaitStep(0.4f))
              .Add(new SayTextStep("(二楼密室——石砖回廊在前,道具位置等策划锚点。走到左边缘可沿木梯返回梯子密室。)"));
            cs.OnFinished += () => { if (pc != null) pc.SetControllable(true); };
            cs.Play();
        }
    }

    /// <summary>
    /// 2026-07-24 二楼密室右边缘 Portal 走入触发时,把"旗帜已展示过"的持久化 flag 置位,
    /// 并把当前场景里的旗帜物体立即销毁。目的:玩家二次回到二楼密室时不再刷旗帜。
    ///
    /// 为什么用 Update 轮询而不是 OnTriggerEnter/事件回调:
    ///   · ScenePortal.triggerOnEnter 用的是自己的 Update+矩形包含判定,不走 Physics2D 事件;
    ///   · 我们只需要在"进入 Portal 区域"这个语义时机做一次清扫 → 复用同一套矩形判定最简单。
    /// </summary>
    public class UpperChamberFlagCleanupOnEnter : MonoBehaviour
    {
        bool _done;
        void Update()
        {
            if (_done) return;
            var pc = PlayerController.Instance;
            if (pc == null) return;
            float px = pc.transform.position.x;
            float halfW = Mathf.Abs(transform.lossyScale.x) * 0.5f;
            if (px >= transform.position.x - halfW && px <= transform.position.x + halfW)
            {
                _done = true;
                GameState.SetFlag("edge_flag_shown_to_upper_corridor", true);
                // 销毁当前场景的旗帜(如果还在)
                if (transform.parent != null)
                {
                    var flag = transform.parent.Find("EdgeGuideFlag_Triangle_ToUpperCorridor");
                    if (flag != null) Destroy(flag.gameObject);
                }
            }
        }
    }
}
