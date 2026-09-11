// ============================================================================
//  FoyerInteractables.cs —— 神庙门厅交互物集合(D5)
//  一个文件收下第一幕门厅的 4 个 Interactable(展台/石门/楼梯废墟),
//  外加 D3 密室(陶罐钥匙切青年)的 Portal 由 ScenePortal 直接摆,不写子类。
//
//  组件命名保持 "Interact_*" 前缀,与 InteractableBase 生态一致。
// ============================================================================

using UnityEngine;
using UnityEngine.UI;

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
        bool _firstTimeDone = false;
        static GameObject _questPrompt;  // 常驻任务指引，只创建一次

        public override void OnClick()
        {
            // 2026-07-22 修复:点击展台时设置flag，防止放大镜重复刷新
            const string kFlag = "prologue_foyer_podium_clicked";
            GameState.SetFlag(kFlag, true);

            // 2026-07-25 持有光幕投影仪且未解 → 走"安装投影仪"路径
            bool hasProjector =
                InventorySystem.Has(Items.LightProjector) &&
                !GameState.GetFlag(Flags.Prologue_ProjectorSolved);

            if (hasProjector)
            {
                ShowPodiumProjectorInstall();
                return;
            }

            // 2026-07-19 添加:优先显示展台特写
            ShowPodiumCloseup();
        }

        void ShowPodiumCloseup()
        {
            // 2026-07-21 换特写:美术给了新的 Closeups/podium_closeup_dark(3400×1200,黑瓷砖俯视黄铜机关)
            //   与中间场景条带比例(2.98:1)几乎一致,直接铺满即可。
            var tex = Resources.Load<Texture2D>("Closeups/podium_closeup_dark");
            if (tex == null)
            {
                // 兜底旧图
                tex = Resources.Load<Texture2D>("Props/Prologue/prop_brass_base");
            }
            if (tex == null)
            {
                Debug.LogWarning("[Interact_Podium] 未找到展台特写资源 'Closeups/podium_closeup_dark'");
                ShowDefaultInteraction();
                return;
            }

            // 创建Sprite
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

            // 特写 UI:特写图尺寸 3400×1200(比例 2.833),中间条带 1920×~678(比例 2.833)——**完全一致**。
            //   直接给 sizeDelta=(1920, 678) 让图正好铺满中间条带,上下留出 letterbox 黑边(视觉与场景连贯)。
            //   preserveAspect=false 因为容器比例已与图匹配,无需再补留白。
            var go = new GameObject("_PodiumCloseupTemplate");
            var rt = go.AddComponent<RectTransform>();
            // 1080 × (1 - 2 × 0.1863) ≈ 677.7 → 取 678
            rt.sizeDelta = new Vector2(1920, 678);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.preserveAspect = false;     // 容器 = 图比例(2.833:1),不用 preserve
            img.raycastTarget = false;

            Debug.Log($"[Interact_Podium] 打开展台特写, texture={tex.width}x{tex.height}");

            // 锁定玩家控制
            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);

            // 2026-07-21 修复:必须通过 CloseupView.Open 将特写加到UI Canvas下才能显示
            //   之前直接创建在世界空间，相机看不到UI元素
            CloseupView.Open(go, null);

            // 2026-07-21:特写打开后,自动开始逐句字幕,点击切换下一句,全部播完才能点背景关闭
            StartCoroutine(PlaySubtitleSequence(go, pc, () =>
            {
                Object.Destroy(go);
                if (pc != null) pc.SetControllable(true);
                // 看完展台特写 → 显示左侧指引旗帜,销毁放大镜
                PrologueFoyerScene.OnPodiumCloseupDone();
                // 第一次看完 → 弹出感叹号继续对话
                if (!_firstTimeDone)
                {
                    _firstTimeDone = true;
                    SpawnExclamationPrompt();
                }
                // 非第一次:再次点击只显示一句静态文案,不播完整对话
                // 已经由 PlaySubtitleSequence 处理
            }));
        }

        // ── 投影仪安装路径(2026-07-25) ─────────────────────────────────
        //   玩家持有 LightProjector 时点击展台 → 展台特写 + 安装文案 + 闪白动画占位
        void ShowPodiumProjectorInstall()
        {
            var tex = Resources.Load<Texture2D>("Closeups/podium_closeup_dark");
            if (tex == null)
            {
                // 兜底:用旧的底座图
                tex = Resources.Load<Texture2D>("Props/Prologue/prop_brass_base");
            }
            if (tex == null)
            {
                Debug.LogWarning("[Interact_Podium] 未找到展台特写资源,退回默认交互");
                ShowDefaultInteraction();
                return;
            }

            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

            var go = new GameObject("_PodiumProjectorInstall");
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1920, 678);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.preserveAspect = false;
            img.raycastTarget = false;

            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);

            CloseupView.Open(go, null);

            StartCoroutine(PlayProjectorInstallSequence(go, pc));
        }

        System.Collections.IEnumerator PlayProjectorInstallSequence(GameObject closeupGo, PlayerController pc)
        {
            // 字幕播放期间禁用背景点击
            CloseupView.SetCanClose(false);

            bool done = false;
            DialogueSystem.ShowText(
                "提尔将光幕投影仪放上了展台的连接处，底座与展台完美的连接在了一起。",
                () => done = true);
            while (!done) yield return null;

            // ── 动画占位通道(闪一下) ──
            // 美术后续会给资源,这里先用白闪代替。
            // TODO(D6): 替换为真实的投影仪安装/启动动画。
            var overlay = FadeOverlayColored.Get();
            if (overlay != null)
            {
                yield return overlay.FadeToColor(Color.white, 0.2f);
                yield return new WaitForSecondsRealtime(0.15f);
                yield return overlay.FadeToClear(0.25f);
            }
            else
            {
                yield return new WaitForSecondsRealtime(0.5f);
            }

            // 允许关闭特写
            CloseupView.SetCanClose(true);

            // 等待玩家关闭
            yield return new WaitWhile(() => CloseupView.IsOpen);

            Object.Destroy(closeupGo);

            // 销毁展台上方的放大镜(安装阶段结束)
            PrologueFoyerScene.OnPodiumCloseupDone();

            // 注意:不恢复玩家控制,直接进入齿轮解谜(解谜结束后在回调里恢复)
            // ── D6: 开门齿轮解谜小游戏 ──
            //  安装完投影仪后立即弹出开门齿轮谜题,解谜成功 → 开门过场 → 设置 flag → 切换到大殿
            // 2026-07-27 修复: 解谜完成、动画播放完后切换到大殿场景
            GearDialCloseup.Show(() => {
                Debug.Log("========== [齿轮解谜回调] 开始执行 ==========");
                GameState.SetFlag(Flags.Prologue_ProjectorSolved, true);
                GameState.SetFlag(Flags.Prologue_DoorOpen, true);
                InventorySystem.Remove(Items.LightProjector);

                Debug.Log("[齿轮解谜回调] 尝试关闭特写...");
                CloseupView.Close();
                Debug.Log("[齿轮解谜回调] CloseupView.Close() 已调用, IsOpen=" + CloseupView.IsOpen);

                Debug.Log("[齿轮解谜回调] 创建场景切换Runner...");
                var runner = new GameObject("~SceneTransitionRunner");
                Object.DontDestroyOnLoad(runner);
                runner.AddComponent<SceneTransitionRunner>().StartTransition(Rooms.Chapter1_Hall, 1.0f);
                Debug.Log("[齿轮解谜回调] SceneTransitionRunner 已启动, 将在1秒后切换到大殿");
            });
        }

        /// <summary>在特写打开后逐句播放字幕,全部播完才允许关闭特写。</summary>
        System.Collections.IEnumerator PlaySubtitleSequence(GameObject closeupGo, PlayerController pc, System.Action onFinished)
        {
            bool done = false;

            // 字幕播放期间禁用背景点击关闭特写
            CloseupView.SetCanClose(false);

            if (!_firstTimeDone)
            {
                // 第一次点击：完整的四句对话
                // 1. 纯文案(无表情)
                DialogueSystem.ShowText("台面上布满了精密的机械纹路和齿轮。", () => done = true);
                while (!done) yield return null;
                done = false;

                // 2. 老年对话(带表情特写)
                // 连续对话 → 前一句不隐藏表情,留给下一句复用
                DialogueSystem.ShowMonologue("把特定部件放置上去，就可以触发这个机关。", null, () => done = true, hidePortraitAfter: false);
                while (!done) yield return null;
                done = false;

                // 3. 老年对话(带表情特写)
                DialogueSystem.ShowMonologue("让我检查一下，四个锁扣，凸起的……看来它缺个盖子", null, () => done = true, hidePortraitAfter: false);
                while (!done) yield return null;
                done = false;

                // 4. 老年对话(带表情特写)
                DialogueSystem.ShowMonologue("或者，是一个完整的机器，这个展台为机器供能，让它可以被使用", null, () => done = true, hidePortraitAfter: true);
                while (!done) yield return null;
                done = false;
            }
            else
            {
                // 非第一次点击：只显示一句静态文案
                DialogueSystem.ShowText("台面上布满了精密的机械纹路和齿轮。", () => done = true);
                while (!done) yield return null;
                done = false;
            }

            // 全部字幕播完 → 允许关闭特写
            CloseupView.SetCanClose(true);
            onFinished?.Invoke();
        }

        /// <summary>退出特写后,在玩家头顶生成感叹号,点击继续播放后续对话</summary>
        void SpawnExclamationPrompt()
        {
            var player = PlayerController.Instance?.gameObject;
            if (player == null)
            {
                ShowDefaultInteraction();
                return;
            }

            // 使用 PlayerTalkPrompt 复用现成的感叹号机制
            var prompt = PlayerTalkPrompt.Attach(player);
            prompt.SetDialogue(
                new PlayerTalkPrompt.Line("看来我们得去找找，有没有什么东西可以安装上这个展台。", null),
                new PlayerTalkPrompt.Line("还没有和你说过，我年轻时是个钟表匠，每天就和这些东西打交道呢。", null)
            );
            // 全部说完后 → 显示技能提示，然后显示任务指引
            prompt.OnAllSpoken += () =>
            {
                // 技能提示放在最后
                DialogueSystem.ShowText("【岁月洞察】技能：按下q键，让提示物体周围亮起光圈", () =>
                {
                    // 添加场景操作提示
                    DialogueSystem.ShowText("操控人物走到左/右边缘点击可切换场景", () =>
                    {
                        ShowQuestPrompt();
                        ShowDefaultInteraction();
                    });
                });
            };
            prompt.Show();
        }

        /// <summary>在屏幕左上角创建常驻任务指引: ● 收集工具，寻找可以被安装上展台的部件</summary>
        void ShowQuestPrompt()
        {
            // 如果已经存在，不需要重复创建
            if (_questPrompt != null)
                return;

            // 创建顶层 UI Canvas，排序高于一切保证可见
            _questPrompt = new GameObject("QuestPrompt");
            UnityEngine.Object.DontDestroyOnLoad(_questPrompt);

            var canvas = _questPrompt.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1300;  // 高于 CloseupView(1050)、Dialogue(1100)、Portrait(1200)
            var scaler = _questPrompt.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            _questPrompt.AddComponent<GraphicRaycaster>();

            // 创建背景面板
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(_questPrompt.transform, false);
            var panelRT = panelGo.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0f, 1f);  // 左上角锚点
            panelRT.anchorMax = new Vector2(0f, 1f);
            panelRT.pivot = new Vector2(0f, 1f);
            panelRT.anchoredPosition = new Vector2(20f, -20f);  // 边距 20px

            // 创建白色实心圆点
            var dotGo = new GameObject("Dot");
            dotGo.transform.SetParent(panelGo.transform, false);
            var dotImg = dotGo.AddComponent<Image>();
            dotImg.color = Color.white;
            dotImg.raycastTarget = false;
            var dotRT = dotGo.GetComponent<RectTransform>();
            dotRT.anchorMin = new Vector2(0f, 0.5f);
            dotRT.anchorMax = new Vector2(0f, 0.5f);
            dotRT.pivot = new Vector2(0.5f, 0.5f);
            dotRT.sizeDelta = new Vector2(16f, 16f);  // 圆点大小
            dotRT.anchoredPosition = new Vector2(8f, 0f);

            // 创建任务文字
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(panelGo.transform, false);
            var text = textGo.AddComponent<Text>();
            text.font = GameFonts.Primary;
            text.fontSize = 28;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.text = "  收集工具，寻找可以被安装上展台的部件";
            // 添加黑色描边保证可读性
            var outline = textGo.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            var textRT = text.GetComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0f, 0.5f);
            textRT.anchorMax = new Vector2(0f, 0.5f);
            textRT.pivot = new Vector2(0f, 0.5f);
            textRT.anchoredPosition = new Vector2(24f, 0f);
            textRT.sizeDelta = new Vector2(600f, 36f);

            Debug.Log("[Interact_Podium] 任务指引已创建");
        }

        void ShowDefaultInteraction()
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
    //  Interact_Mural —— 壁画交互（2026-07-19）
    //   · 洞察后显示在墙上
    //   · 点击显示全屏特写
    // ─────────────────────────────────────────────────────────────────────
    public class Interact_Mural : InteractableBase
    {
        private MuralPulseEffect _pulseEffect;  // 脉冲效果引用

        void Start()
        {
            // 在场景中查找壁画图层的脉冲效果组件
            _pulseEffect = Object.FindObjectOfType<MuralPulseEffect>();
            if (_pulseEffect == null)
            {
                Debug.LogWarning("[Interact_Mural] 未找到 MuralPulseEffect 组件");
            }
            else
            {
                Debug.Log("[Interact_Mural] 成功找到 MuralPulseEffect 组件");
            }
        }

        void Update()
        {
            // 监听Q键（岁月洞察技能键）
            if (Input.GetKeyDown(KeyCode.Q))
            {
                if (_pulseEffect != null && !_pulseEffect.IsActive())
                {
                    _pulseEffect.StartPulse();
                    Debug.Log("[Interact_Mural] 按下Q键，启动壁画脉冲效果");
                }
            }
        }

        public override void OnClick()
        {
            ShowMuralCloseup();
        }

        void ShowMuralCloseup()
        {
            // 加载壁画特写（宽一些的版本，用于全屏展示）
            var tex = Resources.Load<Texture2D>("Closeups/mural_prologue");
            if (tex == null)
            {
                // 如果旧版不存在，使用v2版本
                tex = Resources.Load<Texture2D>("Closeups/mural_prologue_v2");
            }

            if (tex == null)
            {
                Debug.LogWarning("[Interact_Mural] 未找到壁画特写资源");
                DialogueSystem.ShowText("古老的壁画，刻画着神秘的图腾和符号。");
                return;
            }

            // 创建Sprite
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

            // 创建特写UI对象 → 和展台特写一样铺满中间场景条带
            // 中间条带 1920 × 678，正好匹配letterbox比例，铺满整个场景区域
            var go = new GameObject("_MuralCloseupTemplate");
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1920, 678);  // 正好铺满中间letterbox区域
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.preserveAspect = false;  // 容器比例正好匹配，不用留白
            img.raycastTarget = false;

            Debug.Log($"[Interact_Mural] 打开壁画特写, texture={tex.width}x{tex.height}");

            // 锁定玩家控制
            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);

            // 2026-07-22: 和展台特写保持一致 → 在特写页面内播放文案，全部播完才能关闭
            CloseupView.Open(go, null);
            StartCoroutine(PlayMuralSubtitleSequence(go, pc, () =>
            {
                CloseupView.Close();
                Object.Destroy(go);
                if (pc != null) pc.SetControllable(true);

                // 关闭特写后停止脉冲效果
                if (_pulseEffect != null)
                {
                    _pulseEffect.StopPulse();
                    Debug.Log("[Interact_Mural] 关闭特写，停止壁画脉冲效果");
                }
            }));
        }

        /// <summary>在壁画特写打开后播放文案，全部播完才允许关闭特写。</summary>
        System.Collections.IEnumerator PlayMuralSubtitleSequence(GameObject closeupGo, PlayerController pc, System.Action onFinished)
        {
            bool done = false;

            // 字幕播放期间禁用背景点击关闭特写
            CloseupView.SetCanClose(false);

            // 播放文案（在特写页面内显示）
            DialogueSystem.ShowText("古老的壁画，似乎在讲述一个久远的故事……", () => done = true);
            while (!done) yield return null;

            // 全部字幕播完 → 允许关闭特写
            CloseupView.SetCanClose(true);
            onFinished?.Invoke();
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Interact_Crowbar —— 撬棍（2026-07-19）
    //   · 点击拾取撬棍，加入背包
    // ─────────────────────────────────────────────────────────────────────
    public class Interact_Crowbar : InteractableBase
    {
        public override void OnClick()
        {
            if (InventorySystem.Has(Items.Crowbar))
            {
                DialogueSystem.ShowText("你已经拿过撬棍了。");
                return;
            }

            // 显示特写并添加到背包
            ShowCrowbarCloseup();
        }

        void ShowCrowbarCloseup()
        {
            var tex = Resources.Load<Texture2D>("Props/Prologue/prop_crowbar");
            if (tex == null)
            {
                Debug.LogWarning("[Interact_Crowbar] 未找到撬棍特写资源");
                GiveCrowbar();
                return;
            }

            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

            var go = new GameObject("_CrowbarCloseupTemplate");
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(600, 400);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;

            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);

            CloseupView.Open(go, () =>
            {
                Object.Destroy(go);
                if (pc != null) pc.SetControllable(true);
                GiveCrowbar();
            });
        }

        void GiveCrowbar()
        {
            InventorySystem.Add(Items.Crowbar);
            DialogueSystem.ShowText("你得到了一根铁撬棍，看起来很结实。");
            // 隐藏场景中的撬棍
            if (gameObject != null) gameObject.SetActive(false);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Interact_Wrench —— 扳手（2026-07-19）
    //   · 点击拾取扳手，加入背包
    // ─────────────────────────────────────────────────────────────────────
    public class Interact_Wrench : InteractableBase
    {
        public override void OnClick()
        {
            if (InventorySystem.Has(Items.HexWrench))
            {
                DialogueSystem.ShowText("你已经拿过扳手了。");
                return;
            }

            // 显示特写并添加到背包
            ShowWrenchCloseup();
        }

        void ShowWrenchCloseup()
        {
            var tex = Resources.Load<Texture2D>("Closeups/wrench");
            if (tex == null)
            {
                Debug.LogWarning("[Interact_Wrench] 未找到扳手特写资源");
                GiveWrench();
                return;
            }

            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

            var go = new GameObject("_WrenchCloseupTemplate");
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(500, 300);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;

            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);

            CloseupView.Open(go, () =>
            {
                Object.Destroy(go);
                if (pc != null) pc.SetControllable(true);
                GiveWrench();
            });
        }

        void GiveWrench()
        {
            InventorySystem.Add(Items.HexWrench);
            DialogueSystem.ShowText("你得到了一把六角扳手，可能派得上用场。");
            // 隐藏场景中的扳手
            if (gameObject != null) gameObject.SetActive(false);
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

    // ============================================================================
    //  SceneTransitionRunner —— 延迟场景切换辅助类
    //   用于在解谜回调中执行场景切换,避免协程被场景销毁中断。
    //   挂在 DontDestroyOnLoad 对象上,延迟指定时间后切换场景并自毁。
    // ============================================================================
    public class SceneTransitionRunner : MonoBehaviour
    {
        public void StartTransition(string targetRoom, float delay)
        {
            StartCoroutine(TransitionCoroutine(targetRoom, delay));
        }

        System.Collections.IEnumerator TransitionCoroutine(string targetRoom, float delay)
        {
            Debug.Log($"[SceneTransitionRunner] 协程开始, 将在{delay}秒后切换到: {targetRoom}");
            yield return new WaitForSeconds(delay);
            Debug.Log($"[SceneTransitionRunner] 延迟结束, 现在切换场景: {targetRoom}");
            SceneLoader.GoToRoom(targetRoom);
            Debug.Log($"[SceneTransitionRunner] SceneLoader.GoToRoom 已调用");
            Destroy(gameObject);
        }
    }
}
