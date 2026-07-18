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
            if (Camera.main != null) return;
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;           // 固定单屏
            cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(0, 0, -10);
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.15f);
            go.AddComponent<ClickInputManager>();
        }

        // ── 房间内容 ──
        void BuildRoomContent(string roomName)
        {
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

        // 切换三形态(验证用):销毁当前老人 → 改 Era → 原地重建立绘。相机/房间/背包不变。
        void SwitchEra(Era era)
        {
            if (GameState.CurrentEra == era) return;
            GameState.CurrentEra = era;
            var old = GameObject.Find("Player");
            if (old != null) Destroy(old);
            BuildPlayer();
            Flash($"切换形态: {era}");
        }

        void BuildPlayer()
        {
            string spriteName = EraToSpriteName(GameState.CurrentEra);

            // 1) 优先加载骨骼动画 Prefab(Resources/Characters_Rigged/{era}.prefab)——含 SpriteSkin + Animator
            var riggedPrefab = Resources.Load<GameObject>("Characters_Rigged/" + spriteName);

            GameObject go;
            SpriteRenderer sr = null;
            Animator anim = null;

            if (riggedPrefab != null)
            {
                go = Instantiate(riggedPrefab);
                go.name = "Player";
                go.transform.position = new Vector2(0f, GroundY);

                // 骨骼版 PSB 尺寸远大于像素(Unity 单位),缩到与占位方块一个量级。
                // ——psb 里部位相对 root 位置 y≈10,加上部位 sprite 本身高度,原始约 15 单位高;
                // 想让人物有 ~1.8 单位高,scale ≈ 0.12。先给 0.12,不合适再调。
                // 之前 0.006 是为扁 sprite 那份算的(psb 500px),对 rigged 版会缩到看不见。
                go.transform.localScale = Vector3.one * 0.12f;

                // 找一个 SpriteRenderer 作为翻转/排序参考(取任意可见的 body 部位)
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
                    go.transform.position = new Vector2(0f, GroundY);
                    sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = art;
                    var col = go.AddComponent<BoxCollider2D>();
                    col.isTrigger = true;
                    col.size = sr.sprite.bounds.size;
                    col.offset = sr.sprite.bounds.center;
                }
                else
                {
                    go = BuildBlock("Player", new Vector2(0f, GroundY), new Vector2(0.8f, 1.8f),
                        new Color(0.6f, 0.2f, 0.2f));
                    sr = go.GetComponent<SpriteRenderer>();
                }
            }

            var pc = go.AddComponent<PlayerController>();
            pc.moveSpeed = EraToSpeed(GameState.CurrentEra);
            // 骨骼版每个部位都有自己的 sortingOrder(内部叠放),不能被 PlayerController 覆盖
            pc.sortingTarget = (anim != null) ? null : sr;
            pc.animator = anim;              // 有骨骼就接上 Animator,自动播 Idle/Walk
            pc.spriteFacesRight = false;     // 老人立绘默认朝左
            pc.Teleport(new Vector2(0f, GroundY));
        }

        static string EraToSpriteName(Era era)
        {
            switch (era)
            {
                case Era.Young: return "young";
                case Era.Middle: return "middle";
                default: return "old";
            }
        }

        static float EraToSpeed(Era era)
        {
            switch (era)
            {
                case Era.Young: return 1.8f;   // 青年:快、轻盈
                case Era.Middle: return 1.3f;  // 中年:中速沉稳
                default: return 0.9f;          // 老年:慢、拖拽
            }
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
            foreach (var n in new[] { "Room_" + ROOM_DARK_FOREST, "Room_" + ROOM_TEMPLE_ENTRY })
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
                $"4=占位沙盒  5=黑暗森林  6=神庙入口\n" +
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
