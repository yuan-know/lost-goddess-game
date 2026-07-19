// ============================================================================
//  PrologueChamber3Scene.cs —— 第二幕【密室 3(洗礼池)】(D6)
//
//  剧本(docs/序幕落地清单.md §2.3 + docs/序幕脚本.md 第二幕):
//    老人走到楼梯左侧密室 → 若干陶罐 + 池底锁孔。
//    · 打碎陶罐 → 其中一个掉出【钥匙 PotteryKey】
//    · 钥匙插入池底锁孔 → 闪白 + 切【青年 Era】+ 解锁 UnlockedYoung Flag → 一句独白 → 场景切回门厅
//
//  美术未出图:场景全用纯代码"石墙 + 地板"占位。三层视差先复用 TempleEntry(临时借光),
//  等美术给 Chamber3/{bg_far,bg_mid,bg_near}.png 后 SceneRoomBuilder 会自动切过来。
//
//  D6 落地要点:
//    · 3 个陶罐(x=-2/0/+2),点击一个"打碎"消失;运行时随机选一个作为"藏钥匙的"
//    · 拾到钥匙后 → 弹提示"背包里多了一枚陶片钥匙"
//    · 锁孔:x=+6(池底),需 PotteryKey → 播 Cutscene 切青年 → 回门厅
// ============================================================================

using UnityEngine;

namespace LostGoddess.Content
{
    public static class PrologueChamber3Scene
    {
        public const float SpawnX = -6f;   // 从门厅走进来时,老人出生在密室左侧

        public static void Build()
        {
            // 场景:美术已给「神庙一楼密室」专用图,SceneRoomBuilder 会自动加载
            //   Resources/Scenes/TempleChamber1F/{bg_far, prop_pottery, bg_full}。
            //   现只有 2 张分层图(bg_far + prop_pottery),bg_mid/bg_near 缺图 warn 无害。
            var room = SceneRoomBuilder.Build(SceneRoomBuilder.TempleChamber1F);
            room.name = "Room_" + Rooms.Prologue_Chamber3;
            float groundY = SceneRoomBuilder.TempleChamber1F.groundY;

            // 老人 - 按当前 Era(从门厅进来通常是 Old)
            PlayerBuilder.Build(GameState.CurrentEra, groundY, SpawnX);

            // ── 陶罐(全部可交互,右侧一个藏钥匙) ────────────────────
            //  2026-07-19 按示意图接线:沿洗礼池前零散摆放,锁孔在池底正中。
            //  无钥匙陶罐也做成可交互,增加探索感。
            float potteryY = groundY + 0.5f;
            var potteryXs = new float[] { -11f, -8f, -5f, -2f, 2f, 5f, 8f, 11f };
            int keyIndex = Mathf.Abs(GameState.CurrentRoom.GetHashCode()) % potteryXs.Length;
            // 固定让右侧偏中的陶罐藏钥匙(与示意图黄色圈大致对应),用偏移把 hash 结果映射到右半边
            keyIndex = (keyIndex % 3) + potteryXs.Length - 3; // 取最右 3 个之一
            for (int i = 0; i < potteryXs.Length; i++)
            {
                BuildPottery(room.transform, i, new Vector2(potteryXs[i], potteryY),
                    hasKey: (i == keyIndex), groundY: groundY);
            }

            // ── 池底锁孔(策划图正中绿色圆圈) ────────────────────────
            BuildLockhole(room.transform, new Vector2(0f, groundY + 0.3f), groundY);

            // ── 返回 Portal(右端 → 梯子密室,策划图 2026-07-19) ────
            //  陶罐间(左 3)朝右走 = 回梯子密室(左 2),再朝右才回神庙前厅
            BuildBackPortal(room.transform, new Vector2(14f, groundY), groundY,
                            Rooms.Prologue_LadderChamber);

            // 首帧:一句独白引路(仅首次)
            room.AddComponent<PrologueChamber3Director>();
        }

        // ── helpers ─────────────────────────────────────────────────

        static void BuildPottery(Transform parent, int idx, Vector2 pos, bool hasKey, float groundY)
        {
            var go = MakeBlock(parent, $"Interact_Pottery_{idx}", pos, new Vector2(0.7f, 1.0f),
                new Color(0.6f, 0.42f, 0.28f, 0.9f));
            var it = go.AddComponent<Interact_Pottery>();
            it.hasKey = hasKey;
            it.highlightTarget = go.GetComponent<SpriteRenderer>();
            it.interactPoint = MakePoint(go.transform, new Vector2(pos.x, groundY));
        }

        static void BuildLockhole(Transform parent, Vector2 pos, float groundY)
        {
            var go = MakeBlock(parent, "Interact_Lockhole", pos, new Vector2(1.2f, 0.6f),
                new Color(0.25f, 0.28f, 0.35f, 0.9f));
            var it = go.AddComponent<Interact_Lockhole>();
            it.highlightTarget = go.GetComponent<SpriteRenderer>();
            it.interactPoint = MakePoint(go.transform, new Vector2(pos.x - 1.2f, groundY));
        }

        static void BuildBackPortal(Transform parent, Vector2 pos, float groundY, string targetRoom)
        {
            var go = MakeBlock(parent, "Portal_BackTo_" + targetRoom, pos, new Vector2(1.2f, 3.0f),
                new Color(0.15f, 0.15f, 0.25f, 0.5f));
            var portal = go.AddComponent<ScenePortal>();
            portal.targetRoom = targetRoom;
            portal.highlightTarget = go.GetComponent<SpriteRenderer>();
            // interactPoint 站在 Portal 内侧(portal 在右端 → 老人站左侧才触发)
            portal.interactPoint = MakePoint(go.transform, new Vector2(pos.x - 1.5f, groundY));
        }

        static GameObject MakeBlock(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            sr.color = color;
            sr.sortingOrder = 15;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            // 占位期挂个文字标签,让用户一眼看清"哪个方块是啥"
            AddDebugLabel(go, name + $"\n({pos.x:F1},{pos.y:F1})");
            return go;
        }

        /// <summary>在占位方块头顶加文字标签(占位期专用,美术出图后可整块移除)。</summary>
        static void AddDebugLabel(GameObject host, string text)
        {
            var lbl = new GameObject("_debug_label");
            lbl.transform.SetParent(host.transform, false);
            var hs = host.transform.localScale;
            lbl.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, hs.x),
                                                    1f / Mathf.Max(0.01f, hs.y), 1f);
            lbl.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            var tm = lbl.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 40;
            tm.characterSize = 0.05f;
            tm.color = new Color(1f, 0.95f, 0.6f);
            var mr = lbl.GetComponent<MeshRenderer>();
            mr.sortingOrder = 100;
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

    /// <summary>密室 3 首帧一次性独白。</summary>
    public class PrologueChamber3Director : MonoBehaviour
    {
        void Start()
        {
            const string kFlag = "prologue_entered_chamber3";
            if (GameState.GetFlag(kFlag)) return;
            GameState.SetFlag(kFlag, true);

            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);
            var cs = gameObject.AddComponent<Cutscene>();
            cs.Add(new WaitStep(0.4f))
              .Add(new SayTextStep("这里是……洗礼池?那些陶罐里藏着什么。"));
            cs.OnFinished += () => { if (pc != null) pc.SetControllable(true); };
            cs.Play();
        }
    }
}
