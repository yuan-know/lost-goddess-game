// ============================================================================
//  ScenePortal.cs —— 通用"点它跳房间"交互物(内容层 B 复用组件)
//  用法:场景里建一个 GameObject → 加 Collider2D(isTrigger)+ SpriteRenderer(可为空)→
//        挂本组件 → Inspector 或代码里填 targetRoom / requireItem / requireFlag / allowedEras。
//  逻辑:
//    · Era 不匹配 → InteractableBase 会自动播 wrongEraDialogueId(在基类里处理)
//    · requireItem 非空 && 未持有 → 播 lackItemDialogueId
//    · requireFlag 非空 && flag 未 true → 播 lackFlagDialogueId
//    · 全通过 → 可选播 successDialogueId(默认无) → SceneLoader.GoToRoom(targetRoom)
//  失败对白全部为空时 → 默认走 wrongEraDialogueId(基类)或 Debug.Log 一句。
// ============================================================================

using UnityEngine;

namespace LostGoddess.Content
{
    public class ScenePortal : InteractableBase
    {
        [Header("目标房间")]
        [Tooltip("SceneLoader.GoToRoom 会用这个名字")]
        public string targetRoom = "";

        [Tooltip("场景切换淡入淡出时长(0=用 SceneLoader.DefaultFade)")]
        public float fadeTime = 0f;

        [Header("前置条件")]
        [Tooltip("需要持有的道具 id(空=不需要)")]
        public string requireItem = "";
        [Tooltip("道具不足时播的对白 id(空=沉默)")]
        public string lackItemDialogueId = "";

        [Tooltip("需要为 true 的 flag(空=不需要)")]
        public string requireFlag = "";
        [Tooltip("flag 未满足时播的对白 id(空=沉默)")]
        public string lackFlagDialogueId = "";

        [Header("成功过场")]
        [Tooltip("跳场景前播的对白 id(空=不播,直接跳)")]
        public string successDialogueId = "";

        public override void OnClick()
        {
            // Item 检查
            if (!string.IsNullOrEmpty(requireItem) && !InventorySystem.Has(requireItem))
            {
                if (!string.IsNullOrEmpty(lackItemDialogueId))
                    DialogueSystem.Show(lackItemDialogueId);
                return;
            }
            // Flag 检查
            if (!string.IsNullOrEmpty(requireFlag) && !GameState.GetFlag(requireFlag))
            {
                if (!string.IsNullOrEmpty(lackFlagDialogueId))
                    DialogueSystem.Show(lackFlagDialogueId);
                return;
            }
            // 通过 → 走过场对白(如有)然后跳场景
            if (!string.IsNullOrEmpty(successDialogueId))
            {
                DialogueSystem.Show(successDialogueId, DoGoToRoom);
            }
            else
            {
                DoGoToRoom();
            }
        }

        void DoGoToRoom()
        {
            if (string.IsNullOrEmpty(targetRoom))
            {
                Debug.LogWarning($"[ScenePortal] {name}: targetRoom 未填,不跳场景。");
                return;
            }
            if (fadeTime > 0f) SceneLoader.GoToRoom(targetRoom, fadeTime);
            else SceneLoader.GoToRoom(targetRoom);
        }
    }
}
