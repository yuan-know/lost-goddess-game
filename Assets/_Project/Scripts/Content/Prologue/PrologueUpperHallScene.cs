// ============================================================================
//  PrologueUpperHallScene.cs —— 第二幕【二楼回廊】(D6)
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
            // 场景:先用 TempleEntry 视差占位(等美术出 UpperHall 三层图)
            var room = SceneRoomBuilder.Build(SceneRoomBuilder.TempleEntry);
            room.name = "Room_" + Rooms.Prologue_UpperHall;
            float groundY = SceneRoomBuilder.TempleEntry.groundY;

            // 玩家:从楼梯爬上来的必然是青年 —— 但保守起见,不强制切 Era,
            //       只按当前 Era 建 Player。剧情里必然是 Young。
            PlayerBuilder.Build(GameState.CurrentEra, groundY, SpawnX);

            // ── 铁撬棍(青年拾取,拿了才能撬铁笼) ────────────────────
            BuildCrowbarPickup(room.transform, new Vector2(-8f, groundY + 0.3f), groundY);

            // ── 铁笼 ────────────────────────────────────────────────
            BuildIronCage(room.transform, new Vector2(0f, groundY + 1.0f), groundY);

            // ── 齿轮箱 ──────────────────────────────────────────────
            BuildGearBox(room.transform, new Vector2(5f, groundY + 0.8f), groundY);

            // ── 齿轮机关(触发切场景) ─────────────────────────────
            BuildGearsMechanism(room.transform, new Vector2(10f, groundY + 1.4f), groundY);

            // ── 楼梯口 Portal(回门厅) ────────────────────────────
            BuildStairsPortal(room.transform, new Vector2(-14f, groundY), groundY);

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
            var go = MakeBlock(parent, "Portal_BackToFoyer", pos, new Vector2(1.2f, 3.0f),
                new Color(0.15f, 0.15f, 0.25f, 0.5f));
            var portal = go.AddComponent<ScenePortal>();
            portal.targetRoom = Rooms.Prologue_Foyer;
            portal.highlightTarget = go.GetComponent<SpriteRenderer>();
            portal.interactPoint = MakePoint(go.transform, new Vector2(pos.x + 1.5f, groundY));
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
            if (sr != null) { var c = sr.color; c.a = 0.15f; sr.color = c; }
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
              .Add(new SayTextStep("(二楼回廊——铁笼、齿轮箱、机关整齐排布,像被谁刻意留下的谜题。)"));
            cs.OnFinished += () => { if (pc != null) pc.SetControllable(true); };
            cs.Play();
        }
    }
}
