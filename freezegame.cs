using BepInEx;
using UnityEngine;
using System;
using System.Runtime.InteropServices;

namespace WWTBAM.Accessibility
{
    [BepInPlugin("com.accessibility.wwtbam.gamefreeze", "WWTBAM Game Freeze Plugin", "1.0.0")]
    public class GameFreezePlugin : BaseUnityPlugin
    {
        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_testIfRunning();

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_speakText([MarshalAs(UnmanagedType.LPWStr)] string text);

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_cancelSpeech();

        private bool isFrozen = false;
        private float originalTimeScale = 1.0f;
        private string statusMessage = "";
        private float messageDisplayTime = 0f;
        private const float MESSAGE_DURATION = 2.0f;
        private bool nvdaAvailable = false;

        void Awake()
        {
            Logger.LogInfo("WWTBAM Game Freeze Plugin loaded!");
            Logger.LogInfo("Press F5 to freeze/unfreeze game");

            try
            {
                int result = nvdaController_testIfRunning();
                nvdaAvailable = (result == 0);

                if (nvdaAvailable)
                {
                    Logger.LogInfo("NVDA detected and ready!");
                }
                else
                {
                    Logger.LogWarning("NVDA not running or not available");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize NVDA: {ex.Message}");
                nvdaAvailable = false;
            }
        }

        void Update()
        {
            // Check for F5 key press
            if (Input.GetKeyDown(KeyCode.F5))
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

                Speak("Unfrozen");
            }
            else
            {
                // Freeze
                originalTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                isFrozen = true;
                statusMessage = "GAME FROZEN - Press F5 to unfreeze";
                messageDisplayTime = Time.realtimeSinceStartup;
                Logger.LogInfo("=== GAME FROZEN ===");
                Logger.LogInfo("Press F5 again to unfreeze");

                Speak("Frozen");
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

                string displayText = isFrozen ? "⏸ GAME FROZEN (F5) ⏸" : statusMessage;
                GUI.Box(new Rect(Screen.width / 2 - 300, 20, 600, 60), displayText, style);
            }
        }

        private void Speak(string text)
        {
            if (!nvdaAvailable)
            {
                Logger.LogWarning($"[NVDA NOT AVAILABLE] Would speak: {text}");
                return;
            }

            try
            {
                nvdaController_cancelSpeech();
                int result = nvdaController_speakText(text);

                if (result == 0)
                {
                    Logger.LogInfo($"[NVDA] Spoke: {text}");
                }
                else
                {
                    Logger.LogWarning($"[NVDA] Failed to speak (code {result}): {text}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error speaking via NVDA: {ex.Message}");
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