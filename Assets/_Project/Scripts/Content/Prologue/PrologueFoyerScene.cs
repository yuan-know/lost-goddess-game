// ============================================================================
//  PrologueFoyerScene.cs —— 第一幕【神庙门厅】(占位落地版 D4)
//  当前只做:
//    · 三层视差(复用 TempleEntry)
//    · 老人在场景左端出生(Era.Old)
//    · 弹一次 prologue_1_01 内心独白 + 提示"D4 只到这一步,后续 D5 补交互物"
//  后续 D5 会加:展台/石门/楼梯/密室入口三个 Portal + 岁月洞察壁画和二楼高亮。
// ============================================================================

using UnityEngine;

namespace LostGoddess.Content
{
    public static class PrologueFoyerScene
    {
        const float SpawnX = -14f;  // TempleEntry 舞台宽 34,左端 -17 +3 = -14

        public static void Build()
        {
            // 沿用当前 Era(通常是 Old,从第 0 幕来的)
            var root = SceneRoomBuilder.Build(SceneRoomBuilder.TempleEntry);
            PlayerBuilder.Build(GameState.CurrentEra, SceneRoomBuilder.TempleEntry.groundY, SpawnX);

            root.AddComponent<PrologueFoyerDirector>();
        }
    }

    public class PrologueFoyerDirector : MonoBehaviour
    {
        void Start()
        {
            // 首次进入播 os,已进入过就跳过
            if (GameState.GetFlag(Flags.Prologue_EnteredFoyer)) return;
            GameState.SetFlag(Flags.Prologue_EnteredFoyer, true);

            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);

            var cs = gameObject.AddComponent<Cutscene>();
            cs.Add(new WaitStep(0.4f))
              .Add(new SayStep(Dialogues.prologue_1_01))
              .Add(new SayStep(Dialogues.prologue_1_02))
              .Add(new SayTextStep("(D4 占位:门厅已到,交互物 D5 补齐。此时可按 Q 试岁月洞察 / 1 试切青年 / 2 试切中年 —— 但形态尚未在剧情中解锁。)"));
            cs.OnFinished += () =>
            {
                if (pc != null) pc.SetControllable(true);
            };
            cs.Play();
        }
    }
}
