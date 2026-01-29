using BepInEx;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Runtime.InteropServices;
using System.Collections;
using Text = UnityEngine.UI.Text;
using Image = UnityEngine.UI.Image;

namespace WWTBAM.Accessibility
{
    [BepInPlugin("com.accessibility.wwtbam.familymodeflow", "WWTBAM Family Mode Flow Accessibility", "1.0.0")]
    public class FamilyModeFlowAccessibility : BaseUnityPlugin
    {
        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_testIfRunning();

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_speakText([MarshalAs(UnmanagedType.LPWStr)] string text);

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_cancelSpeech();

        private bool nvdaAvailable = false;

        // Screen states
        private bool isInMessageScreen = false;
        private bool isInCandidatesScreen = false;
        private bool hasAnnouncedMessage = false;

        // Candidates screen tracking
        private int lastCandidateCount = -1;
        private string lastSelectedElement = "";

        void Awake()
        {
            Logger.LogInfo("WWTBAM Family Mode Flow Accessibility Plugin loaded!");

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
            CheckMessageScreen();
            CheckCandidatesScreen();
        }

        // ========== MESSAGE SCREEN ==========
        private void CheckMessageScreen()
        {
            bool currentlyInMessageScreen = IsMessageScreen();

            if (currentlyInMessageScreen && !isInMessageScreen)
            {
                OnEnterMessageScreen();
                isInMessageScreen = true;
            }
            else if (!currentlyInMessageScreen && isInMessageScreen)
            {
                OnExitMessageScreen();
                isInMessageScreen = false;
            }
        }

        private bool IsMessageScreen()
        {
            try
            {
                // Look for common paths where message might appear
                // Try multiple possible paths
                string[] possiblePaths = new string[]
                {
                    "Canvas/ResizedBase/Panel_GamePlay/Canvas_Menu/Menu/Multi_Family/Message",
                    "Canvas/ResizedBase/Panel_GamePlay/Canvas_PlayerSelect/PlayerSelect/FadePopup",
                    "Canvas/ResizedBase/Panel_GamePlay/MessagePopup",
                    "Canvas/FadePopup",
                    "Canvas/PopUp"
                };

                foreach (string path in possiblePaths)
                {
                    GameObject messageObj = GameObject.Find(path);
                    if (messageObj != null && messageObj.activeInHierarchy)
                    {
                        // Check if it contains text about controller
                        Text[] texts = messageObj.GetComponentsInChildren<Text>(false);
                        foreach (Text text in texts)
                        {
                            if (text.text.Contains("controller") || text.text.Contains("PRESS"))
                            {
                                Logger.LogInfo($"Found message screen at: {path}");
                                Logger.LogInfo($"Message text: {text.text}");
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking message screen: {ex.Message}");
                return false;
            }
        }

        private void OnEnterMessageScreen()
        {
            Logger.LogInfo("=== ENTERED Message Screen ===");
            hasAnnouncedMessage = false;

            // Announce after short delay to let screen fully appear
            StartCoroutine(AnnounceMessageAfterDelay(0.5f));
        }

        private IEnumerator AnnounceMessageAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (!hasAnnouncedMessage && isInMessageScreen)
            {
                string message = GetMessageText();
                if (!string.IsNullOrEmpty(message))
                {
                    Speak(message);
                    hasAnnouncedMessage = true;
                }
            }
        }

        private string GetMessageText()
        {
            try
            {
                // Search all active Text components for the message
                Text[] allTexts = FindObjectsOfType<Text>();
                foreach (Text text in allTexts)
                {
                    if (text.gameObject.activeInHierarchy &&
                        (text.text.Contains("controller") || text.text.Contains("requires")))
                    {
                        Logger.LogInfo($"Found message: {text.text}");
                        return text.text + ". Press to continue";
                    }
                }

                return "Press to continue";
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error getting message text: {ex.Message}");
                return "";
            }
        }

        private void OnExitMessageScreen()
        {
            Logger.LogInfo("=== EXITED Message Screen ===");
            hasAnnouncedMessage = false;
        }

        // ========== CANDIDATES SCREEN ==========
        private void CheckCandidatesScreen()
        {
            bool currentlyInCandidatesScreen = IsCandidatesScreen();

            if (currentlyInCandidatesScreen && !isInCandidatesScreen)
            {
                OnEnterCandidatesScreen();
                isInCandidatesScreen = true;
            }
            else if (!currentlyInCandidatesScreen && isInCandidatesScreen)
            {
                OnExitCandidatesScreen();
                isInCandidatesScreen = false;
            }
            else if (currentlyInCandidatesScreen)
            {
                MonitorCandidatesScreen();
            }
        }

        private bool IsCandidatesScreen()
        {
            try
            {
                // Look for "NUMBER OF CANDIDATES" text or similar
                Text[] allTexts = FindObjectsOfType<Text>();
                foreach (Text text in allTexts)
                {
                    if (text.gameObject.activeInHierarchy &&
                        text.text.Contains("CANDIDATES"))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking candidates screen: {ex.Message}");
                return false;
            }
        }

        private void OnEnterCandidatesScreen()
        {
            Logger.LogInfo("=== ENTERED Candidates Screen ===");
            lastCandidateCount = -1;
            lastSelectedElement = "";

            // Get initial state silently
            int currentCount = GetCandidateCount();
            if (currentCount > 0)
            {
                lastCandidateCount = currentCount;
                Logger.LogInfo($"Initial candidate count: {currentCount}");
            }
        }

        private void MonitorCandidatesScreen()
        {
            try
            {
                // Monitor candidate count changes
                int currentCount = GetCandidateCount();
                if (currentCount > 0 && currentCount != lastCandidateCount)
                {
                    Logger.LogInfo($"Candidate count changed: {lastCandidateCount} -> {currentCount}");
                    Speak($"{currentCount} candidates");
                    lastCandidateCount = currentCount;
                }

                // Monitor navigation (number selector vs READY button)
                string currentElement = GetSelectedElement();
                if (!string.IsNullOrEmpty(currentElement) && currentElement != lastSelectedElement)
                {
                    Logger.LogInfo($"Selection changed: {lastSelectedElement} -> {currentElement}");

                    if (currentElement == "ready")
                    {
                        Speak("Ready");
                    }
                    else if (currentElement == "number")
                    {
                        Speak($"Number of candidates, {lastCandidateCount}");
                    }

                    lastSelectedElement = currentElement;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error monitoring candidates screen: {ex.Message}");
            }
        }

        private int GetCandidateCount()
        {
            try
            {
                // Look for large number display
                Text[] allTexts = FindObjectsOfType<Text>();
                foreach (Text text in allTexts)
                {
                    if (text.gameObject.activeInHierarchy)
                    {
                        // Look for large numbers (font size > 40)
                        if (text.fontSize > 40 && int.TryParse(text.text.Trim(), out int number))
                        {
                            if (number >= 1 && number <= 10)
                            {
                                Logger.LogInfo($"Found candidate count: {number} (fontSize={text.fontSize})");
                                return number;
                            }
                        }
                    }
                }

                return -1;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error getting candidate count: {ex.Message}");
                return -1;
            }
        }

        private string GetSelectedElement()
        {
            try
            {
                // Look for READY button with green highlight or selected state
                Text[] allTexts = FindObjectsOfType<Text>();
                foreach (Text text in allTexts)
                {
                    if (text.gameObject.activeInHierarchy && text.text.Contains("READY"))
                    {
                        // Check if parent has highlight/selection
                        Transform parent = text.transform.parent;
                        if (parent != null)
                        {
                            Image[] images = parent.GetComponentsInChildren<Image>(false);
                            foreach (Image img in images)
                            {
                                // Check for green color (ready button selected)
                                if (img.color.g > 0.8f && img.color.r < 0.5f)
                                {
                                    return "ready";
                                }
                            }
                        }
                    }
                }

                // Default to number selector
                return "number";
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error getting selected element: {ex.Message}");
                return "";
            }
        }

        private void OnExitCandidatesScreen()
        {
            Logger.LogInfo("=== EXITED Candidates Screen ===");
            lastCandidateCount = -1;
            lastSelectedElement = "";
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
            Logger.LogInfo("WWTBAM Family Mode Flow Accessibility Plugin unloaded");
        }
    }
}