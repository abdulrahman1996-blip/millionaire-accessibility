using BepInEx;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Runtime.InteropServices;
using Image = UnityEngine.UI.Image;
using Text = UnityEngine.UI.Text;

namespace WWTBAM.Accessibility
{
    [BepInPlugin("com.accessibility.wwtbam.walkawaypopup", "WWTBAM Walk Away Popup Accessibility", "1.0.0")]
    public class WalkAwayPopupAccessibility : BaseUnityPlugin
    {
        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_testIfRunning();

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_speakText([MarshalAs(UnmanagedType.LPWStr)] string text);

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_cancelSpeech();

        private bool isInWalkAwayPopup = false;
        private bool nvdaAvailable = false;
        private string lastAnnouncedButton = "";

        private const float CHECK_INTERVAL = 0.15f;
        private float timeSinceLastCheck = 0f;

        void Awake()
        {
            Logger.LogInfo("WWTBAM Walk Away Popup Accessibility Plugin loaded!");

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

            bool currentlyInWalkAwayPopup = IsWalkAwayPopup();

            if (currentlyInWalkAwayPopup && !isInWalkAwayPopup)
            {
                OnEnterWalkAwayPopup();
                isInWalkAwayPopup = true;
            }
            else if (!currentlyInWalkAwayPopup && isInWalkAwayPopup)
            {
                OnExitWalkAwayPopup();
                isInWalkAwayPopup = false;
            }
            else if (currentlyInWalkAwayPopup)
            {
                MonitorButtonSelection();
            }
        }

        private bool IsWalkAwayPopup()
        {
            try
            {
                // Check for PopUp canvas
                GameObject popupCanvas = GameObject.Find("Canvas/Resizer/PopUp");
                if (popupCanvas == null || !popupCanvas.activeInHierarchy)
                    return false;

                // Verify it's Walk Away popup by checking text
                GameObject textObj = GameObject.Find("Canvas/Resizer/PopUp/Text");
                if (textObj == null || !textObj.activeInHierarchy)
                    return false;

                Text text = textObj.GetComponent<Text>();
                if (text != null && text.text.Contains("Walk away"))
                {
                    // Double check YES/NO buttons exist
                    GameObject yesButton = GameObject.Find("Canvas/Resizer/PopUp/Image_Yes");
                    GameObject noButton = GameObject.Find("Canvas/Resizer/PopUp/Image_No");

                    if (yesButton != null && noButton != null &&
                        yesButton.activeInHierarchy && noButton.activeInHierarchy)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking walk away popup: {ex.Message}");
                return false;
            }
        }

        private void OnEnterWalkAwayPopup()
        {
            Logger.LogInfo("=== ENTERED Walk Away Popup ===");
            lastAnnouncedButton = "";

            // Get current winnings if available
            string winnings = GetCurrentWinnings();

            // Announce the popup opened
            string announcement = "Walk away with your winnings?";
            if (!string.IsNullOrEmpty(winnings))
            {
                announcement = $"Walk away with {winnings}?";
            }

            Speak(announcement);
            Logger.LogInfo($"Walk Away popup: {announcement}");

            // Announce current button immediately
            string currentButton = GetCurrentButton();
            if (!string.IsNullOrEmpty(currentButton))
            {
                Speak(currentButton);
                lastAnnouncedButton = currentButton;
                Logger.LogInfo($"Initial button: {currentButton}");
            }
        }

        private string GetCurrentWinnings()
        {
            try
            {
                // Try to find current winnings amount from money tree
                // This is optional - just for better UX
                GameObject moneyTreeCanvas = GameObject.Find("UI_Canvas_WithPostProcess/Resizer/SoloClassic/Canvas_Money_Tree");
                if (moneyTreeCanvas != null && moneyTreeCanvas.activeInHierarchy)
                {
                    // Look for highlighted money amount
                    // This is a best-effort attempt
                    // If not found, just return empty and use generic message
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error getting current winnings: {ex.Message}");
                return string.Empty;
            }
        }

        private void MonitorButtonSelection()
        {
            try
            {
                string currentButton = GetCurrentButton();

                if (!string.IsNullOrEmpty(currentButton) && currentButton != lastAnnouncedButton)
                {
                    lastAnnouncedButton = currentButton;
                    Speak(currentButton);
                    Logger.LogInfo($"Button selection changed: {currentButton}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error monitoring button selection: {ex.Message}");
            }
        }

        private string GetCurrentButton()
        {
            try
            {
                // Check YES highlight
                GameObject yesHighlight = GameObject.Find("Canvas/Resizer/PopUp/Image_Yes/Highlight");
                if (yesHighlight != null && yesHighlight.activeInHierarchy)
                {
                    Image yesImg = yesHighlight.GetComponent<Image>();
                    if (yesImg != null && yesImg.enabled)
                    {
                        return "YES";
                    }
                }

                // Check NO highlight
                GameObject noHighlight = GameObject.Find("Canvas/Resizer/PopUp/Image_No/Highlight");
                if (noHighlight != null && noHighlight.activeInHierarchy)
                {
                    Image noImg = noHighlight.GetComponent<Image>();
                    if (noImg != null && noImg.enabled)
                    {
                        return "NO";
                    }
                }

                return "";
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error getting current button: {ex.Message}");
                return "";
            }
        }

        private void OnExitWalkAwayPopup()
        {
            Logger.LogInfo("=== EXITED Walk Away Popup ===");
            lastAnnouncedButton = "";
            isInWalkAwayPopup = false;
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
            Logger.LogInfo("WWTBAM Walk Away Popup Accessibility Plugin unloaded");
        }
    }
}