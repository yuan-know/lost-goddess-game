// ============================================================================
//  SaveSystem.cs —— 存档读写(JSON → persistentDataPath)  🟢
//  用 Newtonsoft Json(包已装,能可靠序列化字典/List;JsonUtility 也可,
//  这里为稳妥用 SaveData 的 Pack/Unpack + JsonUtility,零额外依赖)。
// ============================================================================

using System.IO;
using UnityEngine;

namespace LostGoddess
{
    public static class SaveSystem
    {
        const string FileName = "save_slot0.json";

        static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool HasSave() => File.Exists(SavePath);

        /// <summary>把 GameState 当前数据写盘。</summary>
        public static void Save()
        {
            var data = GameState.Data;
            data.PackForSave();
            string json = JsonUtility.ToJson(data, prettyPrint: true);
            try
            {
                File.WriteAllText(SavePath, json);
                Debug.Log($"[SaveSystem] 已保存 → {SavePath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveSystem] 保存失败: {e.Message}");
            }
        }

        /// <summary>读盘并注入 GameState;无存档则返回 false。</summary>
        public static bool Load()
        {
            if (!HasSave())
            {
                Debug.Log("[SaveSystem] 无存档,跳过读取。");
                return false;
            }
            try
            {
                string json = File.ReadAllText(SavePath);
                var data = JsonUtility.FromJson<SaveData>(json);
                GameState._SetData(data);
                Debug.Log($"[SaveSystem] 已读取 ← {SavePath}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveSystem] 读取失败: {e.Message}");
                return false;
            }
        }

        /// <summary>新游戏:清空存档数据(不删文件,存盘时覆盖)。</summary>
        public static void NewGame()
        {
            GameState._SetData(new SaveData());
            Debug.Log("[SaveSystem] 开始新游戏(内存已重置)。");
        }

        /// <summary>删除存档文件。</summary>
        public static void DeleteSave()
        {
            if (HasSave())
            {
                File.Delete(SavePath);
                Debug.Log("[SaveSystem] 存档文件已删除。");
            }
        }
    }
}
