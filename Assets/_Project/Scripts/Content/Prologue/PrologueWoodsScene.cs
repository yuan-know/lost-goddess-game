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
        // 2026-07-20 letterbox 后相机横向能看 29.8 单位,美术 bg_near 前景剪影
        //   分布是"左密(0-40%) / 中间开口(45-70%) / 右密(75-100%)",
        //   对应世界坐标"中间开口"约 x∈[-2, +8]。让老人从中间开口出场,
        //   往右走靠近神庙入口(Portal@x=14),自然过渡到右侧密林+神庙轮廓。
        public const float SpawnX = 0f;      // 中间开口(相机也在 0,严格对齐参考图构图)
        public const float WalkPoint1 = 5f;  // 走到中间偏右
        public const float WalkPoint2 = 11f; // 走近神庙入口(Portal 在 x=14)

        public static void Build()
        {
            // 起始状态 = 老年(前言里"他花了几十年"的那位)
            GameState.CurrentEra = Era.Old;
            GameState.UnlockEra(Era.Old);
            GameState.SetFlag(Flags.prologue_started, true);

            // 三层视差 + WalkableArea + 相机跟随
            var root = SceneRoomBuilder.Build(SceneRoomBuilder.DarkForest);

            // 老人在中间开口出生
            // 2026-07-27 使用旧的站位高度（调整前的 yOffset=0）
            var player = PlayerBuilder.Build(Era.Old, SceneRoomBuilder.DarkForest.groundY, SpawnX, yOffsetOverride: 0f);

            // 2026-07-21 新增:最右侧地面放指引旗帜(浮动效果),走近自动切场景
            // 2026-07-21 用户要求往左移动一点 → 19f → 17f
            BuildEdgeFlag(root.transform, SceneRoomBuilder.DarkForest.groundY, x: 17f, Rooms.Prologue_Gate, enterDir: "left");

            // 神庙入口触发器:走到最右端自动切场景
            BuildTempleEntrance(root.transform);

            // 2026-07-20 头顶感叹号交互:两句环境旁白 → 感叹号出现 → 点击 → 老年独白×3 → 消失
            //   再次点击 → 老年独白×6(自我介绍)
            var talk = PlayerTalkPrompt.Attach(player, headOffsetY: 5.65f);

            // 挂个 director 组件在场景根,收着 Cutscene(切场景时随根一起 Destroy)
            var director = root.AddComponent<PrologueWoodsDirector>();
            director.talkPrompt = talk;
            director.StartCutscene();
        }

        /// <summary>在场景边缘 x 位置建一个三角形指引旗帜(带浮动动画),走近触发切场景。</summary>
        static void BuildEdgeFlag(Transform parent, float groundY, float x, string targetRoom, string enterDir)
        {
            // EdgeFlagPortal自己会检查flag，这里不需要额外检查
            // 旗帜放在地面上方一点,排序层比前景低一级
            // 用户反馈:文件名和内容搞反了 → triangle_flag.png实际存的是放大镜,magnifying_glass实际存的是旗帜
            var sprite = Resources.Load<Sprite>("UI/Icons/magnifying_glass");
            if (sprite == null)
            {
                Debug.LogError("[PrologueWoodsScene] ❌ 找不到旗帜资源 Resources/UI/Icons/magnifying_glass —— 请检查文件路径!");
                return;
            }
            Debug.Log($"[PrologueWoodsScene] ✓ 成功加载三角形旗帜: {sprite.name}");

            var go = new GameObject("EdgeGuideFlag_Triangle");
            go.transform.SetParent(parent, false);
            // 放在地面上方约 1.2 单位
            go.transform.position = new Vector3(x, groundY + 1.2f, 0f);
            // 放到 Ignore Raycast 层 —— 物理检测直接跳过，绝对不阻挡点击
            go.layer = 2; // Layer 2 = Ignore Raycast

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 50;    // 在 bg 之上、前景之下
            // 图标实际 PPU=300,原始像素 → 不需要再缩小太多,调整到合适大小
            go.transform.localScale = Vector3.one * 1.5f;

            // Unity导入Sprite时会自动生成PolygonCollider2D（根据alpha轮廓）
            //   必须销毁自动生成的碰撞体，否则阻挡下方地面点击
            //   触发完全由代码距离检测，不需要任何碰撞体
            Collider2D[] allCols = go.GetComponents<Collider2D>();
            foreach (var col in allCols) UnityEngine.Object.Destroy(col);

            // 添加浮动动画
            var floatAnim = go.AddComponent<FloatingIndicator>();
            floatAnim.amplitude = 0.18f;
            floatAnim.speed = 2.2f;

            // 添加触发切场景（代码检测距离，不需要碰撞体）
            var portal = go.AddComponent<EdgeFlagPortal>();
            portal.triggerRadius = 1.5f;
            portal.targetRoom = targetRoom;
            portal.enterDirection = enterDir;
        }

        static void BuildTempleEntrance(Transform parent)
        {
            float groundY = SceneRoomBuilder.DarkForest.groundY; // 与 DarkForest 保持一致
            // 2026-07-20 改成"走到场景最右端自动切换场景"
            //   舞台 halfW=21.25, WalkableArea.maxX ≈ 20.75(留 0.5 padding),
            //   触发区放在 x=19(中心),宽度 6,高度 8 → 老人走到 x≥16 时触碰触发,
            //   自然过渡到 Prologue_Gate,不需要再点击。
            const float x = 19f;
            const float w = 6f;
            const float h = 8f;
            float centerY = groundY + h * 0.5f;

            var go = new GameObject("Portal_TempleEntrance");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, centerY, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            // Portal 视觉完全隐藏(alpha=0),纯逻辑触发区
            sr.color = new Color(0.9f, 0.8f, 0.4f, 0f);
            sr.sortingOrder = 60;
            go.transform.localScale = new Vector3(w, h, 1f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var portal = go.AddComponent<ScenePortal>();
            portal.targetRoom = Rooms.Prologue_Gate;
            portal.successDialogueId = "";
            portal.highlightTarget = sr;
            portal.fadeTime = 0.6f;
            portal.triggerOnEnter = true;   // 走入触发,不需点击
            portal.walkToBeforeInteract = false;
            // 2026-07-21: 用户要求只有走到右侧旗帜才触发切场景,彻底禁用原有自动触发
            portal.enabled = false;
            // 禁用碰撞体，防止阻挡旗帜下方的点击，并且放到Ignore Raycast层
            col.enabled = false;
            go.layer = 2; // Layer 2 = Ignore Raycast

            // 保留 interactPoint,以便其他系统(如老玩家习惯 / 未来点击兼容)可参考
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
        public PlayerTalkPrompt talkPrompt;

        public void StartCutscene()
        {
            // 老人先 SetControllable(false),序幕内玩家不能自由走
            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);

            _cs = gameObject.AddComponent<Cutscene>();
            _cs
                // 2026-07-20 两句环境旁白,纯文本、无表情特写,点击切下一行
                .Add(new SayTextStep("空气闷热潮湿,天边传来隐隐约约的压抑雷声。"))
                .Add(new SayTextStep("森林不远处隐约现出神庙的灰黑色的轮廓。"))

                // 标记已抵达神庙,由场景右端 Portal_TempleEntrance 触发换场景
                .Add(new SetFlagStep(Flags.Prologue_MetTemple, true));

            _cs.OnFinished += () =>
            {
                // 环境旁白结束后:交还控制权 + 头顶感叹号出现,玩家点它触发独白
                var pc2 = PlayerController.Instance;
                if (pc2 != null) pc2.SetControllable(true);

                if (talkPrompt != null)
                {
                    // 合并两组对话：点击感叹号后一次性显示全部9句
                    talkPrompt.SetDialogue(
                        // 第一组:老年独白 3 句,情绪由平静滑向自嘲/悲凉
                        new PlayerTalkPrompt.Line("这么多年了,还是回到了这里……", "kind"),
                        new PlayerTalkPrompt.Line("什么都没变……这里的时间好像静止了……", "kind"),
                        new PlayerTalkPrompt.Line("只有我,老得快要死了……", "sad"),
                        // 第二组:老人向"看向他的人"打招呼,情绪从平静 → 微笑 → 稍害羞 → 感激
                        new PlayerTalkPrompt.Line("噢……你好,我是提尔。", "smile"),
                        new PlayerTalkPrompt.Line("真意外,这里已经很久没有人会进入了。", "curious"),
                        new PlayerTalkPrompt.Line("你问我来这里干什么吗?", "kind"),
                        new PlayerTalkPrompt.Line("不好意思,其实我有一点害羞……", "shocked"),
                        new PlayerTalkPrompt.Line("请允许我保持沉默。", "kind"),
                        new PlayerTalkPrompt.Line("不过我非常感激你的陪伴。", "smile")
                    );
                    talkPrompt.Show();
                }
            };
            _cs.Play();
        }
    }
}
