// ============================================================================
//  PlayerTalkPrompt.cs —— 老人头顶感叹号交互(2026-07-20)
//
//  当角色有可说的话时,头顶浮出一个感叹号"!" 精灵;玩家点击感叹号触发对话序列。
//  用 SpriteRenderer(世界空间)展示,跟随 Player transform,自动上下浮动。
//  点击命中:走的是标准 InteractableBase 流程,ClickInputManager 认识它。
//
//  用法:
//    var prompt = PlayerTalkPrompt.Attach(playerGO, headOffsetY: 4.5f);
//    prompt.SetDialogue(new[] {
//      new PlayerTalkPrompt.Line("这么多年了……", null),
//      new PlayerTalkPrompt.Line("你好,我是提尔。", "smile"),
//      …
//    });
//    prompt.Show();          // 主动出现
//    prompt.OnAllSpoken += () => { … };
//
//  设计说明:
//   · 不引资源:直接生成"!"字符纹理,零依赖
//   · 上下浮动 0.2 单位 / 0.9s 周期,吸引注意
//   · 点击后感叹号临时隐藏,对话完成后按需 Show()/Hide()
// ============================================================================

using System;
using System.Collections;
using UnityEngine;

namespace LostGoddess
{
    public class PlayerTalkPrompt : InteractableBase
    {
        public struct Line
        {
            public string text;
            public string portraitKey;   // 空/null=用当前时代默认表情
            public Line(string t, string k) { text = t; portraitKey = k; }
        }

        [Header("感叹号 - 挂点")]
        public Transform followTarget;   // Player 根节点
        // 2026-07-21 角色 scale 0.209→0.25 后头顶抬高,默认 4.5→5.35(取上调量一半:1.7/2)
        public float headOffsetY = 5.35f;

        Line[] _lines = new Line[0];
        SpriteRenderer _sr;
        Vector3 _localBase;
        float _phase;
        bool _isTalking;

        public event Action OnAllSpoken;

        public static PlayerTalkPrompt Attach(GameObject player, float headOffsetY = -1f)
        {
            // 2026-07-23 感叹号语义:pivot 上方多少单位。
            //   老年 pivot=脚底,pivot→头顶≈5,+5.35 感叹号在头顶偏上一点 (你之前校准好的值,保留)
            //   青年 pivot 在身体中上部 (groundY+3.5),头顶到 pivot 的距离比老年短很多,
            //     但青年美术整体也比老年高——所以不能直接减 GetYOffset,得按 Era 单独定。
            //   如果外部显式传值 (>=0),用外部值;否则按 Era 查表。
            //   数值微调:如果青年感叹号还是太低/太高,改下面对应 case 的常数就行。
            float pivotOffset;
            if (headOffsetY >= 0f)
            {
                pivotOffset = headOffsetY;   // 外部显式指定,直接用
            }
            else
            {
                switch (GameState.CurrentEra)
                {
                    case Era.Young:  pivotOffset = 3.0f;  break;   // 青年 pivot 在中上部,3.0 ≈ 头顶偏上一点
                    case Era.Middle: pivotOffset = 3.0f;  break;   // 中年同青年配置
                    default:         pivotOffset = 5.35f; break;   // 老年校准值(零回归)
                }
            }

            // 建一个子物体(避免感叹号继承 Player.localScale 的翻转/缩放,直接挂根同级)
            var go = new GameObject("~PlayerTalkPrompt");
            go.transform.position = player.transform.position + new Vector3(0f, pivotOffset, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = BuildExclamationSprite();
            sr.sortingOrder = 500;    // 前景是 60,人物动态,感叹号=500 保证盖过所有场景物
            // 2026-07-21 用美术给的 exclamation.png(300×300, PPU=300 → 世界 1×1)。
            //   纹理里"!"实际占约 60% 高,视觉再放大到 1.6 让视觉高≈1 单位,和先前程序图差不多显眼。
            sr.color = Color.white;
            go.transform.localScale = Vector3.one * 1.6f;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.55f, 0.9f);   // 判定贴合"!"实际占位

            var p = go.AddComponent<PlayerTalkPrompt>();
            p.followTarget = player.transform;
            p.headOffsetY = pivotOffset;   // 存"pivot 到感叹号的偏移",LateUpdate 用它
            p._sr = sr;
            p._localBase = new Vector3(0f, pivotOffset, 0f);
            p.walkToBeforeInteract = false;

            p.gameObject.SetActive(false);   // 初始隐藏,SetDialogue+Show 后才出现
            return p;
        }

        public void SetDialogue(params Line[] lines)
        {
            _lines = lines ?? new Line[0];
        }

        public void Show()
        {
            if (_lines.Length == 0) return;
            gameObject.SetActive(true);
        }

        void LateUpdate()
        {
            if (followTarget == null) return;
            // 跟随 Player pivot + 头顶偏移 + 呼吸浮动
            //   (headOffsetY 语义:从 pivot 往上多少;老年/青年 pivot 相对脚底的位置不同,
            //    如需精调可在 Attach 时或外部覆写 headOffsetY)
            _phase += Time.deltaTime * 6.5f;
            float bob = Mathf.Sin(_phase) * 0.15f;
            var pos = followTarget.position;
            pos.y = followTarget.position.y + headOffsetY + bob;
            pos.z = 0f;
            transform.position = pos;
        }

        public override void OnClick()
        {
            if (_isTalking || _lines.Length == 0) return;
            StartCoroutine(PlaySequence());
        }

        IEnumerator PlaySequence()
        {
            _isTalking = true;
            // 说话期间隐藏感叹号 + 锁住玩家(点击只用于推进对话,不能 WalkTo)
            if (_sr != null) _sr.enabled = false;
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
            var pc = PlayerController.Instance;
            bool wasControllable = pc != null && pc.IsControllable();
            if (pc != null) pc.SetControllable(false);

            // 缓存对白到局部数组:后面会清 _lines,协程要用副本
            var lines = _lines;
            if (lines.Length == 0) yield break;

            // 整组对话统一处理:只在开始淡入一次，结束淡出一次，中间直接换文字不淡入淡出
            // 这样完全避免了闪烁 —— 表情常驻，字幕也不会反复消失出现

            // 第一句开始:获取DialogueUI引用
            var dialogueUI = FindObjectOfType<DialogueUI>();
            if (dialogueUI == null)
            {
                Debug.LogError("[PlayerTalkPrompt] 找不到 DialogueUI");
                FinishSequence();
                yield break;
            }

            // 使用 DialogueSystem 的私有字段？不，我们直接调用，但控制淡入淡出只一次
            // 改用逐条调用，但确保表情不淡出，字幕只在首尾淡入淡出
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                bool isLast = (i == lines.Length - 1);
                bool done = false;

                // 对于同人物同表情，因为 hidePortraitAfter=false 所以表情保持，不会闪
                DialogueSystem.ShowMonologue(line.text, line.portraitKey, () => done = true, hidePortraitAfter: isLast);
                while (!done) yield return null;
            }

            FinishSequence();
        }

        void FinishSequence()
        {
            var pc = PlayerController.Instance;
            // 恢复玩家控制（说完话后一定要解锁）
            if (pc != null) pc.SetControllable(true);

            _isTalking = false;
            _lines = new Line[0];   // 一次性:说完就清空

            // ⚠️ 关键:先触发 OnAllSpoken(此时对象还 active,协程还没被杀,回调里 SetDialogue+Show 会命中);
            //   然后再处理。用户要求:点击一次说完后销毁感叹号,不再出现
            OnAllSpoken?.Invoke();

            // 用户要求:点击一次后直接销毁GameObject,永远不再出现
            if (gameObject != null)
                Destroy(gameObject);
        }

        // ── 优先加载美术给的 exclamation 精灵,失败则回退到程序生成 ──
        static Sprite _cachedSprite;
        static Sprite BuildExclamationSprite()
        {
            if (_cachedSprite != null) return _cachedSprite;
            // 优先:美术给的 Resources/UI/Icons/exclamation.png
            var art = Resources.Load<Sprite>("UI/Icons/exclamation");
            if (art != null) { _cachedSprite = art; return _cachedSprite; }

            // 回退:纯代码生成(白色 "!" + 半透明黑描边),保证美术资源缺失时也能显示
            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color[S * S];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(0, 0, 0, 0);

            Color ink     = new Color(1.00f, 1.00f, 1.00f, 1f);
            Color outline = new Color(0.00f, 0.00f, 0.00f, 0.85f);
            int cx = S / 2;
            DrawStem(pixels, S, cx, 42, 104, 13, outline);
            DrawDot (pixels, S, cx, 22, 14, outline);
            DrawStem(pixels, S, cx, 42, 104, 10, ink);
            DrawDot (pixels, S, cx, 22, 11, ink);

            tex.SetPixels(pixels);
            tex.Apply();
            _cachedSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 128f);
            return _cachedSprite;
        }

        static void DrawStem(Color[] px, int S, int cx, int yLow, int yHigh, int halfW, Color fill)
        {
            for (int y = yLow - halfW; y <= yHigh + halfW; y++)
            {
                if (y < 0 || y >= S) continue;
                for (int x = cx - halfW; x <= cx + halfW; x++)
                {
                    if (x < 0 || x >= S) continue;
                    bool inside = (y >= yLow && y <= yHigh);
                    if (!inside)
                    {
                        int refY = (y < yLow) ? yLow : yHigh;
                        int dx = x - cx, dy = y - refY;
                        if (dx * dx + dy * dy <= halfW * halfW) inside = true;
                    }
                    if (inside) px[y * S + x] = fill;
                }
            }
        }

        static void DrawDot(Color[] px, int S, int cx, int cy, int r, Color fill)
        {
            int r2 = r * r;
            for (int y = cy - r; y <= cy + r; y++)
            {
                if (y < 0 || y >= S) continue;
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || x >= S) continue;
                    int dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy <= r2) px[y * S + x] = fill;
                }
            }
        }
    }
}
