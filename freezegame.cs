using BepInEx;
using UnityEngine;

namespace WWTBAM.Accessibility
{
    [BepInPlugin("com.accessibility.wwtbam.gamefreeze", "WWTBAM Game Freeze Plugin", "1.0.0")]
    public class GameFreezePlugin : BaseUnityPlugin
    {
        private bool isFrozen = false;
        private float originalTimeScale = 1.0f;
        private string statusMessage = "";
        private float messageDisplayTime = 0f;
        private const float MESSAGE_DURATION = 2.0f;

        void Awake()
        {
            Logger.LogInfo("WWTBAM Game Freeze Plugin loaded!");
            Logger.LogInfo("Press TAB to freeze/unfreeze game");
        }

        void Update()
        {
            // Check for Tab key press
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                ToggleFreeze();
            }
        }

        private void ToggleFreeze()
        {
            if (isFrozen)
            {
                // Unfreeze
                Time.timeScale = originalTimeScale;
                isFrozen = false;
                statusMessage = "GAME UNFROZEN";
                messageDisplayTime = Time.realtimeSinceStartup;
                Logger.LogInfo("=== GAME UNFROZEN ===");
                Logger.LogInfo($"Time.timeScale restored to {originalTimeScale}");
            }
            else
            {
                // Freeze
                originalTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                isFrozen = true;
                statusMessage = "GAME FROZEN - Press TAB to unfreeze";
                messageDisplayTime = Time.realtimeSinceStartup;
                Logger.LogInfo("=== GAME FROZEN ===");
                Logger.LogInfo("Press TAB again to unfreeze");
            }
        }

        void OnGUI()
        {
            // Show freeze status on screen
            if (isFrozen || (!string.IsNullOrEmpty(statusMessage) &&
                Time.realtimeSinceStartup - messageDisplayTime < MESSAGE_DURATION))
            {
                // Create big red box at top of screen
                GUI.backgroundColor = Color.red;
                GUIStyle style = new GUIStyle(GUI.skin.box);
                style.fontSize = 30;
                style.fontStyle = FontStyle.Bold;
                style.normal.textColor = Color.white;
                style.alignment = TextAnchor.MiddleCenter;

                string displayText = isFrozen ? "⏸ GAME FROZEN ⏸" : statusMessage;
                GUI.Box(new Rect(Screen.width / 2 - 300, 20, 600, 60), displayText, style);
            }
        }

        void OnDestroy()
        {
            // Make sure to unfreeze when plugin unloads
            if (isFrozen)
            {
                Time.timeScale = originalTimeScale;
                Logger.LogInfo("Plugin unloaded - game unfrozen");
            }
        }
    }
}