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
//    8) 等玩家点击 → GoToScene(**Prologue_Gate**)——石拱门外景过场
//    9) Gate 场景走完 → GoToScene(Prologue_Foyer)——第一幕神庙门厅内景
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
        public const float SpawnX = -14f;
        public const float WalkPoint1 = -3f;
        public const float WalkPoint2 = 8f;

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

            // 神庙入口触发器:剧情结束后玩家点击此处进入 Prologue_Gate
            //  放在场景右侧,覆盖神庙大门视觉区域,作为剧情兜底与脚本要求的"点击神庙大门进入"
            BuildTempleEntrance(root.transform);

            // 挂个 director 组件在场景根,收着 Cutscene(切场景时随根一起 Destroy)
            var director = root.AddComponent<PrologueWoodsDirector>();
            director.StartCutscene();
        }

        static void BuildTempleEntrance(Transform parent)
        {
            float groundY = SceneRoomBuilder.DarkForest.groundY; // 与 DarkForest 保持一致
            const float x = 14f;          // 神庙大门所在区域(在 WalkPoint2=8 右侧)
            const float w = 6f;           // 宽大判定区,方便点击
            const float h = 6f;
            float centerY = groundY + h * 0.5f;

            var go = new GameObject("Portal_TempleEntrance");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, centerY, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            sr.color = new Color(0.9f, 0.8f, 0.4f, 0.08f); // 极淡黄,验证期可见,正式期可改 0
            sr.sortingOrder = 60;
            go.transform.localScale = new Vector3(w, h, 1f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var portal = go.AddComponent<ScenePortal>();
            portal.targetRoom = Rooms.Prologue_Gate;
            portal.successDialogueId = "";
            portal.highlightTarget = sr;
            portal.fadeTime = 0.6f;

            var ip = new GameObject("interactPoint").transform;
            ip.SetParent(go.transform, false);
            ip.position = new Vector3(x - 2f, groundY, 0f);
            portal.interactPoint = ip;
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
                .Add(new WalkPlayerToStep(PrologueWoodsScene.WalkPoint1))

                // 老年独白 2
                .Add(new SayStep(Dialogues.prologue_0_02))

                // 走近神庙轮廓
                .Add(new WalkPlayerToStep(PrologueWoodsScene.WalkPoint2))

                // 老年独白 3
                .Add(new SayStep(Dialogues.prologue_0_03))

                // 剧情结束:标记已抵达神庙,把控制权交还玩家,由场景右侧的 Portal_TempleEntrance 触发换场景
                //  这样即使 Cutscene 步行被点击干扰,玩家也能主动点击神庙入口进入,避免卡死
                .Add(new SetFlagStep(Flags.Prologue_MetTemple, true))
                .Add(new SayTextStep("(走到神庙大门前,点击进入)"));

            _cs.OnFinished += () =>
            {
                // 确保角色可控,玩家可以点击神庙入口 Portal
                var pc = PlayerController.Instance;
                if (pc != null) pc.SetControllable(true);
            };
            _cs.Play();
        }
    }
}
