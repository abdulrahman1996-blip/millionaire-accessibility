using BepInEx;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Runtime.InteropServices;
using Text = UnityEngine.UI.Text;
using Image = UnityEngine.UI.Image;

namespace WWTBAM.Accessibility
{
    [BepInPlugin("com.accessibility.wwtbam.phoneafriend", "WWTBAM Phone a Friend Accessibility", "1.0.0")]
    public class PhoneAFriendAccessibility : BaseUnityPlugin
    {
        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_testIfRunning();

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_speakText([MarshalAs(UnmanagedType.LPWStr)] string text);

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_cancelSpeech();

        private bool nvdaAvailable = false;
        private string lastSelectedFriend = "";

        private const float CHECK_INTERVAL = 0.15f;
        private float timeSinceLastCheck = 0f;

        void Awake()
        {
            Logger.LogInfo("WWTBAM Phone a Friend Accessibility Plugin loaded!");

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

            // Continuously monitor friend selection
            MonitorFriendSelection();
        }

        private void MonitorFriendSelection()
        {
            try
            {
                string currentFriend = GetSelectedFriend();

                // Only announce if friend changed AND is not empty
                if (!string.IsNullOrEmpty(currentFriend) && currentFriend != lastSelectedFriend)
                {
                    lastSelectedFriend = currentFriend;
                    Speak(currentFriend);
                    Logger.LogInfo($"Friend highlighted: {currentFriend}");
                }
                else if (string.IsNullOrEmpty(currentFriend) && !string.IsNullOrEmpty(lastSelectedFriend))
                {
                    // Reset when no friend selected
                    lastSelectedFriend = "";
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error monitoring friend selection: {ex.Message}");
            }
        }

        private string GetSelectedFriend()
        {
            try
            {
                // Check HighLight (arrow key navigation), NOT Selected (Enter confirmation)
                for (int i = 1; i <= 4; i++)
                {
                    GameObject highlightObj = GameObject.Find($"UI_Canvas_WithPostProcess/Resizer/SoloClassic/Lifeline_Panel/Call_A_Friend/Canvas_Choice/{i}/HighLight_{i}");

                    if (highlightObj != null && highlightObj.activeInHierarchy)
                    {
                        Image highlightImg = highlightObj.GetComponent<Image>();
                        if (highlightImg != null && highlightImg.enabled)
                        {
                            // Get friend name and job
                            GameObject nameObj = GameObject.Find($"UI_Canvas_WithPostProcess/Resizer/SoloClassic/Lifeline_Panel/Call_A_Friend/Canvas_Choice/{i}/Name_{i}");
                            GameObject jobObj = GameObject.Find($"UI_Canvas_WithPostProcess/Resizer/SoloClassic/Lifeline_Panel/Call_A_Friend/Canvas_Choice/{i}/Job_{i}");

                            string name = "";
                            string job = "";

                            if (nameObj != null)
                            {
                                Text nameText = nameObj.GetComponent<Text>();
                                if (nameText != null)
                                    name = nameText.text;
                            }

                            if (jobObj != null)
                            {
                                Text jobText = jobObj.GetComponent<Text>();
                                if (jobText != null)
                                    job = jobText.text;
                            }

                            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(job))
                            {
                                return $"{name}, {job}";
                            }
                            else if (!string.IsNullOrEmpty(name))
                            {
                                return name;
                            }
                        }
                    }
                }

                return "";
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error getting selected friend: {ex.Message}");
                return "";
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
            Logger.LogInfo("WWTBAM Phone a Friend Accessibility Plugin unloaded");
        }
    }
}