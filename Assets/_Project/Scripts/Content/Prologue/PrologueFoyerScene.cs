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

            // ── 交互物 4 件套 ────────────────────────────────────────────
            //  x 坐标是"世界坐标",相机 clamp 到 [-17+ortho, 17-ortho] 范围
            //  interactPoint 都对齐地平线 groundY

            // 展台:门前 x=8(神庙入口画布"门中心"约在 x=8)
            BuildPodium(root.transform, new Vector2(6f, groundY + 0.6f), groundY);

            // 组合工作台(中年拼投影仪):展台左侧 x=2
            BuildAssembleTable(root.transform, new Vector2(2f, groundY + 0.5f), groundY);

            // 石门:x=9,略高于展台
            BuildStoneDoor(root.transform, new Vector2(9f, groundY + 1.6f), groundY);

            // 楼梯废墟:x=-3(神庙入口左侧,靠近门厅左半侧)
            BuildStairs(root.transform, new Vector2(-3f, groundY + 1.2f), groundY);

            // ── 岁月洞察显影物 2 件套 ────────────────────────────────────
            // 壁画显影:x=-8,在楼梯左边墙上,悬浮高度 1.5,老年按 Q 时浮现
            BuildMuralPhantom(root.transform, new Vector2(-8f, groundY + 2.2f));

            // 二楼高亮点:x=-3(与楼梯同 x),悬浮更高,老年按 Q 时脉动闪光
            BuildUpperHallGlow(root.transform, new Vector2(-3f, groundY + 4.2f));

            // ── 场景切换 Portal(左侧密室 3 = 切青年触发) ─────────────────
            // 密室 3:x=-14 附近(场景最左端),暂时用一个 Portal 表示"往密室方向走"
            //  当前 Prologue_Chamber3 场景 D6 才做,先埋 ScenePortal,点击提示"D6 补齐"
            BuildChamber3Portal(root.transform, new Vector2(-15f, groundY), groundY);

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
            return go;
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
            // 首次进入:播 2 句 os + 提示按 Q
            if (GameState.GetFlag(Flags.Prologue_EnteredFoyer))
            {
                // 复入:什么都不播,把控制权还给玩家
                if (PlayerController.Instance != null)
                    PlayerController.Instance.SetControllable(true);
                return;
            }
            GameState.SetFlag(Flags.Prologue_EnteredFoyer, true);

            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);

            var cs = gameObject.AddComponent<Cutscene>();
            cs.Add(new WaitStep(0.5f))
              .Add(new SayStep(Dialogues.prologue_1_01))
              .Add(new SayStep(Dialogues.prologue_1_02))
              .Add(new SayStep(Dialogues.prologue_1_insight_hint));
            cs.OnFinished += () =>
            {
                // 用当前时刻 Instance,避免闭包捕获旧引用
                var p = PlayerController.Instance;
                if (p != null) p.SetControllable(true);
            };
            cs.Play();
        }
    }
}
