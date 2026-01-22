using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Runtime.InteropServices;
using Text = UnityEngine.UI.Text;

namespace WWTBAM.Accessibility
{
    [BepInPlugin("com.accessibility.wwtbam.characterselection", "WWTBAM Character Selection Accessibility", "1.0.0")]
    public class CharacterSelectionAccessibility : BaseUnityPlugin
    {
        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_testIfRunning();

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_speakText([MarshalAs(UnmanagedType.LPWStr)] string text);

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_cancelSpeech();

        private bool isInCharacterScreen = false;
        private string lastAnnouncedCharacter = "";
        private bool nvdaAvailable = false;

        private const float CHECK_INTERVAL = 0.15f;
        private float timeSinceLastCheck = 0f;

        void Awake()
        {
            Logger.LogInfo("WWTBAM Character Selection Accessibility Plugin loaded!");

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

            bool currentlyInCharacterScreen = IsCharacterScreen();

            if (currentlyInCharacterScreen && !isInCharacterScreen)
            {
                OnEnterCharacterScreen();
                isInCharacterScreen = true;
            }
            else if (!currentlyInCharacterScreen && isInCharacterScreen)
            {
                OnExitCharacterScreen();
                isInCharacterScreen = false;
            }
            else if (currentlyInCharacterScreen)
            {
                MonitorCharacterChanges();
            }
        }

        private bool IsCharacterScreen()
        {
            try
            {
                GameObject canvas = GameObject.Find("Canvas/ResizedBase/Panel_GamePlay/Canvas_PlayerSelect");
                if (canvas == null || !canvas.activeInHierarchy)
                    return false;

                Transform playerSelect = canvas.transform.Find("PlayerSelect");
                if (playerSelect == null || !playerSelect.gameObject.activeInHierarchy)
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking character screen: {ex.Message}");
                return false;
            }
        }

        private string GetCurrentCharacterInfo()
        {
            try
            {
                string basePath = "Canvas/ResizedBase/Panel_GamePlay/Canvas_PlayerSelect/PlayerSelect/Candidate";

                // Get character name
                GameObject nameObj = GameObject.Find($"{basePath}/Candidate/Image/Player_Name");
                Text nameText = nameObj?.GetComponent<Text>();
                string name = nameText != null ? nameText.text : "";

                if (string.IsNullOrEmpty(name))
                    return "";

                // Get age
                GameObject ageObj = GameObject.Find($"{basePath}/Bkg_Age/Bkg_Answer/Text_Age");
                Text ageText = ageObj?.GetComponent<Text>();
                string age = ageText != null ? ageText.text : "";

                // Get profession
                GameObject jobObj = GameObject.Find($"{basePath}/Bkg_Profession/Bkg_Answer/Text_Job");
                Text jobText = jobObj?.GetComponent<Text>();
                string job = jobText != null ? jobText.text : "";

                // Get passion
                GameObject passionObj = GameObject.Find($"{basePath}/Bkg_Passion/Bkg_Answer/Text_Passion");
                Text passionText = passionObj?.GetComponent<Text>();
                string passion = passionText != null ? passionText.text : "";

                // Build announcement
                string announcement = name;
                if (!string.IsNullOrEmpty(age))
                    announcement += $", {age} years old";
                if (!string.IsNullOrEmpty(job))
                    announcement += $", {job}";
                if (!string.IsNullOrEmpty(passion))
                    announcement += $", passion: {passion}";

                return announcement;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error getting character info: {ex.Message}");
            }

            return string.Empty;
        }

        private void OnEnterCharacterScreen()
        {
            Logger.LogInfo("=== ENTERED Character Selection Screen ===");

            string characterInfo = GetCurrentCharacterInfo();

            if (!string.IsNullOrEmpty(characterInfo))
            {
                lastAnnouncedCharacter = characterInfo;
                Speak(characterInfo);
                Logger.LogInfo($"Character selection: {characterInfo}");
            }
            else
            {
                Logger.LogWarning("Could not detect current character");
            }
        }

        private void MonitorCharacterChanges()
        {
            try
            {
                string currentCharacter = GetCurrentCharacterInfo();

                if (!string.IsNullOrEmpty(currentCharacter) &&
                    currentCharacter != lastAnnouncedCharacter)
                {
                    Logger.LogInfo($"Character changed: {lastAnnouncedCharacter} -> {currentCharacter}");
                    Speak(currentCharacter);
                    lastAnnouncedCharacter = currentCharacter;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error monitoring character changes: {ex.Message}");
            }
        }

        private void OnExitCharacterScreen()
        {
            Logger.LogInfo("=== EXITED Character Selection Screen ===");
            lastAnnouncedCharacter = string.Empty;
            isInCharacterScreen = false;
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
            Logger.LogInfo("WWTBAM Character Selection Accessibility Plugin unloaded");
        }
    }
}