// ============================================================================
//  PrologueWoodsScene.cs —— 第 0 幕【荒山野道】(内容层 B,契约 §11+§12)
//  流程(见 docs/序幕脚本.md 第 0 幕):
//    1) 黑幕淡入(SceneLoader 已在做了,这里跳过)
//    2) 前言黑屏白字:prologue_0_prelude
//    3) 老年独白 prologue_0_01
//    4) 走到 x=6(旧标记附近)
//    5) 老年独白 prologue_0_02
//    6) 走到 x=13(神庙轮廓渐近)
//    7) 老年独白 prologue_0_03
//    8) 等玩家点击 → GoToScene(Prologue_Foyer)
//
//  场景生成:复用 DarkForest 三层视差(SceneRoomBuilder.DarkForest)。
//  演员:PlayerBuilder.Build(Era.Old, groundY, spawnX)
//  演出:Cutscene + 9 步骤 API
//
//  用法:SandboxBootstrap.BuildRoomContent("Prologue_Woods") 会调本类的 Build。
// ============================================================================

using System.Collections;
using UnityEngine;

namespace LostGoddess.Content
{
    public static class PrologueWoodsScene
    {
        // 舞台宽 = DarkForest 42.5 单位(bg 4250×1200 / PPU 100)
        // 老人走得慢(0.9 单位/秒),两段各控制在 ~11 单位内(≈12s),别把观众看睡着
        const float SpawnX = -14f;
        const float WalkPoint1 = -3f;
        const float WalkPoint2 = 8f;

        public static void Build()
        {
            // 起始状态 = 老年(前言里"他花了几十年"的那位)
            GameState.CurrentEra = Era.Old;
            GameState.UnlockEra(Era.Old);
            GameState.SetFlag(Flags.prologue_started, true);

            // 三层视差 + WalkableArea + 相机跟随
            var root = SceneRoomBuilder.Build(SceneRoomBuilder.DarkForest);

            // 老人在左端出生
            PlayerBuilder.Build(Era.Old, SceneRoomBuilder.DarkForest.groundY, SpawnX);

            // 挂个 director 组件在场景根,收着 Cutscene(切场景时随根一起 Destroy)
            var director = root.AddComponent<PrologueWoodsDirector>();
            director.StartCutscene();
        }
    }

    /// <summary>第 0 幕的导演组件:场景加载完自动启动 Cutscene,结束跳门厅。</summary>
    public class PrologueWoodsDirector : MonoBehaviour
    {
        Cutscene _cs;

        public void StartCutscene()
        {
            // 老人先 SetControllable(false),序幕内玩家不能自由走
            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);

            _cs = gameObject.AddComponent<Cutscene>();
            _cs
                // 前言黑屏白字(SayText 直接显示,不走对白表 id 也行,但我们已在表里注册了)
                .Add(new SayStep(Dialogues.prologue_0_prelude))

                // 老年独白 1
                .Add(new SayStep(Dialogues.prologue_0_01))

                // 走到旧标记附近
                .Add(new WalkPlayerToStep(WalkPoint1))

                // 老年独白 2
                .Add(new SayStep(Dialogues.prologue_0_02))

                // 走近神庙轮廓
                .Add(new WalkPlayerToStep(WalkPoint2))

                // 老年独白 3
                .Add(new SayStep(Dialogues.prologue_0_03))

                // 等玩家点击(此刻画面停在神庙轮廓前,提示"点击进入")
                .Add(new SayTextStep("(点击画面继续:走向神庙大门)"))

                // 切场景到神庙门厅
                .Add(new SetFlagStep(Flags.Prologue_MetTemple, true))
                .Add(new GoToSceneStep(Rooms.Prologue_Foyer, 0.6f));

            _cs.OnFinished += () =>
            {
                // GoToSceneStep 走完就换场景了,这里其实到不了
            };
            _cs.Play();
        }
    }
}
