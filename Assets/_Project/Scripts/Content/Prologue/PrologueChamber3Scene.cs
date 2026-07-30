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
using UnityEngine.UI;
using System.Collections;

namespace LostGoddess.Content
{
    public static class PrologueChamber3Scene
    {
        public const float SpawnX = -6f;   // 从门厅走进来时,老人出生在密室左侧(默认值)

        public static void Build()
        {
            // 场景:美术已给「神庙一楼密室」专用图,SceneRoomBuilder 会自动加载
            //   Resources/Scenes/TempleChamber1F/{bg_far, prop_pottery, bg_full}。
            //   现只有 2 张分层图(bg_far + prop_pottery),bg_mid/bg_near 缺图 warn 无害。
            var room = SceneRoomBuilder.Build(SceneRoomBuilder.TempleChamber1F);
            room.name = "Room_" + Rooms.Prologue_Chamber3;
            float groundY = SceneRoomBuilder.TempleChamber1F.groundY;

            // 2026-07-19 改进:根据进入方向决定老人出生位置和朝向
            float spawnX;
            bool faceRight;
            string enterDir = SceneLoader.EnterDirection;

            if (enterDir == "left")
            {
                spawnX = -12f;   // 从左边(无尽头)进入,出生在左边缘朝右
                faceRight = true;
            }
            else if (enterDir == "right")
            {
                spawnX = 12f;    // 从右边(梯子密室)进入,出生在右边缘朝左
                faceRight = false;
            }
            else
            {
                // 默认:从中央进入
                spawnX = SpawnX;
                faceRight = true;
            }

            var player = PlayerBuilder.Build(GameState.CurrentEra, groundY, spawnX, yOffsetOverride: GetChamber3YOffset(GameState.CurrentEra));
            // 设置初始朝向
            if (player != null)
            {
                var pc = player.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.spriteFacesRight = false;  // 立绘默认朝左
                    // 通过scale设置朝向
                    var scale = player.transform.localScale;
                    scale.x = Mathf.Abs(scale.x) * (faceRight ? -1f : 1f);
                    player.transform.localScale = scale;
                }
            }

            // ── 返回 Portal(右端 → 梯子密室,策划图 2026-07-19) ────
            //  陶罐间(左 3)朝右走 = 回梯子密室(左 2),再朝右才回神庙前厅
            BuildBackPortal(room.transform, new Vector2(14f, groundY), groundY,
                            Rooms.Prologue_LadderChamber);

            // 添加锁孔交互（池底中央）
            BuildLockhole(room.transform, new Vector2(0f, groundY + 1.8f), groundY);

            // 添加3个陶罐交互物（避免背景层重影，单独创建）
            BuildPotteries(room.transform, groundY);

            // 禁用背景prop_pottery图层的碰撞体（它会挡住我们的交互点击）
            DisableBackgroundPotteryCollider(room.transform);

            // 添加放大镜指引在锁孔位置（仅首次）
            BuildLockholeMagnifyingGlass(room.transform, new Vector2(0f, groundY + 1.8f + 0.6f), groundY);

            // 首帧:一句独白引路(仅首次)
            room.AddComponent<PrologueChamber3Director>();
        }

        // ── helpers ──────────────────────────────────────────────────

        /// <summary>创建陶罐交互物(2026-07-24 用户红涂鸦精准像素定位,11 个),随机选一个藏钥匙</summary>
        static void BuildPotteries(Transform parent, float groundY)
        {
            // 2026-07-24 用户给了红涂鸦交互图(神庙一楼密室全.png,3400×1200,与场景同尺寸)。
            //   删掉旧的 3 个陶罐(-12.5/0/+7),改成红涂鸦检测出的 11 块陶罐区域。
            //   像素→世界:PPU=100,pivot=Center → wx=(px-1700)/100, wy=(600-py)/100。
            //   钥匙随机藏进 11 个陶罐之一;其余点击弹随机"空罐子"文案。
            //   (锁孔交互不动,见 BuildLockhole。)
            //   每项:{世界X, 世界Y(区域中心), 宽, 高}
            var potteries = new (float x, float y, float w, float h)[]
            {
                (-14.21f, -4.29f, 1.82f, 2.00f),
                (-11.80f, -3.94f, 2.11f, 2.49f),
                ( -8.84f, -4.67f, 1.33f, 1.31f),
                ( -6.55f, -4.89f, 0.78f, 0.70f),
                ( -1.28f, -4.75f, 1.40f, 1.17f),
                (  4.67f, -4.29f, 1.65f, 1.62f),
                (  6.99f, -3.92f, 2.11f, 2.37f),
                (  9.51f, -3.62f, 1.72f, 3.11f),
                ( 11.02f, -4.33f, 0.91f, 1.42f),
                ( 12.37f, -4.68f, 0.87f, 1.05f),
                ( 14.28f, -4.14f, 2.22f, 2.19f),
            };

            int keyIndex = Random.Range(0, potteries.Length);

            for (int i = 0; i < potteries.Length; i++)
            {
                var p = potteries[i];
                BuildPottery(parent, new Vector2(p.x, p.y), new Vector2(p.w, p.h), i == keyIndex, i);
            }

            Debug.Log($"[PrologueChamber3Scene] 陶罐交互区已创建({potteries.Length} 个)，藏钥匙的索引={keyIndex}");
        }

        /// <summary>创建单个陶罐交互区域(透明点击热区,尺寸按红涂鸦大小)</summary>
        static void BuildPottery(Transform parent, Vector2 pos, Vector2 size, bool hasKey, int index)
        {
            var go = new GameObject($"Interact_Pottery_{index}");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            // 2026-07-24 打包用:透明热区(alpha=0),不再显示调试色差块
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            sr.color = new Color(1f, 1f, 1f, 0f);
            sr.sortingOrder = 100;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            // 交互碰撞体:size=(1,1) 因为已经被 transform.localScale 放大到目标尺寸
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = Vector2.one;

            // 强制设置为Default层，保证ClickInputManager能检测到（不能是Ignore Raycast）
            go.layer = 0;  // Layer 0 = Default

            // 添加交互组件
            var interact = go.AddComponent<Interact_Pottery>();
            interact.hasKey = hasKey;
            interact.highlightTarget = sr;   // 有 sprite 后可高亮反馈

            col.enabled = true;

            if (interact == null)
                Debug.LogError($"[PrologueChamber3Scene] ❌ 陶罐 {index} 添加 Interact_Pottery 组件失败!");
            else
                Debug.Log($"[PrologueChamber3Scene] ✓ 陶罐 {index} @ x={pos.x:F1}, hasKey={hasKey}, size={size}, 就绪");
        }

        /// <summary>创建锁孔交互（半透明色差块，方便调试位置和大小）</summary>
        static void BuildLockhole(Transform parent, Vector2 pos, float groundY)
        {
            var go = new GameObject("Interact_Lockhole");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            Vector2 size = new Vector2(2.5f, 2.5f);

            // 2026-07-23 调试色差块:黄色区分陶罐(红绿蓝)
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            sr.color = new Color(1f, 0.9f, 0.2f, 0f);
            sr.sortingOrder = 100;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            // 碰撞体:size=(1,1),被 localScale 放大到目标尺寸
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = Vector2.one;

            // 强制设置为Default层，保证ClickInputManager能检测到（不能是Ignore Raycast）
            go.layer = 0;  // Layer 0 = Default

            // 添加锁孔交互组件
            var interact = go.AddComponent<Interact_Lockhole>();
            interact.highlightTarget = sr;   // 高亮反馈

            // 确认碰撞体确实启用
            col.enabled = true;

            // 确认组件成功添加
            if (interact == null)
                Debug.LogError($"[PrologueChamber3Scene] ❌ 锁孔添加 Interact_Lockhole 组件失败!");
            else
                Debug.Log($"[PrologueChamber3Scene] ✓ 锁孔已创建 @ x={pos.x}, layer={go.layer}, collider.enabled={col.enabled}, 就绪");

        }

        static void BuildBackPortal(Transform parent, Vector2 pos, float groundY, string targetRoom)
        {
            var go = MakeBlock(parent, "Portal_BackTo_" + targetRoom, pos, new Vector2(1.2f, 3.0f),
                new Color(0.15f, 0.15f, 0.25f, 0.5f));
            var portal = go.AddComponent<ScenePortal>();
            portal.targetRoom = targetRoom;
            portal.highlightTarget = go.GetComponent<SpriteRenderer>();
            portal.triggerOnEnter = true;  // 走入触发模式：玩家走到边缘区域自动触发切场景
            // interactPoint 站在 Portal 内侧(portal 在右端 → 老人站左侧才触发)
            portal.interactPoint = MakePoint(go.transform, new Vector2(pos.x - 1.5f, groundY));
        }

        /// <summary>在锁孔位置添加放大镜图标指引，点击触发对话</summary>
        static void BuildLockholeMagnifyingGlass(Transform parent, Vector2 pos, float groundY)
        {
            // 如果已经点击过，不再创建
            const string kFlag = "prologue_chamber3_lockhole_clicked";
            if (GameState.GetFlag(kFlag))
                return;

            // 放大镜图标：复用现有资源，放在锁孔上方偏一点
            // 用户反馈:文件名和内容搞反了 → triangle_flag.png实际存的是放大镜
            var sprite = Resources.Load<Sprite>("UI/Icons/triangle_flag");
            if (sprite == null)
            {
                Debug.LogError("[PrologueChamber3Scene] ❌ 找不到放大镜资源 Resources/UI/Icons/triangle_flag");
                return;
            }

            var glassGo = new GameObject("Lockhole_Magnifying");
            glassGo.transform.SetParent(parent, false);
            glassGo.transform.position = new Vector3(pos.x, pos.y, 0f);

            var sr = glassGo.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 55;
            glassGo.transform.localScale = Vector3.one * 0.8f;

            // 浮动动画
            var floatAnim = glassGo.AddComponent<FloatingIndicator>();
            floatAnim.amplitude = 0.15f;
            floatAnim.speed = 2.0f;

            // 点击碰撞盒
            var col = glassGo.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.8f, 1.2f);

            // 添加交互组件：点击显示文案，然后弹出感叹号
            var interact = glassGo.AddComponent<Interact_LockholePrompt>();
            interact.highlightTarget = sr;  // 设置高亮目标
            // 点击后设置flag，防止重复创建
            interact.flagToSetOnClick = kFlag;

            // 放到 Ignore Raycast 层不阻挡锁孔本身的点击
            glassGo.layer = 2; // Layer 2 = Ignore Raycast

            Debug.Log($"[PrologueChamber3Scene] 锁孔放大镜指引已创建 @ x={pos.x}");
        }

        static GameObject MakeBlock(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            // 2026-07-19 打包Demo：占位方块设为透明
            sr.color = new Color(color.r, color.g, color.b, 0f);
            sr.sortingOrder = 5;  // 降低排序，确保在美术sprite之下，避免挡住造成重影
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            // 2026-07-19 打包Demo：移除调试标签
            // AddDebugLabel(go, name + $"\n({pos.x:F1},{pos.y:F1})");
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

        /// <summary>禁用背景prop_pottery图层的碰撞体，避免挡住交互点击</summary>
        static void DisableBackgroundPotteryCollider(Transform parent)
        {
            // 只找到并禁用prop_pottery这个特定背景图层（它有Unity自动生成的碰撞体）
            // 不碰我们自己创建的交互物碰撞体！
            int disabledCount = 0;
            foreach (Transform child in parent)
            {
                if (child.name == "prop_pottery")
                {
                    DisableAllCollidersRecursive(child, ref disabledCount);
                    Debug.Log($"[PrologueChamber3Scene] 已找到prop_pottery背景层，共禁用 {disabledCount} 个碰撞体");
                    break;
                }
            }
        }

        /// <summary>递归禁用指定transform下所有子物体上的碰撞体</summary>
        static void DisableAllCollidersRecursive(Transform parent, ref int disabledCount)
        {
            var col = parent.GetComponent<Collider2D>();
            if (col != null)
            {
                col.enabled = false;
                disabledCount++;
                Debug.Log($"[PrologueChamber3Scene] ✓ 已禁用 '{parent.name}' 的碰撞体 (背景层)");
            }

            foreach (Transform child in parent)
            {
                DisableAllCollidersRecursive(child, ref disabledCount);
            }
        }

        /// <summary>陶罐间使用旧的站位高度（调整前的yOffset）</summary>
        static float GetChamber3YOffset(Era era)
        {
            switch (era)
            {
                case Era.Young:
                case Era.Middle:
                    return 3.5f;   // 旧的青年/中年站位
                case Era.Old:
                default:
                    return 0f;     // 旧的老年站位
            }
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
            StartCoroutine(PlayIntro(pc));
        }

        System.Collections.IEnumerator PlayIntro(PlayerController pc)
        {
            yield return new WaitForSeconds(0.4f);
            bool done = false;
            // 用户要求的进入文案
            DialogueSystem.ShowText("荒废的浴池，无论是墙壁还是地板都被青苔铺满，空气中散发着刺鼻的怪味。", () => done = true);
            while (!done) yield return null;

            if (pc != null) pc.SetControllable(true);
        }
    }

    // ============================================================================
    //  Interact_LockholePrompt —— 锁孔放大镜指引点击交互
    //   用户需求：点击后显示文案 → 文案消失 → 弹出感叹号 → 播放7句对话 → 修改任务指引
    // ============================================================================
    public class Interact_LockholePrompt : InteractableBase
    {
        [HideInInspector]
        public string flagToSetOnClick;  // 点击后设置的flag，防止重复创建
        bool _done = false;

        public override void OnClick()
        {
            if (_done) return;  // 已经点过了，不要再触发

            // 设置flag，防止重复创建
            if (!string.IsNullOrEmpty(flagToSetOnClick))
            {
                GameState.SetFlag(flagToSetOnClick, true);
            }

            var pc = PlayerController.Instance;
            if (pc == null) return;
            pc.SetControllable(false);

            StartCoroutine(PlaySequence());
        }

        IEnumerator PlaySequence()
        {
            bool done = false;
            var pc = PlayerController.Instance;

            // 点击显示文案
            DialogueSystem.ShowText("被镶嵌在浴池前的铁盒。有着明显的上锁痕迹。", () => done = true);
            while (!done) yield return null;

            // 文案消失 → 销毁放大镜 → 弹出感叹号
            if (gameObject != null) Destroy(gameObject);
            _done = true;

            // 弹出感叹号在玩家头顶
            var player = PlayerController.Instance?.gameObject;
            if (player == null)
            {
                if (pc != null) pc.SetControllable(true);
                yield break;
            }

            // 使用 PlayerTalkPrompt 复用现成机制
            var prompt = PlayerTalkPrompt.Attach(player);
            prompt.SetDialogue(
                new PlayerTalkPrompt.Line("这里好像还写了些什么，让我看看", null),
                new PlayerTalkPrompt.Line("能力……钥匙……封印……就在眼前……", null),
                new PlayerTalkPrompt.Line("我记得这里以前可没有封印过什么能力", null),
                new PlayerTalkPrompt.Line("总之，应该有个钥匙用来开锁", null),
                new PlayerTalkPrompt.Line("钥匙……（环顾四周）", null),
                new PlayerTalkPrompt.Line("眼前除了有这些罐子，还有个大水池……", null),
                new PlayerTalkPrompt.Line("总不能真的得跳进池子里找钥匙吧（笑）", null)
            );
            // 全部说完 → 修改任务指引
            prompt.OnAllSpoken += () =>
            {
                UpdateQuestPrompt("寻找钥匙开锁");
                if (pc != null) pc.SetControllable(true);
            };
            prompt.Show();
        }

        /// <summary>更新左上角常驻任务指引文字</summary>
        void UpdateQuestPrompt(string newText)
        {
            // 找到现有的任务指引（由 Interact_Podium 创建，DontDestroyOnLoad）
            var promptObj = GameObject.Find("QuestPrompt");
            if (promptObj == null)
            {
                Debug.LogWarning("[Interact_LockholePrompt] 找不到现有的任务指引，跳过更新");
                return;
            }

            // 找到 Text 组件修改文字
            var text = promptObj.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = "●  " + newText;
                Debug.Log($"[Interact_LockholePrompt] 任务指引已更新为: {newText}");
            }
        }
    }
}
