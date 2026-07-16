// ============================================================================
//  Interact_Door.cs —— 示例交互物:门(内容层 B 写法示范)  🟢
//  演示:老人走近 → 若已点亮提灯(持有 item_lamp)则开门+切房间;
//        否则播"锁着"旁白。展示 flag/背包判定 + SceneLoader 用法。
// ============================================================================

using UnityEngine;

namespace LostGoddess.Content
{
    public class Interact_Door : InteractableBase
    {
        [Header("门配置")]
        public string requireItem = Items.item_lamp;  // 需要提灯才能开
        public string targetRoom  = Rooms.P_01_Hall;  // 开门后去的房间
        public string openedFlag  = Flags.demo_door_unlocked;

        public override void OnClick()
        {
            if (GameState.GetFlag(openedFlag) || InventorySystem.Has(requireItem))
            {
                GameState.SetFlag(openedFlag, true);
                PlaySfx(Sfx.door_open);
                DialogueSystem.ShowText("门开了。前面还有更深的黑暗——但我已经有了光。", () =>
                {
                    SceneLoader.GoToRoom(targetRoom);
                });
            }
            else
            {
                DialogueSystem.Show(Dialogues.demo_locked);
            }
        }
    }
}
