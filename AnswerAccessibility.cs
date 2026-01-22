using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Image = UnityEngine.UI.Image;
using Text = UnityEngine.UI.Text;

namespace WWTBAM.Accessibility
{
    [BepInPlugin("com.accessibility.wwtbam.answer", "WWTBAM Answer Accessibility", "1.0.0")]
    public class AnswerAccessibility : BaseUnityPlugin
    {
        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_testIfRunning();

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_speakText([MarshalAs(UnmanagedType.LPWStr)] string text);

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_cancelSpeech();

        private bool isInQuestionScreen = false;
        private string lastAnnouncedAnswer = "";
        private bool nvdaAvailable = false;
        private bool answersAnnounced = false;

        private Dictionary<string, bool> orangeStates = new Dictionary<string, bool>();

        private const float CHECK_INTERVAL = 0.15f;
        private float timeSinceLastCheck = 0f;

        private readonly string[] answerLetters = { "A", "B", "C", "D" };

        void Awake()
        {
            Logger.LogInfo("WWTBAM Answer Accessibility Plugin loaded!");

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

            bool currentlyInQuestionScreen = IsQuestionScreen();

            if (currentlyInQuestionScreen && !isInQuestionScreen)
            {
                OnEnterQuestionScreen();
                isInQuestionScreen = true;
            }
            else if (!currentlyInQuestionScreen && isInQuestionScreen)
            {
                OnExitQuestionScreen();
                isInQuestionScreen = false;
            }
            else if (currentlyInQuestionScreen)
            {
                MonitorAnswers();
            }
        }

        private bool IsQuestionScreen()
        {
            try
            {
                GameObject canvas = GameObject.Find("UI_Canvas_WithPostProcess/Resizer/SoloClassic/Canvas_Question");
                if (canvas == null || !canvas.activeInHierarchy)
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking question screen: {ex.Message}");
                return false;
            }
        }

        private string GetAnswerText(string letter)
        {
            try
            {
                GameObject answerObj = GameObject.Find($"UI_Canvas_WithPostProcess/Resizer/SoloClassic/Canvas_Question/Unscaled/Answer_Unscaled_{letter}");
                Text answerText = answerObj?.GetComponent<Text>();
                
                if (answerText != null && !string.IsNullOrEmpty(answerText.text))
                {
                    // Skip placeholder
                    if (answerText.text.Trim() == "Answer")
                        return "";
                        
                    return answerText.text;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error getting answer {letter} text: {ex.Message}");
            }

            return string.Empty;
        }

        private void OnEnterQuestionScreen()
        {
            Logger.LogInfo("=== ENTERED Question Screen (Answers) ===");

            orangeStates.Clear();
            lastAnnouncedAnswer = "";
            answersAnnounced = false;
        }

        private void MonitorAnswers()
        {
            try
            {
                // If answers not announced yet, try to announce them
                if (!answersAnnounced)
                {
                    string allAnswers = "";
                    bool hasRealAnswers = false;

                    foreach (string letter in answerLetters)
                    {
                        string answerText = GetAnswerText(letter);
                        
                        if (!string.IsNullOrEmpty(answerText))
                        {
                            hasRealAnswers = true;
                            if (!string.IsNullOrEmpty(allAnswers))
                                allAnswers += ". ";
                            allAnswers += $"{letter}, {answerText}";
                        }
                    }

                    if (hasRealAnswers && !string.IsNullOrEmpty(allAnswers))
                    {
                        Speak(allAnswers);
                        answersAnnounced = true;
                        Logger.LogInfo($"Answers announced: {allAnswers}");
                    }
                }

                // Monitor Orange for selection
                string currentFocusedAnswer = "";
                
                foreach (string letter in answerLetters)
                {
                    GameObject orangeObj = GameObject.Find($"UI_Canvas_WithPostProcess/Resizer/SoloClassic/Canvas_Question/{GetAnswerPath(letter)}/Orange_{letter}");
                    
                    if (orangeObj == null)
                    {
                        Logger.LogWarning($"Orange_{letter} GameObject NOT FOUND");
                        continue;
                    }

                    Image orangeImg = orangeObj.GetComponent<Image>();
                    if (orangeImg == null)
                    {
                        Logger.LogWarning($"Orange_{letter} Image component NULL");
                        continue;
                    }

                    Logger.LogInfo($"Orange_{letter} enabled = {orangeImg.enabled}");

                    // If THIS answer's Orange is enabled, it's focused
                    if (orangeImg.enabled)
                    {
                        currentFocusedAnswer = letter;
                        Logger.LogInfo($">>> FOUND FOCUSED: {letter}");
                        break; // Found focused answer, stop checking
                    }
                }
                
                // If we found a focused answer and it's different from last
                if (!string.IsNullOrEmpty(currentFocusedAnswer))
                {
                    string answerText = GetAnswerText(currentFocusedAnswer);
                    if (!string.IsNullOrEmpty(answerText))
                    {
                        string announcement = $"{currentFocusedAnswer}, {answerText}";
                        
                        if (announcement != lastAnnouncedAnswer)
                        {
                            lastAnnouncedAnswer = announcement;
                            Speak(announcement);
                            Logger.LogInfo($"Answer focused: {announcement}");
                        }
                    }
                }
                else
                {
                    Logger.LogInfo(">>> NO focused answer detected");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error monitoring answers: {ex.Message}");
            }
        }

        private string GetAnswerPath(string letter)
        {
            if (letter == "A" || letter == "B")
                return "AB_Holder/" + letter;
            else
                return "CD_Holder/" + letter;
        }

        private void OnExitQuestionScreen()
        {
            Logger.LogInfo("=== EXITED Question Screen (Answers) ===");
            orangeStates.Clear();
            lastAnnouncedAnswer = string.Empty;
            answersAnnounced = false;
            isInQuestionScreen = false;
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
            Logger.LogInfo("WWTBAM Answer Accessibility Plugin unloaded");
        }
    }
}
