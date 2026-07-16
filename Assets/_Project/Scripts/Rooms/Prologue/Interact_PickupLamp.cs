// ============================================================================
//  Interact_PickupLamp.cs —— 示例交互物:捡起提灯(内容层 B 写法示范)  🟢
//  演示:老人走近 → 捡道具 → 入包 → 设 flag → 播音效 → 旁白。物件消失。
//  这是给 B 看的"如何继承 InteractableBase"范例,放在 Rooms/Prologue 示例目录。
// ============================================================================

using UnityEngine;

namespace LostGoddess.Content
{
    public class Interact_PickupLamp : InteractableBase
    {
        public string itemId = Items.item_lamp;
        public string pickedFlag = Flags.demo_lamp_lit;

        public override void OnClick()
        {
            PlaySfx(Sfx.pickup);
            InventorySystem.Add(itemId);
            GameState.SetFlag(pickedFlag, true);
            DialogueSystem.ShowText("一盏旧提灯……握住它,手竟不那么抖了。");
            gameObject.SetActive(false); // 捡走后消失
        }

        protected override void OnEraMismatch()
        {
            DialogueSystem.ShowText("这不是现在的我该碰的东西。");
        }
    }
}
