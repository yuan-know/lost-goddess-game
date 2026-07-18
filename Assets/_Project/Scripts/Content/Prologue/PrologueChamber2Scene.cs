// ============================================================================
//  PrologueChamber2Scene.cs —— 第三幕【密室 2(棺材)】(D7)
//
//  剧本(docs/序幕落地清单.md §2.5 + docs/序幕脚本.md 第三幕):
//    青年拨齿轮机关 → 陷入棺材密室 → 棺材小游戏 → 解开 → 切【中年 Era】→ 回门厅
//
//  D7 占位:
//    · 棺材:点 3 次解开(每次进度 +1/3),第 3 次触发 Cutscene 切中年
//    · 小游戏 D8+ 打磨真实解谜(3 层圆盘/滑块)
//
//  背景:先复用 TempleEntry 视差占位。
// ============================================================================

using UnityEngine;

namespace LostGoddess.Content
{
    public static class PrologueChamber2Scene
    {
        public const float SpawnX = -6f;

        public static void Build()
        {
            var room = SceneRoomBuilder.Build(SceneRoomBuilder.TempleEntry);
            room.name = "Room_" + Rooms.Prologue_Chamber2;
            float groundY = SceneRoomBuilder.TempleEntry.groundY;

            // 从青年过来 —— 只按当前 Era 建 Player
            PlayerBuilder.Build(GameState.CurrentEra, groundY, SpawnX);

            // 棺材:场景中央
            BuildCoffin(room.transform, new Vector2(3f, groundY + 0.5f), groundY);

            // 首帧引路
            room.AddComponent<PrologueChamber2Director>();
        }

        static void BuildCoffin(Transform parent, Vector2 pos, float groundY)
        {
            var go = MakeBlock(parent, "Interact_Coffin", pos, new Vector2(2.6f, 1.0f),
                new Color(0.35f, 0.28f, 0.22f, 0.95f));
            var it = go.AddComponent<Interact_Coffin>();
            it.highlightTarget = go.GetComponent<SpriteRenderer>();
            it.interactPoint = MakePoint(go.transform, new Vector2(pos.x - 2f, groundY));
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

    /// <summary>棺材小游戏(D7 占位):点 3 次解开,第 3 次触发切中年 Cutscene。</summary>
    public class Interact_Coffin : InteractableBase
    {
        int _progress;
        bool _solved;

        public override void OnClick()
        {
            if (_solved) return;
            _progress++;
            PlaySfx(Sfx.mechanism_click);
            if (_progress < 3)
            {
                DialogueSystem.ShowText($"(棺材上的机关又松动了一层……{_progress}/3)");
                return;
            }
            _solved = true;

            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);

            var cs = gameObject.AddComponent<Cutscene>();
            cs.Add(new SayTextStep("棺材缓缓合上——你被吸进了另一段时光。"))
              .Add(new SetFlagStep(Flags.Prologue_UnlockedMiddle, true))
              .Add(new SwitchEraStep(Era.Middle, flash: true))
              .Add(new WaitStep(0.2f))
              .Add(new SayStep(Dialogues.prologue_3_awake))         // "奇怪,这是哪?我不是在修表吗。"
              .Add(new SayStep(Dialogues.prologue_3_go_downstairs)) // "还是先下楼看看吧。"
              .Add(new GoToSceneStep(Rooms.Prologue_Foyer, 0.6f));
            cs.OnFinished += () =>
            {
                var p = PlayerController.Instance;
                if (p != null) p.SetControllable(true);
            };
            cs.Play();
        }
    }

    /// <summary>棺材密室首帧引路。</summary>
    public class PrologueChamber2Director : MonoBehaviour
    {
        void Start()
        {
            const string kFlag = "prologue_entered_chamber2";
            if (GameState.GetFlag(kFlag)) return;
            GameState.SetFlag(kFlag, true);

            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);
            var cs = gameObject.AddComponent<Cutscene>();
            cs.Add(new WaitStep(0.4f))
              .Add(new SayTextStep("(石棺静卧于密室中央,盖上的机关花纹微微发亮。)"));
            cs.OnFinished += () => { if (pc != null) pc.SetControllable(true); };
            cs.Play();
        }
    }
}
