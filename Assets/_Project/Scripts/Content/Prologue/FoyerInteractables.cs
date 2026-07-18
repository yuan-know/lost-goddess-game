// ============================================================================
//  FoyerInteractables.cs —— 神庙门厅交互物集合(D5)
//  一个文件收下第一幕门厅的 4 个 Interactable(展台/石门/楼梯废墟),
//  外加 D3 密室(陶罐钥匙切青年)的 Portal 由 ScenePortal 直接摆,不写子类。
//
//  组件命名保持 "Interact_*" 前缀,与 InteractableBase 生态一致。
// ============================================================================

using UnityEngine;

namespace LostGoddess.Content
{
    // ─────────────────────────────────────────────────────────────────────
    //  Interact_Podium —— 展台(门前那个凹槽)
    //   · 老年点:播"门是锁死的,这好像缺了什么东西"(prologue_1_02)
    //   · 中年点(持有 LightProjector):放上去 → SetFlag(ProjectorSolved)
    //     → 触发 CloseupView("DiscPuzzle") 或占位对白(D5 只做占位,D6 补三层圆盘)
    // ─────────────────────────────────────────────────────────────────────
    public class Interact_Podium : InteractableBase
    {
        public override void OnClick()
        {
            // 中年 + 光幕投影仪 → 激活三层圆盘小游戏(D5 占位:直接过关)
            if (GameState.CurrentEra == Era.Middle && InventorySystem.Has(Items.LightProjector))
            {
                if (!GameState.GetFlag(Flags.Prologue_ProjectorSolved))
                {
                    // 占位:一句对白代替小游戏 → 直接解谜完成 + 开门 flag
                    DialogueSystem.ShowText(
                        "(占位:三层圆盘旋转小游戏 D6 补齐。此处直接判定解谜通过。)",
                        () =>
                        {
                            GameState.SetFlag(Flags.Prologue_ProjectorSolved, true);
                            GameState.SetFlag(Flags.Prologue_DoorOpen, true);
                            InventorySystem.Remove(Items.LightProjector);
                            DialogueSystem.ShowText("咔——嗡嗡……光幕折射出正确的图腾。门在颤动。");
                        });
                }
                else
                {
                    DialogueSystem.ShowText("(展台上还留着微弱的光幕。)");
                }
                return;
            }

            // 中年但无光幕:提示还需组合
            if (GameState.CurrentEra == Era.Middle)
            {
                DialogueSystem.ShowText("这个凹槽的形状……需要一件精密的仪器嵌进去。");
                return;
            }

            // 老年 / 青年 默认反应
            DialogueSystem.Show(Dialogues.prologue_1_02);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Interact_StoneDoor —— 石门(黄铜机械锁死的大门)
    //   · 未开:播 prologue_1_door_locked
    //   · 已开(Prologue_DoorOpen=true):跳 Prologue_Chase 触发黑雾追击剧情杀(D7)
    // ─────────────────────────────────────────────────────────────────────
    public class Interact_StoneDoor : InteractableBase
    {
        public override void OnClick()
        {
            if (GameState.GetFlag(Flags.Prologue_DoorOpen))
            {
                PlaySfx(Sfx.mechanism_rumble);
                DialogueSystem.Show(Dialogues.prologue_4_door_open, () =>
                {
                    SceneLoader.GoToRoom(Rooms.Prologue_Chase);
                });
            }
            else
            {
                DialogueSystem.Show(Dialogues.prologue_1_door_locked);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Interact_Assemble_Middle —— 组合工作台(中年拼 FocusLens + BrassBase → LightProjector)
    //  D7 补:落地清单 §2.6 要求中年下楼后在门厅有个"拼投影仪"的位置。
    //  · 老年/青年点:播提示对白(能力不足)
    //  · 中年 + 持 FocusLens + BrassBase:两件消耗 → 拿到 LightProjector
    //  · 已拿过 LightProjector:提示"投影仪已在包里,可以带去展台"
    // ─────────────────────────────────────────────────────────────────────
    public class Interact_Assemble_Middle : InteractableBase
    {
        public override void OnClick()
        {
            if (InventorySystem.Has(Items.LightProjector))
            {
                DialogueSystem.ShowText("光幕投影仪已经在包里。带去门前的展台。");
                return;
            }
            if (GameState.CurrentEra != Era.Middle)
            {
                DialogueSystem.Show(Dialogues.wrong_era_need_middle);
                return;
            }
            if (!InventorySystem.Has(Items.FocusLens) || !InventorySystem.Has(Items.BrassBase))
            {
                DialogueSystem.ShowText("这里是拼装的地方,但我手里的零件还不够。透镜和底座,两样都得凑齐。");
                return;
            }
            InventorySystem.Remove(Items.FocusLens);
            InventorySystem.Remove(Items.BrassBase);
            InventorySystem.Add(Items.LightProjector);
            DialogueSystem.Show(Dialogues.prologue_3_combine, () =>
            {
                DialogueSystem.Show(Dialogues.prologue_3_projector_use);
            });
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Interact_Stairs —— 楼梯废墟(通往二楼回廊)
    //   · 老年:播"这腿爬不上去了" prologue_1_cant_climb
    //   · 青年:跳 Prologue_UpperHall
    //   · 中年:未在剧情里被要求爬楼,退化为老年提示
    // ─────────────────────────────────────────────────────────────────────
    public class Interact_Stairs : InteractableBase
    {
        public override void OnClick()
        {
            if (GameState.CurrentEra == Era.Young)
            {
                DialogueSystem.ShowText("身手一纵,借力爬上了坍塌的楼梯。", () =>
                {
                    SceneLoader.GoToRoom(Rooms.Prologue_UpperHall);
                });
                return;
            }
            // 老年 / 中年 都爬不上去
            DialogueSystem.Show(Dialogues.prologue_1_cant_climb);
        }
    }
}
