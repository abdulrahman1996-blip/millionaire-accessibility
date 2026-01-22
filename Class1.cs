using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;
using Text = UnityEngine.UI.Text;

namespace WWTBAM.Accessibility
{
    [BepInPlugin("com.accessibility.wwtbam", "WWTBAM Accessibility", "1.0.0")]
    public class WWTBAMAccessibility : BaseUnityPlugin
    {
        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_testIfRunning();

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_speakText([MarshalAs(UnmanagedType.LPWStr)] string text);

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_cancelSpeech();

        private bool nvdaAvailable = false;
        private const float CHECK_INTERVAL = 0.15f;
        private float timeSinceLastCheck = 0f;

        private bool isInCountrySelection = false;
        private string lastSelectedCountry = "";
        private string confirmedCountry = "";

        private bool isInMainMenu = false;
        private string lastSelectedMenuItem = "";

        private readonly string[] supportedCountries = new string[] { "United States", "United Kingdom" };

        private readonly string[] countryFlags = new string[]
        {
            "Flag_Germany",
            "Flag_Fance",
            "Flag_Spain",
            "Flag_Italy",
            "Flag_UK",
            "Flag_US"
        };

        private readonly Dictionary<string, string> menuItems = new Dictionary<string, string>()
        {
            { "Bkg_Play", "PLAY" },
            { "Bkg_RePlay", "REPLAY" },
            { "Bkg_Option", "OPTIONS" },
            { "Bkg_Shop", "SHOP" },
            { "Bkg_DLC", "DLC" },
            { "Bkg_Credits", "CREDITS" },
            { "Bkg_Exit", "EXIT" }
        };

        void Awake()
        {
            Logger.LogInfo("WWTBAM Accessibility Plugin loaded!");

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

            bool inCountry = IsCountrySelectionScreen();
            bool inMenu = IsMainMenuScreen();

            if (inCountry && !isInCountrySelection)
            {
                OnEnterCountrySelection();
                isInCountrySelection = true;
            }
            else if (!inCountry && isInCountrySelection)
            {
                OnExitCountrySelection();
                isInCountrySelection = false;
            }
            else if (inCountry)
            {
                MonitorCountrySelectionChanges();
            }

            if (inMenu && !isInMainMenu)
            {
                OnEnterMainMenu();
                isInMainMenu = true;
            }
            else if (!inMenu && isInMainMenu)
            {
                OnExitMainMenu();
                isInMainMenu = false;
            }
            else if (inMenu)
            {
                MonitorMainMenuChanges();
            }
        }

        private bool IsCountrySelectionScreen()
        {
            try
            {
                GameObject canvasLangues = GameObject.Find("Canvas/ResizedBase/Canvas_Langues");
                if (canvasLangues == null || !canvasLangues.activeInHierarchy)
                    return false;

                Transform panelLangue = canvasLangues.transform.Find("Panel_Langue");
                if (panelLangue == null || !panelLangue.gameObject.activeInHierarchy)
                    return false;

                Transform panelCountry = panelLangue.Find("Panel_Country");
                return panelCountry != null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking country selection screen: {ex.Message}");
                return false;
            }
        }

        private string GetCurrentlySelectedCountry()
        {
            try
            {
                string basePath = "Canvas/ResizedBase/Canvas_Langues/Panel_Langue/Panel_Country";

                foreach (string flagName in countryFlags)
                {
                    GameObject orangeObj = GameObject.Find($"{basePath}/{flagName}/Orange");

                    if (orangeObj != null)
                    {
                        Image orangeImage = orangeObj.GetComponent<Image>();

                        if (orangeImage != null && orangeImage.enabled)
                        {
                            GameObject flagObj = GameObject.Find($"{basePath}/{flagName}");
                            if (flagObj != null)
                            {
                                Transform countryTextTransform = flagObj.transform.Find("Country");
                                if (countryTextTransform != null)
                                {
                                    Text countryText = countryTextTransform.GetComponent<Text>();
                                    if (countryText != null)
                                    {
                                        return countryText.text;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error getting selected country: {ex.Message}");
            }

            return string.Empty;
        }

        private int GetCountryCount()
        {
            try
            {
                string basePath = "Canvas/ResizedBase/Canvas_Langues/Panel_Langue/Panel_Country";
                int count = 0;

                foreach (string flagName in countryFlags)
                {
                    GameObject flagObj = GameObject.Find($"{basePath}/{flagName}");
                    if (flagObj != null && flagObj.activeInHierarchy)
                    {
                        count++;
                    }
                }

                return count;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error counting countries: {ex.Message}");
            }

            return 0;
        }

        private void OnEnterCountrySelection()
        {
            Logger.LogInfo("=== ENTERED Country Selection Screen ===");

            string currentCountry = GetCurrentlySelectedCountry();
            int countryCount = GetCountryCount();

            if (!string.IsNullOrEmpty(currentCountry))
            {
                lastSelectedCountry = currentCountry;
                confirmedCountry = currentCountry;

                // Announce with country count
                string announcement = $"{currentCountry}, {countryCount} items";
                Speak(announcement);
                Logger.LogInfo($"Country selection: {announcement}");
            }
            else
            {
                Logger.LogWarning("Could not detect current country selection");
            }
        }

        private void MonitorCountrySelectionChanges()
        {
            try
            {
                string currentCountry = GetCurrentlySelectedCountry();

                if (!string.IsNullOrEmpty(currentCountry))
                {
                    if (currentCountry != lastSelectedCountry)
                    {
                        Logger.LogInfo($"Country selection changed: {lastSelectedCountry} -> {currentCountry}");
                        Speak(currentCountry);
                        lastSelectedCountry = currentCountry;
                    }

                    confirmedCountry = currentCountry;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error monitoring country selection changes: {ex.Message}");
            }
        }

        private void OnExitCountrySelection()
        {
            Logger.LogInfo("=== EXITED Country Selection Screen ===");
            lastSelectedCountry = string.Empty;
            isInCountrySelection = false;
        }

        private bool IsMainMenuScreen()
        {
            try
            {
                GameObject canvasMain = GameObject.Find("Canvas/ResizedBase/Canvas_Main");
                if (canvasMain == null || !canvasMain.activeInHierarchy)
                    return false;

                Transform panelMain = canvasMain.transform.Find("Panel_Main");
                if (panelMain == null || !panelMain.gameObject.activeInHierarchy)
                    return false;

                Transform menu = panelMain.Find("Menu");
                return menu != null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking main menu screen: {ex.Message}");
                return false;
            }
        }

        private bool IsEnglishShow()
        {
            if (string.IsNullOrEmpty(confirmedCountry))
            {
                return false;
            }

            foreach (string country in supportedCountries)
            {
                if (confirmedCountry.Equals(country, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private int GetMenuItemCount()
        {
            try
            {
                string basePath = "Canvas/ResizedBase/Canvas_Main/Panel_Main/Menu";
                int count = 0;

                foreach (var menuItem in menuItems)
                {
                    GameObject itemObj = GameObject.Find($"{basePath}/{menuItem.Key}");
                    if (itemObj != null && itemObj.activeInHierarchy)
                    {
                        count++;
                    }
                }
                return count;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error counting menu items: {ex.Message}");
            }

            return 0;
        }

        private string GetCurrentlySelectedMenuItem()
        {
            try
            {
                string basePath = "Canvas/ResizedBase/Canvas_Main/Panel_Main/Menu";

                foreach (var menuItem in menuItems)
                {
                    string menuKey = menuItem.Key;
                    string menuLabel = menuItem.Value;

                    GameObject orangeObj = GameObject.Find($"{basePath}/{menuKey}/Orange");

                    if (orangeObj != null)
                    {
                        Image orangeImage = orangeObj.GetComponent<Image>();

                        if (orangeImage != null && orangeImage.enabled)
                        {
                            return menuLabel;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error getting selected menu item: {ex.Message}");
            }

            return string.Empty;
        }

        private void OnEnterMainMenu()
        {
            Logger.LogInfo("=== ENTERED Main Menu Screen ===");

            string currentMenuItem = GetCurrentlySelectedMenuItem();
            int itemCount = GetMenuItemCount();

            if (!string.IsNullOrEmpty(currentMenuItem))
            {
                lastSelectedMenuItem = currentMenuItem;

                // Announce with item count
                string announcement = $"{currentMenuItem}, {itemCount} items";
                Speak(announcement);
                Logger.LogInfo($"Main menu: {announcement}");
            }
            else
            {
                Logger.LogWarning("Could not detect current menu selection");
            }
        }

        private void MonitorMainMenuChanges()
        {
            try
            {
                string currentMenuItem = GetCurrentlySelectedMenuItem();

                if (!string.IsNullOrEmpty(currentMenuItem) &&
                    currentMenuItem != lastSelectedMenuItem)
                {
                    Logger.LogInfo($"Menu selection changed: {lastSelectedMenuItem} -> {currentMenuItem}");
                    Speak(currentMenuItem);
                    lastSelectedMenuItem = currentMenuItem;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error monitoring main menu changes: {ex.Message}");
            }
        }

        private void OnExitMainMenu()
        {
            Logger.LogInfo("=== EXITED Main Menu Screen ===");
            lastSelectedMenuItem = string.Empty;
            isInMainMenu = false;
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
            Logger.LogInfo("WWTBAM Accessibility Plugin unloaded");
        }
    }
}