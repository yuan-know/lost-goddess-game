// ============================================================================
//  SaveData.cs —— 可序列化存档数据体
//  GameState 对外是静态 API(契约 §1),内部持有这个可序列化对象,
//  SaveSystem 负责把它写成 JSON。对内容层 B 完全透明。
// ============================================================================

using System.Collections.Generic;

namespace LostGoddess
{
    /// <summary>时代/形态(契约 §1)。分章固定:进章时确定,整章不变。</summary>
    public enum Era { Young, Middle, Old }

    /// <summary>整个存档的数据。可被 JsonUtility / Newtonsoft 序列化。</summary>
    [System.Serializable]
    public class SaveData
    {
        public string currentRoom = Rooms.Sandbox; // 当前所在房间
        public Era currentEra = Era.Old;            // 当前时代(序章=老年)

        // JsonUtility 不支持 Dictionary,故用两个 List 存 flag 键值对
        public List<string> flagKeys = new List<string>();
        public List<bool>   flagVals = new List<bool>();

        public List<string> items = new List<string>();

        // ── 运行时字典(不参与序列化,加载时从 List 重建)──
        [System.NonSerialized] public Dictionary<string, bool> flags = new Dictionary<string, bool>();

        /// <summary>存盘前:把字典拍平成两个 List。</summary>
        public void PackForSave()
        {
            flagKeys.Clear();
            flagVals.Clear();
            foreach (var kv in flags)
            {
                flagKeys.Add(kv.Key);
                flagVals.Add(kv.Value);
            }
        }

        /// <summary>读盘后:把两个 List 还原成字典。</summary>
        public void UnpackAfterLoad()
        {
            flags = new Dictionary<string, bool>();
            int n = System.Math.Min(flagKeys.Count, flagVals.Count);
            for (int i = 0; i < n; i++)
                flags[flagKeys[i]] = flagVals[i];
            if (items == null) items = new List<string>();
        }
    }
}
