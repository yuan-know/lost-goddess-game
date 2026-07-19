// ============================================================================
//  PrologueGateScene.cs —— 第 0 幕结尾【神庙入口 · 石拱门下】过场房间
//
//  剧本(docs/序幕(1).docx 第 0 幕结尾):
//    "画面:神庙的全貌显现,人物站立于神庙正前方,庞大的神庙与渺小的人物形成反差。
//     (点击神庙大门进入)"
//
//  设计:
//    · 背景:TempleGate 三层(bg_far 远景山 / bg_temple 神庙主体 / bg_near 近景遮挡)
//    · 老年从左端 SpawnX=-12 出生,自动 WalkPlayerTo 到石拱门下 x=+8
//    · 无任何交互物(不是解谜场景,只是"走进庙门"这一 beat)
//    · 走到位置后,一句 SayText 提示,再淡出跳到 Prologue_Foyer 内景
//
//  ⚠ 千万别加展台/石门/楼梯这些第一幕内景的道具 —— 那些应该出现在下一个场景 TempleFoyer 里
// ============================================================================

using System.Collections;
using UnityEngine;

namespace LostGoddess.Content
{
    public static class PrologueGateScene
    {
        public const float SpawnX = -12f;    // 左端进入
        public const float ArchX  = 8f;      // 石拱门位置(TempleGate 图中神庙主体的位置)

        public static void Build()
        {
            var root = SceneRoomBuilder.Build(SceneRoomBuilder.TempleGate);
            root.name = "Room_" + Rooms.Prologue_Gate;
            float groundY = SceneRoomBuilder.TempleGate.groundY;

            // 老人沿用当前 Era(从第 0 幕过来时是 Old)
            PlayerBuilder.Build(GameState.CurrentEra, groundY, SpawnX);

            // 挂过场 director
            root.AddComponent<PrologueGateDirector>();
        }
    }

    /// <summary>神庙入口过场:老人自动走到石拱门下,一句提示,淡出跳 Foyer 内景。</summary>
    public class PrologueGateDirector : MonoBehaviour
    {
        Cutscene _cs;

        void Start()
        {
            // 序幕内玩家不能自由走
            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);

            _cs = gameObject.AddComponent<Cutscene>();
            _cs
                .Add(new WaitStep(0.6f))
                // 老年自动走到石拱门下
                .Add(new WalkPlayerToStep(PrologueGateScene.ArchX))
                .Add(new WaitStep(0.4f))
                // 一句氛围旁白(不进对白表,直接文本)
                .Add(new SayTextStep("(庞大的神庙近在眼前,你走到了石拱门下。)"))
                .Add(new SayTextStep("(点击画面继续:走进神庙。)"))
                // 淡出到第一幕神庙门厅内景
                .Add(new GoToSceneStep(Rooms.Prologue_Foyer, 0.6f));
            _cs.Play();
        }
    }
}
