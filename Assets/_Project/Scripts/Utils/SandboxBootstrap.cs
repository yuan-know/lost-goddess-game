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

        Text _hud;

        void Start()
        {
            EnsureGameManager();
            PlayerBuilder.EnableAutoRebuild();

            // 注册"程序化房间构建器":切到任何没有 .unity 的房间名时,重建一个占位房
            SceneLoader.ProceduralRoomBuilder = BuildRoomProcedural;

            BuildCamera();
            BuildRoomContent(GameState.CurrentRoom);
            BuildHud();
        }

        void Update()
        {
            // 存/读快捷键(验证用)
            if (Input.GetKeyDown(KeyCode.F5)) { SaveSystem.Save(); Flash("已保存 (F5)"); }
            if (Input.GetKeyDown(KeyCode.F9))
            {
                if (SaveSystem.Load()) { RebuildAfterLoad(); Flash("已读取 (F9)"); }
                else Flash("无存档");
            }
            // 三形态切换(验证用):按 1/2/3 换 青年/中年/老年 立绘并原地重建
            if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchEra(Era.Young);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchEra(Era.Middle);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchEra(Era.Old);
            // 场景切换(验证用):4=沙盒占位房、5=黑暗森林、6=神庙入口
            if (Input.GetKeyDown(KeyCode.Alpha4)) SwitchRoom("Sandbox");
            if (Input.GetKeyDown(KeyCode.Alpha5)) SwitchRoom(ROOM_DARK_FOREST);
            if (Input.GetKeyDown(KeyCode.Alpha6)) SwitchRoom(ROOM_TEMPLE_ENTRY);
            // 序幕真戏入口:7=第 0 幕荒山野道(Cutscene)  8=第一幕神庙门厅(占位)
            if (Input.GetKeyDown(KeyCode.Alpha7)) SwitchRoom(Rooms.Prologue_Woods);
            if (Input.GetKeyDown(KeyCode.Alpha8)) SwitchRoom(Rooms.Prologue_Foyer);

            // 当前场景地平线微调(验证用):[ 抬背景/相当于降低地面 5% ] 降背景/抬高地面
            // 按住 Shift 是细调 1%,不按是粗调 5%
            float step = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 0.01f : 0.05f;
            if (Input.GetKeyDown(KeyCode.LeftBracket))  NudgeGround(+step);
            if (Input.GetKeyDown(KeyCode.RightBracket)) NudgeGround(-step);
            if (Input.GetKeyDown(KeyCode.P)) PrintGround();
            RefreshHud();
        }

        // 运行时上下平移背景三层,微调"画上地面 vs 老人脚底"对齐
        // delta > 0:背景整体上移 → 视觉上地面变高 → 老人显得更"陷"
        // delta < 0:背景下移 → 老人显得更"浮"起
        // 单位:相对图片高度的比例(0.05 = 12*0.05 = 0.6 世界单位)
        // ParallaxLayer 只操作 .x,这里只改 .y,不冲突。
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
            float suggested = 0.13f - _groundOffsetPct;
            Flash($"地平线 {(_groundOffsetPct>=0?"+":"")}{_groundOffsetPct*100f:F0}% → groundFromBottom≈{suggested:F2}");
        }
        void PrintGround()
        {
            float suggested = 0.13f - _groundOffsetPct;
            Debug.Log($"[Sandbox] {GameState.CurrentRoom} 累计微调 {_groundOffsetPct*100f:F0}%, SceneDef.groundFromBottom 建议改为 {suggested:F3}");
            Flash($"建议 groundFromBottom = {suggested:F3}");
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
                newCam.orthographicSize = 5f;
                newCam.transform.position = new Vector3(0, 0, -10);
                go.AddComponent<ClickInputManager>();
            }

            // 无论是新建还是场景里已有的相机,强制固定这些属性:
            //  · 正交(2D 单屏)
            //  · 纯色清屏(不是 Skybox,否则底部露出棕色地平线)
            //  · 深色背景(与序幕氛围一致)
            var cam = Camera.main;
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.08f);  // 近黑,不喧宾夺主
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
            if (roomName == Rooms.Prologue_Foyer)
            {
                LostGoddess.Content.PrologueFoyerScene.Build();
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
            BuildRoomContent(roomName);
            yield return null;
        }

        void RebuildAfterLoad()
        {
            DestroyRoomObjects();
            BuildRoomContent(GameState.CurrentRoom);
        }

        void DestroyRoomObjects()
        {
            // 占位房 4 件套
            foreach (var n in new[] { "WalkableArea", "Player", "Lamp_提灯", "Door_门" })
            {
                var g = GameObject.Find(n);
                if (g != null) Destroy(g);
            }
            // 美术场景根节点(SceneRoomBuilder 建的)
            foreach (var n in new[] {
                "Room_" + ROOM_DARK_FOREST, "Room_" + ROOM_TEMPLE_ENTRY,
                "Room_" + Rooms.Prologue_Woods, "Room_" + Rooms.Prologue_Foyer,
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
            var canvasGo = new GameObject("SandboxHUD");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            var txtGo = new GameObject("Info");
            txtGo.transform.SetParent(canvasGo.transform, false);
            _hud = txtGo.AddComponent<Text>();
            _hud.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _hud.fontSize = 20;
            _hud.color = Color.white;
            var rt = _hud.rectTransform;
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(16, -16);
            rt.sizeDelta = new Vector2(700, 200);
        }

        float _flashUntil;
        string _flashMsg = "";
        void Flash(string msg) { _flashMsg = msg; _flashUntil = Time.time + 2f; }

        void RefreshHud()
        {
            if (_hud == null) return;
            string flash = (Time.time < _flashUntil) ? $"\n<{_flashMsg}>" : "";
            _hud.text =
                $"[失落的女神 · 系统层验证沙盒]\n" +
                $"点空地=角色走过去  点物件=走近触发\n" +
                $"F5 存档 / F9 读档   1/2/3 切换青年/中年/老年\n" +
                $"4=占位沙盒  5=黑暗森林  6=神庙入口  7=序幕荒山  8=序幕门厅\n" +
                $"[ / ] 微调地平线(±5%,按 Shift ±1%)   P 打印建议值\n" +
                $"房间: {GameState.CurrentRoom}   时代: {GameState.CurrentEra}\n" +
                $"持有提灯: {InventorySystem.Has(Items.item_lamp)}   门已开: {GameState.GetFlag(Flags.demo_door_unlocked)}" +
                flash;
        }

        // ── 工具 ──
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
