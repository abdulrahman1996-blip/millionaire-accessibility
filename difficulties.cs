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
    [BepInPlugin("com.accessibility.wwtbam.difficulty", "WWTBAM Difficulty Selection Accessibility", "1.0.0")]
    public class DifficultySelectionAccessibility : BaseUnityPlugin
    {
        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_testIfRunning();

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_speakText([MarshalAs(UnmanagedType.LPWStr)] string text);

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_cancelSpeech();

        private bool isInDifficultyScreen = false;
        private string lastSelectedDifficulty = "";
        private bool nvdaAvailable = false;

        private const float CHECK_INTERVAL = 0.15f;
        private float timeSinceLastCheck = 0f;

        private readonly Dictionary<string, string> difficultyItems = new Dictionary<string, string>()
        {
            { "Bkg_Child", "EASY" },
            { "Bkg_Adult", "NORMAL" }
        };

        void Awake()
        {
            Logger.LogInfo("WWTBAM Difficulty Selection Accessibility Plugin loaded!");

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

            bool currentlyInDifficultyScreen = IsDifficultyScreen();

            if (currentlyInDifficultyScreen && !isInDifficultyScreen)
            {
                OnEnterDifficultyScreen();
                isInDifficultyScreen = true;
            }
            else if (!currentlyInDifficultyScreen && isInDifficultyScreen)
            {
                OnExitDifficultyScreen();
                isInDifficultyScreen = false;
            }
            else if (currentlyInDifficultyScreen)
            {
                MonitorDifficultyChanges();
            }
        }

        private bool IsDifficultyScreen()
        {
            try
            {
                GameObject canvasMenu = GameObject.Find("Canvas/ResizedBase/Panel_GamePlay/Canvas_Menu");
                if (canvasMenu == null || !canvasMenu.activeInHierarchy)
                    return false;

                Transform menu = canvasMenu.transform.Find("Menu");
                if (menu == null || !menu.gameObject.activeInHierarchy)
                    return false;

                Transform age = menu.Find("Age");
                if (age == null || !age.gameObject.activeInHierarchy)
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking difficulty screen: {ex.Message}");
                return false;
            }
        }

        private string GetCurrentlySelectedDifficulty()
        {
            try
            {
                string basePath = "Canvas/ResizedBase/Panel_GamePlay/Canvas_Menu/Menu/Age";

                foreach (var diffItem in difficultyItems)
                {
                    string diffKey = diffItem.Key;
                    string diffLabel = diffItem.Value;

                    GameObject orangeObj = GameObject.Find($"{basePath}/{diffKey}/Orange");

                    if (orangeObj != null)
                    {
                        Image orangeImage = orangeObj.GetComponent<Image>();

                        if (orangeImage != null && orangeImage.enabled)
                        {
                            return diffLabel;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error getting selected difficulty: {ex.Message}");
            }

            return string.Empty;
        }

        private int GetDifficultyCount()
        {
            try
            {
                string basePath = "Canvas/ResizedBase/Panel_GamePlay/Canvas_Menu/Menu/Age";
                int count = 0;

                foreach (var diffItem in difficultyItems)
                {
                    GameObject diffObj = GameObject.Find($"{basePath}/{diffItem.Key}");
                    if (diffObj != null && diffObj.activeInHierarchy)
                    {
                        count++;
                    }
                }

                return count;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error counting difficulties: {ex.Message}");
            }

            return 0;
        }

        private void OnEnterDifficultyScreen()
        {
            Logger.LogInfo("=== ENTERED Difficulty Selection Screen ===");

            string currentDiff = GetCurrentlySelectedDifficulty();
            int diffCount = GetDifficultyCount();

            if (!string.IsNullOrEmpty(currentDiff))
            {
                lastSelectedDifficulty = currentDiff;

                string announcement = $"{currentDiff}, {diffCount} items";
                Speak(announcement);
                Logger.LogInfo($"Difficulty selection: {announcement}");
            }
            else
            {
                Logger.LogWarning("Could not detect current difficulty selection");
            }
        }

        private void MonitorDifficultyChanges()
        {
            try
            {
                string currentDiff = GetCurrentlySelectedDifficulty();

                if (!string.IsNullOrEmpty(currentDiff) &&
                    currentDiff != lastSelectedDifficulty)
                {
                    Logger.LogInfo($"Difficulty selection changed: {lastSelectedDifficulty} -> {currentDiff}");
                    Speak(currentDiff);
                    lastSelectedDifficulty = currentDiff;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error monitoring difficulty changes: {ex.Message}");
            }
        }

        private void OnExitDifficultyScreen()
        {
            Logger.LogInfo("=== EXITED Difficulty Selection Screen ===");
            lastSelectedDifficulty = string.Empty;
            isInDifficultyScreen = false;
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
            Logger.LogInfo("WWTBAM Difficulty Selection Accessibility Plugin unloaded");
        }
    }
}