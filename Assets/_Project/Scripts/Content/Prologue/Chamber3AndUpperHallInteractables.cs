// ============================================================================
//  Chamber3AndUpperHallInteractables.cs —— 密室 3 + 二楼回廊的交互物集合(D6)
//
//  · Interact_Pottery(密室 3):打碎陶罐,有 1/3 藏钥匙
//  · Interact_Lockhole(密室 3):池底锁孔,插【陶罐钥匙】→ Cutscene 切青年 → 回门厅
//  · Interact_IronCage(UpperHall):青年 + 铁撬棍 → 撬开 → 拿【聚焦透镜】
//  · Interact_GearBox(UpperHall):青年 → 拽出【黄铜底座】
//  · Interact_Gears(UpperHall):青年 → 拨齿轮 → 触发闪回(切中年,场景切到密室 2)
// ============================================================================

using UnityEngine;

namespace LostGoddess.Content
{
    // ─────────────────────────────────────────────────────────────────────
    //  Interact_Pottery —— 陶罐(打碎 → 有 hasKey=true 时掉钥匙)
    // ─────────────────────────────────────────────────────────────────────
    public class Interact_Pottery : InteractableBase
    {
        [Tooltip("这个陶罐里是否藏了钥匙")]
        public bool hasKey;
        bool _broken;

        public override void OnClick()
        {
            if (_broken) return;
            _broken = true;
            PlaySfx(Sfx.pottery_break);

            // 打碎:视觉上淡出到 0.15 alpha 并禁 Collider,保留碎片提示
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) { var c = sr.color; c.a = 0.15f; sr.color = c; }
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            if (hasKey)
            {
                if (!InventorySystem.Has(Items.PotteryKey))
                    InventorySystem.Add(Items.PotteryKey);
                DialogueSystem.ShowText("碎陶片里滚出一枚锈迹斑斑的钥匙——像是给池底那个锁孔用的。");
            }
            else
            {
                DialogueSystem.ShowText("只是空罐子。");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Interact_Lockhole —— 池底锁孔(触发切青年剧情杀)
    // ─────────────────────────────────────────────────────────────────────
    public class Interact_Lockhole : InteractableBase
    {
        bool _fired;

        public override void OnClick()
        {
            if (_fired) return;
            if (!InventorySystem.Has(Items.PotteryKey))
            {
                DialogueSystem.ShowText("池底有一个锁孔。得先找到能插进去的东西。");
                return;
            }
            _fired = true;

            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);

            var cs = gameObject.AddComponent<Cutscene>();
            cs.Add(new SayTextStep("钥匙滑进锁孔——池水开始颤动,倒影里出现了另一张脸。"))
              .Add(new SetFlagStep(Flags.Prologue_UnlockedYoung, true))
              .Add(new SwitchEraStep(Era.Young, flash: true))   // 内置闪白 + 重建 Player
              .Add(new WaitStep(0.2f))
              .Add(new SayStep(Dialogues.prologue_2_awake))     // "刚刚发生了什么?"
              .Add(new SayStep(Dialogues.prologue_2_resolve))   // "得赶快解开门锁"
              .Add(new GoToSceneStep(Rooms.Prologue_Foyer, 0.6f));
            cs.OnFinished += () =>
            {
                InventorySystem.Remove(Items.PotteryKey);
                var p = PlayerController.Instance;
                if (p != null) p.SetControllable(true);
            };
            cs.Play();
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Interact_IronCage —— 铁笼(青年 + 撬棍 → 拿聚焦透镜)
    // ─────────────────────────────────────────────────────────────────────
    public class Interact_IronCage : InteractableBase
    {
        bool _opened;

        public override void OnClick()
        {
            if (_opened) { DialogueSystem.ShowText("笼子已经开了,里面空空的。"); return; }

            if (GameState.CurrentEra != Era.Young)
            {
                DialogueSystem.Show(Dialogues.wrong_era_need_young);
                return;
            }
            if (!InventorySystem.Has(Items.Crowbar))
            {
                DialogueSystem.ShowText("锁扣锈死了。徒手拧不开,得找个撬的家伙。");
                return;
            }
            _opened = true;
            PlaySfx(Sfx.mechanism_click);
            DialogueSystem.Show(Dialogues.prologue_2_pry_cage, () =>
            {
                InventorySystem.Add(Items.FocusLens);
                DialogueSystem.ShowText("笼子里躺着一片打磨光洁的透镜。");
            });
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Interact_GearBox —— 齿轮箱(青年 → 拽出黄铜底座)
    // ─────────────────────────────────────────────────────────────────────
    public class Interact_GearBox : InteractableBase
    {
        bool _taken;

        public override void OnClick()
        {
            if (_taken) { DialogueSystem.ShowText("箱子里已经空了。"); return; }
            if (GameState.CurrentEra != Era.Young)
            {
                DialogueSystem.Show(Dialogues.wrong_era_need_young);
                return;
            }
            _taken = true;
            InventorySystem.Add(Items.BrassBase);
            DialogueSystem.Show(Dialogues.prologue_2_got_base);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Interact_Gears —— 齿轮机关(青年拨动 → 触发切中年 + 场景切密室 2)
    //  D6 简版:直接触发切中年(棺材小游戏在 D7 补),完成"从青年过渡到中年"闭环。
    // ─────────────────────────────────────────────────────────────────────
    public class Interact_Gears : InteractableBase
    {
        bool _fired;

        public override void OnClick()
        {
            if (_fired) return;
            if (GameState.CurrentEra != Era.Young)
            {
                DialogueSystem.Show(Dialogues.wrong_era_need_young);
                return;
            }
            _fired = true;

            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);

            var cs = gameObject.AddComponent<Cutscene>();
            cs.Add(new SayStep(Dialogues.prologue_2_gear_sound))
              .Add(new WaitStep(0.3f))
              .Add(new GoToSceneStep(Rooms.Prologue_Chamber2, 0.6f));
            cs.OnFinished += () =>
            {
                var p = PlayerController.Instance;
                if (p != null) p.SetControllable(true);
            };
            cs.Play();
        }
    }
}
