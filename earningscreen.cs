using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Runtime.InteropServices;
using Text = UnityEngine.UI.Text;

namespace WWTBAM.Accessibility
{
    [BepInPlugin("com.accessibility.wwtbam.earnings", "WWTBAM Earnings Screen Accessibility", "1.0.0")]
    public class EarningsScreenAccessibility : BaseUnityPlugin
    {
        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_testIfRunning();

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_speakText([MarshalAs(UnmanagedType.LPWStr)] string text);

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_cancelSpeech();

        private bool isInEarningsScreen = false;
        private bool earningsAnnounced = false;
        private bool nvdaAvailable = false;

        private const float CHECK_INTERVAL = 0.15f;
        private float timeSinceLastCheck = 0f;

        void Awake()
        {
            Logger.LogInfo("WWTBAM Earnings Screen Accessibility Plugin loaded!");

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

            bool currentlyInEarningsScreen = IsEarningsScreen();

            if (currentlyInEarningsScreen && !isInEarningsScreen)
            {
                OnEnterEarningsScreen();
                isInEarningsScreen = true;
            }
            else if (!currentlyInEarningsScreen && isInEarningsScreen)
            {
                OnExitEarningsScreen();
                isInEarningsScreen = false;
            }
        }

        private bool IsEarningsScreen()
        {
            try
            {
                // Check Canvas_Gain active
                GameObject canvas = GameObject.Find("Canvas/Resizer/Canvas_Gain");
                if (canvas == null || !canvas.activeInHierarchy)
                    return false;

                // CRITICAL: Skip if TIPS present (not real earnings screen!)
                GameObject tipsObj = GameObject.Find("Canvas/Resizer/Canvas_Gain/Main/Visu_Bot/Tips");
                if (tipsObj != null && tipsObj.activeInHierarchy)
                {
                    Logger.LogInfo("TIPS screen detected - skipping (not real earnings)");
                    return false;
                }

                // Check if earnings text present
                GameObject gainObj = GameObject.Find("Canvas/Resizer/Canvas_Gain/Main/Visu_Bot/Gain_Amount");
                if (gainObj == null)
                    return false;

                Text gainText = gainObj.GetComponent<Text>();
                if (gainText == null || string.IsNullOrEmpty(gainText.text))
                    return false;

                // Skip if placeholder amount (1,000,000)
                if (gainText.text.Contains("1 000 000") || gainText.text.Contains("1000000"))
                {
                    Logger.LogInfo("Placeholder earnings detected - skipping");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking earnings screen: {ex.Message}");
                return false;
            }
        }

        private void OnEnterEarningsScreen()
        {
            Logger.LogInfo("=== ENTERED Earnings Screen ===");
            earningsAnnounced = false;
            AnnounceEarnings();
        }

        private void AnnounceEarnings()
        {
            if (earningsAnnounced) return;

            try
            {
                GameObject gainObj = GameObject.Find("Canvas/Resizer/Canvas_Gain/Main/Visu_Bot/Gain_Amount");
                Text gainText = gainObj?.GetComponent<Text>();

                if (gainText != null && !string.IsNullOrEmpty(gainText.text))
                {
                    string earnings = gainText.text;
                    string announcement = $"Total earnings: {earnings}";

                    Speak(announcement);
                    earningsAnnounced = true;
                    Logger.LogInfo($"Earnings announced: {announcement}");
                }
                else
                {
                    Logger.LogWarning("Could not find earnings amount");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error announcing earnings: {ex.Message}");
            }
        }

        private void OnExitEarningsScreen()
        {
            Logger.LogInfo("=== EXITED Earnings Screen ===");
            earningsAnnounced = false;
            isInEarningsScreen = false;
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
            Logger.LogInfo("WWTBAM Earnings Screen Accessibility Plugin unloaded");
        }
    }
}