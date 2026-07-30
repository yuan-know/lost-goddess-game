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
        // 地上背包位置(2026-07-21 按截图调:再往右一点,y 抬到脚边稍上)
        public const float BackpackX = 3.6f;
        // 背包相对 groundY 的 y 偏移(pivot=中心);越大越靠上
        public const float BackpackYOffset = 1.05f;
        // 石拱门开口位置 —— 按用户第 2 次截图校准:相机 x=8.1、拱门洞口屏幕 x=78%
        //   → 世界 x = -0.79 + 0.78 × 17.78 ≈ +13.1(拱门圆弧开口正中)。
        //   注:相机 clamp maxX ≈ +8.11,可视右边界 ≈ +17,判定块完全可视。
        public const float ArchX  = 13.1f;

        public static void Build()
        {
            var root = SceneRoomBuilder.Build(SceneRoomBuilder.TempleGate);
            root.name = "Room_" + Rooms.Prologue_Gate;
            float groundY = SceneRoomBuilder.TempleGate.groundY;

            // 老人沿用当前 Era(从第 0 幕过来时是 Old),玩家自控走向石拱门
            // 2026-07-19: 游戏开始时,老人在左侧朝右(准备向右走向神庙)
            var player = PlayerBuilder.Build(GameState.CurrentEra, groundY, SpawnX);
            if (player != null)
            {
                var pc = player.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.spriteFacesRight = false;  // 立绘默认朝左
                    // 游戏开始朝右:scale.x 为负(翻转)
                    var scale = player.transform.localScale;
                    scale.x = -Mathf.Abs(scale.x);
                    player.transform.localScale = scale;
                }
            }

            // 石拱门:不可见 ScenePortal(BoxCollider2D isTrigger,SpriteRenderer 透明)
            //  ── 老人走近 interactPoint → OnClick → 跳到内景 Foyer
            BuildArchPortal(root.transform, new Vector2(ArchX, groundY + 1.5f), groundY);

            // 2026-07-21 新增:石拱门位置放指引旗帜(浮动效果),走近自动触发切场景(保持透明 Portal 兼容)
            BuildEdgeFlag(root.transform, groundY, x: ArchX + 0.5f, Rooms.Prologue_Foyer, enterDir: "gate");

            // 2026-07-21 地上背包:一次性拾取。已经捡过就跳过(flag 已置)
            if (!GameState.GetFlag("prologue_gate_backpack_taken"))
            {
                var backpackSprite = Resources.Load<Sprite>("UI/Inventory/backpack_icon");
                if (backpackSprite != null)
                {
                    // 背包放在地面上,sprite pivot 是中心 → y = groundY + BackpackYOffset
                    Interact_PickupBackpack.Attach(root.transform,
                        new Vector2(BackpackX, groundY + BackpackYOffset), backpackSprite);
                }
                else
                {
                    Debug.LogWarning("[Gate] UI/Inventory/backpack_icon 加载失败,跳过地上背包");
                }
            }

            // 挂 Director:一句一次性引导独白
            root.AddComponent<PrologueGateDirector>();
        }

        static void BuildArchPortal(Transform parent, Vector2 pos, float groundY)
        {
            // pos.x = 石拱门开口画面中心 x。判定块只覆盖**拱门开口区**(不覆盖两侧石墙),
            //   避免玩家点石墙误触发进门。开口范围目测:
            //     宽度 ~2.5 世界单位(拱门内侧净宽)
            //     高度 ~5 世界单位(地面到拱顶圆弧最高点)
            const float archOpeningWidth  = 3.0f;
            const float archOpeningHeight = 5.5f;
            float centerY = groundY + archOpeningHeight * 0.5f;

            var go = new GameObject("Portal_ToFoyer_石拱门");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, centerY, 0f);

            // 不可见判定块(2.5×5 世界单位,只覆盖拱门开口)
            //  · sortingOrder=70 > bg_near(60),点近景剪影下的拱门也响应
            // 2026-07-19 打包Demo：Portal完全透明
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            sr.color = new Color(1f, 0.9f, 0.4f, 0f);  // alpha = 0，完全透明
            sr.sortingOrder = 70;
            go.transform.localScale = new Vector3(archOpeningWidth, archOpeningHeight, 1f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            // 2026-07-21: 用户要求只有走近**旗帜**才触发切场景,彻底禁用原有Portal
            //   → collider禁用 + portal禁用 → 绝对不会误触发
            col.enabled = false;

            var portal = go.AddComponent<ScenePortal>();
            portal.targetRoom = Rooms.Prologue_Foyer;
            portal.successDialogueId = "";
            portal.highlightTarget = sr;
            portal.fadeTime = 0.6f;
            portal.enabled = false;
            portal.triggerOnEnter = false;

            // 交互点:老人站在石拱门正下方地平线
            var p = new GameObject("interactPoint").transform;
            p.SetParent(go.transform, false);
            p.position = new Vector3(pos.x, groundY, 0f);
            portal.interactPoint = p;

            Debug.Log($"[Gate] Portal_ToFoyer at x={pos.x} y∈[{groundY:F2},{groundY+archOpeningHeight:F2}] size={archOpeningWidth}×{archOpeningHeight} → {Rooms.Prologue_Foyer}");
        }

        /// <summary>在场景边缘 x 位置建一个三角形指引旗帜(带浮动动画),走近触发切场景。</summary>
        static void BuildEdgeFlag(Transform parent, float groundY, float x, string targetRoom, string enterDir)
        {
            // EdgeFlagPortal自己会检查flag，这里不需要额外检查
            // 用户反馈:文件名和内容搞反了 → triangle_flag.png实际存的是放大镜,magnifying_glass实际存的是旗帜
            var sprite = Resources.Load<Sprite>("UI/Icons/magnifying_glass");
            if (sprite == null)
            {
                Debug.LogError("[PrologueGateScene] ❌ 找不到旗帜资源 Resources/UI/Icons/magnifying_glass —— 请检查文件路径!");
                return;
            }
            Debug.Log($"[PrologueGateScene] ✓ 成功加载三角形旗帜: {sprite.name}");

            var go = new GameObject("EdgeGuideFlag_Triangle");
            go.transform.SetParent(parent, false);
            // 放在拱门开口上方一点
            go.transform.position = new Vector3(x, groundY + 2.5f, 0f);
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

    /// <summary>神庙入口一次性引导独白。老人始终可控,不锁移动。
    /// 2026-07-21 拾取背包剧情接管这里的开场引导,Director 里的旧独白已停用。</summary>
    public class PrologueGateDirector : MonoBehaviour
    {
        void Start()
        {
            // 玩家自控向右走,别锁
            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(true);

            // 2026-07-21 旧的"庞大的神庙近在眼前……"独白让位给拾取背包剧情
            //   (Interact_PickupBackpack 自己会锁玩家 + 弹感叹号 + 播 3 条文本)
        }
    }
}
