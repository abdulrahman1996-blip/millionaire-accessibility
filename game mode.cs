using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;
using Text = UnityEngine.UI.Text;

namespace WWTBAM.Accessibility
{
    [BepInPlugin("com.accessibility.wwtbam.gamemode", "WWTBAM Game Mode Selection Accessibility", "1.0.0")]
    public class GameModeSelectionAccessibility : BaseUnityPlugin
    {
        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_testIfRunning();

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_speakText([MarshalAs(UnmanagedType.LPWStr)] string text);

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_cancelSpeech();

        private bool isInGameModeScreen = false;
        private string lastSelectedMode = "";
        private bool nvdaAvailable = false;

        private const float CHECK_INTERVAL = 0.15f;
        private float timeSinceLastCheck = 0f;

        private readonly Dictionary<string, string> gameModeItems = new Dictionary<string, string>()
        {
            { "Bkg_Solo", "SOLO" },
            { "Bkg_Online", "ONLINE MULTIPLAYER" },
            { "Bkg_Multi_Local", "LOCAL MULTIPLAYER" },
            { "Bkg_Multi_Famille", "FAMILY MODE" }
        };

        void Awake()
        {
            Logger.LogInfo("WWTBAM Game Mode Selection Accessibility Plugin loaded!");

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
            timeSinceLastCheck += Time.unscaledDeltaTime;
            if (timeSinceLastCheck < CHECK_INTERVAL)
                return;

            timeSinceLastCheck = 0f;

            bool currentlyInGameModeScreen = IsGameModeScreen();

            if (currentlyInGameModeScreen && !isInGameModeScreen)
            {
                OnEnterGameModeScreen();
                isInGameModeScreen = true;
            }
            else if (!currentlyInGameModeScreen && isInGameModeScreen)
            {
                OnExitGameModeScreen();
                isInGameModeScreen = false;
            }
            else if (currentlyInGameModeScreen)
            {
                MonitorGameModeChanges();
            }
        }

        private bool IsGameModeScreen()
        {
            try
            {
                GameObject canvasMenu = GameObject.Find("Canvas/ResizedBase/Panel_GamePlay/Canvas_Menu");
                if (canvasMenu == null || !canvasMenu.activeInHierarchy)
                    return false;

                Transform menu = canvasMenu.transform.Find("Menu");
                if (menu == null || !menu.gameObject.activeInHierarchy)
                    return false;

                Transform gameMode = menu.Find("GameMode");
                if (gameMode == null || !gameMode.gameObject.activeInHierarchy)
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking game mode screen: {ex.Message}");
                return false;
            }
        }

        private string GetCurrentlySelectedGameMode()
        {
            try
            {
                string basePath = "Canvas/ResizedBase/Panel_GamePlay/Canvas_Menu/Menu/GameMode";

                foreach (var modeItem in gameModeItems)
                {
                    string modeKey = modeItem.Key;
                    string modeLabel = modeItem.Value;

                    GameObject orangeObj = GameObject.Find($"{basePath}/{modeKey}/Orange");

                    if (orangeObj != null)
                    {
                        Image orangeImage = orangeObj.GetComponent<Image>();

                        if (orangeImage != null && orangeImage.enabled)
                        {
                            return modeLabel;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error getting selected game mode: {ex.Message}");
            }

            return string.Empty;
        }

        private int GetGameModeCount()
        {
            try
            {
                string basePath = "Canvas/ResizedBase/Panel_GamePlay/Canvas_Menu/Menu/GameMode";
                int count = 0;

                foreach (var modeItem in gameModeItems)
                {
                    GameObject modeObj = GameObject.Find($"{basePath}/{modeItem.Key}");
                    if (modeObj != null && modeObj.activeInHierarchy)
                    {
                        count++;
                    }
                }

                return count;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error counting game modes: {ex.Message}");
            }

            return 0;
        }

        private void OnEnterGameModeScreen()
        {
            Logger.LogInfo("=== ENTERED Game Mode Selection Screen ===");

            string currentMode = GetCurrentlySelectedGameMode();
            int modeCount = GetGameModeCount();

            if (!string.IsNullOrEmpty(currentMode))
            {
                lastSelectedMode = currentMode;

                // Announce with count
                string announcement = $"{currentMode}, {modeCount} items";
                Speak(announcement);
                Logger.LogInfo($"Game mode selection: {announcement}");
            }
            else
            {
                Logger.LogWarning("Could not detect current game mode selection");
            }
        }

        private void MonitorGameModeChanges()
        {
            try
            {
                string currentMode = GetCurrentlySelectedGameMode();

                if (!string.IsNullOrEmpty(currentMode) &&
                    currentMode != lastSelectedMode)
                {
                    Logger.LogInfo($"Game mode selection changed: {lastSelectedMode} -> {currentMode}");
                    Speak(currentMode);
                    lastSelectedMode = currentMode;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error monitoring game mode changes: {ex.Message}");
            }
        }

        private void OnExitGameModeScreen()
        {
            Logger.LogInfo("=== EXITED Game Mode Selection Screen ===");
            lastSelectedMode = string.Empty;
            isInGameModeScreen = false;
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
            Logger.LogInfo("WWTBAM Game Mode Selection Accessibility Plugin unloaded");
        }
    }
}