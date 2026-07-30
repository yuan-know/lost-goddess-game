// ============================================================================
//  PrologueLinkRooms.cs —— 神庙前厅左右链上 4 个过场房间的落地入口
//
//  策划场景切换图(2026-07-19):
//    Foyer ↔ LadderChamber ↔ Chamber3     (左链)
//    Foyer ↔ GearRoom ↔ StatueRoom        (右链)
//    LadderChamber↑ UpperChamber(二楼密室,真实美术) ↔ UpperHall(二楼回廊,暂用占位)   (二楼链)
//
//  每个场景真实美术接线:
//    LadderChamber → Scenes/LadderChamber/ (bg_far + prop_ladder)
//    GearRoom      → Scenes/Chamber1/      (齿轮骨骸间 bg_far + bg_near)
//    StatueRoom    → Scenes/Chamber2/      (人形石雕像 + 8 小雕像 + 方格墙)
//    UpperChamber  → Scenes/UpperHall/ (二楼密室,真实美术,原命名错误为二楼回廊)
//    UpperHall     → Scenes/LadderChamber/ (二楼回廊,暂用梯子室占位,美术待导入)
// ============================================================================

using UnityEngine;
using UnityEngine.UI;

namespace LostGoddess.Content
{
    public static class PrologueLadderChamberScene
    {
        public static void Build()
        {
            // 前厅左 2:梯子密室(向左到陶罐间 / 向右返回神庙前厅 / 中央爬梯直达二楼密室)
            PrologueLinkRoomBuilder.Build(new LinkRoomDef {
                roomName  = Rooms.Prologue_LadderChamber,
                leftRoom  = Rooms.Prologue_Chamber3,     // 朝左 → 陶罐间
                rightRoom = Rooms.Prologue_Foyer,        // 朝右 → 返回神庙前厅
                upRoom    = Rooms.Prologue_UpperChamber, // 中央木梯 → 二楼密室(青年才爬得动)
                title     = "梯子密室",
                scene     = SceneRoomBuilder.LadderChamber,
            });
        }
    }

    public static class PrologueGearRoomScene
    {
        public static void Build()
        {
            // 前厅右 1:齿轮骨骸间(向左返回前厅 / 向右到石雕室)
            var root = PrologueLinkRoomBuilder.Build(new LinkRoomDef {
                roomName  = Rooms.Prologue_GearRoom,
                leftRoom  = Rooms.Prologue_Foyer,
                rightRoom = Rooms.Prologue_StatueRoom,
                title     = "齿轮骨骸间",
                scene     = SceneRoomBuilder.GearRoom,
            });

            // 2026-07-19 添加齿轮间的道具交互
            Debug.Log($"[PrologueGearRoomScene] 开始添加道具，root={root?.name}");
            float groundY = -3.3f;  // 使用场景的实际地面高度

            // 撬棍：在左侧骸骨旁边
            BuildCrowbar(root.transform, new Vector2(-8f, groundY + 0.5f), groundY);  // 往右移并抬高

            // 扳手：在地面中央偏前
            BuildWrench(root.transform, new Vector2(0f, groundY + 0.2f), groundY);

            Debug.Log("[PrologueGearRoomScene] 道具已添加");
        }

        static void BuildCrowbar(Transform parent, Vector2 pos, float groundY)
        {
            var go = new GameObject("Prop_Crowbar");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var sr = go.AddComponent<SpriteRenderer>();

            // 先尝试加载Sprite，失败则加载Texture2D
            var sprite = Resources.Load<Sprite>("Props/Prologue/prop_crowbar_ground");
            if (sprite == null)
            {
                var tex = Resources.Load<Texture2D>("Props/Prologue/prop_crowbar_ground");
                if (tex != null)
                {
                    sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);  // PPU=100
                    Debug.Log($"[BuildCrowbar] 从Texture2D创建Sprite成功: {tex.width}x{tex.height}");
                }
                else
                {
                    Debug.LogError("[BuildCrowbar] Texture2D也加载失败");
                }
            }
            else
            {
                Debug.Log($"[BuildCrowbar] 撬棍Sprite加载成功: {sprite.name}");
            }

            sr.sprite = sprite;
            sr.sortingOrder = 55;  // 在角色和前景之间
            sr.color = Color.white;  // 确保颜色正常

            // 2048x512的图按100PPU，世界尺寸是20.48x5.12，缩小到合适大小
            go.transform.localScale = Vector3.one * 0.2f;  // 放大一点

            var boxCollider = go.AddComponent<BoxCollider2D>();
            boxCollider.size = new Vector2(20f, 5f);  // 基于原始尺寸的碰撞框

            var interact = go.AddComponent<Interact_Crowbar>();
            interact.highlightTarget = sr;
            interact.interactPoint = MakePoint(go.transform, new Vector2(pos.x, groundY));

            Debug.Log($"[BuildCrowbar] 撬棍已创建，位置={pos}, sortingOrder=55, scale=0.2, sprite={sprite != null}");
        }

        static void BuildWrench(Transform parent, Vector2 pos, float groundY)
        {
            var go = new GameObject("Prop_Wrench");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var sr = go.AddComponent<SpriteRenderer>();

            // 先尝试加载Sprite，失败则加载Texture2D
            var sprite = Resources.Load<Sprite>("Closeups/wrench_ground");
            if (sprite == null)
            {
                var tex = Resources.Load<Texture2D>("Closeups/wrench_ground");
                if (tex != null)
                {
                    sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);  // PPU=100
                    Debug.Log($"[BuildWrench] 从Texture2D创建Sprite成功: {tex.width}x{tex.height}");
                }
                else
                {
                    Debug.LogError("[BuildWrench] Texture2D也加载失败");
                }
            }
            else
            {
                Debug.Log($"[BuildWrench] 扳手Sprite加载成功: {sprite.name}");
            }

            sr.sprite = sprite;
            sr.sortingOrder = 55;  // 在角色和前景之间

            // 512x512的图按100PPU，世界尺寸是5.12x5.12
            go.transform.localScale = Vector3.one * 0.3f;  // 缩小到约1.5单位

            var boxCollider = go.AddComponent<BoxCollider2D>();
            boxCollider.size = new Vector2(5f, 5f);  // 基于原始尺寸的碰撞框

            var interact = go.AddComponent<Interact_Wrench>();
            interact.highlightTarget = sr;
            interact.interactPoint = MakePoint(go.transform, new Vector2(pos.x, groundY));

            Debug.Log($"[BuildWrench] 扳手已创建，位置={pos}, sortingOrder=55, scale=0.3");
        }

        static Transform MakePoint(Transform parent, Vector2 worldPos)
        {
            var p = new GameObject("interactPoint").transform;
            p.SetParent(parent, worldPositionStays: true);
            p.position = worldPos;
            return p;
        }
    }

    public static class PrologueStatueRoomScene
    {
        public static void Build()
        {
            // 前厅右 2:方格墙石雕室(向左返回齿轮间;右端是尽头)
            PrologueLinkRoomBuilder.Build(new LinkRoomDef {
                roomName  = Rooms.Prologue_StatueRoom,
                leftRoom  = Rooms.Prologue_GearRoom,
                rightRoom = "",   // 右端尽头
                title     = "石雕室",
                scene     = SceneRoomBuilder.StatueRoom,
            });
        }
    }

    /// <summary>
    /// 2026-07-24 女神像密室(二楼回廊右端接的新房间):
    ///   · 美术:Resources/Scenes/GoddessChamber/{bg_full, bg_near}
    ///   · 左端返回二楼回廊(Prologue_UpperHall),右端暂空(尽头)
    ///   · 交互物(用户红色涂鸦精准像素定位):
    ///     - 底部 8 个兜帽小神像(左 4 + 右 4)全部弹同一段"低头祈祷"文案
    ///     - 中央顶部小方块 → 透镜特写(focus_lens)
    ///     - 中央大块女神像 → "女神心似明镜..."文案
    ///     - 中央底部小方块 → 石棺特写(coffin 占位)+ 长文案
    /// </summary>
    public static class PrologueGoddessChamberScene
    {
        public static void Build()
        {
            var root = PrologueLinkRoomBuilder.Build(new LinkRoomDef {
                roomName  = Rooms.Prologue_GoddessChamber,
                leftRoom  = Rooms.Prologue_UpperHall,    // 左端返回二楼回廊
                rightRoom = "",                          // 右端由本函数手工建 Portal → 残骸间(见下)
                title     = "女神像密室",
                scene     = SceneRoomBuilder.GoddessChamber,
            });

            AddGoddessChamberInteractions(root.transform);

            // 2026-07-24 场景挂"老年按 Q 洞察 → 弹提示"监听:进本房间就能触发,提示文案给一条明确指引。
            //   由 flag 保证一场存档只弹一次,避免每次开合都刷屏。
            root.AddComponent<GoddessChamberInsightHint>();

            // 2026-07-24 女神像密室右端 → 残骸间。用户要求 "残骸间出生点在右侧朝左",
            //   意味着从 GC 右端出去 → C1 也从"右侧"进入。手写 ScenePortal,enterDirection="right"
            //   (不能走 LinkRoomBuilder 的 rightRoom 默认逻辑 —— 那会 enterDirection="left"
            //    让 C1 从左侧出生朝右,与用户要求相反)。
            BuildGoddessChamberRightPortalToChamber1(root.transform);
        }

        static void BuildGoddessChamberRightPortalToChamber1(Transform parent)
        {
            var scene = SceneRoomBuilder.GoddessChamber;
            float halfW = scene.bgPixelWidth / scene.bgPPU * 0.5f;
            float portalX = halfW - 0.6f;
            float groundY = scene.groundY;

            const float w = 3.0f * 0.75f, h = 5.5f;   // 判定宽度和 UpperCorridor 左端一致,收窄 1/4
            float centerY = groundY + h * 0.5f;

            var go = new GameObject("Portal_Right_Chamber1");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(portalX, centerY, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PortalSolidSprite();
            sr.color = new Color(1f, 1f, 1f, 0f);
            sr.sortingOrder = 70;
            go.transform.localScale = new Vector3(w, h, 1f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var portal = go.AddComponent<ScenePortal>();
            portal.targetRoom = Rooms.Prologue_Chamber1;
            portal.successDialogueId = "";
            portal.highlightTarget = sr;
            portal.fadeTime = 0.6f;
            portal.triggerOnEnter = true;
            portal.enterDirection = "right";   // 关键:残骸间从右侧进入,出生右端朝左

            var ip = new GameObject("interactPoint").transform;
            ip.SetParent(go.transform, false);
            ip.position = new Vector3(portalX - 1.5f, groundY, 0f);
            portal.interactPoint = ip;
        }

        static Sprite _portalSolid;
        static Sprite PortalSolidSprite()
        {
            if (_portalSolid != null) return _portalSolid;
            var tex = new Texture2D(2, 2);
            var px = new Color[] { Color.white, Color.white, Color.white, Color.white };
            tex.SetPixels(px); tex.Apply();
            _portalSolid = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            return _portalSolid;
        }

        static void AddGoddessChamberInteractions(Transform root)
        {
            const string kHoodedText = "这个身着兜帽长袍的神秘神像，他低着头祈祷。在神庙里似乎总能见到他。";

            // ── 底部 8 个兜帽小神像(4 左 + 4 右,y≈-3.7) —— 全部同一段文案 ──
            var hoodedColor = new Color(0.35f, 0.35f, 0.55f, 0.35f);
            BuildGoddessLoreProp(root, "兜帽神像_L1", new Vector2(-9.75f, -3.91f), new Vector2(0.80f, 2.32f), hoodedColor, kHoodedText);
            BuildGoddessLoreProp(root, "兜帽神像_L2", new Vector2(-7.29f, -3.71f), new Vector2(0.81f, 2.03f), hoodedColor, kHoodedText);
            BuildGoddessLoreProp(root, "兜帽神像_L3", new Vector2(-5.00f, -3.85f), new Vector2(0.72f, 1.91f), hoodedColor, kHoodedText);
            BuildGoddessLoreProp(root, "兜帽神像_L4", new Vector2(-2.67f, -3.65f), new Vector2(0.81f, 1.83f), hoodedColor, kHoodedText);
            BuildGoddessLoreProp(root, "兜帽神像_R1", new Vector2( 2.94f, -3.71f), new Vector2(0.78f, 1.89f), hoodedColor, kHoodedText);
            BuildGoddessLoreProp(root, "兜帽神像_R2", new Vector2( 5.06f, -3.65f), new Vector2(0.66f, 1.89f), hoodedColor, kHoodedText);
            BuildGoddessLoreProp(root, "兜帽神像_R3", new Vector2( 7.46f, -3.78f), new Vector2(0.66f, 1.68f), hoodedColor, kHoodedText);
            BuildGoddessLoreProp(root, "兜帽神像_R4", new Vector2( 9.85f, -3.73f), new Vector2(0.67f, 1.71f), hoodedColor, kHoodedText);

            // ── 中央大块女神像(y≈-0.18) —— 环境文案 ──
            BuildGoddessLoreProp(root, "中央女神像", new Vector2(0.01f, -0.18f), new Vector2(2.14f, 5.48f),
                new Color(1f, 0.9f, 0.5f, 0.35f),
                "\"女神心似明镜，她所照拂的地方便是真理诞生之地\"");

            // ── 中央顶部小方块(y≈+4.91) —— 透镜特写 ──
            BuildGoddessFocusLens(root, new Vector2(0.32f, 4.91f), new Vector2(1.81f, 1.14f));

            // ── 中央底部小方块(y≈-4.11) —— 石棺特写 ──
            BuildGoddessCoffin(root, new Vector2(0.02f, -4.11f), new Vector2(2.54f, 1.46f));
        }

        static void BuildGoddessLoreProp(Transform parent, string name, Vector2 center, Vector2 size, Color color, string loreText)
        {
            var go = new GameObject("Interact_" + name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GoddessSolidSprite();
            sr.color = new Color(color.r, color.g, color.b, 0f);  // 正式版隐藏调试色差方块
            sr.sortingOrder = 30;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var it = go.AddComponent<Interact_LoreProp>();
            it.loreText = loreText;
            it.highlightTarget = sr;

            float groundY = SceneRoomBuilder.GoddessChamber.groundY;
            var ip = new GameObject("interactPoint").transform;
            ip.SetParent(go.transform, worldPositionStays: true);
            ip.position = new Vector3(center.x, groundY, 0f);
            it.interactPoint = ip;
        }

        static void BuildGoddessFocusLens(Transform parent, Vector2 center, Vector2 size)
        {
            var go = new GameObject("Interact_透镜");
            go.transform.SetParent(parent, false);
            go.transform.position = center;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GoddessSolidSprite();
            sr.color = new Color(0.3f, 0.9f, 1f, 0f);   // 冷色调标记特殊物
            sr.sortingOrder = 30;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var it = go.AddComponent<Interact_GoddessFocusLens>();
            it.highlightTarget = sr;

            float groundY = SceneRoomBuilder.GoddessChamber.groundY;
            var ip = new GameObject("interactPoint").transform;
            ip.SetParent(go.transform, worldPositionStays: true);
            // 透镜挂高处,老人走近点还是在底下女神像脚下
            ip.position = new Vector3(center.x, groundY, 0f);
            it.interactPoint = ip;
        }

        static void BuildGoddessCoffin(Transform parent, Vector2 center, Vector2 size)
        {
            var go = new GameObject("Interact_石棺");
            go.transform.SetParent(parent, false);
            go.transform.position = center;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GoddessSolidSprite();
            sr.color = new Color(0.6f, 0.4f, 0.9f, 0f);   // 紫色调标记特殊物
            sr.sortingOrder = 30;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var it = go.AddComponent<Interact_GoddessCoffin>();
            it.highlightTarget = sr;

            float groundY = SceneRoomBuilder.GoddessChamber.groundY;
            var ip = new GameObject("interactPoint").transform;
            ip.SetParent(go.transform, worldPositionStays: true);
            ip.position = new Vector3(center.x, groundY, 0f);
            it.interactPoint = ip;
        }

        static Sprite _goddessSolid;
        static Sprite GoddessSolidSprite()
        {
            if (_goddessSolid != null) return _goddessSolid;
            var tex = new Texture2D(2, 2);
            var px = new Color[] { Color.white, Color.white, Color.white, Color.white };
            tex.SetPixels(px); tex.Apply();
            _goddessSolid = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            return _goddessSolid;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Interact_GoddessFocusLens —— 女神像密室顶部透镜(2026-07-24)
    //   · 未持有 → 点击 → CloseupView 弹 focus_lens 特写 + 文案 → 关闭 → 收 Items.FocusLens 入包
    //   · 已持有 → 轻提示
    //   与 Interact_GearRags / Interact_GeometryPillar 同套模式,特写关闭即入包。
    // ═══════════════════════════════════════════════════════════════════════
    public class Interact_GoddessFocusLens : InteractableBase
    {
        const string kLoreText = "透镜，可组合使用或嵌入特定机械装置中。";
        bool _busy;

        public override void OnClick()
        {
            if (_busy) return;

            if (InventorySystem.Has(Items.FocusLens))
            {
                DialogueSystem.ShowText("透镜已经收入背包。");
                return;
            }

            _busy = true;
            ShowLensCloseup();
        }

        void ShowLensCloseup()
        {
            var texture = Resources.Load<Texture2D>("UI/Icons/focus_lens_v2")
                          ?? Resources.Load<Texture2D>("UI/Icons/focus_lens");
            if (texture == null)
            {
                Debug.LogWarning("[Interact_GoddessFocusLens] 未找到 UI/Icons/focus_lens[_v2] 资源,直接兜底给道具");
                DialogueSystem.ShowText(kLoreText, () => GiveLens());
                _busy = false;
                return;
            }

            var sprite = Sprite.Create(texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));

            var go = new GameObject("_FocusLensCloseupTemplate");
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(600f, 600f);
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
            StartCoroutine(PlayCloseupSequence(go, pc));
        }

        System.Collections.IEnumerator PlayCloseupSequence(GameObject closeupGo, PlayerController pc)
        {
            bool done = false;
            DialogueSystem.ShowText(kLoreText, () => done = true);
            while (!done) yield return null;

            // 2026-07-24 【入包修复】文案读完立即入包,不再等特写关闭那一步(见 Interact_Chamber1Pickup 说明)
            GiveLens();
            // 视觉表示已拿走:方块淡到几乎不可见
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) { var c = sr.color; c.a = 0f; sr.color = c; }

            CloseupView.SetCanClose(true);
            yield return new WaitWhile(() => CloseupView.IsOpen);

            Object.Destroy(closeupGo);
            if (pc != null) pc.SetControllable(true);
            _busy = false;
        }

        void GiveLens()
        {
            if (!InventorySystem.Has(Items.FocusLens))
                InventorySystem.Add(Items.FocusLens);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  FloatingIndicatorUI —— UI 元素的上下呼吸浮动效果(2026-07-25)
    //   与世界空间的 FloatingIndicator 对应,但挂在 UGUI RectTransform 上,
    //   用 Mathf.Sin 改 anchoredPosition.y。
    // ─────────────────────────────────────────────────────────────────────
    public class FloatingIndicatorUI : MonoBehaviour
    {
        public float amplitude = 5f;   // 浮动幅度(像素)
        public float speed = 2f;       // 浮动速度
        public float phaseOffset = 0f;

        RectTransform _rt;
        Vector2 _basePos;
        float _t;

        void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _basePos = _rt.anchoredPosition;
        }

        void Update()
        {
            if (_rt == null) return;
            _t += Time.unscaledDeltaTime * speed;
            float dy = Mathf.Sin(_t + phaseOffset) * amplitude;
            _rt.anchoredPosition = _basePos + new Vector2(0f, dy);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Interact_GoddessCoffin —— 女神像密室底部石棺(2026-07-25 v2)
    //   · 未集齐三件套(齿轮1+齿轮2+底座):点石棺 → 石棺盖子特写 + 4 行诗文案 → 点空白关闭
    //   · 已集齐三件套:点石棺 → 石棺盖子特写 + 钟表位置放大镜 → 点放大镜 → 表盘指针谜题特写
    //     → 长短指针都拨到 12 点 → 闪白 → 切中年 → 解锁中年 → 组装投影仪 → 独白
    // ═══════════════════════════════════════════════════════════════════════
    public class Interact_GoddessCoffin : InteractableBase
    {
        const string kLoreText =
            "一副嵌着钟表的石棺。钟表带着发条，好像可以转动。盖子上刻下了一段文字。\n" +
            "日月无形，齿轮有音。\n" +
            "勿逆其序，缓而得之。\n" +
            "旋至始终，死即是生。";

        // 2026-07-25 石棺盖子图上钟表位置(像素坐标,3400×1200 图,原点左上角)。
        //   放大镜图标显示在这里,提示玩家点击进入表盘谜题。
        //   估算位置:钟表在石碑下方的机关处,约在图像水平中央偏左、垂直靠下的位置。
        //   像素→特写UI坐标换算: lid 图 3400×1200,特写框 1200×423(等比),放大镜定位用相对比例。
        const float kMagnifierRelX = 0.485f;  // 相对图宽的比例(≈1650/3400)
        const float kMagnifierRelY = 0.72f;   // 相对图高的比例(≈860/1200)

        bool _busy;
        bool _magnifierClicked;

        public override void OnClick()
        {
            if (_busy) return;
            _busy = true;

            bool hasAllParts =
                InventorySystem.Has(Items.Gear1) &&
                InventorySystem.Has(Items.Gear2) &&
                InventorySystem.Has(Items.BrassBase);

            if (hasAllParts && !GameState.IsEraUnlocked(Era.Middle))
            {
                // 集齐 + 中年未解锁 → 盖子特写 + 放大镜指引 → 表盘谜题
                ShowLidCloseupWithMagnifier();
            }
            else
            {
                // 未集齐 / 已解锁过 → 仅盖子特写 + 诗文
                ShowLidCloseupSimple();
            }
        }

        // ── 路径 A:仅盖子特写 + 文案 ──────────────────────────────────
        void ShowLidCloseupSimple()
        {
            var texture = Resources.Load<Texture2D>("Closeups/coffin_lid");
            if (texture == null)
            {
                Debug.LogWarning("[Interact_GoddessCoffin] 未找到 Closeups/coffin_lid,退回纯文本");
                DialogueSystem.ShowText(kLoreText, () => { _busy = false; });
                return;
            }

            var sprite = Sprite.Create(texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));

            var go = new GameObject("_CoffinLidCloseup");
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1200f, 423f); // 3400:1200 等比,宽 1200
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
            StartCoroutine(PlayLidSimpleSequence(go, pc));
        }

        System.Collections.IEnumerator PlayLidSimpleSequence(GameObject closeupGo, PlayerController pc)
        {
            bool done = false;
            DialogueSystem.ShowText(kLoreText, () => done = true);
            while (!done) yield return null;

            CloseupView.SetCanClose(true);
            yield return new WaitWhile(() => CloseupView.IsOpen);

            Object.Destroy(closeupGo);
            if (pc != null) pc.SetControllable(true);
            _busy = false;
        }

        // ── 路径 B:盖子特写 + 放大镜 + 表盘谜题 ───────────────────────
        void ShowLidCloseupWithMagnifier()
        {
            var texture = Resources.Load<Texture2D>("Closeups/coffin_lid");
            if (texture == null)
            {
                Debug.LogWarning("[Interact_GoddessCoffin] 未找到 Closeups/coffin_lid,跳过放大镜流程");
                ShowLidCloseupSimple();
                return;
            }

            var sprite = Sprite.Create(texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));

            // 容器:盖子图 + 放大镜按钮
            var container = new GameObject("_CoffinLidWithMagnifier");
            var crt = container.AddComponent<RectTransform>();
            crt.sizeDelta = new Vector2(1200f, 423f);
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot     = new Vector2(0.5f, 0.5f);

            // 盖子图
            var lidGo = new GameObject("LidImage");
            lidGo.transform.SetParent(container.transform, false);
            var lidImg = lidGo.AddComponent<UnityEngine.UI.Image>();
            lidImg.sprite = sprite;
            lidImg.preserveAspect = true;
            lidImg.raycastTarget = false;
            var lidRt = lidImg.rectTransform;
            lidRt.anchorMin = Vector2.zero; lidRt.anchorMax = Vector2.one;
            lidRt.offsetMin = Vector2.zero; lidRt.offsetMax = Vector2.zero;

            // 放大镜按钮(三角形旗帜.png 实际存放大镜 → 用 triangle_flag)
            var magSprite = Resources.Load<Sprite>("UI/Icons/triangle_flag");
            var magGo = new GameObject("MagnifierBtn");
            magGo.transform.SetParent(container.transform, false);
            var magImg = magGo.AddComponent<UnityEngine.UI.Image>();
            if (magSprite != null) magImg.sprite = magSprite;
            magImg.preserveAspect = true;
            var magRt = magImg.rectTransform;
            magRt.anchorMin = new Vector2(0, 1);
            magRt.anchorMax = new Vector2(0, 1);
            magRt.pivot = new Vector2(0.5f, 0.5f);
            magRt.sizeDelta = new Vector2(60f, 60f);
            // 位置:按 kMagnifierRelX/Y 比例定位,以左上角为锚点
            // 注意:preserveAspect=true 时盖子图会等比缩放,实际可视区域不一定撑满容器。
            // 为简化,直接按容器尺寸的比例定位(误差不大,因为宽高比与图一致)。
            magRt.anchoredPosition = new Vector2(crt.sizeDelta.x * kMagnifierRelX, -crt.sizeDelta.y * kMagnifierRelY);

            var magBtn = magGo.AddComponent<Button>();
            magBtn.targetGraphic = magImg;
            magBtn.transition = Selectable.Transition.None;
            magBtn.onClick.AddListener(() =>
            {
                // 点放大镜 → 关闭盖子特写 → 打开表盘谜题
                _magnifierClicked = true;   // 标记:是点放大镜进入的,不是点空白退出的
                CloseupView.Close();
                // container 会在 WaitLidCloseupClosed 里 Destroy,这里不做
                ShowDialPuzzle();
            });

            // 浮动动画
            var floatAnim = magGo.AddComponent<FloatingIndicatorUI>();
            floatAnim.amplitude = 5f;
            floatAnim.speed = 2f;

            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);

            CloseupView.OpenExisting(container);  // 用 OpenExisting 保留运行时注册的 Button 回调(Instantiate 会丢失匿名 lambda)
            CloseupView.SetCanClose(true);   // 玩家随时可以点空白关闭盖子特写

            // 等待关闭,重置状态
            StartCoroutine(WaitLidCloseupClosed(container, pc));
        }

        System.Collections.IEnumerator WaitLidCloseupClosed(GameObject container, PlayerController pc)
        {
            yield return new WaitWhile(() => CloseupView.IsOpen);

            Object.Destroy(container);

            if (_magnifierClicked)
            {
                // 点了放大镜 → 进入表盘谜题,保持锁定、保持 _busy=true(解谜流程会接管)
                _magnifierClicked = false;
            }
            else
            {
                // 点空白退出 → 恢复控制、重置状态
                if (pc != null) pc.SetControllable(true);
                _busy = false;
            }
        }

        void ShowDialPuzzle()
        {
            CoffinDialCloseup.Show(() =>
            {
                // 解谜成功回调
                OnDialSolved();
            });
        }

        void OnDialSolved()
        {
            // 解锁中年 + 切到中年形态
            GameState.UnlockEra(Era.Middle);
            GameState.SetFlag(Flags.Prologue_UnlockedMiddle, true);
            GameState.SetEra(Era.Middle);

            // 等切换完成(PlayerBuilder 会重建角色),再挂感叹号
            StartCoroutine(AfterSwitchPlayMonologue());
        }

        System.Collections.IEnumerator AfterSwitchPlayMonologue()
        {
            // 等一帧让 PlayerBuilder 重建完
            yield return null;
            yield return null;

            var pc = PlayerController.Instance;
            var player = pc != null ? pc.gameObject : null;
            if (player == null) { _busy = false; yield break; }

            // 中年独白(点击感叹号后开始)
            // 中年表情资源: think / wry / scared / angry / awkward
            //   smile → wry(苦笑替代), sad → awkward(茫然替代)
            var talk = PlayerTalkPrompt.Attach(player);
            talk.SetDialogue(
                new PlayerTalkPrompt.Line("呃……您好？",                       "awkward"),
                new PlayerTalkPrompt.Line("您需要修表吗？我是提尔，是一个钟表匠。",  "wry"),
                new PlayerTalkPrompt.Line("（看了看背包中收集的物件）",            "think"),
                new PlayerTalkPrompt.Line("这几个，组合一下就可以得到……",          "think")
            );
            talk.OnAllSpoken += ShowProjectorAssembly;
            talk.Show();
        }

        void ShowProjectorAssembly()
        {
            // 投影仪特写:复用 CloseupView,展示组装过程 + 入包
            var tex = Resources.Load<Texture2D>("Closeups/light_projector");
            if (tex == null)
            {
                Debug.LogWarning("[Interact_GoddessCoffin] 未找到 Closeups/light_projector,直接给道具");
                GiveProjectorAndContinue();
                return;
            }

            var sprite = Sprite.Create(tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f));

            var go = new GameObject("_ProjectorAssembly");
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(800f, 600f);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);

            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;

            CloseupView.Open(go);
            CloseupView.SetCanClose(false);
            StartCoroutine(PlayProjectorSequence(go));
        }

        System.Collections.IEnumerator PlayProjectorSequence(GameObject closeupGo)
        {
            bool done = false;
            DialogueSystem.ShowText(
                "提尔将齿轮、透镜与底座拼接起来，获得了【光幕投影仪】",
                () => done = true);
            while (!done) yield return null;

            // 入包
            if (!InventorySystem.Has(Items.LightProjector))
                InventorySystem.Add(Items.LightProjector);
            // 消耗掉三件套
            InventorySystem.Remove(Items.Gear1);
            InventorySystem.Remove(Items.Gear2);
            InventorySystem.Remove(Items.BrassBase);

            CloseupView.SetCanClose(true);
            yield return new WaitWhile(() => CloseupView.IsOpen);

            Object.Destroy(closeupGo);
            ContinueMiddleMonologue();
        }

        void ContinueMiddleMonologue()
        {
            var pc = PlayerController.Instance;
            var player = pc != null ? pc.gameObject : null;
            if (player == null) { _busy = false; return; }

            var talk = PlayerTalkPrompt.Attach(player);
            talk.SetDialogue(
                new PlayerTalkPrompt.Line("所以，这里是神庙......",      "think"),
                new PlayerTalkPrompt.Line("我竟然在这里......",        "awkward"),
                new PlayerTalkPrompt.Line("要去前厅吗？那就走吧。",    "wry")
            );
            talk.OnAllSpoken += () =>
            {
                QuestPromptManager.UpdateText("回到神庙前厅");
                if (pc != null) pc.SetControllable(true);
                _busy = false;
            };
            talk.Show();
        }

        void GiveProjectorAndContinue()
        {
            if (!InventorySystem.Has(Items.LightProjector))
                InventorySystem.Add(Items.LightProjector);
            InventorySystem.Remove(Items.Gear1);
            InventorySystem.Remove(Items.Gear2);
            InventorySystem.Remove(Items.BrassBase);
            ContinueMiddleMonologue();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  GoddessChamberInsightHint —— 女神像密室:老年按 Q 岁月洞察 → 弹提示(2026-07-24)
    //   · 挂在场景根节点,监听 InsightMode.OnToggled
    //   · 只在"老年 + 首次开启洞察"时触发一段提示文案,给玩家藏宝指引
    //   · 用 GameState.Flag 持久化,一档只弹一次;场景重建/回来再洞察也不会重复
    //   · 非老年按 Q 走 InsightMode 已有的 wrong_era 提示,不到本组件
    // ═══════════════════════════════════════════════════════════════════════
    public class GoddessChamberInsightHint : UnityEngine.MonoBehaviour
    {
        const string kFlagShown = "prologue_goddess_chamber_insight_hint_shown";
        const string kHintText  = "女神直视的地方在哪里，哪里就会有宝藏";

        void OnEnable()  { InsightMode.OnToggled += OnInsight; }
        void OnDisable() { InsightMode.OnToggled -= OnInsight; }

        void OnInsight(bool on)
        {
            if (!on) return;                                   // 仅在开启瞬间提示
            if (GameState.CurrentEra != Era.Old) return;       // 双保险:InsightMode.Toggle 里已判过
            if (GameState.GetFlag(kFlagShown)) return;         // 每档只弹一次
            GameState.SetFlag(kFlagShown, true);
            DialogueSystem.ShowText(kHintText);
        }
    }

    /// <summary>
    /// 2026-07-24 残骸间(女神像密室右端接的新房间):
    ///   · 美术复用 Chamber1(3400×1200,齿轮/管道/骨骸场景)
    ///   · 拓扑:左端 Portal 回女神像密室(右侧入口),右端是尽头(不建 Portal)
    ///   · 玩家从女神像密室右端进入 → 出生在残骸间右侧朝左(enterDirection="right")
    ///   · 交互物(用户红色涂鸦精准像素定位,5 个):
    ///     - 左侧锈蚀齿轮组 (x=-15.40) / 缠绕管道枯藤 (x=-12.28) / 白骨骷髅 (x=-6.34)
    ///     - 右侧巨大传动齿轮 (x=+9.31) / 堆叠管道废铁 (x=+15.32)
    /// </summary>
    public static class PrologueChamber1Scene
    {
        public static void Build()
        {
            var root = PrologueLinkRoomBuilder.Build(new LinkRoomDef {
                roomName  = Rooms.Prologue_Chamber1,
                leftRoom  = "",                              // 左端不建 Portal:回 GC 由本函数手写(需 enterDirection="right")
                rightRoom = "",                              // 右端是尽头(用户没要求继续深入)
                title     = "残骸间",
                scene     = SceneRoomBuilder.Chamber1,
            });

            // 2026-07-24 v2 拓扑(用户明确要求):
            //   · 左侧边缘 → 神庙前厅(Foyer):收齐撬棍+扳手后由旗帜引导的"前进"出口。
            //   · 右侧边缘 → 女神像密室(GoddessChamber):玩家进残骸间的来路,原路返回。
            //   玩家从 GC 右端进入残骸间时出生在右侧朝左(enterDirection="right"),
            //   右端 Portal 在其身后,不会一进来就误触发(有 1.2+ 单位缓冲)。
            BuildChamber1LeftPortalToFoyer(root.transform);
            BuildChamber1RightPortalToGC(root.transform);

            AddChamber1Interactions(root.transform);

            // 2026-07-24 地上两件可拾取道具(用户红涂鸦定位):
            //   · 铁撬棍:美术给了 3400×1200 单独图层(crowbar_ground),严格照石板图层的形式原样覆盖;
            //     点击 → 特写(crowbar_v2)+ 文案 → 收入背包(Items.Crowbar)→ 图层与点击块消失。
            //   · 铸铁扳手:美术给了 500×500 地上道具图(UI/Icons/wrench_ground),按红涂鸦位置贴 prop sprite;
            //     点击 → 特写(wrench)+ 文案 → 收入背包(Items.Wrench)→ prop 与点击块消失。
            BuildChamber1CrowbarPickup(root.transform);
            BuildChamber1WrenchPickup(root.transform);

            // 2026-07-24 收齐两件工具 → 头顶感叹号(3 句青年独白)→ 左侧旗帜引导去神庙前厅。
            //   由 Watcher 监听背包,持久化 flag 保证一档只演一次;返回场景若已收齐则直接补旗帜。
            root.AddComponent<Chamber1ToolsWatcher>();
        }

        static void BuildChamber1LeftPortalToFoyer(Transform parent)
        {
            var scene = SceneRoomBuilder.Chamber1;
            float halfW = scene.bgPixelWidth / scene.bgPPU * 0.5f;
            float portalX = halfW - 0.6f;
            float groundY = scene.groundY;

            const float w = 3.0f * 0.75f, h = 5.5f;   // 收窄的判定宽度
            float centerY = groundY + h * 0.5f;

            var go = new GameObject("Portal_Left_Foyer");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(-portalX, centerY, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Chamber1SolidSprite();
            sr.color = new Color(1f, 1f, 1f, 0f);
            sr.sortingOrder = 70;
            go.transform.localScale = new Vector3(w, h, 1f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var portal = go.AddComponent<ScenePortal>();
            portal.targetRoom = Rooms.Prologue_Foyer;
            portal.successDialogueId = "";
            portal.highlightTarget = sr;
            portal.fadeTime = 0.6f;
            portal.triggerOnEnter = true;
            portal.enterDirection = "right";   // 从残骸间左端出去 → 神庙前厅右侧进入(出生右端朝左,便于往左走找梯子密室旗帜)

            var ip = new GameObject("interactPoint").transform;
            ip.SetParent(go.transform, false);
            ip.position = new Vector3(-portalX + 1.5f, groundY, 0f);
            portal.interactPoint = ip;
        }

        static void BuildChamber1RightPortalToGC(Transform parent)
        {
            var scene = SceneRoomBuilder.Chamber1;
            float halfW = scene.bgPixelWidth / scene.bgPPU * 0.5f;
            float portalX = halfW - 0.6f;
            float groundY = scene.groundY;

            const float w = 3.0f * 0.75f, h = 5.5f;
            float centerY = groundY + h * 0.5f;

            var go = new GameObject("Portal_Right_GoddessChamber");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(portalX, centerY, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Chamber1SolidSprite();
            sr.color = new Color(1f, 1f, 1f, 0f);
            sr.sortingOrder = 70;
            go.transform.localScale = new Vector3(w, h, 1f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var portal = go.AddComponent<ScenePortal>();
            portal.targetRoom = Rooms.Prologue_GoddessChamber;
            portal.successDialogueId = "";
            portal.highlightTarget = sr;
            portal.fadeTime = 0.6f;
            portal.triggerOnEnter = true;
            portal.enterDirection = "right";   // 回 GC 时从右侧进入(GC 右端 Portal 处),与玩家离开 GC 的位置对齐

            var ip = new GameObject("interactPoint").transform;
            ip.SetParent(go.transform, false);
            ip.position = new Vector3(portalX - 1.5f, groundY, 0f);
            portal.interactPoint = ip;
        }

        // 红涂鸦像素→世界坐标:3400×1200, PPU=100, pivot=Center → wx=(px-1700)/100, wy=(600-py)/100
        static void AddChamber1Interactions(Transform root)
        {
            // 1. 左侧锈蚀齿轮组 (px cx=160, cy=611 → wx=-15.40, wy=-0.11;bbox 275×316)
            BuildChamber1LoreProp(root, "锈蚀齿轮组", new Vector2(-15.40f, -0.11f), new Vector2(2.75f, 3.16f),
                new Color(0.7f, 0.5f, 0.3f, 0.35f),
                "这颗半埋在地底废墟中的庞大齿轮，边缘的齿牙十分锋利。它的轴心有一个奇特的嵌入槽");

            // 2. 缠绕管道的枯藤 (px cx=472, cy=656 → wx=-12.28, wy=-0.56;bbox 248×222)
            BuildChamber1LoreProp(root, "缠绕管道的枯藤", new Vector2(-12.28f, -0.56f), new Vector2(2.48f, 2.22f),
                new Color(0.5f, 0.7f, 0.4f, 0.35f),
                "死寂的冰冷管道上缠绕着顽强的地下枯藤，甚至夹杂着散发微光的苔藓。");

            // 3. 左侧废墟白骨与骷髅 (px cx=1066, cy=858 → wx=-6.34, wy=-2.58;bbox 270×130)
            BuildChamber1LoreProp(root, "白骨与骷髅", new Vector2(-6.34f, -2.58f), new Vector2(2.70f, 1.30f),
                new Color(0.9f, 0.9f, 0.75f, 0.35f),
                "一具散落在废弃杂物中的冒险者骸骨。");

            // 4. 右侧巨大传动齿轮 (px cx=2631, cy=729 → wx=+9.31, wy=-1.29;bbox 596×257)
            BuildChamber1LoreProp(root, "巨大传动齿轮", new Vector2(9.31f, -1.29f), new Vector2(5.96f, 2.57f),
                new Color(0.6f, 0.6f, 0.7f, 0.35f),
                "大小不一的巨大齿轮咬合在一起，上面布满了青苔与岁月的锈痕。它们如今已不再转动。");

            // 5. 右侧堆叠管道与废铁 (px cx=3232, cy=594 → wx=+15.32, wy=+0.06;bbox 260×352)
            BuildChamber1LoreProp(root, "堆叠管道与废铁", new Vector2(15.32f, 0.06f), new Vector2(2.60f, 3.52f),
                new Color(0.55f, 0.55f, 0.55f, 0.35f),
                "纵横交错的金属管道在这里断裂、堆叠，散落成一摊废铁。");
        }

        static void BuildChamber1LoreProp(Transform parent, string name, Vector2 center, Vector2 size, Color color, string loreText)
        {
            var go = new GameObject("Interact_" + name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Chamber1SolidSprite();
            sr.color = new Color(color.r, color.g, color.b, 0f);  // 正式版隐藏调试色差方块
            sr.sortingOrder = 30;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var it = go.AddComponent<Interact_LoreProp>();
            it.loreText = loreText;
            it.highlightTarget = sr;

            float groundY = SceneRoomBuilder.Chamber1.groundY;
            var ip = new GameObject("interactPoint").transform;
            ip.SetParent(go.transform, worldPositionStays: true);
            ip.position = new Vector3(center.x, groundY, 0f);
            it.interactPoint = ip;
        }

        static Sprite _chamber1Solid;
        static Sprite Chamber1SolidSprite()
        {
            if (_chamber1Solid != null) return _chamber1Solid;
            var tex = new Texture2D(2, 2);
            var px = new Color[] { Color.white, Color.white, Color.white, Color.white };
            tex.SetPixels(px); tex.Apply();
            _chamber1Solid = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            return _chamber1Solid;
        }

        // ── 铁撬棍拾取 ────────────────────────────────────────────────────
        //  美术给了 3400×1200 单独图层(Scenes/Chamber1/crowbar_ground),尺寸与场景完全一致 →
        //  严格照倚靠石板图层(BuildStoneSlabLayer)的形式原样覆盖,零手动定位。
        //  点击块单独建在图层显示区域(红涂鸦 wx≈-3.33),点击 → 特写 + 收入背包 → 图层和点击块一起隐藏。
        static void BuildChamber1CrowbarPickup(Transform parent)
        {
            // 1) 铁撬棍图层(和石板图层完全相同的覆盖计算)
            GameObject layerGo = null;
            var layerSprite = Resources.Load<Sprite>("Scenes/Chamber1/crowbar_ground");
            if (layerSprite != null)
            {
                layerGo = new GameObject("Layer_Crowbar");
                layerGo.transform.SetParent(parent, false);
                var lsr = layerGo.AddComponent<SpriteRenderer>();
                lsr.sprite = layerSprite;
                lsr.color = Color.white;
                lsr.sortingOrder = 20;   // 盖在背景之上、人物(默认 0)之上一点,地上道具应能看清
                float pivotOffsetY = layerSprite.bounds.center.y;
                layerGo.transform.position = new Vector3(0f, 0f - pivotOffsetY, 0f);
                layerGo.transform.localScale = Vector3.one;
            }
            else
            {
                Debug.LogWarning("[PrologueChamber1Scene] 未找到铁撬棍图层 'Scenes/Chamber1/crowbar_ground'");
            }

            // 2) 点击块(红涂鸦左块 wx=-3.33 wy=-2.98,bbox 2.62×0.71 —— 稍微放大高度便于点击)
            var go = new GameObject("Interact_铁撬棍");
            go.transform.SetParent(parent, false);
            Vector2 center = new Vector2(-3.33f, -2.98f);
            go.transform.position = center;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Chamber1SolidSprite();
            sr.color = new Color(1f, 1f, 1f, 0f);   // 全透明:图层负责显示
            sr.sortingOrder = 30;
            go.transform.localScale = new Vector3(2.62f, 1.20f, 1f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var it = go.AddComponent<Interact_Chamber1Pickup>();
            it.itemId       = Items.Crowbar;
            it.closeupIcon  = "UI/Icons/crowbar_v2";
            it.loreText     = "一根锈迹斑斑的铁撬棍。撬头扁平，底部弯折，可用于撬开被卡住或封死的东西。还可以当作武器使用。";
            it.alreadyText  = "撬棍已经收进背包了。";
            it.extraVisual  = layerGo;    // 拾取后连同图层一起隐藏
            it.highlightTarget = sr;

            float groundY = SceneRoomBuilder.Chamber1.groundY;
            var ip = new GameObject("interactPoint").transform;
            ip.SetParent(go.transform, worldPositionStays: true);
            ip.position = new Vector3(center.x, groundY, 0f);
            it.interactPoint = ip;
        }

        // ── 铸铁扳手拾取 ──────────────────────────────────────────────────
        //  美术只给了 500×500 道具图(UI/Icons/wrench),没有整幅场景图层 → 按红涂鸦位置贴一个 prop sprite。
        //  红涂鸦右块 wx=-1.46 wy=-5.05(地面附近),prop 缩放到合适大小放这里。
        static void BuildChamber1WrenchPickup(Transform parent)
        {
            var go = new GameObject("Interact_铸铁扳手");
            go.transform.SetParent(parent, false);
            Vector2 center = new Vector2(-1.46f, -4.55f);   // 比红涂鸦(-5.05)略上抬,让扳手完整落在地面上
            go.transform.position = center;

            // prop 显示层:用美术给的"扳手密室地上"图(500×500),特写图仍用 UI/Icons/wrench
            GameObject propGo = null;
            var propSprite = Resources.Load<Sprite>("UI/Icons/wrench_ground");
            if (propSprite != null)
            {
                propGo = new GameObject("Prop_Wrench");
                propGo.transform.SetParent(go.transform, false);
                propGo.transform.localPosition = Vector3.zero;
                var psr = propGo.AddComponent<SpriteRenderer>();
                psr.sprite = propSprite;
                psr.color = Color.white;
                psr.sortingOrder = 20;   // 和铁撬棍图层同层,地上道具
                // 500px @ PPU=100 = 5 世界单位,太大 → 缩到约 1.2 单位宽
                propGo.transform.localScale = Vector3.one * 0.24f;
                propGo.layer = 2;        // Ignore Raycast:点击直达下方点击块 collider
            }
            else
            {
                Debug.LogWarning("[PrologueChamber1Scene] 未找到扳手地上图 'UI/Icons/wrench_ground'");
            }

            // 点击块(比红涂鸦稍大,确保能点到)
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Chamber1SolidSprite();
            sr.color = new Color(1f, 1f, 1f, 0f);
            sr.sortingOrder = 30;
            go.transform.localScale = new Vector3(1.4f, 1.2f, 1f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var it = go.AddComponent<Interact_Chamber1Pickup>();
            it.itemId       = Items.Wrench;
            it.closeupIcon  = "UI/Icons/wrench";
            it.loreText     = "一把铸铁扳手。可以转动齿轮、螺栓或者某种机关。";
            it.alreadyText  = "扳手已经收进背包了。";
            it.extraVisual  = propGo;     // 拾取后连同 prop 一起隐藏
            it.highlightTarget = sr;

            float groundY = SceneRoomBuilder.Chamber1.groundY;
            var ip = new GameObject("interactPoint").transform;
            ip.SetParent(go.transform, worldPositionStays: true);
            ip.position = new Vector3(center.x, groundY, 0f);
            it.interactPoint = ip;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Interact_Chamber1Pickup —— 残骸间地上可拾取道具(2026-07-24)
    //   · 未持有 → 点击 → CloseupView 弹道具特写 + 文案 → 关闭 → 收入背包 → 场景里道具消失
    //   · 已持有 → 轻提示
    //   通用组件:铁撬棍 / 铸铁扳手共用,靠 itemId/closeupIcon/loreText/extraVisual 参数区分。
    // ═══════════════════════════════════════════════════════════════════════
    public class Interact_Chamber1Pickup : InteractableBase
    {
        public string itemId;
        public string closeupIcon;
        public string loreText;
        public string alreadyText = "已经拿过了。";
        public GameObject extraVisual;   // 场景里道具的显示物(图层/prop),拾取后隐藏

        bool _busy;

        void Start()
        {
            // 切场景返回时:若已拾取过,场景里的道具不该再出现(背包持久,场景每次重建)。
            if (!string.IsNullOrEmpty(itemId) && InventorySystem.Has(itemId))
            {
                if (extraVisual != null) extraVisual.SetActive(false);
                var sr = GetComponent<SpriteRenderer>();
                if (sr != null) { var c = sr.color; c.a = 0f; sr.color = c; }
                var col = GetComponent<Collider2D>();
                if (col != null) col.enabled = false;
            }
        }

        public override void OnClick()
        {
            if (_busy) return;

            if (InventorySystem.Has(itemId))
            {
                DialogueSystem.ShowText(alreadyText);
                return;
            }

            _busy = true;
            ShowCloseup();
        }

        void ShowCloseup()
        {
            var texture = Resources.Load<Texture2D>(closeupIcon);
            if (texture == null)
            {
                Debug.LogWarning($"[Interact_Chamber1Pickup] 未找到特写资源 '{closeupIcon}',直接兜底给道具");
                DialogueSystem.ShowText(loreText, () => Give());
                _busy = false;
                return;
            }

            var sprite = Sprite.Create(texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));

            var go = new GameObject("_Chamber1CloseupTemplate");
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(600f, 600f);
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
            StartCoroutine(PlayCloseupSequence(go, pc));
        }

        System.Collections.IEnumerator PlayCloseupSequence(GameObject closeupGo, PlayerController pc)
        {
            bool done = false;
            DialogueSystem.ShowText(loreText, () => done = true);
            while (!done) yield return null;

            // 2026-07-24 【入包修复】文案读完立即入包 + 让场景道具消失,
            //   不再等"特写关闭"那一步。之前 Give() 放在 WaitWhile(IsOpen) 之后,
            //   若关闭点击的时序被吃掉/协程被打断,道具就永远进不了背包
            //   (表现:除大齿轮外都收不进包)。提前到这里最稳,且符合"弹文案即入包"的需求。
            Give();

            CloseupView.SetCanClose(true);
            yield return new WaitWhile(() => CloseupView.IsOpen);

            Object.Destroy(closeupGo);
            if (pc != null) pc.SetControllable(true);
            _busy = false;
        }

        void Give()
        {
            if (!InventorySystem.Has(itemId))
                InventorySystem.Add(itemId);

            // 场景里道具彻底消失:隐藏显示物 + 禁用点击块
            if (extraVisual != null) extraVisual.SetActive(false);
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) { var c = sr.color; c.a = 0f; sr.color = c; }
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Chamber1ToolsWatcher —— 残骸间"收齐撬棍+扳手"后的演出 + 左侧旗帜引导(2026-07-24)
    //   · 每帧检查背包:一旦 Crowbar && Wrench 都到手 → 头顶感叹号(3 句青年独白)
    //   · 独白说完 → 场景左侧边缘出现旗帜(EdgeFlagPortal → 神庙前厅),引导玩家前进
    //   · 用持久化 flag 保证一档只演一次;返回场景若已收齐,直接补出旗帜(不再演独白)
    //   · 旗帜只做视觉指引 + 走入切场景(EdgeFlagPortal 自带),切场景后自动销毁
    // ═══════════════════════════════════════════════════════════════════════
    public class Chamber1ToolsWatcher : UnityEngine.MonoBehaviour
    {
        const string kFlagSpoken = "prologue_chamber1_tools_collected_spoken";
        const string kFlagName    = "EdgeGuideFlag_Chamber1_ToFoyer";

        bool _handled;

        void Start()
        {
            // 返回场景:若已经演过(收齐并说完话),直接补出左侧旗帜,不再演独白
            if (GameState.GetFlag(kFlagSpoken))
            {
                _handled = true;
                SpawnLeftGuideFlag();
            }
        }

        void Update()
        {
            if (_handled) return;
            if (!InventorySystem.Has(Items.Crowbar) || !InventorySystem.Has(Items.Wrench)) return;

            _handled = true;
            GameState.SetFlag(kFlagSpoken, true);

            var player = PlayerController.Instance?.gameObject;
            if (player == null) { SpawnLeftGuideFlag(); return; }

            var talk = PlayerTalkPrompt.Attach(player);
            talk.SetDialogue(
                new PlayerTalkPrompt.Line("(摇晃着撬棍)，这玩意儿倒挺顺手的", "smile"),
                new PlayerTalkPrompt.Line("这里好像没有其他东西能用了",       "think"),
                new PlayerTalkPrompt.Line("再去第二层上面看看吧",             "think")
            );
            talk.OnAllSpoken += SpawnLeftGuideFlag;
            talk.Show();
        }

        void SpawnLeftGuideFlag()
        {
            var parent = transform;   // 挂在场景根上,旗帜作为其子物体
            if (parent.Find(kFlagName) != null) return;   // 避免重复

            float groundY = SceneRoomBuilder.Chamber1.groundY;
            float halfW = SceneRoomBuilder.Chamber1.bgPixelWidth / SceneRoomBuilder.Chamber1.bgPPU * 0.5f;
            float flagX = -(halfW - 3.0f);   // 场景左侧内缩 3 单位,贴近左端 Portal 前

            // 旗帜图:与其它场景一致,magnifying_glass.png 实际内容是三角旗帜
            var sprite = Resources.Load<Sprite>("UI/Icons/magnifying_glass");
            if (sprite == null)
            {
                Debug.LogError("[Chamber1ToolsWatcher] 找不到旗帜资源 UI/Icons/magnifying_glass");
                return;
            }

            var go = new GameObject(kFlagName);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(flagX, groundY + 1.2f, 0f);
            go.layer = 2;   // Ignore Raycast

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 50;
            go.transform.localScale = Vector3.one * 1.5f;

            var autoCols = go.GetComponents<Collider2D>();
            foreach (var c in autoCols) UnityEngine.Object.Destroy(c);

            var floatAnim = go.AddComponent<FloatingIndicator>();
            floatAnim.amplitude = 0.18f;
            floatAnim.speed = 2.2f;

            // 走入切场景 → 神庙前厅。EdgeFlagPortal 会 SetFlag 记录触发过,切场景后销毁自己。
            var portal = go.AddComponent<EdgeFlagPortal>();
            portal.triggerRadius = 1.5f;
            portal.targetRoom = Rooms.Prologue_Foyer;
            portal.enterDirection = "right";   // 神庙前厅从右侧进入(出生右端朝左)
        }
    }

    public static class PrologueUpperChamberScene
    {
        public static void Build()
        {
            // 二楼回廊(2026-07-24 换真实美术 UpperCorridor,5950×1200 更长):
            //   Prologue_UpperHall(名字保持不变) = 二楼回廊语义,
            //   左端返回二楼密室(UpperChamber)、右端待接下一房间(暂空,尽头)
            var root = PrologueLinkRoomBuilder.Build(new LinkRoomDef {
                roomName  = Rooms.Prologue_UpperHall,
                leftRoom  = Rooms.Prologue_UpperChamber,     // 左端返回二楼密室
                rightRoom = Rooms.Prologue_GoddessChamber,   // 2026-07-24 右端接女神像密室
                title     = "二楼回廊",
                scene     = SceneRoomBuilder.UpperCorridor,   // 真实美术
            });

            // 2026-07-24 用户反馈:二楼回廊左端 Portal 判定太宽 —— 根因不是宽度,是位置错(见下方修复)。
            //   PrologueLinkRoomBuilder 的 Portal 位置以前硬编 x=±15(给 halfW=17 的普通场景设计),
            //   套到 UpperCorridor(halfW=29.75)时 Portal 距离真实左边缘还有 14+ 单位 → 玩家出生就
            //   撞在 Portal 内。已改成 Portal 位置绑定 sceneDef 实际半宽,同时通用宽度收缩到 0.75。
            //   Portal 现在会正确贴在场景左边缘,不再需要在这里手动覆盖 localScale。
            var leftPortalTr = root.transform.Find("Portal_Left_" + Rooms.Prologue_UpperChamber);
            if (leftPortalTr != null)
            {
                var s = leftPortalTr.localScale;
                s.x = 0.75f;   // 1/4 判定宽度(用户要求)
                leftPortalTr.localScale = s;
            }

            // ── 2026-07-24 二楼回廊 8 个环境交互物(用户红色涂鸦精准像素定位) ──
            //  bg 5950×1200, PPU=100, pivot=Center → wx=(px-2975)/100, wy=(600-py)/100
            //  从左到右 8 块红涂鸦对应 8 个物品,最后一个(几何石柱)是双阶段交互 + 获取小齿轮。
            AddUpperCorridorInteractions(root.transform);
        }

        static void AddUpperCorridorInteractions(Transform root)
        {
            // 2026-07-24 v2:用户重新涂鸦精准定位,x 顺序对应 8 段文案。
            //   新版红涂鸦检测出 9 块 —— 图里两个"兜帽长袍神像"(x=1.93 和 x=18.42)共用同一段文案,
            //   呼应"在神庙里似乎总能见到他"的世界观。

            // 1. 缠绕石柱的巨蛇 (x=-26.30)
            BuildCorridorLoreProp(root, "缠绕石柱的巨蛇", new Vector2(-26.30f, -0.74f), new Vector2(1.97f, 5.10f),
                new Color(0.5f, 0.8f, 0.3f, 0.35f),
                "底座篆刻着一段文字：\"邪恶的力量，趁你年轻，驱逐你至无底深海之中\"");

            // 2. 衔尾蛇刻痕 (x=-21.68,符号在拱门旁石墙上)
            BuildCorridorLoreProp(root, "衔尾蛇刻痕", new Vector2(-21.68f, 0.30f), new Vector2(3.06f, 3.56f),
                new Color(0.9f, 0.6f, 0.2f, 0.35f),
                "巨蛇正吞噬着自己的尾巴，这个古老的符号象征着无尽的循环，人们刻下它是为了记录这座神庙不朽的存在。");

            // 3. 多臂神像 (x=-16.96)
            BuildCorridorLoreProp(root, "多臂神像", new Vector2(-16.96f, -0.67f), new Vector2(2.15f, 4.40f),
                new Color(1f, 0.4f, 0.5f, 0.35f),
                "这座神像拥有多条手臂，他的每只手中都握着不同的物品，他的风格看上去与这里格格不入，但它的底座上刻着的那个特殊的圆环符号，似乎是它被供奉于此的原因......");

            // 4. 三角结符文刻痕 (x=-12.19,拱门之间的墙上符号)
            BuildCorridorLoreProp(root, "三角结符文刻痕", new Vector2(-12.19f, 0.89f), new Vector2(3.71f, 3.76f),
                new Color(0.4f, 0.5f, 1f, 0.35f),
                "古老的三角结，代表着三个世界的交汇。");

            // 5. 展翅的雄鹰石像 (x=-7.37)
            BuildCorridorLoreProp(root, "展翅的雄鹰石像", new Vector2(-7.37f, -0.56f), new Vector2(2.10f, 5.26f),
                new Color(0.85f, 0.85f, 0.2f, 0.35f),
                "底座篆刻着一段文字：\"奥丁的信使，奥丁的威严，它为奥丁注视着这片神圣之地。\"");

            // 6. 编织格子刻痕 (x=-2.77,拱门之间的墙上符号)
            BuildCorridorLoreProp(root, "编织格子刻痕", new Vector2(-2.77f, 1.12f), new Vector2(3.24f, 3.42f),
                new Color(0.7f, 0.3f, 0.9f, 0.35f),
                "女神为你编织好了一张命运之网，生命和死亡都被神圣的力量所编织。");

            // 7. 兜帽长袍神像(左) (x=1.93)
            BuildCorridorLoreProp(root, "兜帽长袍神像(左)", new Vector2(1.93f, -0.25f), new Vector2(1.90f, 5.04f),
                new Color(0.35f, 0.35f, 0.5f, 0.35f),
                "这个身着兜帽长袍的神秘神像，他的面孔隐藏在阴影中。在神庙里似乎总能见到他。");

            // 8. 几何石柱石像 (x=9.19,双阶段 + 获取小齿轮 Gear1)
            BuildGeometryPillar(root, new Vector2(9.19f, -0.29f), new Vector2(2.17f, 5.29f));

            // 2026-07-24 用户要求:删除最右侧兜帽神像交互(x=18.42)—— 右端将直接切换到女神像密室,
            //   不需要在场景边缘再放交互点干扰玩家。
        }

        static void BuildCorridorLoreProp(Transform parent, string name, Vector2 center, Vector2 size, Color color, string loreText)
        {
            var go = new GameObject("Interact_" + name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            sr.color = new Color(color.r, color.g, color.b, 0f);  // 正式版隐藏调试色差方块
            sr.sortingOrder = 30;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var it = go.AddComponent<Interact_LoreProp>();
            it.loreText = loreText;
            it.highlightTarget = sr;

            float groundY = SceneRoomBuilder.UpperCorridor.groundY;
            var ip = new GameObject("interactPoint").transform;
            ip.SetParent(go.transform, worldPositionStays: true);
            ip.position = new Vector3(center.x, groundY, 0f);
            it.interactPoint = ip;
        }

        static void BuildGeometryPillar(Transform parent, Vector2 center, Vector2 size)
        {
            var go = new GameObject("Interact_几何石柱");
            go.transform.SetParent(parent, false);
            go.transform.position = center;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            sr.color = new Color(0.6f, 0.9f, 0.9f, 0f);
            sr.sortingOrder = 30;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var it = go.AddComponent<Interact_GeometryPillar>();
            it.highlightTarget = sr;

            float groundY = SceneRoomBuilder.UpperCorridor.groundY;
            var ip = new GameObject("interactPoint").transform;
            ip.SetParent(go.transform, worldPositionStays: true);
            ip.position = new Vector3(center.x, groundY, 0f);
            it.interactPoint = ip;
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

    // ═══════════════════════════════════════════════════════════════════════
    //  Interact_GeometryPillar —— 二楼回廊最右几何石柱(2026-07-24)
    //   · 第 1 次点击(任何形态) → 弹环境文案"这个石柱顶部可以扭动，但是需要些力气"
    //   · 第 2 次点击:
    //       - 老年 → "【你的上肢力量不足以拧开】"(不消耗次数,可反复触发)
    //       - 青年/中年 → prop_gear_small 特写(变暗背景) + 文案 → 关闭 → 收 Gear1 入包
    //   · 已获取后 → 提示已经拿走
    // ═══════════════════════════════════════════════════════════════════════
    public class Interact_GeometryPillar : InteractableBase
    {
        const string kIntroText    = "这个石柱顶部可以扭动，但是需要些力气";
        const string kOldFailText  = "【你的上肢力量不足以拧开】";
        const string kGearLore     = "表面有锈痕的齿轮，可组合使用或嵌入特定机械装置中。";
        const string kFlagIntroDone = "prologue_geometry_pillar_intro_shown";

        bool _busy = false;
        bool _introShown = false;   // 内存态,和 flag 同步

        void Start()
        {
            // 场景重建后恢复"是否已看过环境文案"的状态
            if (GameState.GetFlag(kFlagIntroDone)) _introShown = true;
        }

        public override void OnClick()
        {
            if (_busy) return;

            // 已拿走:轻提示
            if (InventorySystem.Has(Items.Gear1))
            {
                DialogueSystem.ShowText("石柱顶部已经被扭开过了，齿轮已被取走。");
                return;
            }

            // 第 1 次点击:环境文案(任何形态都触发,不判断力气)
            if (!_introShown)
            {
                _introShown = true;
                GameState.SetFlag(kFlagIntroDone, true);
                DialogueSystem.ShowText(kIntroText);
                return;
            }

            // 第 2 次及以后:按形态分流
            if (GameState.CurrentEra == Era.Old)
            {
                // 老年:力气不够,可反复触发
                DialogueSystem.ShowText(kOldFailText);
                return;
            }

            // 青年/中年:扭开 → 弹小齿轮特写 → 收进背包
            _busy = true;
            ShowGearCloseup();
        }

        void ShowGearCloseup()
        {
            var texture = Resources.Load<Texture2D>("UI/Icons/gear_small_v2")
                          ?? Resources.Load<Texture2D>("UI/Icons/gear_small");
            if (texture == null)
            {
                Debug.LogWarning("[Interact_GeometryPillar] 未找到 UI/Icons/gear_small_v2,直接兜底给齿轮");
                GiveGear();
                _busy = false;
                return;
            }

            var sprite = Sprite.Create(texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));

            var go = new GameObject("_GearSmallCloseupTemplate");
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(600f, 600f);
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
            StartCoroutine(PlayGearCloseupSequence(go, pc));
        }

        System.Collections.IEnumerator PlayGearCloseupSequence(GameObject closeupGo, PlayerController pc)
        {
            bool done = false;
            DialogueSystem.ShowText(kGearLore, () => done = true);
            while (!done) yield return null;

            // 2026-07-24 【入包修复】文案读完立即入包(见 Interact_Chamber1Pickup 说明)
            GiveGear();
            // 视觉表示已处理:方块淡到几乎不可见
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
            if (!InventorySystem.Has(Items.Gear1))
                InventorySystem.Add(Items.Gear1);
        }
    }

    /// <summary>青年首次进入二楼密室:玩家头顶感叹号触发 3 句独白。一次性。</summary>
    public class PrologueUpperChamberArrivalDirector : UnityEngine.MonoBehaviour
    {
        void Start()
        {
            var player = PlayerController.Instance?.gameObject;
            if (player == null) return;

            // 挂感叹号:LinkRoomDirector 的进入独白("一间空密室…"两句)会并行播出,
            //   玩家先看完那两句字幕再点头顶感叹号,不会互相打架。
            var talk = PlayerTalkPrompt.Attach(player);
            talk.SetDialogue(
                new PlayerTalkPrompt.Line("这里倒是有很多东西",                "think"),   // 打量
                new PlayerTalkPrompt.Line("来找找看有没有我们想要的东西吧",    "smile"),   // 主动提议
                new PlayerTalkPrompt.Line("说实话，我不喜欢找东西......",       "shy")      // 害羞承认
            );
            talk.OnAllSpoken += () =>
            {
                GameState.SetFlag("upper_chamber_first_arrival_shown", true);
            };
            talk.Show();
        }
    }
}
