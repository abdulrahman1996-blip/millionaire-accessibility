using BepInEx;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Runtime.InteropServices;
using Image = UnityEngine.UI.Image;

namespace WWTBAM.Accessibility
{
    [BepInPlugin("com.accessibility.wwtbam.lifelines", "WWTBAM Lifelines Accessibility", "1.0.0")]
    public class LifelinesAccessibility : BaseUnityPlugin
    {
        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_testIfRunning();

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_speakText([MarshalAs(UnmanagedType.LPWStr)] string text);

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_cancelSpeech();

        private bool nvdaAvailable = false;
        private string lastHighlightedLifeline = "";
        private bool lifelineMenuActive = false;

        private const float CHECK_INTERVAL = 0.15f;
        private float timeSinceLastCheck = 0f;

        void Awake()
        {
            Logger.LogInfo("WWTBAM Lifelines Accessibility Plugin loaded!");
            Logger.LogInfo("Supports: 50:50, Phone a Friend, Ask the Audience, Swap Question, Walk Away");

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

            bool currentlyInLifelineMenu = IsLifelineMenuActive();

            if (currentlyInLifelineMenu && !lifelineMenuActive)
            {
                OnEnterLifelineMenu();
                lifelineMenuActive = true;
            }
            else if (!currentlyInLifelineMenu && lifelineMenuActive)
            {
                OnExitLifelineMenu();
                lifelineMenuActive = false;
            }
            else if (currentlyInLifelineMenu)
            {
                MonitorLifelineSelection();
            }
        }

        private bool IsLifelineMenuActive()
        {
            try
            {
                // Check if Panel_Lifeline is visible
                GameObject panel = GameObject.Find("UI_Canvas_WithPostProcess/Resizer/SoloClassic/Canvas_Money_Tree/Panel_Lifeline");
                if (panel == null || !panel.activeInHierarchy)
                    return false;

                // Check if any highlight is active (user navigating lifelines)
                if (IsAnyLifelineHighlighted())
                    return true;

                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking lifeline menu: {ex.Message}");
                return false;
            }
        }

        private bool IsAnyLifelineHighlighted()
        {
            string[] lifelines = { "50_50", "Call_A_Friend", "Public", "Ask_Pr", "WalkAway" };
            string[] highlightNames = { "HighLight_50", "HighLight_Call", "HighLight_Public", "HighLight_Ask", "HighLight_Walk" };

            for (int i = 0; i < lifelines.Length; i++)
            {
                string path = $"UI_Canvas_WithPostProcess/Resizer/SoloClassic/Canvas_Money_Tree/Panel_Lifeline/{lifelines[i]}_Holder/{highlightNames[i]}";

                // Special case for WalkAway - no _Holder suffix
                if (lifelines[i] == "WalkAway")
                {
                    path = $"UI_Canvas_WithPostProcess/Resizer/SoloClassic/Canvas_Money_Tree/Panel_Lifeline/WalkAway/{highlightNames[i]}";
                }

                GameObject highlightObj = GameObject.Find(path);

                if (highlightObj != null && highlightObj.activeInHierarchy)
                {
                    Image highlightImg = highlightObj.GetComponent<Image>();
                    if (highlightImg != null && highlightImg.enabled)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void OnEnterLifelineMenu()
        {
            Logger.LogInfo("=== ENTERED Lifeline Menu ===");
            lastHighlightedLifeline = "";

            // Announce current lifeline immediately
            string current = GetHighlightedLifeline();
            if (!string.IsNullOrEmpty(current))
            {
                string status = GetLifelineStatus(current);
                string announcement = $"{current}. {status}";
                Speak(announcement);
                lastHighlightedLifeline = current;
                Logger.LogInfo($"Initial lifeline: {announcement}");
            }
        }

        private void MonitorLifelineSelection()
        {
            try
            {
                string currentLifeline = GetHighlightedLifeline();

                if (!string.IsNullOrEmpty(currentLifeline) && currentLifeline != lastHighlightedLifeline)
                {
                    lastHighlightedLifeline = currentLifeline;

                    string status = GetLifelineStatus(currentLifeline);
                    string announcement = $"{currentLifeline}. {status}";

                    Speak(announcement);
                    Logger.LogInfo($"Lifeline selection changed: {announcement}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error monitoring lifeline selection: {ex.Message}");
            }
        }

        private string GetHighlightedLifeline()
        {
            try
            {
                // Check 50:50
                GameObject highlight50 = GameObject.Find("UI_Canvas_WithPostProcess/Resizer/SoloClassic/Canvas_Money_Tree/Panel_Lifeline/50_50_Holder/HighLight_50");
                if (IsHighlightActive(highlight50))
                    return "50 50";

                // Check Phone a Friend
                GameObject highlightCall = GameObject.Find("UI_Canvas_WithPostProcess/Resizer/SoloClassic/Canvas_Money_Tree/Panel_Lifeline/Call_A_Friend_Holder/HighLight_Call");
                if (IsHighlightActive(highlightCall))
                    return "Phone a friend";

                // Check Ask the Audience
                GameObject highlightPublic = GameObject.Find("UI_Canvas_WithPostProcess/Resizer/SoloClassic/Canvas_Money_Tree/Panel_Lifeline/Public_Holder/HighLight_Public");
                if (IsHighlightActive(highlightPublic))
                    return "Ask the audience";

                // Check Swap/Flip Question
                GameObject highlightAsk = GameObject.Find("UI_Canvas_WithPostProcess/Resizer/SoloClassic/Canvas_Money_Tree/Panel_Lifeline/Ask_Pr_Holder/HighLight_Ask");
                if (IsHighlightActive(highlightAsk))
                    return "Swap question";

                // Check Walk Away
                GameObject highlightWalk = GameObject.Find("UI_Canvas_WithPostProcess/Resizer/SoloClassic/Canvas_Money_Tree/Panel_Lifeline/WalkAway/HighLight_Walk");
                if (IsHighlightActive(highlightWalk))
                    return "Walk away";

                return "";
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error getting highlighted lifeline: {ex.Message}");
                return "";
            }
        }

        private bool IsHighlightActive(GameObject highlightObj)
        {
            if (highlightObj == null || !highlightObj.activeInHierarchy)
                return false;

            Image highlightImg = highlightObj.GetComponent<Image>();
            return highlightImg != null && highlightImg.enabled;
        }

        private string GetLifelineStatus(string lifelineName)
        {
            try
            {
                string holderPath = "";
                string usedName = "";

                switch (lifelineName)
                {
                    case "50 50":
                        holderPath = "50_50_Holder";
                        usedName = "Used_50";
                        break;
                    case "Phone a friend":
                        holderPath = "Call_A_Friend_Holder";
                        usedName = "Used_Friend";
                        break;
                    case "Ask the audience":
                        holderPath = "Public_Holder";
                        usedName = "Used_Public";
                        break;
                    case "Swap question":
                        holderPath = "Ask_Pr_Holder";
                        usedName = "Used_Ask";
                        break;
                    case "Walk away":
                        holderPath = "WalkAway";
                        usedName = "Used_Walk";
                        break;
                    default:
                        return "Unknown";
                }

                // Check if used
                GameObject usedObj = GameObject.Find($"UI_Canvas_WithPostProcess/Resizer/SoloClassic/Canvas_Money_Tree/Panel_Lifeline/{holderPath}/{usedName}");
                if (usedObj != null && usedObj.activeInHierarchy)
                {
                    Image usedImg = usedObj.GetComponent<Image>();
                    if (usedImg != null && usedImg.enabled)
                    {
                        return "Used";
                    }
                }

                return "Available";
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error getting lifeline status: {ex.Message}");
                return "Unknown";
            }
        }

        private void OnExitLifelineMenu()
        {
            Logger.LogInfo("=== EXITED Lifeline Menu ===");
            lastHighlightedLifeline = "";
            lifelineMenuActive = false;
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
            Logger.LogInfo("WWTBAM Lifelines Accessibility Plugin unloaded");
        }
    }
}