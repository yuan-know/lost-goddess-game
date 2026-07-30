// ============================================================================
//  PrologueFoyerScene.cs —— 第一幕【神庙门厅】完整落地版(D5)
//
//  按 docs/序幕落地清单.md §2.2 摆:
//    · 展台 Interact_Podium(前置,门锁着提示)
//    · 石门 Interact_StoneDoor(需 Prologue_DoorOpen flag)
//    · 楼梯废墟 Interact_Stairs(老年不行,青年才能爬)
//    · 岁月洞察壁画 Insight_MuralPhantom(mural_prologue.png 显影)
//    · 二楼高亮点 Insight_UpperHallGlow(纯色方块脉冲)
//    · 密室 Portal:去 Chamber3(左侧, 拾取陶罐钥匙→切青年)
//                    去 Chamber1(右侧, 拾取遗失物,策划稿保留但当前不启用)
//
//  首帧演出:两句内心 os → 若首次未使用岁月洞察,顶部弹提示"按 Q 使用【岁月洞察】"
// ============================================================================

using UnityEngine;

namespace LostGoddess.Content
{
    public static class PrologueFoyerScene
    {
        // TempleEntry 世界宽 34 单位,左端 -17,相机可动 X min ≈ -8.11(ortho 5 × aspect 1.78)
        // 让老人一进门厅就在屏幕左侧可见:SpawnX 卡在 -8(相机边界内 0.1 单位)
        public const float SpawnX = -8f;

        // 存储左侧指引旗帜,点击看完展台特写后激活
        static GameObject _leftGuideFlag;

        public static void Build()
        {
            // 场景:第一幕【神庙门厅内景】—— 黄铜机械锁死的巨大石门 + 门前展台 + 楼梯废墟 + 浮雕墙
            //   剧本序列 = 黑暗森林 → 神庙入口 TempleGate(石拱门外景过场)→ **本场景 内景 TempleFoyer**
            //  美术:Resources/Scenes/TempleFoyer/bg_unlit_full.png (关灯全景)
            //  ⚠ 开灯版(bg_lit_full / bg_lit_bg)和展台单件(prop_podium_lit/unlit)等策划确认亮灯触发条件后再接线
            //  ⚠ 门厅内的交互物坐标(展台/大门/楼梯/壁画/密室3 Portal)是按旧 TempleEntry 背景算的 ——
            //   切图后需按 TempleFoyer 视觉锚点重摆(等策划答复展台位置/楼梯位置)
            var root = SceneRoomBuilder.Build(SceneRoomBuilder.TempleFoyer);
            root.name = "Room_" + Rooms.Prologue_Foyer;
            float groundY = SceneRoomBuilder.TempleFoyer.groundY;

            // 2026-07-19 改进:根据进入方向决定老人出生位置和朝向
            float spawnX;
            bool faceRight;
            string enterDir = SceneLoader.EnterDirection;

            // 特殊情况:从神庙入口(石拱门)进入 → 出生在场景中间偏左(门口位置)
            if (enterDir == "gate")
            {
                spawnX = -3f;  // 中间偏左,模拟从石拱门走进来
                faceRight = true;
            }
            else if (enterDir == "left")
            {
                // 从左边（梯子密室）进入 → 出生在左边缘朝右
                spawnX = -12f;
                faceRight = true;
            }
            else if (enterDir == "right")
            {
                // 从右边（齿轮间）进入 → 出生在右边缘朝左
                spawnX = 12f;
                faceRight = false;
            }
            else
            {
                // 默认:中间偏左
                spawnX = SpawnX;  // -8
                faceRight = true;
            }

            var player = PlayerBuilder.Build(GameState.CurrentEra, groundY, spawnX);

            // 设置朝向
            if (player != null)
            {
                var pc = player.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.spriteFacesRight = false;  // 立绘默认朝左
                    var s = player.transform.localScale;
                    s.x = Mathf.Abs(s.x) * (faceRight ? -1f : 1f);  // 朝右时scale.x为负
                    player.transform.localScale = s;
                }
            }

            // ── 交互物摆位:严格对齐 TempleFoyer 背景 + 策划切换图(2026-07-19) ──
            //  背景图 3400×1200 px, PPU=100, 图中心=世界原点 → 像素 X 换算世界 X:
            //    worldX = (pixelX - 1700) / 100
            //  策划切换图约束:前厅里**只有一个石质展台**,其他都是纯装饰(壁画/烛台/双开石门都不能点)。
            //  拓扑:
            //    朝左走到屏幕左边缘 → 切场景到梯子密室(LadderChamber)
            //    朝右走到屏幕右边缘 → 切场景到齿轮骨骸间(GearRoom)
            //  ⚠ 展台真实像素坐标需要按新 TempleFoyer 背景视觉锚点校准 —— 目前放场景中央 x=0

            // 展台:场景中央,老年沿用旧 Interact_Podium 逻辑(点它 = 洞察 os / 显影壁画等)
            BuildPodium(root.transform, new Vector2(0f, groundY + 0.6f), groundY);

            // 场景左边缘 Portal:走到左端点击 → 梯子密室
            BuildEdgePortal(root.transform, Rooms.Prologue_LadderChamber, "Portal_Left_Ladder",
                new Vector2(-15f, groundY + 1.5f), groundY, isLeft: true);

            // 场景右边缘 Portal:走到右端点击 → 齿轮骨骸间
            BuildEdgePortal(root.transform, Rooms.Prologue_GearRoom, "Portal_Right_Gear",
                new Vector2(15f, groundY + 1.5f), groundY, isLeft: false);

            // 壁画幻影:洞察后显示光圈提示，点击整面壁画区域打开特写（2026-07-21 使用美术资源自动定位）
            //   加载壁画单独图层，光圈自动匹配图层的实际尺寸和位置
            BuildMuralHighlight(root.transform, groundY);

            // 2026-07-21 新增:放大镜图标悬浮在展台上方,点击展台看特写后自动销毁
            // 2026-07-21 用户要求调高 → groundY + 4.0f
            BuildExplorationGlass(root.transform, pos: new Vector2(0f, groundY + 4.0f));

            // 预留下左侧指引旗帜,点击看完特写后激活
            _leftGuideFlag = BuildLeftGuideFlag(root.transform, groundY);

            // ── 首帧演出 ────────────────────────────────────────────────
            root.AddComponent<PrologueFoyerDirector>();
        }

        // ── 各交互物的建造 helper ────────────────────────────────────────

        static GameObject BuildPodium(Transform parent, Vector2 pos, float groundY)
        {
            // 2026-07-21 三修:展台单件图与背景层用同一 pivot 补偿摆位,但**点击热区不能挂在图 GameObject 上**——
            //   父 transform.y = -pivotOffsetY(如 -6),再叠 BoxCollider2D.offset 世界坐标就跑到屏幕外了。
            //   做法:图挂父 GO(为了 pivot 补偿),collider 挂独立子 GO(位置直接 = 世界 pos),
            //   Interact_Podium 也放在子 GO 上,ClickInputManager 用 GetComponentInParent 能找到——
            //   但为了简单,直接把整个 Interactable + Collider 都放子 GO,和图无关。
            var art = Resources.Load<Sprite>("Scenes/TempleFoyer/prop_podium_unlit");

            // ── 图层(纯视觉,无碰撞、无脚本)──
            var artGo = new GameObject("Interact_Podium_Art");
            artGo.transform.SetParent(parent, false);
            var sr = artGo.AddComponent<SpriteRenderer>();
            if (art != null)
            {
                sr.sprite = art;
                sr.color = Color.white;
                // pivot 补偿:pivot=BottomCenter 时 bounds.center.y=+halfH,transform.y 要取 -halfH 才能让图中心落在 y=0
                float pivotOffsetY = art.bounds.center.y;
                artGo.transform.position = new Vector3(0f, 0f - pivotOffsetY, 0f);
                artGo.transform.localScale = Vector3.one;
            }
            else
            {
                sr.sprite = SolidSprite();
                sr.color = new Color(0.55f, 0.42f, 0.25f, 1f);
                artGo.transform.position = pos;
                artGo.transform.localScale = new Vector3(2.5f, 1.8f, 1f);
            }
            sr.sortingOrder = 500;

            // ── 交互层(collider + Interact_Podium,位置直接是世界 pos,不受父 pivot 补偿影响)──
            var go = new GameObject("Interact_Podium");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);

            // 2026-07-21 扩点击区域:原 2.5×2.3 → 3.2×2.8 方便点击
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(3.2f, 2.8f);
            col.offset = Vector2.zero;

            var interact = go.AddComponent<Interact_Podium>();
            interact.highlightTarget = sr;   // 高亮的是 art 层
            // 亮度调成两倍:HDR 允许 RGB>1,黄色高亮 ×2
            interact.highlightColor = new Color(2f, 2f, 1.2f);
            interact.interactPoint = MakePoint(go.transform, new Vector2(pos.x - 1.2f, groundY));
            return go;
        }

        static GameObject BuildAssembleTable(Transform parent, Vector2 pos, float groundY)
        {
            // 占位方块:灰绿色矮工作台
            var go = MakeBlock(parent, "Interact_Assemble_Middle", pos, new Vector2(1.4f, 0.7f),
                new Color(0.35f, 0.4f, 0.3f, 0.85f));
            var interact = go.AddComponent<Interact_Assemble_Middle>();
            interact.highlightTarget = go.GetComponent<SpriteRenderer>();
            interact.interactPoint = MakePoint(go.transform, new Vector2(pos.x - 1.2f, groundY));
            return go;
        }

        static GameObject BuildStoneDoor(Transform parent, Vector2 pos, float groundY)
        {
            // 占位方块:深棕色高门
            var go = MakeBlock(parent, "Interact_StoneDoor", pos, new Vector2(1.6f, 3.2f),
                new Color(0.35f, 0.28f, 0.22f, 0.7f));
            var interact = go.AddComponent<Interact_StoneDoor>();
            interact.highlightTarget = go.GetComponent<SpriteRenderer>();
            interact.interactPoint = MakePoint(go.transform, new Vector2(pos.x - 1.5f, groundY));
            return go;
        }

        static GameObject BuildStairs(Transform parent, Vector2 pos, float groundY)
        {
            // 占位方块:灰蓝坡状(实际上是矩形)
            var go = MakeBlock(parent, "Interact_Stairs", pos, new Vector2(2.0f, 1.8f),
                new Color(0.35f, 0.4f, 0.5f, 0.7f));
            var interact = go.AddComponent<Interact_Stairs>();
            interact.highlightTarget = go.GetComponent<SpriteRenderer>();
            interact.interactPoint = MakePoint(go.transform, new Vector2(pos.x + 1.2f, groundY));
            return go;
        }

        static GameObject BuildChamber3Portal(Transform parent, Vector2 pos, float groundY)
        {
            var go = MakeBlock(parent, "Portal_ToChamber3", pos, new Vector2(1.2f, 3.0f),
                new Color(0.15f, 0.15f, 0.25f, 0.5f));
            var portal = go.AddComponent<ScenePortal>();
            portal.targetRoom = Rooms.Prologue_Chamber3;
            portal.successDialogueId = "";  // 直接跳
            portal.highlightTarget = go.GetComponent<SpriteRenderer>();
            portal.interactPoint = MakePoint(go.transform, new Vector2(pos.x + 1.5f, groundY));
            return go;
        }

        // 场景左/右边缘的"看不见的判定块"Portal ── 策划切换图 2026-07-19 新增
        //   size 3×5.5,sortingOrder=70,占位期 α=0.10 淡色 debug tint(左蓝右红),
        //   正式期把 alpha 改 0 即可(不改逻辑)。
        static GameObject BuildEdgePortal(Transform parent, string targetRoom, string name,
                                          Vector2 pos, float groundY, bool isLeft)
        {
            const float w = 3.0f, h = 5.5f;
            float centerY = groundY + h * 0.5f;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, centerY, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            // 2026-07-21 用户要求完全隐藏占位色差方块 → alpha=0
            sr.color = isLeft ? new Color(0.3f, 0.6f, 1f, 0f)
                              : new Color(1f, 0.5f, 0.4f, 0f);
            sr.sortingOrder = 70;
            go.transform.localScale = new Vector3(w, h, 1f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var portal = go.AddComponent<ScenePortal>();
            portal.targetRoom = targetRoom;
            portal.successDialogueId = "";
            portal.highlightTarget = sr;
            portal.fadeTime = 0.6f;
            portal.triggerOnEnter = true;  // 2026-07-23 统一为走入触发,与 LinkRoom 一致(点击容易 miss)

            // 走入触发模式下,interactPoint 不需要了(路径由 Update() 里的 X 区间判定接管)
            // 但保留字段为空,避免基类 walkToBeforeInteract 误走去 null 位置。
            return go;
        }

        static GameObject BuildMuralHighlight(Transform parent, float groundY)
        {
            // 加载壁画图层资源 (场景中显示的图层，与背景完全贴合)
            // 背景图和壁画图层都是 3400×1200 px，PPU=100，必须使用完全相同的渲染方式
            var muralSprite = Resources.Load<Sprite>("Scenes/TempleFoyer/murals_left_right");

            if (muralSprite == null)
            {
                Debug.LogWarning("[PrologueFoyerScene] 未找到壁画图层资源 'Scenes/TempleFoyer/murals_left_right'");
                return null;
            }

            // 创建壁画图层 GameObject
            var artGo = new GameObject("Layer_Mural");
            artGo.transform.SetParent(parent, false);

            var sr = artGo.AddComponent<SpriteRenderer>();
            sr.sprite = muralSprite;
            sr.color = Color.white;
            sr.sortingOrder = -15;  // 在背景 bg_unlit_full(-20) 之上，展台(500)和人物之下

            // 【关键】与 SceneRoomBuilder.BuildLayer 完全相同的位置计算逻辑：
            // imageCenterY = 0f (背景图中心在世界原点)
            // transform.y = centerY - pivotOffsetY
            float imageCenterY = 0f;
            float pivotOffsetY = muralSprite.bounds.center.y;
            artGo.transform.position = new Vector3(0f, imageCenterY - pivotOffsetY, 0f);
            artGo.transform.localScale = new Vector3(1f, 1f, 1f);

            // 添加脉冲效果：直接操作 SpriteRenderer 的颜色做呼吸效果
            var pulse = artGo.AddComponent<MuralPulseEffect>();
            pulse.targetRenderer = sr;

            // 添加监听Q键的控制器组件到壁画图层本身
            var controller = artGo.AddComponent<MuralInsightController>();
            controller.pulseEffect = pulse;

            // 估算壁画位置（基于场景原图）：
            // 左侧壁画：场景左侧约1/4处
            // 右侧壁画：场景右侧约1/4处
            float muralWidth = 5f;   // 壁画宽度（世界单位）
            float muralHeight = 6f;  // 壁画高度（世界单位）
            float leftX = -10f;      // 左侧壁画中心X坐标
            float rightX = 10f;      // 右侧壁画中心X坐标
            float muralY = groundY + 3f;  // 壁画中心Y坐标（地面上方3单位）

            // 创建左侧壁画点击区域（alpha=0,正式版隐藏调试方块）
            var leftClickArea = MakeBlock(parent, "MuralClickArea_Left",
                new Vector2(leftX, muralY),
                new Vector2(muralWidth, muralHeight),
                new Color(0f, 1f, 0f, 0f));  // 完全透明
            leftClickArea.GetComponent<SpriteRenderer>().sortingOrder = 100;  // 显示在最上层
            var leftCol = leftClickArea.AddComponent<BoxCollider2D>();
            leftCol.isTrigger = true;
            var leftHandler = leftClickArea.AddComponent<MuralClickZone>();
            leftHandler.side = "left";
            leftHandler.pulseEffect = pulse;

            // 创建右侧壁画点击区域（alpha=0,正式版隐藏调试方块）
            var rightClickArea = MakeBlock(parent, "MuralClickArea_Right",
                new Vector2(rightX, muralY),
                new Vector2(muralWidth, muralHeight),
                new Color(1f, 0f, 0f, 0f));  // 完全透明
            rightClickArea.GetComponent<SpriteRenderer>().sortingOrder = 100;  // 显示在最上层
            var rightCol = rightClickArea.AddComponent<BoxCollider2D>();
            rightCol.isTrigger = true;
            var rightHandler = rightClickArea.AddComponent<MuralClickZone>();
            rightHandler.side = "right";
            rightHandler.pulseEffect = pulse;

            Debug.Log($"[PrologueFoyerScene] ✓ 壁画图层已加载: {muralSprite.name}, pivotOffset={pivotOffsetY:F2}, position=({artGo.transform.position.x:F2},{artGo.transform.position.y:F2})");
            Debug.Log($"[PrologueFoyerScene] 左侧点击区域: pos=({leftX},{muralY}), size=({muralWidth}×{muralHeight})");
            Debug.Log($"[PrologueFoyerScene] 右侧点击区域: pos=({rightX},{muralY}), size=({muralWidth}×{muralHeight})");

            return artGo;
        }

        static GameObject BuildUpperHallGlow(Transform parent, Vector2 pos)
        {
            var go = new GameObject("Insight_UpperHallGlow");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            sr.color = new Color(1f, 0.9f, 0.4f, 0f);  // 暖黄光斑
            sr.sortingOrder = 40;
            go.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
            var pulse = go.AddComponent<InsightPulseHighlight>();
            pulse.visibleAlpha = 0.75f;
            pulse.pulseAmplitude = 0.25f;
            pulse.pulseCycle = 1.4f;
            return go;
        }

        // ── 纯代码方块占位工具 ───────────────────────────────────────────
        static GameObject MakeBlock(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            // 2026-07-19 打包Demo：占位方块设为透明
            sr.color = new Color(color.r, color.g, color.b, 0f);  // alpha = 0，完全透明
            sr.sortingOrder = 15;  // 在 bg_mid(-20) 之前、老人 rigged 部位(29~41) 之下
                                     //   → 老人可站在门/展台前;bg_near(60) 依然遮住
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            // 因为 sprite PPU=2(2px=1单位),BoxCollider2D 自动跟 sprite bounds → 已 OK

            // 2026-07-19 打包Demo：移除调试标签
            // AddDebugLabel(go, name + $"\n({pos.x:F1},{pos.y:F1})");
            return go;
        }

        /// <summary>在占位方块头顶加一个文字标签(TextMesh),显示名字 + 世界坐标。</summary>
        static void AddDebugLabel(GameObject host, string text)
        {
            // 2026-07-19 打包Demo：禁用调试标签
            return;
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

        /// <summary>用户看完展台特写后调用:显示左侧指引旗帜,销毁放大镜提示。</summary>
        public static void OnPodiumCloseupDone()
        {
            if (_explorationGlass != null)
                Object.Destroy(_explorationGlass);
            if (_leftGuideFlag != null)
                _leftGuideFlag.SetActive(true);
        }

        static GameObject _explorationGlass;

        /// <summary>在展台上方放探索放大镜图标(浮动效果)。</summary>
        static void BuildExplorationGlass(Transform parent, Vector2 pos)
        {
            // 2026-07-25 如果玩家已获得光幕投影仪且谜题未解开 → 再次显示放大镜指引安装
            bool hasProjectorPhase =
                InventorySystem.Has(Items.LightProjector) &&
                !GameState.GetFlag(Flags.Prologue_ProjectorSolved);

            // 如果已经点击过展台且不是投影仪阶段，不再创建放大镜
            const string kFlag = "prologue_foyer_podium_clicked";
            if (GameState.GetFlag(kFlag) && !hasProjectorPhase)
            {
                _explorationGlass = null;
                return;
            }

            // 用户反馈:文件名和内容搞反了 → triangle_flag.png实际存的是放大镜,magnifying_glass实际存的是旗帜
            var sprite = Resources.Load<Sprite>("UI/Icons/triangle_flag");
            if (sprite == null)
            {
                Debug.LogError("[PrologueFoyerScene] ❌ 找不到放大镜资源 Resources/UI/Icons/triangle_flag —— 请检查文件路径!");
                // 没找到图标也没关系，不影响展台交互
                _explorationGlass = null;
                return;
            }
            Debug.Log($"[PrologueFoyerScene] ✓ 成功加载放大镜图标: {sprite.name}");

            _explorationGlass = new GameObject("ExplorationGlass_Magnifying");
            _explorationGlass.transform.SetParent(parent, false);
            _explorationGlass.transform.position = pos;

            var sr = _explorationGlass.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 55;    // 展台之上
            // 放大镜图标实际 PPU=300,原始像素 → 调整到合适大小
            _explorationGlass.transform.localScale = Vector3.one * 0.8f;

            // 浮动动画
            var floatAnim = _explorationGlass.AddComponent<FloatingIndicator>();
            floatAnim.amplitude = 0.15f;
            floatAnim.speed = 2.0f;

            // 点击放大镜也能打开展台特写(和点展台一样),方便玩家
            var col = _explorationGlass.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            // collider size = 0.8×1.2 适应图标尺寸
            col.size = new Vector2(0.8f, 1.2f);

            // 让点击也触发特写
            var interact = _explorationGlass.AddComponent<Interact_Podium>();
            interact.highlightTarget = sr;
        }

        /// <summary>在场景最左侧建指引旗帜,默认 inactive,看完展台特写后激活。</summary>
        static GameObject BuildLeftGuideFlag(Transform parent, float groundY)
        {
            // EdgeFlagPortal自己会检查flag，但如果还没看展台就需要预先隐藏
            const string kFlag = "prologue_foyer_podium_clicked";
            // 2026-07-24 引导链:从残骸间(收齐撬棍+扳手)回到前厅时也要显示左侧旗帜 → 梯子密室,
            //   指引玩家继续往梯子密室 → 二楼密室。所以"看过展台"或"已收齐两件工具"任一满足即显示。
            bool cameFromChamber1 = InventorySystem.Has(Items.Crowbar) && InventorySystem.Has(Items.Wrench);
            bool shouldShow = GameState.GetFlag(kFlag) || cameFromChamber1;

            // 用户反馈:文件名和内容搞反了 → triangle_flag.png实际存的是放大镜,magnifying_glass实际存的是旗帜
            var sprite = Resources.Load<Sprite>("UI/Icons/magnifying_glass");
            if (sprite == null)
            {
                Debug.LogError("[PrologueFoyerScene] ❌ 找不到旗帜资源 Resources/UI/Icons/magnifying_glass —— 请检查文件路径!");
                return null;
            }
            Debug.Log($"[PrologueFoyerScene] ✓ 成功加载三角形旗帜: {sprite.name}");

            var go = new GameObject("LeftGuideFlag_Triangle");
            go.transform.SetParent(parent, false);
            // 放在场景最左侧 walkable area 边缘,地面上方 1.2 单位
            go.transform.position = new Vector3(-13.5f, groundY + 1.2f, 0f);
            go.SetActive(shouldShow);  // 看过展台才显示
            // 放到 Ignore Raycast 层 —— 物理检测直接跳过，绝对不阻挡点击
            go.layer = 2; // Layer 2 = Ignore Raycast

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 50;
            // 图标实际 PPU=300,原始像素 → 不需要再缩小太多,调整到合适大小
            go.transform.localScale = Vector3.one * 1.5f;

            // Unity导入Sprite时会自动生成PolygonCollider2D（根据alpha轮廓）
            //   必须销毁自动生成的碰撞体，否则阻挡下方地面点击
            //   触发完全由代码距离检测，不需要任何碰撞体
            Collider2D[] allCols = go.GetComponents<Collider2D>();
            foreach (var col in allCols) UnityEngine.Object.Destroy(col);

            // 浮动动画
            var floatAnim = go.AddComponent<FloatingIndicator>();
            floatAnim.amplitude = 0.18f;
            floatAnim.speed = 2.2f;

            // 添加触发切场景（代码检测距离，不需要碰撞体）
            var portal = go.AddComponent<EdgeFlagPortal>();
            portal.triggerRadius = 1.5f;
            portal.targetRoom = Rooms.Prologue_LadderChamber;
            portal.enterDirection = "right";
            // 2026-07-24 【根因修复】此旗帜有两个引导阶段共用左边缘:
            //   ① 老年看完展台(podium_clicked)→ 指引去梯子密室探索;
            //   ② 青年收齐撬棍+扳手回到前厅 → 指引继续去梯子密室爬梯上二楼。
            //   若共用默认 flag(edge_flag_triggered_Prologue_LadderChamber_right),阶段①触发后
            //   会永久置位,导致阶段②的旗帜在 Start() 里被当成"已触发"直接销毁 → 青年看不到旗帜。
            //   解法:两个阶段各用独立持久化 flag,互不干扰,各自触发一次后各自不再出现。
            portal.triggerFlagOverride = cameFromChamber1
                ? "edge_flag_foyer_to_ladder_young_tools"
                : "edge_flag_foyer_to_ladder_old_podium";

            return go;
        }
    }

    /// <summary>门厅的首帧演出 + 一次性提示"按 Q 洞察"。</summary>
    public class PrologueFoyerDirector : MonoBehaviour
    {
        void Start()
        {
            // 首次进入:播 3 句 os(边走边看,不锁老人)
            if (GameState.GetFlag(Flags.Prologue_EnteredFoyer))
            {
                if (PlayerController.Instance != null)
                    PlayerController.Instance.SetControllable(true);
                return;
            }
            GameState.SetFlag(Flags.Prologue_EnteredFoyer, true);

            // 【重要】不用 Cutscene(Cutscene.Run 会 SetControllable(false) 锁老人)。
            // 3 句氛围对白用协程按序播,老人始终可控,玩家可以边听边走。
            if (PlayerController.Instance != null)
                PlayerController.Instance.SetControllable(true);
            StartCoroutine(PlayIntroDialogue());
        }

        System.Collections.IEnumerator PlayIntroDialogue()
        {
            yield return new WaitForSeconds(0.5f);
            // 2026-07-21 替换首帧台词:用户提供文案,前两句纯文案无表情,最后两句老年对话带表情
            //   要求:文案不出人物表情特写,仅人物对话出
            bool done = false;

            // 1. 纯文案
            DialogueSystem.ShowText("昏暗的神庙前厅，前方有一扇锁死的巨大石门，面前还有一个黯淡的展台。零散的青苔铺在地面，青绿的藤曼缠绕在壁画上。", () => done = true);
            while (!done) yield return null;
            done = false;

            // 2. 纯文案
            DialogueSystem.ShowText("神庙像一口深井一样吞噬掉了所有声音，空旷的前厅里，你只能感知到自己厚重的喘息声和急促的心跳声。", () => done = true);
            while (!done) yield return null;
            done = false;

            // 3. 老年对话 → 带表情
            DialogueSystem.ShowMonologue("什么声音都没有，风声、雷声好像全被隔绝在这座庙之外了……太安静了……", () => done = true);
            while (!done) yield return null;
            done = false;

            // 4. 老年对话 → 带表情
            DialogueSystem.ShowMonologue("门是锁死的，让我们看看这个展台……", () => done = true);
            while (!done) yield return null;
        }

        // 保留原函数兼容
        System.Collections.IEnumerator ShowAndWait(string dialogueId)
        {
            bool done = false;
            DialogueSystem.Show(dialogueId, () => done = true);
            while (!done) yield return null;
        }
    }
}
