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
    //  Interact_Pottery —— 陶罐(全部可交互;藏钥匙的会弹出特写)
    // ─────────────────────────────────────────────────────────────────────
    public class Interact_Pottery : InteractableBase
    {
        [Tooltip("这个陶罐里是否藏了钥匙")]
        public bool hasKey;
        bool _broken;

        static readonly string[] _emptyTexts = new string[]
        {
            "只是空罐子。",
            "里面只有碎陶片。",
            "什么也没找到。",
            "罐子已经裂了,里面空空如也。"
        };

        public override void OnClick()
        {
            if (_broken) return;
            _broken = true;
            PlaySfx(Sfx.pottery_break);

            // 打碎:完全淡出并禁 Collider
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) { var c = sr.color; c.a = 0f; sr.color = c; }
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            if (hasKey)
            {
                ShowKeyCloseup();
            }
            else
            {
                // 2026-07-24 用户要求:没钥匙的陶罐点击随机弹一条现有文案
                int idx = Random.Range(0, _emptyTexts.Length);
                DialogueSystem.ShowText(_emptyTexts[idx]);
            }
        }

        void ShowKeyCloseup()
        {
            // key_reveal.png 导入类型是 Texture (不是Sprite),所以用 Texture2D 加载
            var texture = Resources.Load<Texture2D>("Closeups/key_reveal");
            if (texture == null)
            {
                // 特写图缺失时的兜底:直接给钥匙并提示
                Debug.LogWarning("[Interact_Pottery] 未找到特写资源 'Closeups/key_reveal'");
                GiveKey();
                SpawnExclamationPrompt();
                return;
            }

            // 从 Texture2D 创建 Sprite
            var sprite = Sprite.Create(texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));

            // 运行时组装一个 UI 特写对象(用 CloseupView 弹出)
            var go = new GameObject("_KeyRevealTemplate");
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(800f, 600f);  // 固定尺寸
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;  // 图片本身不拦截点击,让背景按钮能响应

            Debug.Log($"[Interact_Pottery] 成功打开钥匙特写, texture={texture.width}x{texture.height}");

            // 锁定玩家控制,防止特写期间移动
            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);

            // 打开特写(必须调用CloseupView.Open才能正确显示)
            CloseupView.Open(go);
            Debug.Log("[Interact_Pottery] 钥匙特写已通过CloseupView.Open打开");

            // 用户要求:特写里带上文案"一枚覆满铜锈的钥匙"
            CloseupView.SetCanClose(false);
            StartCoroutine(PlayKeyCloseupSequence(go, pc));
        }

        /// <summary>钥匙特写打开后播放文案，关闭特写后弹出感叹号</summary>
        System.Collections.IEnumerator PlayKeyCloseupSequence(GameObject closeupGo, PlayerController pc)
        {
            bool done = false;
            // 特写内显示文案
            DialogueSystem.ShowText("一枚覆满铜锈的钥匙", () => done = true);
            while (!done) yield return null;

            // 允许关闭特写
            CloseupView.SetCanClose(true);

            // 等玩家点击背景关闭特写
            yield return new WaitWhile(() => CloseupView.IsOpen);

            // 关闭完成，清理并给钥匙
            Object.Destroy(closeupGo);
            GiveKey();
            if (pc != null) pc.SetControllable(true);

            // 弹出感叹号，点击播放后续对话
            SpawnExclamationPrompt();
        }

        void SpawnExclamationPrompt()
        {
            var player = PlayerController.Instance?.gameObject;
            if (player == null) return;

            // 使用 PlayerTalkPrompt 复用现成的感叹号机制
            var prompt = PlayerTalkPrompt.Attach(player);
            prompt.SetDialogue(
                new PlayerTalkPrompt.Line("到目前为止都很顺利，不是吗？", null),
                new PlayerTalkPrompt.Line("把锁打开究竟会获得什么？", null),
                new PlayerTalkPrompt.Line("试一下就知道了", null)
            );
            prompt.Show();
        }

        void GiveKey()
        {
            if (!InventorySystem.Has(Items.PotteryKey))
                InventorySystem.Add(Items.PotteryKey);
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

            // 2026-07-23 剧情迭代中：闪白切青年 → 青年头顶弹出感叹号，玩家点击后播 8 句对话。
            //   注意：SwitchEraStep 会销毁老年 Player + 重建青年 Player，
            //   所以感叹号必须在 OnFinished 里、SwitchEraStep 跑完之后再挂到"新"Player 上。
            var cs = gameObject.AddComponent<Cutscene>();
            cs.Add(new SetFlagStep(Flags.Prologue_UnlockedYoung, true))
              .Add(new SwitchEraStep(Era.Young, flash: true));   // 闪白 + 重建 Player，会自动 UnlockEra
            cs.OnFinished += () =>
            {
                InventorySystem.Remove(Items.PotteryKey);

                // 2026-07-25 开锁完成 → 移除左上角"寻找钥匙开锁"任务指引(DontDestroyOnLoad 常驻对象)
                var quest = GameObject.Find("QuestPrompt");
                if (quest != null) Object.Destroy(quest);
                // SwitchEraStep 已跑完 → PlayerController.Instance 现在指向新建的青年 Player
                var youngPlayer = PlayerController.Instance;
                if (youngPlayer == null) return;

                // 交还控制权：玩家可以自由走动，直到点头顶感叹号触发对话
                youngPlayer.SetControllable(true);

                // 头顶感叹号 + 8 句对话（表情按语气切换）
                var talk = PlayerTalkPrompt.Attach(youngPlayer.gameObject);
                talk.SetDialogue(
                    new PlayerTalkPrompt.Line("刚刚发生了什么？",            "think"),  // 疑惑
                    new PlayerTalkPrompt.Line("怎么又回到了这里.......",     "frown"),  // 皱眉不悦
                    new PlayerTalkPrompt.Line("算了，好不容易进入了神庙",     "think"),  // 收拾心情
                    new PlayerTalkPrompt.Line("没有什么值得惧怕的",           "happy"),  // 自信
                    new PlayerTalkPrompt.Line("我们必须进入最深处找到神像！", "happy"),  // 坚定
                    new PlayerTalkPrompt.Line("一定会见到女神的，对吧？",     "shy"),    // 不确定
                    new PlayerTalkPrompt.Line("你好，我是提尔！",             "smile"),  // 微笑打招呼
                    new PlayerTalkPrompt.Line("那么，一起去二楼看看吧",       "smile")   // 提议
                );
                talk.Show();
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
