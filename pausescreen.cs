using BepInEx;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Runtime.InteropServices;
using Text = UnityEngine.UI.Text;
using Image = UnityEngine.UI.Image;

namespace WWTBAM.Accessibility
{
    [BepInPlugin("com.accessibility.wwtbam.pausescreen", "WWTBAM Pause Screen Accessibility", "1.0.0")]
    public class PauseScreenAccessibility : BaseUnityPlugin
    {
        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_testIfRunning();

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_speakText([MarshalAs(UnmanagedType.LPWStr)] string text);

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_cancelSpeech();

        private bool isInPauseScreen = false;
        private bool pauseAnnounced = false;
        private bool nvdaAvailable = false;
        private string lastAnnouncedButton = "";

        private const float CHECK_INTERVAL = 0.15f;
        private float timeSinceLastCheck = 0f;

        void Awake()
        {
            Logger.LogInfo("WWTBAM Pause Screen Accessibility Plugin loaded!");

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

            bool currentlyInPauseScreen = IsPauseScreen();

            if (currentlyInPauseScreen && !isInPauseScreen)
            {
                OnEnterPauseScreen();
                isInPauseScreen = true;
            }
            else if (!currentlyInPauseScreen && isInPauseScreen)
            {
                OnExitPauseScreen();
                isInPauseScreen = false;
            }
            else if (currentlyInPauseScreen)
            {
                MonitorButtonSelection();
            }
        }

        private bool IsPauseScreen()
        {
            try
            {
                GameObject pauseCanvas = GameObject.Find("Canvas/Resizer/Canvas_Pause");
                if (pauseCanvas == null || !pauseCanvas.activeInHierarchy)
                    return false;

                // Check specifically for PopUpLeave container (YES/NO dialog)
                // This only exists in REAL pause menu, not in TIPS screen
                GameObject popupLeave = GameObject.Find("Canvas/Resizer/Canvas_Pause/PauseMenu/PopupPause/PopUpLeave");
                if (popupLeave == null || !popupLeave.activeInHierarchy)
                {
                    Logger.LogInfo("PopUpLeave not visible - not real pause menu");
                    return false;
                }

                Logger.LogInfo("Real pause menu detected (PopUpLeave active)");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking pause screen: {ex.Message}");
                return false;
            }
        }

        private void OnEnterPauseScreen()
        {
            Logger.LogInfo("=== ENTERED Pause Screen ===");
            pauseAnnounced = false;
            lastAnnouncedButton = "";
            AnnouncePauseScreen();
        }

        private void AnnouncePauseScreen()
        {
            if (pauseAnnounced) return;

            try
            {
                // Get title
                GameObject titleObj = GameObject.Find("Canvas/Resizer/Canvas_Pause/PauseMenu/PopupPause/Fader/Text");
                Text titleText = titleObj?.GetComponent<Text>();
                string title = titleText != null ? titleText.text : "Game paused";

                // Get question
                GameObject questionObj = GameObject.Find("Canvas/Resizer/Canvas_Pause/PauseMenu/PopupPause/PopUpLeave/Fader/Text");
                Text questionText = questionObj?.GetComponent<Text>();
                string question = questionText != null ? questionText.text : "";

                // Get current selection
                string currentButton = GetCurrentButton();

                // Build announcement
                string announcement = title;
                if (!string.IsNullOrEmpty(question))
                {
                    announcement += ". " + question;
                }
                if (!string.IsNullOrEmpty(currentButton))
                {
                    announcement += ". " + currentButton;
                }

                Speak(announcement);
                pauseAnnounced = true;
                lastAnnouncedButton = currentButton;
                Logger.LogInfo($"Pause screen announced: {announcement}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error announcing pause screen: {ex.Message}");
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
                GameObject yesHighlight = GameObject.Find("Canvas/Resizer/Canvas_Pause/PauseMenu/PopupPause/PopUpLeave/Image_Yes/Highlight");
                if (yesHighlight != null && yesHighlight.activeInHierarchy)
                {
                    Image yesImg = yesHighlight.GetComponent<Image>();
                    if (yesImg != null && yesImg.enabled)
                    {
                        return "YES";
                    }
                }

                // Check NO highlight
                GameObject noHighlight = GameObject.Find("Canvas/Resizer/Canvas_Pause/PauseMenu/PopupPause/PopUpLeave/Image_No/Highlight");
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

        private void OnExitPauseScreen()
        {
            Logger.LogInfo("=== EXITED Pause Screen ===");
            pauseAnnounced = false;
            lastAnnouncedButton = "";
            isInPauseScreen = false;
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
            Logger.LogInfo("WWTBAM Pause Screen Accessibility Plugin unloaded");
        }
    }
}