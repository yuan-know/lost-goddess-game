// ============================================================================
//  PlayerBuilder.cs —— 玩家角色重建(接管 SandboxBootstrap.BuildPlayer 立绘 swap 逻辑)
//  目的:序幕内多次剧情切 Era(如 陶罐钥匙→切青年 / 棺材→切中年 / 剧情杀→切青年),
//        程序侧统一由 PlayerBuilder.Rebuild(era) 处理:销毁旧 Player → 加载新形态骨骼/立绘 →
//        原地重建 PlayerController + Animator。
//  自动响应:PlayerBuilder.EnableAutoRebuild() 后,GameState.OnEraChanged 触发时自动重建。
//
//  用法:
//    正式场景里生成第一个 Player → PlayerBuilder.Build(era, groundY, spawnX)
//    序幕想让 SetEra 自动换装 → PlayerBuilder.EnableAutoRebuild()
//    验证沙盒也复用它,不再自己写立绘 swap。
// ============================================================================

using UnityEngine;

namespace LostGoddess
{
    public static class PlayerBuilder
    {
        static bool _autoRebuildInstalled;
        static float _lastGroundY = -2.4f;

        /// <summary>启用自动响应 GameState.OnEraChanged(整个游戏只需调一次)。</summary>
        public static void EnableAutoRebuild()
        {
            if (_autoRebuildInstalled) return;
            GameState.OnEraChanged += OnEraChanged;
            _autoRebuildInstalled = true;
        }

        static void OnEraChanged(Era from, Era to)
        {
            var existing = GameObject.Find("Player");
            if (existing == null) return;  // 场景里没 Player,不重建(避免误建)
            _lastGroundY = existing.transform.position.y;
            float spawnX = existing.transform.position.x;
            Object.Destroy(existing);
            Build(to, _lastGroundY, spawnX);
        }

        /// <summary>按当前 Era 建 Player,放置在 (spawnX, groundY)。</summary>
        public static GameObject Build(Era era, float groundY, float spawnX = 0f)
        {
            _lastGroundY = groundY;
            string spriteName = EraToSpriteName(era);

            // 1) 优先加载骨骼动画 Prefab(Resources/Characters_Rigged/{era}.prefab)——含 SpriteSkin + Animator
            var riggedPrefab = Resources.Load<GameObject>("Characters_Rigged/" + spriteName);

            GameObject go;
            SpriteRenderer sr = null;
            Animator anim = null;

            if (riggedPrefab != null)
            {
                go = Object.Instantiate(riggedPrefab);
                go.name = "Player";
                go.transform.position = new Vector2(spawnX, groundY);
                go.transform.localScale = Vector3.one * 0.12f;  // 骨骼 psb 尺寸远大,缩到与舞台一个量级

                sr = go.GetComponentInChildren<SpriteRenderer>();
                anim = go.GetComponent<Animator>();
                if (anim == null) anim = go.GetComponentInChildren<Animator>();

                // 加碰撞让"点自己"也能工作(整体 bounding box)
                var col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = new Vector2(1.2f, 2.4f);
                col.offset = new Vector2(0, 1.2f);
            }
            else
            {
                // 2) 回退:静态立绘(Resources/Characters/{era})
                Sprite art = Resources.Load<Sprite>("Characters/" + spriteName);
                if (art != null)
                {
                    go = new GameObject("Player");
                    go.transform.position = new Vector2(spawnX, groundY);
                    sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = art;
                    var col = go.AddComponent<BoxCollider2D>();
                    col.isTrigger = true;
                    col.size = sr.sprite.bounds.size;
                    col.offset = sr.sprite.bounds.center;
                }
                else
                {
                    // 3) 最终回退:纯色方块(纯代码占位,零资源依赖)
                    go = BuildBlock("Player", new Vector2(spawnX, groundY), new Vector2(0.8f, 1.8f),
                        new Color(0.6f, 0.2f, 0.2f));
                    sr = go.GetComponent<SpriteRenderer>();
                }
            }

            var pc = go.AddComponent<PlayerController>();
            pc.moveSpeed = EraToSpeed(era);
            // 骨骼版每个部位都有自己的 sortingOrder(内部叠放),不能被 PlayerController 覆盖
            pc.sortingTarget = (anim != null) ? null : sr;
            pc.animator = anim;              // 有骨骼就接上 Animator,自动播 Idle/Walk
            pc.spriteFacesRight = false;     // 立绘默认朝左
            pc.Teleport(new Vector2(spawnX, groundY));
            return go;
        }

        public static string EraToSpriteName(Era era)
        {
            switch (era)
            {
                case Era.Young: return "young";
                case Era.Middle: return "middle";
                default: return "old";
            }
        }

        public static float EraToSpeed(Era era)
        {
            switch (era)
            {
                case Era.Young: return 1.8f;   // 青年:快、轻盈
                case Era.Middle: return 1.3f;  // 中年:中速沉稳
                default: return 0.9f;          // 老年:慢、拖拽
            }
        }

        // ── 工具:纯色方块占位(与 SandboxBootstrap.BuildBlock 同实现,独立复制以免耦合)──
        static GameObject BuildBlock(string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            sr.color = color;
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            return go;
        }

        static Sprite _solid;
        static Sprite SolidSprite()
        {
            if (_solid != null) return _solid;
            var tex = new Texture2D(2, 2);
            var px = new Color[] { Color.white, Color.white, Color.white, Color.white };
            tex.SetPixels(px); tex.Apply();
            _solid = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            return _solid;
        }
    }
}
