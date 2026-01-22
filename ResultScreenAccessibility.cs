using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Runtime.InteropServices;
using Text = UnityEngine.UI.Text;

namespace WWTBAM.Accessibility
{
    [BepInPlugin("com.accessibility.wwtbam.result", "WWTBAM Result Screen Accessibility", "1.0.0")]
    public class ResultScreenAccessibility : BaseUnityPlugin
    {
        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_testIfRunning();

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_speakText([MarshalAs(UnmanagedType.LPWStr)] string text);

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_cancelSpeech();

        private bool isInResultScreen = false;
        private bool resultAnnounced = false;
        private bool nvdaAvailable = false;
        private float resultEntryTime = 0f;
        private const float RESULT_DELAY = 2.0f; // Wait 2 seconds before announcing

        private const float CHECK_INTERVAL = 0.15f;
        private float timeSinceLastCheck = 0f;

        void Awake()
        {
            Logger.LogInfo("WWTBAM Result Screen Accessibility Plugin loaded!");

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

            bool currentlyInResultScreen = IsResultScreen();

            if (currentlyInResultScreen && !isInResultScreen)
            {
                OnEnterResultScreen();
                isInResultScreen = true;
            }
            else if (!currentlyInResultScreen && isInResultScreen)
            {
                OnExitResultScreen();
                isInResultScreen = false;
            }
            else if (currentlyInResultScreen && !resultAnnounced)
            {
                // Check if enough time passed before announcing
                if (Time.time - resultEntryTime >= RESULT_DELAY)
                {
                    AnnounceResult();
                }
            }
        }

        private bool IsResultScreen()
        {
            try
            {
                // Check Canvas_Gain (result screen)
                GameObject canvas = GameObject.Find("Canvas/Resizer/Canvas_Gain");
                if (canvas != null && canvas.activeInHierarchy)
                    return true;

                // Alternative path
                GameObject canvas2 = GameObject.Find("UI_Canvas_WithPostProcess/Resizer/SoloClassic/Canvas_Gain");
                if (canvas2 != null && canvas2.activeInHierarchy)
                    return true;

                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking result screen: {ex.Message}");
                return false;
            }
        }

        private void OnEnterResultScreen()
        {
            Logger.LogInfo("=== ENTERED Result Screen ===");
            resultAnnounced = false;
            resultEntryTime = Time.time;
            Logger.LogInfo($"Waiting {RESULT_DELAY} seconds before announcing result...");
        }

        private void AnnounceResult()
        {
            if (resultAnnounced) return;

            try
            {
                // Try main path first
                GameObject gainObj = GameObject.Find("Canvas/Resizer/Canvas_Gain/Main/Visu_Bot/Gain_Amount");
                Text gainText = gainObj?.GetComponent<Text>();

                string gainAmount = "";
                if (gainText != null && !string.IsNullOrEmpty(gainText.text))
                {
                    gainAmount = gainText.text;
                }

                // Try alternative path
                if (string.IsNullOrEmpty(gainAmount))
                {
                    GameObject gainObj2 = GameObject.Find("UI_Canvas_WithPostProcess/Resizer/SoloClassic/Canvas_Gain/Gain/Amount");
                    if (gainObj2 != null)
                    {
                        Text gainText2 = gainObj2.GetComponent<Text>();
                        if (gainText2 != null)
                        {
                            gainAmount = gainText2.text;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(gainAmount))
                {
                    string announcement = $"Total earnings: {gainAmount}";
                    Speak(announcement);
                    resultAnnounced = true;
                    Logger.LogInfo($"Result announced: {announcement}");
                }
                else
                {
                    Logger.LogWarning("Could not find earnings amount");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error announcing result: {ex.Message}");
            }
        }

        private void OnExitResultScreen()
        {
            Logger.LogInfo("=== EXITED Result Screen ===");
            resultAnnounced = false;
            isInResultScreen = false;
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
            Logger.LogInfo("WWTBAM Result Screen Accessibility Plugin unloaded");
        }
    }
}
