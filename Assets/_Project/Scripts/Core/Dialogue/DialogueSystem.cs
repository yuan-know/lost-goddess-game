// ============================================================================
//  DialogueSystem.cs —— 对白/旁白(契约 §5)  🟢
//  Show(id) 按 id 从对白表播放。验证期用运行时创建的底部文本条 + 内置示例文本;
//  正式期文案表可换成 ScriptableObject / json,接口对 B 不变。
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LostGoddess
{
    public static class DialogueSystem
    {
        // 简单内置对白表(验证用)。正式期从 Data/Dialogues 资源加载。
        static readonly Dictionary<string, string> _table = new Dictionary<string, string>
        {
            { Dialogues.demo_intro,  "……又是这样一扇门。我这把老骨头,还能推开它吗?" },
            { Dialogues.demo_locked, "锁着的。得先找到点亮它的办法。" },

            // ── 前言(黑屏白字) ──
            { Dialogues.prologue_0_prelude,
              "很多年后,他依然会回想起初次踏入这座神庙的那个下午。\n" +
              "而当时他太过年轻,只沉浸在找到神庙的兴奋中,并没有注意到其他。\n" +
              "于是他花了几十年去回想那些被忽略的一切。\n" +
              "而此刻,他正走在重返神庙的路上。" },

            // ── 第 0 幕 荒山野道 ──
            { Dialogues.prologue_0_01, "这么多年了,还是回到了这里……" },
            { Dialogues.prologue_0_02, "什么都没变……这里的时间好像静止了……只有我,老得快要死了。" },
            { Dialogues.prologue_0_03, "它也一直在这里……几十年来,它好像一直在看着我……" },

            // ── 第一幕 神庙门厅 ──
            { Dialogues.prologue_1_01,           "为什么什么声音都没有,风声、雷声好像全被隔绝在这座庙之外了……太安静了……" },
            { Dialogues.prologue_1_02,           "门是锁死的,这好像缺了什么东西。这个形状是……" },
            { Dialogues.prologue_1_insight_hint, "(按 Q 使用【岁月洞察】)" },
            { Dialogues.prologue_1_mural,        "……墙上浮现出的图腾,像是从远古传来的低语。" },
            { Dialogues.prologue_1_upper_glow,   "……上面那里,有什么东西在闪着幽光。" },
            { Dialogues.prologue_1_cant_climb,   "这腿爬不上去了……先在周围看看有没有什么能用的吧。" },
            { Dialogues.prologue_1_door_locked,  "门是锁死的,现在推不开。" },
            { Dialogues.prologue_1_need_relic,   "我需要的东西,应该在上面……或者,先在周围看看有没有什么能用的吧。" },

            // ── 第二幕 青年 ──
            { Dialogues.prologue_2_awake,           "刚刚发生了什么?" },
            { Dialogues.prologue_2_resolve,         "算了,不管了。好不容易找到神庙,我得赶快解开门锁,找到神像!" },
            { Dialogues.prologue_2_got_crowbar,     "这玩意儿倒挺顺手的,这里好像没有其他东西能用了。我再去上面看看吧。" },
            { Dialogues.prologue_2_pry_cage,        "咔——铁笼被撬开了。" },
            { Dialogues.prologue_2_got_base,        "这是什么东西。等等,这个底座……好像和展台的形状挺像的。" },
            { Dialogues.prologue_2_assemble_fail,   "你无法完成精密仪器拼接!" },
            { Dialogues.prologue_2_gear_sound,     "这个声音……" },
            { Dialogues.prologue_2_wrong_era_middle,"该死,怎么拼接不起来。算了。再看看有没有其他的东西能用吧。" },

            // ── 第三幕 中年 ──
            { Dialogues.prologue_3_awake,          "奇怪,这是哪?我不是在修表吗。" },
            { Dialogues.prologue_3_combine,        "这两个,组合一下就可以得到……" },
            { Dialogues.prologue_3_projector_use,  "但这个在这里有什么用呢。" },
            { Dialogues.prologue_3_go_downstairs,  "还是先下楼看看吧。" },

            // ── 第四幕 大门开启 & 剧情杀 ──
            { Dialogues.prologue_4_door_open,   "大门缓缓开启,门后通道却并没有变得明亮,通道的尽头,有黑雾正在凝聚成形……" },
            { Dialogues.prologue_4_dark_fog,    "不对……那是什么,那是什么!" },
            { Dialogues.prologue_4_frozen,      "你转身想跑,但两条腿像灌了铅一样无法动弹……" },
            { Dialogues.prologue_4_switch_hint, "(按 1 切换青年)" },
            { Dialogues.prologue_4_died,        "怎么就这么……死了?" },
            { Dialogues.prologue_4_awakening,   "等等,死了还能想这件事吗?" },

            // ── Era 不匹配的通用提示 ──
            { Dialogues.wrong_era_need_young,  "这需要一双灵活的手,或者强壮的臂膀。此刻我做不到。" },
            { Dialogues.wrong_era_need_middle, "你毛躁的双手无法完成精密的拼接。" },
            { Dialogues.wrong_era_need_old,    "你还没有那双能看穿岁月的眼睛。" },

            // ── 第一章 场景旁白 ──
            { Dialogues.ch1_hall_intro,
              "来到了大殿,这里被林立的巨柱包围,中央残破的女神残缺的神像在阴影中伫立,透着永恒的肃穆与荒凉。" },
            { Dialogues.ch1_dining_intro,
              "曾经盛极一时的奢华盛宴早已散去,如今只剩下死一般的沉寂。" },
            { Dialogues.ch1_weapons_intro,
              "武器室里挂满了锈蚀的兵器与巨大的圆盾,粗重的锁链与机械齿轮在阴影中默默伫立。传说这里存放着上古神兵征战时使用的兵器。" },
            { Dialogues.ch1_corridor_intro,
              "走廊两旁,矗立着整齐划一的雕像,它们沉默地守护着古老的秘密。拱门上无数只形态各异的眼睛正如鬼魅般时刻凝视着入侵者。你可以听见鬼魂在不远处的叹息声。" },
        };

        static DialogueUI _ui;

        /// <summary>当前是否有对话正在播放(用于全局拦截场景点击)</summary>
        public static bool IsPlaying { get; internal set; }

        static void EnsureUI()
        {
            if (_ui != null) return;
            _ui = DialogueUI.CreateAttached();
        }

        public static void Show(string dialogueId) => Show(dialogueId, null);

        public static void Show(string dialogueId, Action onFinish)
        {
            EnsureUI();
            string text = _table.TryGetValue(dialogueId, out var t) ? t : dialogueId; // 找不到就直接显示 id 文本
            _ui.Play(text, onFinish);
        }

        /// <summary>显示一行文本,默认不显示表情(纯环境旁白/系统提示)</summary>
        public static void ShowText(string text, Action onFinish = null)
        {
            EnsureUI();
            _ui.Play(text, onFinish, showPortrait: false);
        }

        /// <summary>按 id 播放旁白/场景介绍文案,不显示人物表情特写。
        /// 场景介绍、环境描述等非角色对话用这个。</summary>
        public static void ShowNarration(string dialogueId, Action onFinish = null)
        {
            EnsureUI();
            string text = _table.TryGetValue(dialogueId, out var t) ? t : dialogueId;
            _ui.Play(text, onFinish, showPortrait: false);
        }

        /// <summary>显示角色内心独白(OS),带当前时代的默认表情特写</summary>
        public static void ShowMonologue(string text, Action onFinish = null)
        {
            EnsureUI();
            _ui.Play(text, onFinish, showPortrait: true);   // 用默认表情
        }

        /// <summary>显示角色对话,带指定表情。
        /// portraitKey 例如 "kind"/"smile"/"think"/"sad" 等,会自动拼成 UI/Portraits/{era}_{key}。
        /// 传 null/空 则用当前时代默认表情。</summary>
        public static void ShowMonologue(string text, string portraitKey, Action onFinish = null)
        {
            EnsureUI();
            _ui.Play(text, onFinish, showPortrait: true, portraitKey: portraitKey);
        }

        /// <summary>整组对话的一句:表情特写不淡出/不闪。
        /// hidePortraitAfter=false → 本句结束不淡出表情(供下一句复用,只切 sprite);
        /// 组末最后一句传 true 才会淡出。首句若表情未显示,会自动淡入。</summary>
        public static void ShowMonologue(string text, string portraitKey, Action onFinish, bool hidePortraitAfter)
        {
            EnsureUI();
            _ui.Play(text, onFinish, showPortrait: true, portraitKey: portraitKey, hidePortraitAfter: hidePortraitAfter);
        }

        /// <summary>强制立刻淡出表情特写(意外中断整组对话时兜底)。</summary>
        public static void HidePortrait()
        {
            if (_ui != null) _ui.HidePortraitNow();
        }
    }

    /// <summary>运行时创建的底部对白条(纯代码,零预制体)。点击/等待后消失。</summary>
    public class DialogueUI : MonoBehaviour
    {
        Text _label;
        CanvasGroup _cg;
        Action _onFinish;
        Coroutine _routine;

        // 2026-07-19 添加表情特写
        GameObject _portraitObject;
        Image _portraitImage;
        CanvasGroup _portraitCG;
        string _pendingPortraitKey;   // 2026-07-20 本次 Play 指定的表情 key(null=用默认)

        public static DialogueUI CreateAttached()
        {
            var go = new GameObject("~DialogueUI");
            DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 2026-07-20 sortingOrder 提到 1100,盖过 LetterboxOverlay(1000),
            // 让对话文字显示在下方黑边区域内(黑边已经是纯黑背景,天然的字幕带)。
            canvas.sortingOrder = 1100;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();

            // 2026-07-20 对话条:锚定屏幕底部,高度 = 下方黑边高度(1080 * 0.2018 ≈ 218),
            // 文字天然居中显示在黑边内;底色改成完全透明(黑边本身就是背景)。
            // 上边缘用一个 8px 高的更浅条做柔和过渡,避免"硬切"感 —— 现在直接省了。
            var barGo = new GameObject("Bar");
            barGo.transform.SetParent(go.transform, false);
            var barImg = barGo.AddComponent<Image>();
            barImg.color = new Color(0, 0, 0, 0f);   // 透明,由 LetterboxOverlay 的黑边充当底
            barImg.raycastTarget = false;
            var brt = barImg.rectTransform;
            brt.anchorMin = new Vector2(0, 0);
            brt.anchorMax = new Vector2(1, 0);
            brt.pivot = new Vector2(0.5f, 0);
            brt.sizeDelta = new Vector2(0, 1080f * LetterboxOverlay.BarHeightPct);  // 与黑边等高
            brt.anchoredPosition = Vector2.zero;

            // 文本
            var txtGo = new GameObject("Label");
            txtGo.transform.SetParent(barGo.transform, false);
            var label = txtGo.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 30;
            label.color = new Color(1f, 0.96f, 0.9f);   // 微暖白
            label.alignment = TextAnchor.MiddleCenter;   // 剧本感:居中
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            // 加轻微描边,让字幕在浅色画面上也读得清
            var outline = txtGo.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(120, 20);
            lrt.offsetMax = new Vector2(-120, -20);

            // 2026-07-20 表情特写按参考图布局:
            //   · 位置:紧贴屏幕右下角(整个屏幕最下方,不受 letterbox 黑边影响)
            //   · 高度:屏幕高的一半(1080/2 = 540)
            //   · 表情原图 1000×1000 正方形,preserveAspect=true → 宽度自动=540
            //   · 图层最前:独立子 Canvas + sortingOrder=1200,盖过对白文字(1100)/黑边(1000)/所有UI
            var portraitGo = new GameObject("Portrait");
            portraitGo.transform.SetParent(go.transform, false);
            // 独立 Canvas 提升 sortingOrder(需要 RectTransform,new GO 已自带)
            var portraitCanvas = portraitGo.AddComponent<Canvas>();
            portraitCanvas.overrideSorting = true;
            portraitCanvas.sortingOrder = 1200;   // 最前
            var portraitImg = portraitGo.AddComponent<Image>();
            portraitImg.preserveAspect = true;
            portraitImg.raycastTarget = false;
            var prt = portraitImg.rectTransform;
            prt.anchorMin = new Vector2(1f, 0f);  // 右下角锚点
            prt.anchorMax = new Vector2(1f, 0f);
            prt.pivot = new Vector2(1f, 0f);
            prt.anchoredPosition = new Vector2(0f, 0f);   // 紧贴屏幕右下角,无边距
            prt.sizeDelta = new Vector2(540, 540);        // 高=屏幕高一半(参考 1080),宽等比=540
            var portraitCG = portraitGo.AddComponent<CanvasGroup>();
            portraitCG.alpha = 0f;

            var ui = go.AddComponent<DialogueUI>();
            ui._label = label;
            ui._cg = go.AddComponent<CanvasGroup>();
            ui._cg.alpha = 0f;
            ui._cg.blocksRaycasts = false;
            ui._portraitObject = portraitGo;
            ui._portraitImage = portraitImg;
            ui._portraitCG = portraitCG;
            return ui;
        }

        public void Play(string text, Action onFinish, bool showPortrait = true, string portraitKey = null, bool hidePortraitAfter = true)
        {
            if (_routine != null) StopCoroutine(_routine);
            _onFinish = onFinish;
            _label.text = text;
            _pendingPortraitKey = portraitKey;
            _routine = StartCoroutine(Run(showPortrait, hidePortraitAfter));
        }

        /// <summary>立刻淡出表情特写(整组中断兜底)。</summary>
        public void HidePortraitNow()
        {
            if (_portraitCG != null && _portraitCG.alpha > 0f)
                StartCoroutine(FadePortrait(_portraitCG.alpha, 0f, 0.2f));
        }

        IEnumerator Run(bool showPortrait, bool hidePortraitAfter)
        {
            DialogueSystem.IsPlaying = true;

            // 加载并显示表情特写:只切 sprite,不重复淡入淡出(避免整组对话中的闪烁)
            if (showPortrait)
            {
                LoadPortrait();
                // 首次显示(alpha≈0)才淡入,否则保持已显示状态,直接换 sprite
                // 如果表情已经显示（alpha > 0.01），不管有没有换 sprite 都不重新淡入，避免闪烁
                if (_portraitCG.alpha < 0.01f && _portraitImage.sprite != null)
                {
                    StartCoroutine(FadePortrait(0f, 1f, 0.3f));
                }
            }

            // 如果字幕已经完全显示（alpha ≈ 1），不需要重新淡入
            // 只有首次显示才淡入 —— 整组连续对话中间只换文字不淡入淡出，彻底避免闪烁
            if (_cg.alpha < 0.99f)
            {
                _cg.blocksRaycasts = true;
                yield return FadeAlpha(0f, 1f, 0.25f);
            }

            // 最短保底显示时长
            float minShow = 0.4f;
            float t = 0f;
            while (t < minShow) { t += Time.deltaTime; yield return null; }

            // 等玩家点击继续
            while (!Input.GetMouseButtonDown(0)) yield return null;

            // 只有最后一句 (hidePortraitAfter=true) 才淡出字幕和表情
            // 中间句 (hidePortraitAfter=false) 不淡出字幕，直接结束让下一句换文字
            bool fadeDoneAlpha = false;
            bool fadeDonePortrait = false;

            if (hidePortraitAfter)
            {
                // 最后一句：淡出字幕 + (需要则淡出表情)
                _cg.blocksRaycasts = false;
                if (showPortrait && _portraitImage.sprite != null)
                {
                    // 同时启动两个淡出，都完成再继续
                    StartCoroutine(WaitAndFlag(FadeAlpha(1f, 0f, 0.2f), () => fadeDoneAlpha = true));
                    StartCoroutine(WaitAndFlag(FadePortrait(1f, 0f, 0.2f), () => fadeDonePortrait = true));
                    while (!fadeDoneAlpha || !fadeDonePortrait) yield return null;
                }
                else
                {
                    // 只淡出字幕
                    yield return FadeAlpha(1f, 0f, 0.2f);
                }
            }
            // else → 中间句，不淡出字幕，保持显示，让下一句直接换文字

            var cb = _onFinish;
            _onFinish = null;
            _routine = null;
            DialogueSystem.IsPlaying = false;
            cb?.Invoke();
        }

        IEnumerator WaitAndFlag(IEnumerator coroutine, Action onDone)
        {
            yield return coroutine;
            onDone();
        }

        void LoadPortrait()
        {
            // 根据当前时代加载对应表情
            string portraitPath = GetPortraitPath();
            if (string.IsNullOrEmpty(portraitPath))
            {
                Debug.LogWarning("[DialogueUI] 未找到当前时代的表情资源");
                return;
            }

            var sprite = Resources.Load<Sprite>(portraitPath);
            if (sprite == null)
            {
                // 尝试从Texture2D加载
                var tex = Resources.Load<Texture2D>(portraitPath);
                if (tex != null)
                {
                    sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    Debug.Log($"[DialogueUI] 从Texture2D加载表情: {portraitPath}");
                }
            }

            // 如果 sprite 没变化，不需要重新赋值，避免视觉闪烁
            if (_portraitImage.sprite != sprite)
                _portraitImage.sprite = sprite;
        }

        string GetPortraitPath()
        {
            // 2026-07-20 支持临时指定表情 key(每句对话可以不同)
            var era = GameState.CurrentEra;
            string prefix;
            switch (era)
            {
                case Era.Old:    prefix = "old"; break;
                case Era.Middle: prefix = "middle"; break;
                case Era.Young:  prefix = "young"; break;
                default: return null;
            }
            // 显式指定 key → 拼成 UI/Portraits/{prefix}_{key}
            if (!string.IsNullOrEmpty(_pendingPortraitKey))
                return $"UI/Portraits/{prefix}_{_pendingPortraitKey}";
            // 未指定 → 用当前时代的默认表情
            switch (era)
            {
                case Era.Old:    return "UI/Portraits/old_kind";
                case Era.Middle: return "UI/Portraits/middle_think";
                case Era.Young:  return "UI/Portraits/young_think";
            }
            return null;
        }

        IEnumerator FadePortrait(float from, float to, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                _portraitCG.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
                yield return null;
            }
            _portraitCG.alpha = to;
        }

        IEnumerator FadeAlpha(float from, float to, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                _cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
                yield return null;
            }
            _cg.alpha = to;
        }
    }
}
