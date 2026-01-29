using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Runtime.InteropServices;
using Text = UnityEngine.UI.Text;
using Image = UnityEngine.UI.Image;

namespace WWTBAM.Accessibility
{
    [BepInPlugin("com.accessibility.wwtbam.timer", "WWTBAM Timer Accessibility", "1.0.0")]
    public class TimerAccessibility : BaseUnityPlugin
    {
        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_testIfRunning();

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_speakText([MarshalAs(UnmanagedType.LPWStr)] string text);

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_cancelSpeech();

        private bool nvdaAvailable = false;
        private string lastAnnouncedTime = "";
        private bool timerActive = false;

        // Config
        private ConfigEntry<KeyCode> announceTimeKey;
        private ConfigEntry<KeyCode> toggleVisibilityKey;
        private ConfigEntry<bool> autoAnnounceEvery5Seconds;
        private ConfigEntry<bool> makeTimerVisible;

        private const float CHECK_INTERVAL = 0.5f;
        private float timeSinceLastCheck = 0f;
        private float timeSinceLastAutoAnnounce = 0f;

        void Awake()
        {
            Logger.LogInfo("WWTBAM Timer Accessibility Plugin loaded!");

            // Config
            announceTimeKey = Config.Bind("Keybinds", "AnnounceTime", KeyCode.T,
                "Key to announce remaining time (default: T)");

            toggleVisibilityKey = Config.Bind("Keybinds", "ToggleVisibility", KeyCode.V,
                "Key to toggle timer visibility (default: V)");

            autoAnnounceEvery5Seconds = Config.Bind("Timer", "AutoAnnounce", false,
                "Automatically announce time every 5 seconds");

            makeTimerVisible = Config.Bind("Timer", "MakeVisible", true,
                "Make timer graphics visible (default: true)");

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
            // Handle keybinds
            if (Input.GetKeyDown(announceTimeKey.Value))
            {
                AnnounceCurrentTime(true);
            }

            if (Input.GetKeyDown(toggleVisibilityKey.Value))
            {
                ToggleTimerVisibility();
            }

            // Regular checks
            timeSinceLastCheck += Time.unscaledDeltaTime;
            if (timeSinceLastCheck < CHECK_INTERVAL)
                return;

            timeSinceLastCheck = 0f;

            bool currentlyTimerActive = IsTimerActive();

            if (currentlyTimerActive && !timerActive)
            {
                OnTimerStart();
                timerActive = true;
            }
            else if (!currentlyTimerActive && timerActive)
            {
                OnTimerEnd();
                timerActive = false;
            }
            else if (currentlyTimerActive)
            {
                // Auto-announce every 5 seconds if enabled
                if (autoAnnounceEvery5Seconds.Value)
                {
                    timeSinceLastAutoAnnounce += CHECK_INTERVAL;
                    if (timeSinceLastAutoAnnounce >= 5f)
                    {
                        AnnounceCurrentTime(false);
                        timeSinceLastAutoAnnounce = 0f;
                    }
                }

                // Apply visibility setting
                if (makeTimerVisible.Value)
                {
                    EnsureTimerVisible();
                }
            }
        }

        private bool IsTimerActive()
        {
            try
            {
                GameObject timerObj = GameObject.Find("UI_Canvas_WithPostProcess/Resizer/SoloClassic/Answer_Timer");
                if (timerObj == null || !timerObj.activeInHierarchy)
                    return false;

                GameObject textObj = GameObject.Find("UI_Canvas_WithPostProcess/Resizer/SoloClassic/Answer_Timer/Text");
                if (textObj == null || !textObj.activeInHierarchy)
                    return false;

                Text text = textObj.GetComponent<Text>();
                if (text != null && !string.IsNullOrEmpty(text.text))
                {
                    // Check if it's a valid number
                    if (int.TryParse(text.text, out int time))
                    {
                        return time > 0;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking timer active: {ex.Message}");
                return false;
            }
        }

        private string GetCurrentTime()
        {
            try
            {
                GameObject textObj = GameObject.Find("UI_Canvas_WithPostProcess/Resizer/SoloClassic/Answer_Timer/Text");
                if (textObj != null && textObj.activeInHierarchy)
                {
                    Text text = textObj.GetComponent<Text>();
                    if (text != null && !string.IsNullOrEmpty(text.text))
                    {
                        return text.text;
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error getting current time: {ex.Message}");
                return string.Empty;
            }
        }

        private void OnTimerStart()
        {
            Logger.LogInfo("=== TIMER STARTED ===");
            lastAnnouncedTime = "";
            timeSinceLastAutoAnnounce = 0f;

            string currentTime = GetCurrentTime();
            if (!string.IsNullOrEmpty(currentTime))
            {
                Speak($"Timer started, {currentTime} seconds");
                Logger.LogInfo($"Timer: {currentTime} seconds");
            }
        }

        private void OnTimerEnd()
        {
            Logger.LogInfo("=== TIMER ENDED ===");
            lastAnnouncedTime = "";
            timerActive = false;
        }

        private void AnnounceCurrentTime(bool manual)
        {
            try
            {
                string currentTime = GetCurrentTime();

                if (!string.IsNullOrEmpty(currentTime))
                {
                    // Only announce if time changed or if manually requested
                    if (manual || currentTime != lastAnnouncedTime)
                    {
                        Speak($"{currentTime} seconds");
                        Logger.LogInfo($"Time announced: {currentTime} seconds");
                        lastAnnouncedTime = currentTime;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error announcing time: {ex.Message}");
            }
        }

        private void EnsureTimerVisible()
        {
            try
            {
                // Make Fill visible
                GameObject fillObj = GameObject.Find("UI_Canvas_WithPostProcess/Resizer/SoloClassic/Answer_Timer/Fill");
                if (fillObj != null)
                {
                    Image fillImage = fillObj.GetComponent<Image>();
                    if (fillImage != null)
                    {
                        Color fillColor = fillImage.color;
                        if (fillColor.a < 0.5f)
                        {
                            fillColor.a = 1.0f;
                            fillImage.color = fillColor;
                        }
                    }
                }

                // Make Halo visible
                GameObject haloObj = GameObject.Find("UI_Canvas_WithPostProcess/Resizer/SoloClassic/Answer_Timer/Halo");
                if (haloObj != null)
                {
                    Image haloImage = haloObj.GetComponent<Image>();
                    if (haloImage != null)
                    {
                        Color haloColor = haloImage.color;
                        if (haloColor.a < 0.5f)
                        {
                            haloColor.a = 0.8f;
                            haloImage.color = haloColor;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error making timer visible: {ex.Message}");
            }
        }

        private void ToggleTimerVisibility()
        {
            makeTimerVisible.Value = !makeTimerVisible.Value;
            string state = makeTimerVisible.Value ? "visible" : "hidden";
            Speak($"Timer graphics {state}");
            Logger.LogInfo($"Timer visibility toggled: {state}");

            if (!makeTimerVisible.Value)
            {
                // Hide timer graphics
                try
                {
                    GameObject fillObj = GameObject.Find("UI_Canvas_WithPostProcess/Resizer/SoloClassic/Answer_Timer/Fill");
                    if (fillObj != null)
                    {
                        Image fillImage = fillObj.GetComponent<Image>();
                        if (fillImage != null)
                        {
                            Color fillColor = fillImage.color;
                            fillColor.a = 0f;
                            fillImage.color = fillColor;
                        }
                    }

                    GameObject haloObj = GameObject.Find("UI_Canvas_WithPostProcess/Resizer/SoloClassic/Answer_Timer/Halo");
                    if (haloObj != null)
                    {
                        Image haloImage = haloObj.GetComponent<Image>();
                        if (haloImage != null)
                        {
                            Color haloColor = haloImage.color;
                            haloColor.a = 0f;
                            haloImage.color = haloColor;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error hiding timer: {ex.Message}");
                }
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
            Logger.LogInfo("WWTBAM Timer Accessibility Plugin unloaded");
        }
    }
}