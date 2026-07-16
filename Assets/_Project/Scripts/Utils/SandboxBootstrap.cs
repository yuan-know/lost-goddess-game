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
            RefreshHud();
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

        // ── 房间内容(占位)──
        void BuildRoomContent(string roomName)
        {
            // 可走区(屏幕下半部的一条带)
            BuildWalkableArea();

            // 老人占位:高一点的暗红方块(老年形态占位)
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
            var old = GameObject.Find("Player_老人");
            if (old != null) Destroy(old);
            BuildPlayer();
            Flash($"切换形态: {era}");
        }

        void BuildPlayer()
        {
            // 按当前时代选立绘(Resources/Characters/young|middle|old);缺图回退占位方块
            string spriteName = EraToSpriteName(GameState.CurrentEra);
            Sprite art = Resources.Load<Sprite>("Characters/" + spriteName);

            GameObject go;
            SpriteRenderer sr;
            if (art != null)
            {
                go = new GameObject("Player_老人");
                go.transform.position = new Vector2(0f, GroundY);
                sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = art;             // 锚点已由导入器设为脚底居中,故物体原点=脚底,落在地平线上
                // 立绘 PPU 已是目标视觉尺寸,localScale 保持 1(不像占位方块那样拉伸)
                var col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = sr.sprite.bounds.size;
                col.offset = sr.sprite.bounds.center;
            }
            else
            {
                go = BuildBlock("Player_老人", new Vector2(0f, GroundY), new Vector2(0.8f, 1.8f),
                    new Color(0.6f, 0.2f, 0.2f));
                sr = go.GetComponent<SpriteRenderer>();
            }

            var pc = go.AddComponent<PlayerController>();
            pc.moveSpeed = EraToSpeed(GameState.CurrentEra);
            pc.sortingTarget = sr;
            pc.animator = null;              // 占位期无动画机(骨骼动画待美术在 Unity 绑好后接入)
            pc.spriteFacesRight = false;     // 立绘为 3/4 偏正面、身体略朝左,默认朝左
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
                case Era.Young: return 3.4f;   // 青年:快、轻盈
                case Era.Middle: return 2.8f;  // 中年:中速沉稳
                default: return 2.2f;                    // 老年:慢、拖拽
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
            foreach (var n in new[] { "WalkableArea", "Player_老人", "Lamp_提灯", "Door_门" })
            {
                var g = GameObject.Find(n);
                if (g != null) Destroy(g);
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
                $"点空地=老人走过去  点物件=走近触发\n" +
                $"F5 存档 / F9 读档   1/2/3 切换青年/中年/老年\n" +
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
