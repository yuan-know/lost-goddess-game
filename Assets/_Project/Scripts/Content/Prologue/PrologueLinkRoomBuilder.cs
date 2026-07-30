// ============================================================================
//  PrologueLinkRoomBuilder.cs —— 神庙前厅左右链上"过场房间"通用生成器
//
//  策划场景切换图(2026-07-19):
//    · 神庙前厅为中心
//      朝左:Foyer ↔ LadderChamber(左2,梯子密室) ↔ Chamber3(左3,陶罐间)
//      朝右:Foyer ↔ GearRoom(右1,齿轮骨骸间) ↔ StatueRoom(右2,石雕室)
//    · 从 LadderChamber 的木梯爬上二楼:UpperChamber(二楼密室,真实美术) ↔ UpperHall(二楼回廊,暂用占位)
//
//  每个链式房间都是"两端 Portal + 老人在中间可控行走"的过场结构,美术占位期先用
//  DarkForest 三层视差顶着,等真图交付后 SceneDef 换一下 sprite 名字就行。
//
//  用法:
//    PrologueLinkRoomBuilder.Build(new LinkRoomDef {
//        roomName = Rooms.Prologue_LadderChamber,
//        leftRoom = Rooms.Prologue_Chamber3,   // 朝左返回/往下走的目标
//        rightRoom = Rooms.Prologue_Foyer,     // 朝右前进/返回门厅
//        title = "梯子密室",
//    });
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace LostGoddess.Content
{
    public struct LinkRoomDef
    {
        public string roomName;    // 本房间常量(Rooms.xxx)
        public string leftRoom;    // 朝左 Portal 目标(空=不建左出口)
        public string rightRoom;   // 朝右 Portal 目标(空=不建右出口)
        public string title;       // 中文名(HUD/首帧独白显示)
        public string upRoom;      // 朝上(爬梯)Portal 目标(空=不建;仅 LadderChamber 用)
        public SceneDef scene;     // 用哪份美术 SceneDef(空=退回 DarkForest 占位)
    }

    public static class PrologueLinkRoomBuilder
    {
        public static GameObject Build(LinkRoomDef def)
        {
            // 背景:优先用 def.scene(真图),缺则退回 DarkForest 占位视差
            var sceneDef = def.scene ?? SceneRoomBuilder.DarkForest;
            var root = SceneRoomBuilder.Build(sceneDef);
            root.name = "Room_" + def.roomName;
            float groundY = sceneDef.groundY;

            // 2026-07-19 改进:根据进入方向决定老人出生位置和朝向
            //   从左边进入(EnterDirection="left") → 出生在左边缘,朝右
            //   从右边进入(EnterDirection="right") → 出生在右边缘,朝左
            //   从下方进入(EnterDirection="up",爬楼梯) → 出生在中央偏左,朝右
            //   从上方进入(EnterDirection="down",下楼梯) → 出生在中央偏左,朝右
            // 2026-07-24 【超宽场景兼容】出生 X 也绑定实际半宽,不再硬编 ±12。
            //   之前硬编 -12/+12 对 UpperCorridor(halfW=29.75)来说出生在场景中间,
            //   玩家还得走 17+ 单位才到另一端。改成从边缘内缩 3 单位出生,与出口 Portal 距离固定。
            float halfWForSpawn = (def.scene ?? SceneRoomBuilder.DarkForest).bgPixelWidth /
                                  (def.scene ?? SceneRoomBuilder.DarkForest).bgPPU * 0.5f;
            float spawnEdge = halfWForSpawn - 3f;

            float spawnX;
            bool faceRight;
            string enterDir = SceneLoader.EnterDirection;

            if (enterDir == "left")
            {
                spawnX = -spawnEdge;   // 从左端进入 → 出生在场景左侧内缩 3 单位处
                faceRight = true;  // 朝右
            }
            else if (enterDir == "right")
            {
                spawnX = spawnEdge;    // 从右端进入 → 出生在场景右侧内缩 3 单位处
                faceRight = false; // 朝左
            }
            else if (enterDir == "up" || enterDir == "down")
            {
                spawnX = -8f;    // 中央偏左(梯子位置)
                faceRight = true;  // 朝右
            }
            else
            {
                // 默认:游戏开始时,从中央朝右
                spawnX = 0f;
                faceRight = true;
            }

            var player = PlayerBuilder.Build(GameState.CurrentEra, groundY, spawnX);
            // 设置初始朝向
            if (player != null)
            {
                var pc = player.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.spriteFacesRight = false;  // 立绘默认朝左
                    // 通过scale设置朝向
                    var scale = player.transform.localScale;
                    scale.x = Mathf.Abs(scale.x) * (faceRight ? -1f : 1f);  // 朝右时翻转
                    player.transform.localScale = scale;
                }
            }

            // 2026-07-24 【根因修复】Portal 位置不再硬编 x=±15,而是绑定到场景实际半宽:
            //   之前 UpperCorridor(halfW=29.75)沿用 x=-15 → Portal 距真实左边缘还有 14+ 世界单位,
            //   玩家出生 x=-12 时"跑一步就切回去了",看起来判定区超宽。
            //   改成:Portal 中心 = ±(halfW - 0.6),judgement bar 贴在场景真实边缘,内缩 0.6 给一点缓冲。
            float halfW = sceneDef.bgPixelWidth / sceneDef.bgPPU * 0.5f;
            float portalX = halfW - 0.6f;

            // 场景左端 Portal:朝左走 = 返回上一房间
            if (!string.IsNullOrEmpty(def.leftRoom))
            {
                BuildEdgePortal(root.transform, def.leftRoom, "Portal_Left_" + def.leftRoom,
                    new Vector2(-portalX, groundY + 1.5f), groundY, isLeft: true);
            }

            // 场景右端 Portal:朝右走 = 前进到下一房间
            if (!string.IsNullOrEmpty(def.rightRoom))
            {
                BuildEdgePortal(root.transform, def.rightRoom, "Portal_Right_" + def.rightRoom,
                    new Vector2(portalX, groundY + 1.5f), groundY, isLeft: false);
            }

            // 场景中央梯子:改为点击交互 + 放大镜提示，不再自动触发
            //  木梯在 prop_ladder 像素 x∈[758,1008] → 世界 x≈-8.17,放大镜放在梯子偏下方
            if (!string.IsNullOrEmpty(def.upRoom))
            {
                BuildLadderInteractive(root.transform, def.upRoom, new Vector2(-8.17f, groundY + 2.5f), groundY);
            }

            // Director:首帧独白 + 强制可控 + 进入提示
            var dir = root.AddComponent<PrologueLinkRoomDirector>();
            dir.roomTitle = def.title;
            dir.targetUpRoom = def.upRoom;

            return root;  // 2026-07-19 返回场景根对象，便于后续添加道具
        }

        // 场景左右边缘的"看不见的 3×5 Portal 判定块",走近点击即切场景
        static void BuildEdgePortal(Transform parent, string targetRoom, string name,
                                    Vector2 pos, float groundY, bool isLeft)
        {
            const float w = 3.0f, h = 5.5f;
            float centerY = groundY + h * 0.5f;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, centerY, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            // 2026-07-19 打包Demo：Portal完全透明
            sr.color = new Color(1f, 1f, 1f, 0f);  // alpha = 0
            sr.sortingOrder = 70;
            go.transform.localScale = new Vector3(w, h, 1f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var portal = go.AddComponent<ScenePortal>();
            portal.targetRoom = targetRoom;
            portal.successDialogueId = "";
            portal.highlightTarget = sr;
            portal.fadeTime = 0.6f;
            portal.triggerOnEnter = true;  // 走入触发模式：玩家走到边缘区域自动触发切场景
            // 2026-07-24 从左端 Portal 出去 → 目标房间从"右侧"进入(=老人在目标场景右侧出生,朝左);
            //                从右端 Portal 出去 → 目标房间从"左侧"进入。
            portal.enterDirection = isLeft ? "right" : "left";

            // interactPoint:老人走到 Portal 内侧地平线上再触发(左边 portal 站右侧、右边 portal 站左侧)
            float ipx = isLeft ? pos.x + 1.5f : pos.x - 1.5f;
            var ip = new GameObject("interactPoint").transform;
            ip.SetParent(go.transform, false);
            ip.position = new Vector3(ipx, groundY, 0f);
            portal.interactPoint = ip;

            Debug.Log($"[LinkRoom] {name} @ x={pos.x} → {targetRoom} (triggerOnEnter=true)");
        }

        // 中央梯子交互：添加引导图标 + 点击对话 + 根据年龄判断是否能爬
        static void BuildLadderInteractive(Transform parent, string targetRoom, Vector2 pos, float groundY)
        {
            // 2026-07-24 v2 用户要求:图标按形态区分
            //   · 老年(初次探索):显示【放大镜】提示"点击探索",点击梯子后放大镜销毁,再进不出现。
            //   · 青年:显示【三角旗帜】指引"点击前往切换场景"(爬梯上二楼)。
            //   文件名/内容互换约定:triangle_flag.png 实际存"放大镜";magnifying_glass.png 实际存"三角旗帜"。
            bool isYoung = GameState.CurrentEra == Era.Young;
            const string kOldExplored = "prologue_ladder_old_explored";  // 老年点过梯子后置位,不再出放大镜

            // 老年已探索过则不再创建图标(但梯子本体交互仍然保留)
            bool showIcon = isYoung || !GameState.GetFlag(kOldExplored);
            string iconPath = isYoung ? "UI/Icons/magnifying_glass"   // 青年 → 旗帜
                                      : "UI/Icons/triangle_flag";     // 老年 → 放大镜
            Sprite sprite = showIcon ? Resources.Load<Sprite>(iconPath) : null;
            if (showIcon && sprite == null)
            {
                Debug.LogError($"[PrologueLinkRoomBuilder] ❌ 找不到梯子引导图标资源 Resources/{iconPath}");
            }

            // 图标放在梯子偏下方
            GameObject glassGo = null;
            if (showIcon)
            {
                float iconY = pos.y - 1.0f;
                glassGo = new GameObject("LadderGuide_Icon");
                glassGo.transform.SetParent(parent, false);
                glassGo.transform.position = new Vector3(pos.x, iconY, 0f);

                var sr = glassGo.AddComponent<SpriteRenderer>();
                if (sprite != null) sr.sprite = sprite;
                sr.sortingOrder = 55;    // 在梯子之上，角色之下
                glassGo.transform.localScale = Vector3.one * 0.8f;

                // 浮动动画
                var floatAnim = glassGo.AddComponent<FloatingIndicator>();
                floatAnim.amplitude = 0.15f;
                floatAnim.speed = 2.0f;

                // 放到 Ignore Raycast 层不阻挡其他点击(见下方梯子 collider 唯一命中的根因说明)
                glassGo.layer = 2; // Layer 2 = Ignore Raycast
            }

            // 2026-07-23 【根因修复】引导图标不能带独立 collider:它会落在梯子 collider 内部,
            //   ClickInputManager 用 Physics2D.OverlapPoint 只取 hits[0],若命中图标(无 InteractableBase)
            //   梯子 OnClick 就拿不到点击。所以图标放 Ignore Raycast 层且无 collider,点击直达梯子。
            // 梯子主体点击区域(唯一 collider)
            var ladderGo = new GameObject("Interact_Ladder");
            ladderGo.transform.SetParent(parent, false);
            ladderGo.transform.position = new Vector3(pos.x, pos.y, 0f);
            var ladderCol = ladderGo.AddComponent<BoxCollider2D>();
            ladderCol.isTrigger = true;
            ladderCol.size = new Vector2(1.5f, 5.0f);  // 和原 Portal 大小一致

            // 添加交互组件：处理点击对话和切场景
            var interact = ladderGo.AddComponent<Interact_Ladder>();
            interact.targetUpRoom = targetRoom;
            if (glassGo != null) interact.highlightTarget = glassGo.GetComponent<SpriteRenderer>();
            interact.magnifyingGlass = glassGo;      // 保存图标引用，点击后销毁(青年切场景/老年对话尾均销毁)
            interact.oldExploredFlag = "prologue_ladder_old_explored";  // 老年点过后置位,不再出放大镜
            // 走 InteractableBase 默认行为:点击 → 走到梯子跟前 → 触发 OnClick(青年切场景/其他形态弹对话)

            Debug.Log($"[LinkRoom] 梯子交互已创建 @ x={pos.x}, target={targetRoom}, young={isYoung}, showIcon={showIcon}");
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

    // ============================================================================
    //  Interact_Ladder —— 梯子点击交互
    //   · 青年形态：点击直接切场景到二楼密室(2026-07-23)
    //   · 其他形态(老年/中年)：播放"爬不上去"对话，不切场景
    //   放大镜点击一次后自动销毁，不再出现
    // ============================================================================
    public class Interact_Ladder : InteractableBase
    {
        public string targetUpRoom;
        public GameObject magnifyingGlass;  // 指引图标(老年放大镜/青年旗帜)，点击后销毁
        public string oldExploredFlag = ""; // 老年点过梯子后置位的 flag,置位后返场景不再出放大镜

        bool _done = false;

        public override void OnClick()
        {
            // 2026-07-23 【根因修复】_done 标志位由老年对话流程设置,但切换 Era 时房间不重建,
            //   Interact_Ladder 对象存活, _done 仍为 true → 青年点击被 if (_done) return 拦掉。
            //   修:青年判断放在 _done 之前,青年切场景是无条件不可撤销的边界过渡。
            if (GameState.CurrentEra == Era.Young && !string.IsNullOrEmpty(targetUpRoom))
            {
                var pc2 = PlayerController.Instance;
                if (pc2 == null) return;
                _done = true;
                pc2.SetControllable(false);
                if (magnifyingGlass != null) Destroy(magnifyingGlass);
                SceneLoader.EnterDirection = "up";
                SceneLoader.GoToRoom(targetUpRoom);
                return;
            }

            if (_done) return;

            var pc = PlayerController.Instance;
            if (pc == null) return;

            // 老年/中年:仍是"爬不上去"的对话流程
            pc.SetControllable(false);
            StartCoroutine(PlayDialogueSequence());
        }

        IEnumerator PlayDialogueSequence()
        {
            bool done = false;

            // 第一句：固定文案，不管什么年龄
            DialogueSystem.ShowText("摇摇欲坠的梯子，踩上去还会落下灰尘", () => done = true);
            while (!done) yield return null;
            done = false;

            // 三段老年对话（所有形态都按这个走，因为老年是初始形态，所有形态都爬不了）
            DialogueSystem.ShowMonologue("爬上梯子，就可以前往神庙的第二层", null, () => done = true, hidePortraitAfter: false);
            while (!done) yield return null;
            done = false;

            DialogueSystem.ShowMonologue("我知道第二层有个地方放了很多……宝藏", null, () => done = true, hidePortraitAfter: false);
            while (!done) yield return null;
            done = false;

            DialogueSystem.ShowMonologue("开启展台的部件，可能就在上面……", null, () => done = true, hidePortraitAfter: true);
            while (!done) yield return null;
            done = false;

            // 提示文字 + 无法攀爬 + 结尾对话
            DialogueSystem.ShowText("点击梯子，攀爬至神庙第二层", () => done = true);
            while (!done) yield return null;
            done = false;

            DialogueSystem.ShowText("【你的身体无法攀爬】", () => done = true);
            while (!done) yield return null;
            done = false;

            DialogueSystem.ShowMonologue("(叹气)这腿抬不起来，爬不上去……", null, () => done = true, hidePortraitAfter: false);
            while (!done) yield return null;
            done = false;

            DialogueSystem.ShowMonologue("去其它密室找找吧", null, () => done = true, hidePortraitAfter: true);
            while (!done) yield return null;

            // 销毁放大镜指引，点击后不再出现
            if (magnifyingGlass != null)
                Destroy(magnifyingGlass);

            // 记住老年已探索过梯子:返场景不再出放大镜(青年阶段会换成旗帜,由 build 时判断)
            if (!string.IsNullOrEmpty(oldExploredFlag))
                GameState.SetFlag(oldExploredFlag, true);

            _done = true;

            // 恢复控制，始终不切换场景（所有形态都爬不了）
            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(true);
        }
    }

    /// <summary>链式过场房间的通用 Director:首句一句进入提示 + 强制可控。</summary>
    public class PrologueLinkRoomDirector : MonoBehaviour
    {
        public string roomTitle = "";
        public string targetUpRoom;

        void Start()
        {
            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(true);

            // 一次性提示：仅第一次进入显示
            string kFlag = "linkroom_entered_" + roomTitle;
            if (GameState.GetFlag(kFlag)) return;
            GameState.SetFlag(kFlag, true);

            StartCoroutine(PlayIntro());
        }

        System.Collections.IEnumerator PlayIntro()
        {
            yield return new WaitForSeconds(0.4f);
            bool done = false;

            // 用户要求：进入文案
            // "一间空密室，该密室可以通往【神庙第二层】
            // 操控人物走到左/右边缘点击可切换场景"
            DialogueSystem.ShowText("一间空密室，该密室可以通往【神庙第二层】", () => done = true);
            while (!done) yield return null;
            done = false;

            DialogueSystem.ShowText("操控人物走到左/右边缘点击可切换场景", () => done = true);
            while (!done) yield return null;
        }
    }
}
