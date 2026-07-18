// ============================================================================
//  RoomBuilder_Scene.cs —— 通用"美术给三层背景 + 老人接入"的房间构建器
//  美术把 bg_far / bg_mid / bg_near 三张全画布 PNG 丢在 Resources/Scenes/<房间名>/
//  本脚本读一份 SceneDef,按定义构建:
//    · 三层 SpriteRenderer(远/中/近),排序层从后到前
//    · 地平线锚点算好,让画上的地面 Y = 老人脚底 Y(与 SandboxBootstrap.GroundY 一致)
//    · WalkableArea 按背景宽度自动圈边界
//    · 相机加 CameraFollow,跟老人 X,clamp 到背景两端
//    · 老人由 SandboxBootstrap.BuildPlayer 之前已放好,这里不管
//
//  【只做场景,不放交互物】——交互物等策划给清单再摆。
// ============================================================================
using System.Collections;
using UnityEngine;

namespace LostGoddess.Content
{
    /// <summary>一个场景房间的定义:图片路径+地平线百分比+视差/世界大小/摄像机可动区间。</summary>
    public class SceneDef
    {
        public string roomName;         // 房间标识(等于 Resources/Scenes/{roomName}/ 目录)
        public float bgPixelWidth;      // 背景图像素宽(4250 / 3400 / …)
        public float bgPixelHeight;     // 背景图像素高(通常 1200)
        public float bgPPU;             // 与 BackgroundImporter 保持一致(100)
        public float groundFromBottom;  // 画上地平线离图底的比例(0..1),0.25 / 0.18…
        public float groundY;           // 老人脚底所在世界 Y(与 SandboxBootstrap.GroundY 一致)
        public float parallaxFar;       // 远层 factor
        public float parallaxMid;
        public float parallaxNear;
        public float leftPadding = 0.5f;  // 老人可走线两端各留一点边(不让走出背景)
        public float rightPadding = 0.5f;
    }

    public static class SceneRoomBuilder
    {
        public static readonly SceneDef DarkForest = new SceneDef
        {
            roomName = "DarkForest",
            bgPixelWidth = 4250f,
            bgPixelHeight = 1200f,
            bgPPU = 100f,
            // mid 层石质地面顶面约在图底 13%(前次目测合成图 20% 偏高,人物浮空)
            // 运行时可用 [ / ] 键微调,调好后回写这里
            groundFromBottom = 0.13f,
            groundY = -2.4f,
            parallaxFar = 0.20f,
            parallaxMid = 0.55f,
            parallaxNear = 1.00f,
        };

        public static readonly SceneDef TempleEntry = new SceneDef
        {
            roomName = "TempleEntry",
            bgPixelWidth = 3400f,
            bgPixelHeight = 1200f,
            bgPPU = 100f,
            // 神庙入口石砌平台顶面约在图底 13%(可用 [ / ] 微调)
            groundFromBottom = 0.13f,
            groundY = -2.4f,
            parallaxFar = 0.20f,
            parallaxMid = 0.55f,
            parallaxNear = 1.00f,
        };

        /// <summary>按定义构建场景:三层背景 + WalkableArea + 相机跟随。返回根节点。</summary>
        public static GameObject Build(SceneDef def)
        {
            var root = new GameObject("Room_" + def.roomName);
            float worldWidth = def.bgPixelWidth / def.bgPPU;    // 42.5 / 34.0
            float worldHeight = def.bgPixelHeight / def.bgPPU;  // 12.0
            float halfW = worldWidth * 0.5f;

            // 图底 Y(BottomCenter 锚点下,SpriteRenderer 的 transform.y 就是图底 Y)
            // 我们要画上地平线落在 def.groundY:
            //   image.bottomY = def.groundY - worldHeight * def.groundFromBottom
            float imageBottomY = def.groundY - worldHeight * def.groundFromBottom;

            // 三层背景(排序:远最靠后,近最靠前;老年 rigged 部位 sortingOrder=29~41,
            // 前景 near 要 > 41 才能"挡住"老人;中景/远景在老人之后画)
            BuildLayer(root.transform, def, "bg_far",  imageBottomY, def.parallaxFar,  sortingOrder: -30);
            BuildLayer(root.transform, def, "bg_mid",  imageBottomY, def.parallaxMid,  sortingOrder: -20);
            BuildLayer(root.transform, def, "bg_near", imageBottomY, def.parallaxNear, sortingOrder:  60);

            // 可走区:X 边界按背景宽度(相机跟随时,人物走到边缘停下,不出背景)
            // 注意:老人不能走到贴边,预留 padding。
            var walkGo = new GameObject("WalkableArea");
            walkGo.transform.SetParent(root.transform, false);
            walkGo.transform.position = new Vector3(0f, def.groundY, 0f);
            var wa = walkGo.AddComponent<WalkableArea>();
            wa.useTransformY = true;
            wa.minX = -halfW + def.leftPadding;
            wa.maxX =  halfW - def.rightPadding;

            // 相机跟随:X 可动区间 = 相机相对背景两端不能出界
            // 相机可动 X ∈ [-halfW + orthoHalfW, halfW - orthoHalfW]
            var cam = Camera.main;
            if (cam != null)
            {
                var follow = cam.GetComponent<CameraFollow>();
                if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
                float orthoHalfW = cam.orthographicSize * cam.aspect;
                follow.minX = -halfW + orthoHalfW;
                follow.maxX =  halfW - orthoHalfW;
                follow.fixedY = 0f;
                follow.smoothTime = 0.15f;
                // target 会自动找 "Player"
            }

            return root;
        }

        static void BuildLayer(Transform parent, SceneDef def, string spriteName, float bottomY, float factor, int sortingOrder)
        {
            var sp = Resources.Load<Sprite>($"Scenes/{def.roomName}/{spriteName}");
            if (sp == null)
            {
                Debug.LogWarning($"[SceneRoomBuilder] 找不到背景图 Resources/Scenes/{def.roomName}/{spriteName}");
                return;
            }
            var go = new GameObject(spriteName);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, bottomY, 0f);   // 图底 Y
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.sortingOrder = sortingOrder;
            var px = go.AddComponent<ParallaxLayer>();
            px.factor = factor;
        }
    }
}
