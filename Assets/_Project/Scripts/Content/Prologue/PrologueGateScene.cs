// ============================================================================
//  PrologueGateScene.cs —— 第 0 幕结尾【神庙入口 · 石拱门下】过场房间
//
//  剧本(docs/序幕(1).docx 第 0 幕结尾):
//    "画面:神庙的全貌显现,人物站立于神庙正前方,庞大的神庙与渺小的人物形成反差。
//     (点击神庙大门进入)"
//
//  设计:
//    · 背景:TempleGate 三层(bg_far 远景山 / bg_temple 神庙主体 / bg_near 近景遮挡)
//    · 老年从左端 SpawnX=-8 出生,玩家**自控**走向右侧石拱门
//    · 石拱门位置放一个**不可见 ScenePortal**(老人走近→点击→跳 Prologue_Foyer)
//    · 无其他交互物(不是解谜场景,只是"走进庙门"这一 beat)
//
//  ⚠ 千万别加展台/石门/楼梯这些第一幕内景的道具 —— 那些应该出现在下一个场景 TempleFoyer 里
// ============================================================================

using UnityEngine;

namespace LostGoddess.Content
{
    public static class PrologueGateScene
    {
        public const float SpawnX = -8f;     // 老人在左端可见处出生
        public const float ArchX  = 8f;      // 石拱门位置(TempleGate 图中神庙主体的中心)

        public static void Build()
        {
            var root = SceneRoomBuilder.Build(SceneRoomBuilder.TempleGate);
            root.name = "Room_" + Rooms.Prologue_Gate;
            float groundY = SceneRoomBuilder.TempleGate.groundY;

            // 老人沿用当前 Era(从第 0 幕过来时是 Old),玩家自控走向石拱门
            PlayerBuilder.Build(GameState.CurrentEra, groundY, SpawnX);

            // 石拱门:不可见 ScenePortal(BoxCollider2D isTrigger,SpriteRenderer 透明)
            //  ── 老人走近 interactPoint → OnClick → 跳到内景 Foyer
            BuildArchPortal(root.transform, new Vector2(ArchX, groundY + 1.5f), groundY);

            // 挂 Director:一句一次性引导独白
            root.AddComponent<PrologueGateDirector>();
        }

        static void BuildArchPortal(Transform parent, Vector2 pos, float groundY)
        {
            // pos.x = 石拱门画面中心 x,pos.y 不重要(下面按 y 范围重算中心)
            // 判定块 y 范围要盖住"从地面到石拱门顶":TempleGate 图 3400×1200,
            //   图底 y=-5,图顶 y=+7,石拱门画面高度 ~4~5 世界单位(下沿地面 ~+7 图占比)。
            //   我们让判定块 y ∈ [groundY, groundY+7],覆盖脚底到拱门最高处。
            const float archTopOffset = 7f;      // 拱门顶大约在地面上 7 单位
            const float archBottomOffset = 0f;   // 拱门底就在地面
            float centerY = groundY + (archTopOffset + archBottomOffset) * 0.5f;
            float height  = archTopOffset - archBottomOffset;

            var go = new GameObject("Portal_ToFoyer_石拱门");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, centerY, 0f);

            // 不可见判定块(4×7 世界单位,足够任何"点石拱门"的位置命中)
            //  · sortingOrder=70 > bg_near(60),点近景剪影下的拱门也响应
            //  · debug 期给 15% 淡黄 tint 便于用户看到判定块的边界(以后改回 α=0)
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            sr.color = new Color(1f, 0.9f, 0.4f, 0.10f);  // 极淡黄(15%),验证期可视化,后期改 α=0
            sr.sortingOrder = 70;
            go.transform.localScale = new Vector3(4f, height, 1f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var portal = go.AddComponent<ScenePortal>();
            portal.targetRoom = Rooms.Prologue_Foyer;
            portal.successDialogueId = "";
            portal.highlightTarget = sr;
            portal.fadeTime = 0.6f;

            // 交互点:老人站在石拱门正下方地平线
            var p = new GameObject("interactPoint").transform;
            p.SetParent(go.transform, false);
            p.position = new Vector3(pos.x, groundY, 0f);
            portal.interactPoint = p;

            Debug.Log($"[Gate] Portal_ToFoyer built at x={pos.x} y∈[{groundY:F2},{groundY+archTopOffset:F2}] size=4×{height} → targetRoom={Rooms.Prologue_Foyer}");
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

    /// <summary>神庙入口一次性引导独白。老人始终可控,不锁移动。</summary>
    public class PrologueGateDirector : MonoBehaviour
    {
        void Start()
        {
            // 玩家自控向右走,别锁
            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(true);

            // 一次性引导(避免每次进都弹)
            const string kFlag = "prologue_gate_entered";
            if (GameState.GetFlag(kFlag)) return;
            GameState.SetFlag(kFlag, true);

            StartCoroutine(PlayIntro());
        }

        System.Collections.IEnumerator PlayIntro()
        {
            yield return new WaitForSeconds(0.5f);
            bool done = false;
            DialogueSystem.ShowText("(庞大的神庙近在眼前。走到石拱门下,点击进入。)", () => done = true);
            while (!done) yield return null;
        }
    }
}
