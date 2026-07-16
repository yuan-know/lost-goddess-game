// ============================================================================
//  GameManager.cs —— 全局入口 / 单例 / 章节·时代调度  🟢
//  DontDestroyOnLoad 常驻。提供全局协程宿主,统管 Boot 初始化。
// ============================================================================

using UnityEngine;

namespace LostGoddess
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("启动设置")]
        [Tooltip("启动时是否自动尝试读档;否则开新游戏")]
        public bool autoLoadOnStart = false;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitSystems();
        }

        void InitSystems()
        {
            // 初始化各静态系统的运行时宿主(SceneLoader/AudioManager 需要一个 MonoBehaviour 跑协程)
            SceneLoader.Init(this);
            AudioManager.Init(this);

            if (autoLoadOnStart && SaveSystem.HasSave())
                SaveSystem.Load();
            else
                SaveSystem.NewGame();

            Debug.Log($"[GameManager] 系统初始化完成。当前时代={GameState.CurrentEra}, 房间={GameState.CurrentRoom}");
        }

        /// <summary>进入某章:设定时代并跳到该章首个房间(分章固定形态)。</summary>
        public void EnterChapter(Era era, string firstRoom)
        {
            GameState.CurrentEra = era;
            SceneLoader.GoToRoom(firstRoom);
        }

        /// <summary>快捷存/读(可绑到菜单按钮)。</summary>
        public void SaveGame() => SaveSystem.Save();
        public void LoadGame()
        {
            if (SaveSystem.Load())
                SceneLoader.GoToRoom(GameState.CurrentRoom);
        }
    }
}
