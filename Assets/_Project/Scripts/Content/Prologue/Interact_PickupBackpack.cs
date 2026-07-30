// ============================================================================
//  Interact_PickupBackpack.cs —— 神庙入口 · 地上背包拾取(2026-07-21)
//
//  行为:
//    1) 老人从左端往右走,进入背包水平范围(x∈[bx-triggerHalfW, bx+triggerHalfW])
//       → 自动停止移动 + 锁住控制 + 头顶感叹号浮出
//    2) 玩家点感叹号 → 播 3 条文本:
//         · 老年独白(带表情):"真没想到它也一直在这里……"
//         · 环境文本(无表情):"你获得了背包"
//         · 环境文本(无表情):"离神庙越来越近,远处灰黑色的轮廓更加清晰"
//    3) 全部说完 → 拾取:
//         · GameState.SetFlag("prologue_gate_backpack_taken", true)
//         · InventoryUI.SetVisible(true) → 右上角背包按钮出现
//         · 地上背包对象销毁 + 玩家恢复控制
//         · ⚠ 不 InventorySystem.Add(backpack) —— 背包是"容器"不是"道具",
//           打开时字幕位置常驻背包描述,详见 InventoryUI.BuildDescriptionBar
//
//  设计说明:
//    · 不用 InteractableBase(点击流)——需求是"靠近自动停下",标准点击流不合适
//    · 感叹号图标复用美术给的 UI/Icons/exclamation,但不用 PlayerTalkPrompt——
//      那个组件把对话列表和 DialogueSystem.ShowMonologue 强绑定,
//      而这里要求 1 句带表情 + 2 句纯文本,直接自建更清晰
//    · 感叹号只出现一次;flag 已置时 Gate 场景不重建背包
// ============================================================================

using System.Collections;
using UnityEngine;

namespace LostGoddess.Content
{
    public class Interact_PickupBackpack : MonoBehaviour
    {
        [Tooltip("老人 x 与背包 x 的差值 ≤ 这个值时自动停 + 显示感叹号")]
        public float triggerHalfWidth = 1.2f;
        [Tooltip("感叹号浮在老人头顶的世界高度")]
        // 2026-07-21 三修:上次 4.8→6.5 太高,按用户"取上调量一半"改为 5.65(4.8 + 1.7/2)
        public float promptHeadOffsetY = 5.65f;

        bool _triggered;

        public static Interact_PickupBackpack Attach(Transform parent, Vector2 groundPos, Sprite backpackSprite)
        {
            var go = new GameObject("Backpack_地上");
            go.transform.SetParent(parent, false);
            go.transform.position = groundPos;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = backpackSprite;
            sr.sortingOrder = 55;   // 高于 bg_far/mid,低于 bg_near(60);和老人差不多层
            // 微微调暗融合场景(荒山夜色偏冷)
            sr.color = new Color(0.85f, 0.85f, 0.90f, 1f);
            go.transform.localScale = Vector3.one * 1.05f;   // 2026-07-21 从 0.75 放大到 1.05,视觉更明显

            return go.AddComponent<Interact_PickupBackpack>();
        }

        void Update()
        {
            if (_triggered) return;
            var pc = PlayerController.Instance;
            if (pc == null) return;

            float dx = Mathf.Abs(pc.transform.position.x - transform.position.x);
            if (dx > triggerHalfWidth) return;

            _triggered = true;
            // 停下 + 锁玩家(玩家在对话结束前不能移动)
            pc.StopMoving();
            pc.SetControllable(false);
            SpawnPromptAndBind(pc);
        }

        void SpawnPromptAndBind(PlayerController pc)
        {
            var promptGo = new GameObject("~BackpackPrompt");
            var sprite = Resources.Load<Sprite>("UI/Icons/exclamation");
            var sr = promptGo.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 500;
            promptGo.transform.localScale = Vector3.one * 1.6f;
            promptGo.transform.position = pc.transform.position + new Vector3(0f, promptHeadOffsetY, 0f);

            var col = promptGo.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.55f, 0.9f);

            var follower = promptGo.AddComponent<BackpackPromptFollower>();
            follower.followTarget = pc.transform;
            follower.headOffsetY = promptHeadOffsetY;
            follower.onClicked = () => StartCoroutine(PlaySequence(promptGo, pc));
        }

        IEnumerator PlaySequence(GameObject promptGo, PlayerController pc)
        {
            // 隐藏感叹号 + 关点击(避免连续点击重入)
            var promptSr = promptGo.GetComponent<SpriteRenderer>();
            var promptCol = promptGo.GetComponent<Collider2D>();
            if (promptSr != null) promptSr.enabled = false;
            if (promptCol != null) promptCol.enabled = false;

            // ① 老年独白,带表情
            bool done = false;
            DialogueSystem.ShowMonologue("真没想到它也一直在这里……", "kind", () => done = true);
            while (!done) yield return null;

            // ② 环境文本,无表情
            done = false;
            DialogueSystem.ShowText("你获得了背包", () => done = true);
            while (!done) yield return null;

            // ③ 环境文本,无表情
            done = false;
            DialogueSystem.ShowText("离神庙越来越近,远处灰黑色的轮廓更加清晰。", () => done = true);
            while (!done) yield return null;

            // 拾取:仅置 flag + 显示背包 UI 按钮(背包本身不算一件道具,不 Add 进物品栏)
            //   之前会调 InventorySystem.Add(Items.Backpack),让背包自己进入自己的格子——违反直觉。
            //   2026-07-21 修:去掉 Add;背包 UI 打开时在下方字幕位置显示常驻描述(见 InventoryUI)。
            GameState.SetFlag("prologue_gate_backpack_taken", true);
            if (InventoryUI.Instance != null) InventoryUI.Instance.SetVisible(true);

            if (promptGo != null) Destroy(promptGo);
            Destroy(gameObject);

            // 恢复玩家控制
            if (pc != null) pc.SetControllable(true);
        }
    }

    /// <summary>感叹号跟随老人头顶 + 呼吸浮动 + 点击回调。独立于 PlayerTalkPrompt,避免和它的对话列表冲突。</summary>
    public class BackpackPromptFollower : MonoBehaviour
    {
        public Transform followTarget;
        // 2026-07-21 三修:与 promptHeadOffsetY 同步
        public float headOffsetY = 5.65f;
        public System.Action onClicked;

        float _phase;

        void LateUpdate()
        {
            if (followTarget == null) return;
            _phase += Time.deltaTime * 6.5f;
            float bob = Mathf.Sin(_phase) * 0.15f;
            var p = followTarget.position;
            p.y += headOffsetY + bob;
            p.z = 0f;
            transform.position = p;
        }

        void OnMouseDown()
        {
            onClicked?.Invoke();
        }
    }
}
