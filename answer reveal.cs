using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Runtime.InteropServices;
using Image = UnityEngine.UI.Image;
using Text = UnityEngine.UI.Text;

namespace WWTBAM.Accessibility
{
    [BepInPlugin("com.accessibility.wwtbam.answerreveal", "WWTBAM Answer Reveal Accessibility", "1.0.0")]
    public class AnswerRevealAccessibility : BaseUnityPlugin
    {
        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_testIfRunning();

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_speakText([MarshalAs(UnmanagedType.LPWStr)] string text);

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_cancelSpeech();

        private bool nvdaAvailable = false;
        private bool correctAnswerAnnounced = false;
        private string lastGreenDetected = "";

        private const float CHECK_INTERVAL = 0.15f;
        private float timeSinceLastCheck = 0f;

        void Awake()
        {
            Logger.LogInfo("WWTBAM Answer Reveal Accessibility Plugin loaded!");

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

            CheckForGreenAnswer();
        }

        private void CheckForGreenAnswer()
        {
            try
            {
                // Check if Canvas_Question still active
                GameObject canvasQuestion = GameObject.Find("UI_Canvas_WithPostProcess/Resizer/SoloClassic/Canvas_Question");
                if (canvasQuestion == null || !canvasQuestion.activeInHierarchy)
                {
                    // Reset when question screen closes
                    if (correctAnswerAnnounced)
                    {
                        correctAnswerAnnounced = false;
                        lastGreenDetected = "";
                        Logger.LogInfo("Question screen closed - reset answer reveal state");
                    }
                    return;
                }

                // Check for Green answer
                string correctAnswer = GetCorrectAnswer();

                if (!string.IsNullOrEmpty(correctAnswer) && correctAnswer != lastGreenDetected)
                {
                    lastGreenDetected = correctAnswer;

                    string announcement = $"The correct answer was {correctAnswer}";
                    Speak(announcement);
                    correctAnswerAnnounced = true;
                    Logger.LogInfo($"Correct answer announced: {announcement}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking for green answer: {ex.Message}");
            }
        }

        private string GetCorrectAnswer()
        {
            try
            {
                string[] letters = { "A", "B", "C", "D" };

                foreach (string letter in letters)
                {
                    string path = $"UI_Canvas_WithPostProcess/Resizer/SoloClassic/Canvas_Question/{GetAnswerPath(letter)}/Green_{letter}";
                    GameObject greenObj = GameObject.Find(path);

                    if (greenObj == null) continue;

                    Image greenImg = greenObj.GetComponent<Image>();
                    if (greenImg == null) continue;

                    if (greenImg.enabled)
                    {
                        // Found green answer! Get answer text
                        GameObject answerObj = GameObject.Find($"UI_Canvas_WithPostProcess/Resizer/SoloClassic/Canvas_Question/Unscaled/Answer_Unscaled_{letter}");
                        Text answerText = answerObj?.GetComponent<Text>();

                        if (answerText != null && !string.IsNullOrEmpty(answerText.text))
                        {
                            return $"{letter}, {answerText.text}";
                        }

                        return letter; // Return letter only if can't get text
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error getting correct answer: {ex.Message}");
            }

            return "";
        }

        private string GetAnswerPath(string letter)
        {
            if (letter == "A" || letter == "B")
                return "AB_Holder/" + letter;
            else
                return "CD_Holder/" + letter;
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
            Logger.LogInfo("WWTBAM Answer Reveal Accessibility Plugin unloaded");
        }
    }
}