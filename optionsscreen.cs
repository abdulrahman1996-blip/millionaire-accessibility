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
    [BepInPlugin("com.accessibility.wwtbam.options", "WWTBAM Options Screen Accessibility", "1.0.0")]
    public class OptionsScreenAccessibility : BaseUnityPlugin
    {
        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_testIfRunning();

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_speakText([MarshalAs(UnmanagedType.LPWStr)] string text);

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_cancelSpeech();

        private bool isInOptionsScreen = false;
        private string lastSelectedOption = "";
        private string lastValue = "";
        private bool nvdaAvailable = false;

        private const float CHECK_INTERVAL = 0.10f;
        private float timeSinceLastCheck = 0f;

        // Menu structure - defines ALL sections and items
        private readonly Dictionary<string, (string section, string displayName)> menuItems = new Dictionary<string, (string, string)>()
        {
            // Language section
            { "Subtitle", ("Langue", "Subtitles") },
            { "Country", ("Langue", "Language") },
            
            // Video section
            { "Resolution", ("Video", "Resolution") },
            { "Quality", ("Video", "Graphics Quality") },
            { "Dislpay", ("Video", "Display Mode") },
            
            // Sound section
            { "Master", ("Sound", "Master Volume") },
            { "Voice", ("Sound", "Voice Volume") },
            { "SFX", ("Sound", "Sound Effects Volume") },
            { "Music", ("Sound", "Music Volume") }
        };

        // Navigation order - THIS IS THE KEY!
        // Game navigates through items in this exact order
        private readonly List<string> navigationOrder = new List<string>()
        {
            "Subtitle",    // Langue
            "Country",     // Langue
            "Resolution",  // Video
            "Quality",     // Video
            "Dislpay",     // Video
            "Master",      // Sound
            "Voice",       // Sound
            "SFX",         // Sound
            "Music"        // Sound (wraps back to Subtitle)
        };

        void Awake()
        {
            Logger.LogInfo("WWTBAM Options Screen Accessibility Plugin loaded!");

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

            bool currentlyInOptionsScreen = IsOptionsScreen();

            if (currentlyInOptionsScreen && !isInOptionsScreen)
            {
                OnEnterOptionsScreen();
                isInOptionsScreen = true;
            }
            else if (!currentlyInOptionsScreen && isInOptionsScreen)
            {
                OnExitOptionsScreen();
                isInOptionsScreen = false;
            }
            else if (currentlyInOptionsScreen)
            {
                MonitorOptionChanges();
            }
        }

        private bool IsOptionsScreen()
        {
            try
            {
                GameObject canvasOptions = GameObject.Find("Canvas/ResizedBase/Canvas_Options");
                if (canvasOptions == null || !canvasOptions.activeInHierarchy)
                    return false;

                Transform panelOption = canvasOptions.transform.Find("Panel_Option");
                if (panelOption == null || !panelOption.gameObject.activeInHierarchy)
                    return false;

                Transform menu = panelOption.Find("Menu");
                return menu != null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking options screen: {ex.Message}");
                return false;
            }
        }

        private string GetCurrentlySelectedOption()
        {
            try
            {
                string basePath = "Canvas/ResizedBase/Canvas_Options/Panel_Option/Menu";

                // Check items in navigation order
                // Return the FIRST item found with Orange enabled
                foreach (string itemKey in navigationOrder)
                {
                    if (!menuItems.ContainsKey(itemKey))
                        continue;

                    string section = menuItems[itemKey].section;
                    string path = $"{basePath}/{section}/{itemKey}/Orange";

                    GameObject orangeObj = GameObject.Find(path);
                    if (orangeObj != null && orangeObj.activeInHierarchy)
                    {
                        Image orangeImage = orangeObj.GetComponent<Image>();
                        if (orangeImage != null && orangeImage.enabled)
                        {
                            // Found it!
                            return itemKey;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error getting selected option: {ex.Message}");
            }

            return string.Empty;
        }

        private string GetOptionValue(string optionKey)
        {
            try
            {
                if (!menuItems.ContainsKey(optionKey))
                    return string.Empty;

                string section = menuItems[optionKey].section;
                string basePath = $"Canvas/ResizedBase/Canvas_Options/Panel_Option/Menu/{section}/{optionKey}";

                // Language & Video sections - get from Panel/Text
                if (section == "Langue" || section == "Video")
                {
                    GameObject textObj = GameObject.Find($"{basePath}/Panel/Text");
                    if (textObj != null && textObj.activeInHierarchy)
                    {
                        Text text = textObj.GetComponent<Text>();
                        if (text != null && !string.IsNullOrEmpty(text.text))
                        {
                            return text.text;
                        }
                    }
                }
                // Sound section - get from Score_Value
                else if (section == "Sound")
                {
                    GameObject scoreObj = GameObject.Find($"{basePath}/Score_Value");
                    if (scoreObj != null && scoreObj.activeInHierarchy)
                    {
                        Text text = scoreObj.GetComponent<Text>();
                        if (text != null && !string.IsNullOrEmpty(text.text))
                        {
                            return text.text;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error getting option value for {optionKey}: {ex.Message}");
            }

            return string.Empty;
        }

        private void OnEnterOptionsScreen()
        {
            Logger.LogInfo("=== ENTERED Options Screen ===");

            string currentOption = GetCurrentlySelectedOption();

            if (!string.IsNullOrEmpty(currentOption) && menuItems.ContainsKey(currentOption))
            {
                string displayName = menuItems[currentOption].displayName;
                string value = GetOptionValue(currentOption);

                lastSelectedOption = currentOption;
                lastValue = value;

                string announcement = displayName;
                if (!string.IsNullOrEmpty(value))
                {
                    announcement += $" {value}";
                }

                Speak(announcement);
                Logger.LogInfo($"Options entered: {currentOption} = {announcement}");
            }
            else
            {
                Logger.LogWarning("Could not detect current option selection");
            }
        }

        private void MonitorOptionChanges()
        {
            try
            {
                string currentOption = GetCurrentlySelectedOption();

                if (string.IsNullOrEmpty(currentOption))
                    return;

                string currentValue = GetOptionValue(currentOption);

                // Option changed (navigation)
                if (currentOption != lastSelectedOption)
                {
                    if (!menuItems.ContainsKey(currentOption))
                        return;

                    string displayName = menuItems[currentOption].displayName;
                    string announcement = displayName;

                    if (!string.IsNullOrEmpty(currentValue))
                    {
                        announcement += $" {currentValue}";
                    }

                    Speak(announcement);
                    Logger.LogInfo($"Option changed: {currentOption} -> {announcement}");

                    lastSelectedOption = currentOption;
                    lastValue = currentValue;
                }
                // Value changed (same option, different value)
                else if (currentValue != lastValue && !string.IsNullOrEmpty(currentValue))
                {
                    Speak(currentValue);
                    Logger.LogInfo($"Value changed: {currentOption} = {currentValue}");
                    lastValue = currentValue;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error monitoring option changes: {ex.Message}");
            }
        }

        private void OnExitOptionsScreen()
        {
            Logger.LogInfo("=== EXITED Options Screen ===");
            lastSelectedOption = string.Empty;
            lastValue = string.Empty;
            isInOptionsScreen = false;
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
            Logger.LogInfo("WWTBAM Options Screen Accessibility Plugin unloaded");
        }
    }
}