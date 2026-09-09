// ============================================================================
//  SandboxBootstrap.cs —— 程序化占位验证房间(零美术依赖)  🟢
//  挂在 Sandbox 场景里唯一的 GameObject 上,Play 后:
//   1) 建 GameManager(若无)→ 初始化全部系统
//   2) 建相机(固定单屏正交)、老人占位方块、WalkableArea、两个占位交互物、简易背包UI
//   3) 注册程序化房间构建器(GoToRoom 到无 .unity 的房间时重建占位内容)
//  验证链路:点空地→老人走过去 / 点提灯→走近捡起入包 / 点门→走近→(持灯)开门切房间 / F5存 F9读。
//
//  正式期:美术出图、手工建 .unity 场景后,本 Bootstrap 可弃用,系统层脚本原样复用。
// ============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using LostGoddess.Content;

namespace LostGoddess
{
    public class SandboxBootstrap : MonoBehaviour
    {
        // 烟火式横版:全场统一地平线 Y,老人与所有 interactPoint 都对齐到它
        const float GroundY = -2.4f;

        // 场景房间标识(SceneRoomBuilder 的 SceneDef.roomName 对齐)
        const string ROOM_DARK_FOREST = "DarkForest";
        const string ROOM_TEMPLE_ENTRY = "TempleEntry";

        // 2026-07-19 打包Demo：移除调试HUD
        // Text _hud;

        void Start()
        {
            EnsureGameManager();
            PlayerBuilder.EnableAutoRebuild();

            // 注册"程序化房间构建器":切到任何没有 .unity 的房间名时,重建一个占位房
            SceneLoader.ProceduralRoomBuilder = BuildRoomProcedural;

            BuildCamera();

            // 2026-07-20 电影级信箱式黑边(严格复刻策划参考图 840×570 中的 840×341 画面比例)
            LetterboxOverlay.Ensure();

            // 2026-07-19 自动启动序幕：直接进入荒山野道场景
            GameState.CurrentRoom = Rooms.Prologue_Woods;
            GameState.CurrentEra = Era.Old;  // 老年形态开始

            BuildRoomContent(GameState.CurrentRoom);
            BuildHud();
        }

        void Update()
        {
            // 2026-07-19 打包Demo：移除所有调试快捷键
            // 仅保留救急键R

            // 救急键 R:强制解锁角色 + 停掉所有 Cutscene(卡死救援)
            if (Input.GetKeyDown(KeyCode.R))
            {
                if (PlayerController.Instance != null)
                    PlayerController.Instance.SetControllable(true);
                foreach (var cs in FindObjectsOfType<Cutscene>()) cs.Stop();
                Flash("已强制解锁角色 (R)");
            }

            // 调试切换形态:1=青年 2=中年 3=老年(打包前可移除)。
            // SetEra 触发 OnEraChanged → PlayerBuilder 按新形态重建(优先 AI 逐帧 prefab)。
            if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchEra(Era.Young);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchEra(Era.Middle);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchEra(Era.Old);
        }

        // 运行时上下平移背景三层,微调"画上地面 vs 老人脚底"对齐
        // delta > 0:背景整体上移 → 视觉上地面变高 → 老人显得更"陷"
        // delta < 0:背景下移 → 老人显得更"浮"起
        // 单位:相对图片高度的比例(0.05 = 12*0.05 = 0.6 世界单位)
        // ParallaxLayer 只操作 .x,这里只改 .y,不冲突。
        // 校准时按 `[`/`]` 把背景调到"看起来贴脚",P 键输出的就是应写入 SceneDef 的 groundY。
        // 若后续发现所有房间都系统性浮空/陷地,可在此加统一偏移;目前以视觉校准为准。
        const float SpriteFeetOffset = 0f;

        float _groundOffsetPct = 0f;
        void NudgeGround(float deltaPct)
        {
            var scene = GameObject.Find("Room_" + GameState.CurrentRoom);
            if (scene == null) { Flash("当前不是美术场景,无法微调"); return; }
            float dy = 12f * deltaPct;
            foreach (var sr in scene.GetComponentsInChildren<SpriteRenderer>())
            {
                var p = sr.transform.position; p.y += dy; sr.transform.position = p;
            }
            _groundOffsetPct += deltaPct;

            var (suggestedPct, suggestedGroundY) = ComputeSuggestedGround();
            Flash($"地平线 {(_groundOffsetPct>=0?"+":"")}{_groundOffsetPct*100f:F0}% → 建议 groundFromBottom={suggestedPct:F3}");
        }
        void PrintGround()
        {
            var (suggestedPct, suggestedGroundY) = ComputeSuggestedGround();
            var def = LostGoddess.Content.SceneRoomBuilder.LastBuiltDef;
            float basePct = def != null ? def.groundFromBottom : 0.13f;
            Debug.Log($"[Sandbox] {GameState.CurrentRoom} 累计微调 {(_groundOffsetPct>=0?"+":"")}{_groundOffsetPct*100f:F0}%, " +
                      $"当前 groundFromBottom={basePct:F3}, 建议改为 {suggestedPct:F3} (groundY={suggestedGroundY:F2},已扣 0.25 角色偏移)");
            Flash($"建议 groundFromBottom={suggestedPct:F3} groundY={suggestedGroundY:F2}");
        }
        (float pct, float groundY) ComputeSuggestedGround()
        {
            var def = LostGoddess.Content.SceneRoomBuilder.LastBuiltDef;
            float basePct = def != null ? def.groundFromBottom : 0.13f;
            // `_groundOffsetPct` 是背景相对初始位置的偏移比例:
            //   + 表示背景上移 → 地面变高 → groundY 应同步提高
            //   - 表示背景下移 → 地面变低 → groundY 应同步降低
            float rawGroundY = -5f + 12f * (basePct + _groundOffsetPct);
            float adjustedGroundY = rawGroundY + SpriteFeetOffset;
            float adjustedPct = (adjustedGroundY + 5f) / 12f;
            return (adjustedPct, adjustedGroundY);
        }

        void SwitchRoom(string roomName)
        {
            if (GameState.CurrentRoom == roomName) return;
            GameState.CurrentRoom = roomName;
            DestroyRoomObjects();
            BuildRoomContent(roomName);
            _groundOffsetPct = 0f;   // 微调重置(每个场景独立调)
            Flash($"切换场景: {roomName}");
        }

        // ── 系统 ──
        void EnsureGameManager()
        {
            if (GameManager.Instance == null)
            {
                var go = new GameObject("GameManager");
                go.AddComponent<GameManager>();
            }
        }

        void BuildCamera()
        {
            if (Camera.main == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                var newCam = go.AddComponent<Camera>();
                newCam.orthographic = true;           // 固定单屏
                newCam.orthographicSize = 6f;         // 2026-07-21:orthoSize=6 让 12 单位图完整落进中间条带
                newCam.transform.position = new Vector3(0, 0, -10);
                go.AddComponent<ClickInputManager>();
            }

            // 无论是新建还是场景里已有的相机,强制固定这些属性:
            //  · 正交(2D 单屏)
            //  · 纯色清屏(不是 Skybox,否则底部露出棕色地平线)
            //  · 深色背景(与序幕氛围一致)
            var cam = Camera.main;
            cam.orthographic = true;
            cam.orthographicSize = 6f;   // 2026-07-21:与新黑边比联动,让 12 单位场景完整落中间条带
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.08f);  // 近黑,不喧宾夺主

            // 2026-07-20 相机 viewport 限定在中间条带内,让完整场景在中间区域呈现,
            // 不再被 LetterboxOverlay 盖住顶部/底部内容。
            //   · rect.y = 下方黑边高度比例 (0.2018)
            //   · rect.height = 中间画面比例 (1 - 2*0.2018 = 0.5964)
            //   · 相机 aspect 会自动变宽(约 2.98:1 in 1080p),完整渲染进中间条带
            {
                float barPct = LetterboxOverlay.BarHeightPct;
                cam.rect = new Rect(0f, barPct, 1f, 1f - 2f * barPct);
            }

            if (cam.GetComponent<ClickInputManager>() == null)
                cam.gameObject.AddComponent<ClickInputManager>();
        }

        // ── 房间内容 ──
        void BuildRoomContent(string roomName)
        {
            // 序幕真戏:第 0 幕(Cutscene 全自动) / 第一幕(占位落地)
            if (roomName == Rooms.Prologue_Woods)
            {
                LostGoddess.Content.PrologueWoodsScene.Build();
                return;
            }
            if (roomName == Rooms.Prologue_Gate)
            {
                LostGoddess.Content.PrologueGateScene.Build();
                return;
            }
            if (roomName == Rooms.Prologue_Foyer)
            {
                LostGoddess.Content.PrologueFoyerScene.Build();
                return;
            }
            if (roomName == Rooms.Prologue_Chamber3)
            {
                LostGoddess.Content.PrologueChamber3Scene.Build();
                return;
            }
            if (roomName == Rooms.Prologue_UpperChamber)
            {
                // 二楼密室:用真实美术(原二楼回廊的美术资源,名字错了而已)
                LostGoddess.Content.PrologueUpperHallScene.Build();
                return;
            }
            if (roomName == Rooms.Prologue_UpperHall)
            {
                // 二楼回廊:2026-07-24 换真实美术 UpperCorridor(5950×1200,更长)
                LostGoddess.Content.PrologueUpperChamberScene.Build();
                return;
            }
            if (roomName == Rooms.Prologue_Chamber2)
            {
                LostGoddess.Content.PrologueChamber2Scene.Build();
                return;
            }
            if (roomName == Rooms.Prologue_Chase)
            {
                LostGoddess.Content.PrologueChaseScene.Build();
                return;
            }
            if (roomName == Rooms.Chapter1_Hall)
            {
                LostGoddess.Content.Chapter1HallScene.Build();
                return;
            }
            if (roomName == Rooms.Chapter1_DiningHall)
            {
                LostGoddess.Content.DiningHallScene.Build();
                return;
            }
            if (roomName == Rooms.Chapter1_WeaponsRoom)
            {
                LostGoddess.Content.WeaponsRoomScene.Build();
                return;
            }
            if (roomName == Rooms.Chapter1_ChaseCorridor)
            {
                LostGoddess.Content.ChaseCorridorScene.Build();
                return;
            }

            // ── 2026-07-19 策划场景切换图新增(神庙前厅左右链) ──
            if (roomName == Rooms.Prologue_LadderChamber)
            {
                LostGoddess.Content.PrologueLadderChamberScene.Build();
                return;
            }
            if (roomName == Rooms.Prologue_GearRoom)
            {
                LostGoddess.Content.PrologueGearRoomScene.Build();
                return;
            }
            if (roomName == Rooms.Prologue_StatueRoom)
            {
                LostGoddess.Content.PrologueStatueRoomScene.Build();
                return;
            }
            if (roomName == Rooms.Prologue_UpperChamber)
            {
                LostGoddess.Content.PrologueUpperChamberScene.Build();
                return;
            }

            // 2026-07-24 二楼回廊右端 → 女神像密室
            if (roomName == Rooms.Prologue_GoddessChamber)
            {
                LostGoddess.Content.PrologueGoddessChamberScene.Build();
                return;
            }

            // 2026-07-24 女神像密室右端 → 残骸间(复用 Chamber1 资源目录,拓扑独立房间)
            if (roomName == Rooms.Prologue_Chamber1)
            {
                LostGoddess.Content.PrologueChamber1Scene.Build();
                return;
            }

            // 美术已交付的真实场景:调 SceneRoomBuilder 建三层视差背景 + WalkableArea + 相机跟随;
            // 交互物暂缺(等策划)——只把老人放进去就行,看视差滚动 + 老人走场景效果。
            if (roomName == ROOM_DARK_FOREST)
            {
                LostGoddess.Content.SceneRoomBuilder.Build(LostGoddess.Content.SceneRoomBuilder.DarkForest);
                BuildPlayer();
                return;
            }
            if (roomName == ROOM_TEMPLE_ENTRY)
            {
                LostGoddess.Content.SceneRoomBuilder.Build(LostGoddess.Content.SceneRoomBuilder.TempleEntry);
                BuildPlayer();
                return;
            }

            // 兜底:占位沙盒房(暗红方块 + 提灯 + 门)
            BuildWalkableArea();
            BuildPlayer();

            // 提灯占位:黄色小方块(interactPoint 对齐地平线)
            var lamp = BuildBlock("Lamp_提灯", new Vector2(-4f, -1.5f), new Vector2(0.6f, 0.9f),
                new Color(0.95f, 0.85f, 0.2f));
            var lampInteract = lamp.AddComponent<Interact_PickupLamp>();
            lampInteract.highlightTarget = lamp.GetComponent<SpriteRenderer>();
            lampInteract.interactPoint = MakePoint(lamp.transform, new Vector2(-3.4f, GroundY));

            // 门占位:棕色高方块(interactPoint 对齐地平线)
            var door = BuildBlock("Door_门", new Vector2(5f, -0.5f), new Vector2(1.2f, 2.6f),
                new Color(0.5f, 0.32f, 0.18f));
            var doorInteract = door.AddComponent<Interact_Door>();
            doorInteract.highlightTarget = door.GetComponent<SpriteRenderer>();
            doorInteract.interactPoint = MakePoint(door.transform, new Vector2(3.8f, GroundY));
        }

        void BuildWalkableArea()
        {
            // 烟火式横版:一条固定地平线 + 左右边界
            var go = new GameObject("WalkableArea");
            go.transform.position = new Vector3(0, GroundY, 0);
            var wa = go.AddComponent<WalkableArea>();
            wa.useTransformY = true;   // 地平线 = 本物体 Y
            wa.minX = -8f;
            wa.maxX = 8f;
        }

        // 切换三形态(验证用):调 GameState.SetEra → PlayerBuilder 自动重建(经 OnEraChanged)
        void SwitchEra(Era era)
        {
            if (GameState.CurrentEra == era) return;
            GameState.SetEra(era);  // 会触发 OnEraChanged → PlayerBuilder 重建
            Flash($"切换形态: {era}");
        }

        void BuildPlayer()
        {
            PlayerBuilder.Build(GameState.CurrentEra, GroundY, 0f);
        }

        // ── 程序化房间构建器(切到无 .unity 房间时)──
        IEnumerator BuildRoomProcedural(string roomName)
        {
            // 清掉旧房间内容(保留 GameManager / 相机 / 常驻 UI)
            DestroyRoomObjects();
            // 等一帧,确保 Destroy 生效(WalkableArea.OnDisable / PlayerController.OnDestroy 都跑完)
            yield return null;
            BuildRoomContent(roomName);
            yield return null;
            // 新场景就绪后:强制解锁角色,修正 WalkableArea.Current(防旧场景的 Cutscene 泄漏 SetControllable(false))
            if (PlayerController.Instance != null)
                PlayerController.Instance.SetControllable(true);
            // 强制修正 WalkableArea.Current:如果场景里有多个 WalkableArea(旧的没清干净),
            // 取新场景根节点下的那一个。SceneRoomBuilder 用 SceneDef.roomName 命名根节点,
            // 而 Prologue_* 场景里各自又 rename 成了 Room_Prologue_XXX,所以两套名字都要试。
            var newRoot = GameObject.Find("Room_" + roomName)
                       ?? GameObject.Find("Room_TempleGate")
                       ?? GameObject.Find("Room_TempleFoyer")
                       ?? GameObject.Find("Room_TempleChamber1F")
                       ?? GameObject.Find("Room_UpperHall")
                       ?? GameObject.Find("Room_Chamber2")
                       ?? GameObject.Find("Room_LadderChamber")
                       ?? GameObject.Find("Room_GearRoom")
                       ?? GameObject.Find("Room_StatueRoom")
                       ?? GameObject.Find("Room_TempleEntry")
                       ?? GameObject.Find("Room_DarkForest")
                       ?? GameObject.Find("Room_MainHall")
                       ?? GameObject.Find("Room_DiningHall")
                       ?? GameObject.Find("Room_WeaponsRoom")
                       ?? GameObject.Find("Room_ChaseCorridor");
            if (newRoot != null)
            {
                var wa = newRoot.GetComponentInChildren<WalkableArea>();
                if (wa != null && wa.enabled) { wa.enabled = false; wa.enabled = true; }  // 重触发 OnEnable → Current = wa
            }
        }

        void RebuildAfterLoad()
        {
            DestroyRoomObjects();
            BuildRoomContent(GameState.CurrentRoom);
        }

        void DestroyRoomObjects()
        {
            // 占位房 4 件套 + 头顶感叹号(与 Player 同生同死)
            foreach (var n in new[] { "WalkableArea", "Player", "Lamp_提灯", "Door_门", "~PlayerTalkPrompt" })
            {
                var g = GameObject.Find(n);
                if (g != null) Destroy(g);
            }
            // 美术场景根节点(SceneRoomBuilder 建的)
            foreach (var n in new[] {
                "Room_" + ROOM_DARK_FOREST, "Room_" + ROOM_TEMPLE_ENTRY,
                "Room_" + Rooms.Prologue_Woods, "Room_" + Rooms.Prologue_Gate,
                "Room_" + Rooms.Prologue_Foyer,
                "Room_" + Rooms.Prologue_Chamber3, "Room_" + Rooms.Prologue_UpperHall,
                "Room_" + Rooms.Prologue_Chamber2, "Room_" + Rooms.Prologue_Chase,
                "Room_" + Rooms.Chapter1_Hall,
                "Room_" + Rooms.Chapter1_DiningHall,
                "Room_" + Rooms.Chapter1_WeaponsRoom,
                "Room_" + Rooms.Chapter1_ChaseCorridor,
                // 2026-07-19 前厅左右链新增
                "Room_" + Rooms.Prologue_LadderChamber, "Room_" + Rooms.Prologue_GearRoom,
                "Room_" + Rooms.Prologue_StatueRoom, "Room_" + Rooms.Prologue_UpperChamber,
                // 2026-07-24 二楼回廊右端接的女神像密室
                "Room_" + Rooms.Prologue_GoddessChamber,
                // 2026-07-24 女神像密室右端接的残骸间
                "Room_" + Rooms.Prologue_Chamber1,
                // SceneRoomBuilder 用 SceneDef.roomName 命名根节点,与 Prologue_* 逻辑房间名不同:
                "Room_TempleGate", "Room_TempleFoyer", "Room_TempleChamber1F", "Room_UpperHall", "Room_Chamber2",
                "Room_LadderChamber", "Room_GearRoom", "Room_StatueRoom",   // 2026-07-19 前厅左右链新增
                "Room_MainHall", "Room_DiningHall", "Room_WeaponsRoom", "Room_ChaseCorridor",  // 第一章 SceneDef.roomName 命名
            })
            {
                var g = GameObject.Find(n);
                if (g != null) Destroy(g);
            }
            // 相机跟随组件重置(切回占位房时不再跟人)
            var cam = Camera.main;
            if (cam != null)
            {
                var f = cam.GetComponent<CameraFollow>();
                if (f != null) { Destroy(f); cam.transform.position = new Vector3(0, 0, -10); }
            }
        }

        // ── HUD(验证信息)──
        void BuildHud()
        {
            // 2026-07-19 打包Demo：移除左上角调试信息
            // 调试HUD已禁用
        }

        void RefreshHud()
        {
            // 2026-07-19 打包Demo：移除左上角调试信息
            // 调试HUD已禁用
        }

        // 2026-07-19 打包Demo：禁用闪烁提示
        // float _flashUntil;
        // string _flashMsg = "";
        void Flash(string msg) {
            // 禁用闪烁提示
        }

        // ── 工具 ──
        static GameObject BuildBlock(string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            // 2026-07-19 打包Demo：占位方块设为透明
            sr.color = new Color(color.r, color.g, color.b, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            return go;
        }

        static Transform MakePoint(Transform parent, Vector2 worldPos)
        {
            var p = new GameObject("interactPoint").transform;
            p.SetParent(parent, worldPositionStays: true);
            p.position = worldPos;
            return p;
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
