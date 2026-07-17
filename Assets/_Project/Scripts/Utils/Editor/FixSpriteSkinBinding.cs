using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace LostGoddess.EditorTools
{
    /// <summary>
    /// 修复从 PSB 拖入场景后部位缺失 / 损坏的 Sprite Skin 组件。
    /// 关键:重绑到场景中【已存在】的 bone 层级,且按骨骼 guid 精确匹配
    /// (不靠名字 —— 本骨架存在大量重名骨骼 bone_14/17/18/2/4/6,
    ///  按名字匹配会绑错到同名骨骼,导致"权重正常但贴图不动")。
    ///
    /// 实现:调用 Unity 官方 internal 方法 SpriteSkin.GetSpriteBonesTransforms,
    /// 它按 sprite 里存的 bone guid 在 rootBone 子层级里精确查找,失败再降级路径匹配。
    ///
    /// 用法:
    ///  1. 从 PSB 拖一个角色进场景(带完整 bone 层级)。
    ///  2. Hierarchy 选中角色根节点(characters_3forms)。
    ///  3. 运行本菜单。
    /// </summary>
    public static class FixSpriteSkinBinding
    {
        [MenuItem("失落的女神/修复选中角色的SpriteSkin绑定")]
        public static void Fix()
        {
            var root = Selection.activeGameObject;
            if (root == null)
            {
                EditorUtility.DisplayDialog("未选中对象",
                    "请先在 Hierarchy 里选中角色根节点(characters_3forms),再运行本菜单。", "好");
                return;
            }

            var boneType = System.Type.GetType(
                "UnityEngine.U2D.Animation.Bone, Unity.2D.Animation.Runtime");

            // Unity 官方按 guid 精确匹配的重绑方法(internal)
            var skinType = typeof(SpriteSkin);
            MethodInfo getBonesTransforms = skinType.GetMethod(
                "GetSpriteBonesTransforms", BindingFlags.Static | BindingFlags.NonPublic);

            var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            int added = 0, rebound = 0, cleaned = 0, skipped = 0, failed = 0;
            var failedNames = new System.Text.StringBuilder();

            foreach (var sr in renderers)
            {
                var go = sr.gameObject;

                // 1. 删除损坏的丢失脚本组件
                cleaned += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);

                if (sr.sprite == null) { skipped++; continue; }

                // 2. 补 SpriteSkin
                var skin = go.GetComponent<SpriteSkin>();
                if (skin == null)
                {
                    skin = Undo.AddComponent<SpriteSkin>(go);
                    added++;
                }

                // 3. 找到该部位对应的 rootBone:
                //    用 sprite 第一根骨骼的 guid,在整个角色层级里定位它,
                //    再上溯到它所属的那条骨骼链顶端作为 rootBone。
                Transform rootBone = ResolveRootBone(root.transform, sr.sprite, boneType);
                if (rootBone == null)
                {
                    failed++;
                    AppendName(failedNames, go.name, "找不到rootBone");
                    continue;
                }

                // 通过 SerializedObject 写只读字段 m_RootBone
                var so = new SerializedObject(skin);
                var spRootBone = so.FindProperty("m_RootBone");
                if (spRootBone != null) spRootBone.objectReferenceValue = rootBone;
                so.ApplyModifiedProperties();

                // 4. 调 Unity 官方方法,按 guid 精确匹配填 boneTransforms
                bool ok = false;
                if (getBonesTransforms != null)
                {
                    var args = new object[] { skin, null };
                    try { ok = (bool)getBonesTransforms.Invoke(null, args); }
                    catch { ok = false; }

                    if (args[1] is Transform[] transforms && transforms.Length > 0)
                    {
                        var so2 = new SerializedObject(skin);
                        var spBones = so2.FindProperty("m_BoneTransforms");
                        if (spBones != null)
                        {
                            spBones.arraySize = transforms.Length;
                            int matched = 0;
                            for (int i = 0; i < transforms.Length; i++)
                            {
                                spBones.GetArrayElementAtIndex(i).objectReferenceValue = transforms[i];
                                if (transforms[i] != null) matched++;
                            }
                            so2.ApplyModifiedProperties();
                            ok = ok && matched == transforms.Length;
                        }
                    }
                }

                if (ok) rebound++;
                else { failed++; AppendName(failedNames, go.name, "guid匹配不全"); }

                EditorUtility.SetDirty(skin);
            }

            EditorUtility.DisplayDialog("SpriteSkin 修复完成",
                $"检查部位: {renderers.Length}\n" +
                $"删除损坏组件: {cleaned}\n" +
                $"新增 SpriteSkin: {added}\n" +
                $"重绑成功: {rebound}\n" +
                $"重绑失败: {failed}\n" +
                $"跳过(无Sprite): {skipped}\n\n" +
                (failedNames.Length > 0 ? "失败部位:\n" + failedNames : "全部按 guid 精确重绑成功。"),
                "好");
        }

        static void AppendName(System.Text.StringBuilder sb, string name, string reason)
        {
            if (sb.Length < 400) sb.Append($"  • {name} ({reason})\n");
        }

        // 反射调用扩展方法 SpriteDataAccessExtensions.GetBones(this Sprite)
        static object InvokeGetBones(Sprite sprite)
        {
            if (sprite == null) return null;
            var extType = System.Type.GetType(
                "UnityEngine.U2D.Animation.SpriteDataAccessExtensions, Unity.2D.Animation.Runtime");
            if (extType == null) return null;
            var m = extType.GetMethod("GetBones",
                BindingFlags.Public | BindingFlags.Static);
            if (m == null) return null;
            try { return m.Invoke(null, new object[] { sprite }); }
            catch { return null; }
        }

        /// <summary>
        /// 用 sprite 第一根 bone 的 guid,在角色层级里找到对应 Bone,
        /// 然后上溯到该骨骼链的顶端(父级不再是 Bone 的那一层)作为 rootBone。
        /// 这样即使有重名骨骼,也能靠 guid 找到正确的那条链。
        /// </summary>
        static Transform ResolveRootBone(Transform charRoot, Sprite sprite, System.Type boneType)
        {
            if (boneType == null) return FindAnyRootBone(charRoot, boneType);

            // 反射调用 sprite.GetBones()(扩展方法在 SpriteDataAccessExtensions,可能不可直接引用)
            var spriteBones = InvokeGetBones(sprite) as System.Array;
            if (spriteBones == null || spriteBones.Length == 0)
                return FindAnyRootBone(charRoot, boneType);

            // 拿 sprite 第一根骨骼的 guid
            var firstBoneObj = spriteBones.GetValue(0);
            var guidProp = firstBoneObj.GetType().GetProperty("guid",
                BindingFlags.Instance | BindingFlags.Public);
            string wantGuid = null;
            if (guidProp != null) wantGuid = guidProp.GetValue(firstBoneObj) as string;

            var allBones = charRoot.GetComponentsInChildren(boneType, true);
            var guidBoneProp = boneType.GetProperty("guid",
                BindingFlags.Instance | BindingFlags.Public);

            if (wantGuid != null && guidBoneProp != null)
            {
                foreach (var comp in allBones)
                {
                    var g = guidBoneProp.GetValue(comp) as string;
                    if (g == wantGuid)
                    {
                        // 找到了对应 Bone,上溯到链顶
                        var t = ((Component)comp).transform;
                        return AscendToChainTop(t, boneType);
                    }
                }
            }

            return FindAnyRootBone(charRoot, boneType);
        }

        // 上溯到骨骼链顶端:父级不再挂 Bone 组件的那一层
        static Transform AscendToChainTop(Transform t, System.Type boneType)
        {
            var top = t;
            while (top.parent != null && top.parent.GetComponent(boneType) != null)
                top = top.parent;
            return top;
        }

        // 兜底:优先 bone_1;否则第一根父层级没有 Bone 的
        static Transform FindAnyRootBone(Transform root, System.Type boneType)
        {
            if (boneType == null) return null;
            var bones = root.GetComponentsInChildren(boneType, true);
            foreach (var comp in bones)
            {
                var t = ((Component)comp).transform;
                if (t.name == "bone_1") return t;
            }
            foreach (var comp in bones)
            {
                var t = ((Component)comp).transform;
                if (t.parent == null || t.parent.GetComponent(boneType) == null)
                    return t;
            }
            return bones.Length > 0 ? ((Component)bones[0]).transform : null;
        }
    }
}
