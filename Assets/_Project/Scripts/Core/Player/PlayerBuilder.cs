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
            Debug.Log($"[PlayerBuilder.OnEraChanged] 形态切换: {from} → {to}");

            var existing = GameObject.Find("Player");
            if (existing == null)
            {
                Debug.LogWarning("[PlayerBuilder.OnEraChanged] 场景中没有找到Player对象，跳过重建");
                return;
            }

            float spawnX = existing.transform.position.x;

            // 2026-07-23 【教训】曾经用 renderer.bounds.min.y 测"实际脚底"反推 groundY,
            //   但骨骼预制体首帧 bounds 还没经过 SpriteSkin 变形,minY 会返回错误值
            //   (通常是 pivot 位置附近),导致 delta 反号,把角色越挪越深进地里。
            //   → 撤回,回到"pivot.y - yOffset(from) = groundY"的稳定假设。
            //   前提:Build 里 pivot 严格放在 groundY + yOffset,不做额外后处理。
            float measuredGroundY = existing.transform.position.y - GetYOffset(from);
            _lastGroundY = measuredGroundY;

            Debug.Log($"[PlayerBuilder.OnEraChanged] 旧Player位置: ({spawnX:F2}, {existing.transform.position.y:F2}), " +
                      $"反推 groundY = {measuredGroundY:F2} (from={from}, yOffset={GetYOffset(from):F2})");
            Debug.Log($"[PlayerBuilder.OnEraChanged] 开始构建新Player...");

            Object.Destroy(existing);
            Build(to, measuredGroundY, spawnX);

            Debug.Log($"[PlayerBuilder.OnEraChanged] 新Player构建完成");
        }

        /// <summary>按当前 Era 建 Player,放置在 (spawnX, groundY)。</summary>
        /// <param name="era">角色形态</param>
        /// <param name="groundY">地面Y坐标</param>
        /// <param name="spawnX">出生X坐标</param>
        /// <param name="yOffsetOverride">可选：覆盖默认的Y偏移量（用于特定场景需要不同站位）</param>
        public static GameObject Build(Era era, float groundY, float spawnX = 0f, float? yOffsetOverride = null)
        {
            Debug.Log($"[PlayerBuilder.Build] 开始构建: era={era}, groundY={groundY:F2}, spawnX={spawnX:F2}");

            _lastGroundY = groundY;
            string spriteName = EraToSpriteName(era);

            Debug.Log($"[PlayerBuilder.Build] spriteName={spriteName}");

            // 1) 优先加载骨骼动画 Prefab(Resources/Characters_Rigged/{era}.prefab)——含 SpriteSkin + Animator
            var riggedPrefab = Resources.Load<GameObject>("Characters_Rigged/" + spriteName);

            Debug.Log($"[PlayerBuilder.Build] riggedPrefab={(riggedPrefab != null ? "已加载" : "未找到")}");

            GameObject go;
            SpriteRenderer sr = null;
            Animator anim = null;

            if (riggedPrefab != null)
            {
                Debug.Log($"[PlayerBuilder.Build] 实例化骨骼Prefab...");
                go = Object.Instantiate(riggedPrefab);
                go.name = "Player";

                // 2026-07-21 二次校准:配合 orthoSize=6 + 新黑边比,中间条带世界高 = 12。
                //   要严格 5:12 → 角色视觉高 = 5 单位。prefab 在 scale=1 时视觉高约 20 单位 → scale=0.25。
                //   yOffset 也按新 scale 等比缩放(原参数是 scale=0.209 时的值,现按 0.25/0.209=1.196× 放大)。
                float scale = 0.25f;
                float yOffset = yOffsetOverride ?? GetYOffset(era);  // 允许覆盖默认偏移量

                Debug.Log($"[PlayerBuilder.Build] scale={scale}, yOffset={yOffset:F2}");
                Debug.Log($"[PlayerBuilder.Build] 最终位置: ({spawnX:F2}, {groundY + yOffset:F2})");

                go.transform.position = new Vector2(spawnX, groundY + yOffset);
                go.transform.localScale = Vector3.one * scale;

                sr = go.GetComponentInChildren<SpriteRenderer>();
                anim = go.GetComponent<Animator>();
                if (anim == null) anim = go.GetComponentInChildren<Animator>();

                Debug.Log($"[PlayerBuilder.Build] SpriteRenderer={(sr != null ? "找到" : "未找到")}, Animator={(anim != null ? "找到" : "未找到")}");

                // 2026-07-21 碰撞体同步放大:size ≈ 2.5×5.0(2.09→2.5, 4.18→5.0),offset y=2.5。
                var col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = new Vector2(2.5f, 5.0f);
                col.offset = new Vector2(0, 2.5f);
            }
            else
            {
                Debug.Log($"[PlayerBuilder.Build] 使用静态立绘回退");

                // 获取 yOffset（静态立绘也需要应用偏移）
                float yOffset = yOffsetOverride ?? GetYOffset(era);  // 允许覆盖默认偏移量
                Debug.Log($"[PlayerBuilder.Build] 静态立绘 yOffset={yOffset:F2}");

                // 2) 回退:静态立绘(Resources/Characters/{era})
                Sprite art = Resources.Load<Sprite>("Characters/" + spriteName);
                if (art != null)
                {
                    go = new GameObject("Player");
                    go.transform.position = new Vector2(spawnX, groundY + yOffset);  // 应用 yOffset
                    sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = art;
                    var col = go.AddComponent<BoxCollider2D>();
                    col.isTrigger = true;
                    col.size = sr.sprite.bounds.size;
                    col.offset = sr.sprite.bounds.center;
                }
                else
                {
                    Debug.LogWarning($"[PlayerBuilder.Build] 静态立绘也未找到，使用纯色方块");
                    // 3) 最终回退:纯色方块(纯代码占位,零资源依赖)
                    go = BuildBlock("Player", new Vector2(spawnX, groundY + yOffset), new Vector2(0.8f, 1.8f),
                        new Color(0.6f, 0.2f, 0.2f));
                    sr = go.GetComponent<SpriteRenderer>();
                }
            }

            Debug.Log($"[PlayerBuilder.Build] 添加PlayerController组件...");

            var pc = go.AddComponent<PlayerController>();
            pc.moveSpeed = EraToSpeed(era);
            // 骨骼版每个部位都有自己的 sortingOrder(内部叠放),不能被 PlayerController 覆盖
            pc.sortingTarget = (anim != null) ? null : sr;
            pc.animator = anim;              // 有骨骼就接上 Animator,自动播 Idle/Walk
            pc.spriteFacesRight = false;     // 立绘默认朝左

            // 注意：不要调用 Teleport，因为它会重置 Y 坐标，覆盖掉 yOffset
            // Player 的位置已经在上面设置好了（包括 yOffset）

            Debug.Log($"[PlayerBuilder.Build] 应用颜色调整...");

            // 2026-07-20 人物整体调暗 + 微冷调,融合荒山野道的暗色调场景。
            //   遍历所有 SpriteRenderer(包括骨骼子部件),乘一个 0.72 灰度 + 轻微偏冷。
            //   R/G/B 分别乘不同系数:蓝略高、红略低 → 视觉更"融入夜色而不发烫"。
            //   alpha 不动。
            {
                Color tint = new Color(0.68f, 0.72f, 0.78f, 1f);
                foreach (var r in go.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    var c = r.color;
                    r.color = new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a);
                }
            }

            Debug.Log($"[PlayerBuilder.Build] 设置朝向...");

            // 2026-07-20 出生朝向右(荒山野道流程是"从林间开口往右走进神庙")
            //   立绘默认朝左(spriteFacesRight=false),要面朝右 → scale.x 取负值,
            //   相当于水平翻转贴图。y/z 保持不变。
            {
                var s = go.transform.localScale;
                s.x = -Mathf.Abs(s.x);
                go.transform.localScale = s;
            }

            // ─────────────────────────────────────────────────────────────
            // 2026-07-23 【回滚】曾经尝试 AlignFeetToGround(用 renderer.bounds 测脚底)
            //   自动对齐 pivot 位置差异,但骨骼首帧 bounds 未 pose,测出错误 minY
            //   反而把角色挪进地里。撤回,回到 GetYOffset 手调方案。
            //   青年/中年/老年三种形态的 yOffset 见 GetYOffset(era)。
            // ─────────────────────────────────────────────────────────────

            Debug.Log($"[PlayerBuilder.Build] Player构建完成！最终位置: {go.transform.position}");

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

        // 中青年美术资源类型和大小类似,共用同一个 yOffset,确保两形态脚底严格对齐。
        //   如果发现中青年站位不对,改这个值,两个一起变。
        // 2026-07-27 下调站位:从 3.5f 改为 2.8f,让角色脚底更贴近地面
        const float kYoungMiddleYOffset = 2.8f;

        // 2026-07-27 老年形态也需要下调站位
        const float kOldYOffset = -0.5f;

        /// <summary>获取指定形态的Y偏移量(pivot 到脚底的距离)。</summary>
        public static float GetYOffset(Era era)
        {
            // 青年/中年骨骼预制体 pivot 不在脚底(pivot 位于身体中上部),
            //   需要向上偏移才能让脚接近地面。老年 pivot ≈ 脚底,但也需要微调。
            //   数值由目测微调:
            //     2026-07-23 校准: 青年/中年=3.5, 老年=0
            //     2026-07-27 下调: 青年/中年=2.8, 老年=-0.5
            switch (era)
            {
                case Era.Young:
                case Era.Middle:
                    return kYoungMiddleYOffset;   // 中青年共用,脚底严格对齐
                case Era.Old:
                default:
                    return kOldYOffset;            // 老年:微调下移
            }
        }

        /// <summary>第一章场景（大殿、餐厅、武器室、追逐长廊）使用更低的角色站位</summary>
        public static float GetChapter1YOffset(Era era)
        {
            switch (era)
            {
                case Era.Young:
                case Era.Middle:
                    return 2.3f;   // 比默认2.8更低
                case Era.Old:
                default:
                    return -1.0f;  // 比默认-0.5更低
            }
        }

        public static float EraToSpeed(Era era)
        {
            // 2026-07-21 【临时测试】老/中/青 三形态速度全部 × 5,方便测试跑图。
            //   正式提交前必须还原为 0.9 / 1.3 / 1.8。
            const float testMult = 5f;
            switch (era)
            {
                case Era.Young: return 1.8f * testMult;   // 青年:快、轻盈
                case Era.Middle: return 1.3f * testMult;  // 中年:中速沉稳
                default: return 0.9f * testMult;          // 老年:慢、拖拽
            }
        }

        // ── 工具:纯色方块占位(与 SandboxBootstrap.BuildBlock 同实现,独立复制以免耦合)──
        static GameObject BuildBlock(string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            sr.color = new Color(color.r, color.g, color.b, 0f);  // 正式版隐藏调试方块
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
