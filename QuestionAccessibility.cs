using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Runtime.InteropServices;
using Text = UnityEngine.UI.Text;

namespace WWTBAM.Accessibility
{
    [BepInPlugin("com.accessibility.wwtbam.question", "WWTBAM Question Accessibility", "1.0.0")]
    public class QuestionAccessibility : BaseUnityPlugin
    {
        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_testIfRunning();

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_speakText([MarshalAs(UnmanagedType.LPWStr)] string text);

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_cancelSpeech();

        private bool isInQuestionScreen = false;
        private string lastAnnouncedQuestion = "";
        private bool nvdaAvailable = false;

        private const float CHECK_INTERVAL = 0.15f;
        private float timeSinceLastCheck = 0f;

        void Awake()
        {
            Logger.LogInfo("WWTBAM Question Accessibility Plugin loaded!");

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
                MonitorQuestionChanges();
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

        private string GetQuestionText()
        {
            try
            {
                GameObject questionObj = GameObject.Find("UI_Canvas_WithPostProcess/Resizer/SoloClassic/Canvas_Question/Unscaled/Question_Unscaled");
                Text questionText = questionObj?.GetComponent<Text>();
                
                if (questionText != null && !string.IsNullOrEmpty(questionText.text))
                {
                    // Skip placeholder text
                    if (questionText.text.Trim() == "Question")
                        return "";
                        
                    return questionText.text;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error getting question text: {ex.Message}");
            }

            return string.Empty;
        }

        private void OnEnterQuestionScreen()
        {
            Logger.LogInfo("=== ENTERED Question Screen ===");
            lastAnnouncedQuestion = "";
        }

        private void MonitorQuestionChanges()
        {
            try
            {
                string currentQuestion = GetQuestionText();

                // If we have a real question and it's different from last
                if (!string.IsNullOrEmpty(currentQuestion) && 
                    currentQuestion != lastAnnouncedQuestion)
                {
                    // VALIDATE: Check if answers loaded too (confirm real question, not early placeholder)
                    if (AreAnswersLoaded())
                    {
                        lastAnnouncedQuestion = currentQuestion;
                        Speak(currentQuestion);
                        Logger.LogInfo($"Question announced: {currentQuestion}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error monitoring question: {ex.Message}");
            }
        }

        private bool AreAnswersLoaded()
        {
            try
            {
                // Check if at least one answer has real text (not placeholder)
                string[] letters = { "A", "B", "C", "D" };
                
                foreach (string letter in letters)
                {
                    GameObject answerObj = GameObject.Find($"UI_Canvas_WithPostProcess/Resizer/SoloClassic/Canvas_Question/Unscaled/Answer_Unscaled_{letter}");
                    Text answerText = answerObj?.GetComponent<Text>();
                    
                    if (answerText != null && 
                        !string.IsNullOrEmpty(answerText.text) && 
                        answerText.text.Trim() != "Answer")
                    {
                        return true; // Found real answer, question is ready!
                    }
                }
            }
            catch { }
            
            return false; // No real answers yet
        }

        private void OnExitQuestionScreen()
        {
            Logger.LogInfo("=== EXITED Question Screen ===");
            lastAnnouncedQuestion = string.Empty;
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
            Logger.LogInfo("WWTBAM Question Accessibility Plugin unloaded");
        }
    }
}
