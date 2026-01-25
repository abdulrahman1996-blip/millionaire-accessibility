using BepInEx;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Text = UnityEngine.UI.Text;

namespace WWTBAM.Accessibility
{
    [BepInPlugin("com.accessibility.wwtbam.askaudience", "WWTBAM Ask Audience Accessibility", "1.0.0")]
    public class AskAudienceAccessibility : BaseUnityPlugin
    {
        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_testIfRunning();

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_speakText([MarshalAs(UnmanagedType.LPWStr)] string text);

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_cancelSpeech();

        private bool nvdaAvailable = false;
        private bool resultsAnnounced = false;

        private const float CHECK_INTERVAL = 0.15f;
        private float timeSinceLastCheck = 0f;

        void Awake()
        {
            Logger.LogInfo("WWTBAM Ask Audience Accessibility Plugin loaded!");

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

            CheckAudienceResults();
        }

        private void CheckAudienceResults()
        {
            try
            {
                // Check if Ask_Public (the actual results panel) is active
                GameObject askPublic = GameObject.Find("UI_Canvas_WithPostProcess/Resizer/SoloClassic/Lifeline_Panel/Canvas_Public/Ask_Public");

                if (askPublic != null && askPublic.activeInHierarchy && !resultsAnnounced)
                {
                    // Get all percentages
                    string percentA = GetPercentage("A");
                    string percentB = GetPercentage("B");
                    string percentC = GetPercentage("C");
                    string percentD = GetPercentage("D");

                    // Skip if any percentage is empty
                    if (string.IsNullOrEmpty(percentA) || string.IsNullOrEmpty(percentB) ||
                        string.IsNullOrEmpty(percentC) || string.IsNullOrEmpty(percentD))
                    {
                        Logger.LogInfo("Some percentages empty - waiting...");
                        return;
                    }

                    // CRITICAL CHECK: Skip if all are 100% (Press Any screen placeholder!)
                    if (percentA == "100%" && percentB == "100%" &&
                        percentC == "100%" && percentD == "100%")
                    {
                        Logger.LogInfo("All percentages are 100% - Press Any screen detected, SKIPPING");
                        return;
                    }

                    // Calculate total to verify real results
                    int totalPercent = ParsePercentage(percentA) + ParsePercentage(percentB) +
                                      ParsePercentage(percentC) + ParsePercentage(percentD);

                    Logger.LogInfo($"Real results detected! A:{percentA} B:{percentB} C:{percentC} D:{percentD} Total:{totalPercent}%");

                    // Announce results!
                    AnnounceResults();
                }
                else if (askPublic == null || !askPublic.activeInHierarchy)
                {
                    // Reset when screen closes
                    if (resultsAnnounced)
                    {
                        Logger.LogInfo("Ask_Public closed - resetting");
                        resultsAnnounced = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking audience results: {ex.Message}");
            }
        }

        private void AnnounceResults()
        {
            try
            {
                Logger.LogInfo("=== AnnounceResults() called ===");

                // Get all 4 percentages
                string percentA = GetPercentage("A");
                string percentB = GetPercentage("B");
                string percentC = GetPercentage("C");
                string percentD = GetPercentage("D");

                Logger.LogInfo($"Retrieved: A={percentA}, B={percentB}, C={percentC}, D={percentD}");

                // Ensure all percentages are valid
                if (string.IsNullOrEmpty(percentA) || string.IsNullOrEmpty(percentB) ||
                    string.IsNullOrEmpty(percentC) || string.IsNullOrEmpty(percentD))
                {
                    Logger.LogWarning("Some percentages are empty - skipping announcement");
                    return;
                }

                // Find highest percentage
                var votes = new Dictionary<string, int>
                {
                    { "A", ParsePercentage(percentA) },
                    { "B", ParsePercentage(percentB) },
                    { "C", ParsePercentage(percentC) },
                    { "D", ParsePercentage(percentD) }
                };

                string highest = "A";
                int highestValue = 0;

                foreach (var vote in votes)
                {
                    if (vote.Value > highestValue)
                    {
                        highestValue = vote.Value;
                        highest = vote.Key;
                    }
                }

                // Announce only the highest
                string announcement = $"Audience says: {highest}, {highestValue} percent";

                Logger.LogInfo($"About to speak: {announcement}");
                Speak(announcement);
                resultsAnnounced = true;

                Logger.LogInfo("=== Results announced successfully ===");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error announcing results: {ex.Message}");
            }
        }

        private int ParsePercentage(string percent)
        {
            try
            {
                // Remove % sign and parse
                string cleaned = percent.Replace("%", "").Trim();
                return int.Parse(cleaned);
            }
            catch
            {
                return 0;
            }
        }

        private string GetPercentage(string letter)
        {
            try
            {
                string path = $"UI_Canvas_WithPostProcess/Resizer/SoloClassic/Lifeline_Panel/Canvas_Public/Ask_Public/Column_{letter}/Value_{letter}";
                GameObject valueObj = GameObject.Find(path);

                if (valueObj != null)
                {
                    Text valueText = valueObj.GetComponent<Text>();
                    if (valueText != null && !string.IsNullOrEmpty(valueText.text))
                    {
                        string trimmed = valueText.text.Trim();
                        Logger.LogInfo($"Found {letter}: '{trimmed}'");
                        return trimmed;
                    }
                    else
                    {
                        Logger.LogInfo($"{letter}: Text component is null or text is empty");
                    }
                }
                else
                {
                    Logger.LogInfo($"{letter}: GameObject not found at {path}");
                }

                return "";
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error getting percentage for {letter}: {ex.Message}");
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
            Logger.LogInfo("WWTBAM Ask Audience Accessibility Plugin unloaded");
        }
    }
}