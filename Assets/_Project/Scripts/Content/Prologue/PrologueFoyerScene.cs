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
            // 场景:第一幕【神庙门厅内景】—— 黄铜机械锁死的巨大石门 + 门前展台 + 楼梯废墟 + 浮雕墙
            //   剧本序列 = 黑暗森林 → 神庙入口 TempleGate(石拱门外景过场)→ **本场景 内景 TempleFoyer**
            //  美术:Resources/Scenes/TempleFoyer/bg_unlit_full.png (关灯全景)
            //  ⚠ 开灯版(bg_lit_full / bg_lit_bg)和展台单件(prop_podium_lit/unlit)等策划确认亮灯触发条件后再接线
            //  ⚠ 门厅内的交互物坐标(展台/大门/楼梯/壁画/密室3 Portal)是按旧 TempleEntry 背景算的 ——
            //   切图后需按 TempleFoyer 视觉锚点重摆(等策划答复展台位置/楼梯位置)
            var root = SceneRoomBuilder.Build(SceneRoomBuilder.TempleFoyer);
            root.name = "Room_" + Rooms.Prologue_Foyer;
            float groundY = SceneRoomBuilder.TempleFoyer.groundY;

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

            // ── 交互物摆位 ── 【暂时清空】 ────────────────────────────────
            //  原本按旧 TempleEntry 背景算的坐标已确认全部错位(展台/工作台/楼梯/石门/壁画/二楼光斑/Portal),
            //  切图到 TempleFoyer 新背景后需要重摆。等策划答复以下 4 个锚点再一行行接回:
            //    · 展台在门厅哪一侧?老人视线焦点在哪?
            //    · 楼梯废墟在背景图哪个像素范围?
            //    · 石门(黄铜机械锁死)在图上的中心 x?
            //    · 岁月洞察壁画贴哪面墙?
            //  下方 Build*() helper 全部保留,新坐标定下来一行调用即可复活。
            //  当前 Foyer 只保留:背景 + 老人 + 首帧独白 + Q 键洞察提示。玩家进来能走能听对白,
            //   但没有任何点击目标——避免"道具浮空/穿模/落在墙里"的错乱观感。
            //
            //  BuildPodium(root.transform, new Vector2(???, groundY + ???), groundY);
            //  BuildAssembleTable(root.transform, new Vector2(???, groundY + ???), groundY);
            //  BuildStoneDoor(root.transform, new Vector2(???, groundY + ???), groundY);
            //  BuildStairs(root.transform, new Vector2(???, groundY + ???), groundY);
            //  BuildMuralPhantom(root.transform, new Vector2(???, groundY + ???));
            //  BuildUpperHallGlow(root.transform, new Vector2(???, groundY + ???));
            //  BuildChamber3Portal(root.transform, new Vector2(???, groundY), groundY);

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
            // 优先用美术新版壁画特写(3360×1184);缺图时退回旧版
            var newSp = Resources.Load<Sprite>("Closeups/mural_prologue_v2");
            sr.sprite = newSp != null ? newSp : Resources.Load<Sprite>("Closeups/mural_prologue");
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
