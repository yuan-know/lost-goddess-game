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

        public static void Build()
        {
            var root = SceneRoomBuilder.Build(SceneRoomBuilder.TempleEntry);
            float groundY = SceneRoomBuilder.TempleEntry.groundY;

            // 老人沿用当前 Era(从第 0 幕过来时是 Old)
            var player = PlayerBuilder.Build(GameState.CurrentEra, groundY, SpawnX);

            // 强制朝右(立绘默认朝左,scale.x=-|s| 表示朝右)
            //  ── 让老人一进门厅就面朝展台/大门方向,视觉上"从荒山走进庙门"的连续感
            if (player != null)
            {
                var s = player.transform.localScale;
                s.x = -Mathf.Abs(s.x);
                player.transform.localScale = s;
            }

            // ── 交互物摆位:严格对齐 TempleEntry 背景的真实视觉锚点 ─────────
            //  背景图 3400×1200 px, PPU=100, 图中心=世界原点 → 像素 X 换算世界 X:
            //    worldX = (pixelX - 1700) / 100
            //  从视觉分析拿到的关键锚点:
            //    · 拱形石门中心 3120 px    → 世界 X = +14.2  (画面右端 82%)
            //    · 门口台阶      3002~3384 → 世界 X ≈ +13~+17
            //    · 平台中央空旷  1200~2200 → 世界 X ≈ -5~+5  (适合摆工作台)
            //    · 左角断柱残碑  320~430   → 世界 X ≈ -13.5  (适合摆展台/壁画)
            //  动线设计:老人 SpawnX=-8,进入门厅时**左侧**能看到 Portal/壁画/展台
            //    (回头一望的洞察氛围), **右侧**是一路走过去的目标(工作台→楼梯→石门)。
            //    朝右主动线保留"从荒山走进庙门"的推进感,不打乱。

            // 展台:左角断柱旁 x=-11(比断柱残碑靠右一点点,方便和老人交互),矮墩式石台
            BuildPodium(root.transform, new Vector2(-11f, groundY + 0.6f), groundY);

            // 组合工作台(中年拼投影仪):平台中央空旷区 x=0
            BuildAssembleTable(root.transform, new Vector2(0f, groundY + 0.5f), groundY);

            // 石门:严格对齐拱门中心 x=+14.2,高门位置
            BuildStoneDoor(root.transform, new Vector2(14.2f, groundY + 1.6f), groundY);

            // 楼梯废墟:门左侧平台 x=+8("往二楼去"的动线锚点),占位期用矮墩表示
            //   等美术在这个位置画一段坍塌石阶就自动对齐了
            BuildStairs(root.transform, new Vector2(8f, groundY + 1.2f), groundY);

            // ── 岁月洞察显影物 2 件套 ────────────────────────────────────
            // 壁画显影:左侧断柱表面 x=-13(与远景断柱严格对齐),悬浮墙面高度
            BuildMuralPhantom(root.transform, new Vector2(-13f, groundY + 2.2f));

            // 二楼高亮点:楼梯废墟正上方 x=+8,悬浮更高,老年按 Q 时脉动闪光
            //   视觉逻辑:老人朝楼梯看时,头顶浮起"通往二楼"的高光
            BuildUpperHallGlow(root.transform, new Vector2(8f, groundY + 4.2f));

            // ── 场景切换 Portal(左侧密室 3 = 切青年触发) ─────────────────
            // 密室 3:场景最左端 x=-15.5,视觉上"从门厅退到密林深处"
            BuildChamber3Portal(root.transform, new Vector2(-15.5f, groundY), groundY);

            // ── 首帧演出 ────────────────────────────────────────────────
            root.AddComponent<PrologueFoyerDirector>();
        }

        // ── 各交互物的建造 helper ────────────────────────────────────────

        static GameObject BuildPodium(Transform parent, Vector2 pos, float groundY)
        {
            // 占位方块:黄褐色矮墩(等美术出图后 SpriteRenderer 换 sprite 即可)
            var go = MakeBlock(parent, "Interact_Podium", pos, new Vector2(1.2f, 0.9f),
                new Color(0.55f, 0.42f, 0.25f, 0.85f));
            var interact = go.AddComponent<Interact_Podium>();
            interact.highlightTarget = go.GetComponent<SpriteRenderer>();
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

        static GameObject BuildMuralPhantom(Transform parent, Vector2 pos)
        {
            var go = new GameObject("Insight_MuralPhantom");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Resources.Load<Sprite>("Closeups/mural_prologue");
            sr.sortingOrder = 40;   // 在 bg_mid(-20) 之前、bg_near(60) 之后
            sr.color = new Color(1f, 1f, 1f, 0f);  // 起始透明
            // 特写图原本是"全屏立绘"级别的大尺寸(用作 close-up 面板),
            // 当"墙上壁画"用时必须大幅缩小,否则一淡入就吞掉半个屏幕。
            go.transform.localScale = Vector3.one * 0.12f;
            // 老年 Q 键洞察时淡入到 ~0.6(幽幽的显影感,而不是实体贴画)
            var ph = go.AddComponent<InsightPhantom>();
            ph.visibleAlpha = 0.6f;
            return go;
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
            sr.color = color;
            sr.sortingOrder = 15;  // 在 bg_mid(-20) 之前、老人 rigged 部位(29~41) 之下
                                     //   → 老人可站在门/展台前;bg_near(60) 依然遮住
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            // 因为 sprite PPU=2(2px=1单位),BoxCollider2D 自动跟 sprite bounds → 已 OK

            // 占位期挂个文字标签,一眼看清"谁在哪"(等美术出图后可整块移除)
            AddDebugLabel(go, name + $"\n({pos.x:F1},{pos.y:F1})");
            return go;
        }

        /// <summary>在占位方块头顶加一个文字标签(TextMesh),显示名字 + 世界坐标。</summary>
        static void AddDebugLabel(GameObject host, string text)
        {
            var lbl = new GameObject("_debug_label");
            lbl.transform.SetParent(host.transform, false);
            // host 有 localScale(方块的宽高),TextMesh 要抵消掉否则字被拉伸
            var hs = host.transform.localScale;
            lbl.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, hs.x),
                                                    1f / Mathf.Max(0.01f, hs.y), 1f);
            // 放在方块正上方(local y = 0.5 是 sprite 顶,再往上一点)
            lbl.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            var tm = lbl.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 40;
            tm.characterSize = 0.05f;
            tm.color = new Color(1f, 0.95f, 0.6f);
            var mr = lbl.GetComponent<MeshRenderer>();
            mr.sortingOrder = 100;  // 盖在所有背景/占位物之上
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
            yield return ShowAndWait(Dialogues.prologue_1_01);
            yield return ShowAndWait(Dialogues.prologue_1_02);
            yield return ShowAndWait(Dialogues.prologue_1_insight_hint);
        }

        System.Collections.IEnumerator ShowAndWait(string dialogueId)
        {
            bool done = false;
            DialogueSystem.Show(dialogueId, () => done = true);
            while (!done) yield return null;
        }
    }
}
