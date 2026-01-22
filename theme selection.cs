using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Image = UnityEngine.UI.Image;
using Text = UnityEngine.UI.Text;

namespace MillionaireAccessibility
{
    [BepInPlugin("com.accessibility.themeselection", "Theme Selection Accessibility", "1.0.0")]
    public class ThemeSelectionAccessibility : BaseUnityPlugin
    {
        public static ThemeSelectionAccessibility Instance;

        [DllImport("nvdaControllerClient64.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern int nvdaController_testIfRunning();

        [DllImport("nvdaControllerClient64.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern int nvdaController_speakText([MarshalAs(UnmanagedType.LPWStr)] string text);

        private bool nvdaReady = false;
        private Harmony harmony;
        public bool isScreenActive = false;

        private Dictionary<string, bool> highlightStates = new Dictionary<string, bool>();
        private float lastCheckTime = 0f;
        private const float CHECK_INTERVAL = 0.1f;
        private string lastAnnouncedTheme = "";

        void Awake()
        {
            Instance = this;
            Logger.LogInfo("Theme Selection Accessibility loaded!");

            try
            {
                nvdaReady = (nvdaController_testIfRunning() == 0);
                Logger.LogInfo(nvdaReady ? "[NVDA] ready" : "[NVDA] not running");
            }
            catch (Exception ex)
            {
                nvdaReady = false;
                Logger.LogWarning("[NVDA] init fail: " + ex.Message);
            }

            harmony = new Harmony("com.accessibility.themeselection");
            harmony.PatchAll();
        }

        void Update()
        {
            GameObject canvasPacks = GameObject.Find("Canvas/ResizedBase/Panel_GamePlay/Canvas_Packs");

            if (canvasPacks != null && canvasPacks.activeInHierarchy && !isScreenActive)
            {
                OnThemeScreenEntered(canvasPacks);
            }
            else if ((canvasPacks == null || !canvasPacks.activeInHierarchy) && isScreenActive)
            {
                OnThemeScreenExited();
            }

            if (!isScreenActive) return;

            if (Time.time - lastCheckTime > CHECK_INTERVAL)
            {
                lastCheckTime = Time.time;
                CheckForFocusChanges();
            }
        }

        private void CheckForFocusChanges()
        {
            GameObject canvasPacks = GameObject.Find("Canvas/ResizedBase/Panel_GamePlay/Canvas_Packs");
            if (canvasPacks == null) return;

            // Check Continue button
            Transform continueButton = canvasPacks.transform.Find("Packs/Continue");
            if (continueButton != null)
            {
                Transform orange = continueButton.Find("Orange");
                if (orange != null)
                {
                    Image orangeImg = orange.GetComponent<Image>();
                    if (orangeImg != null)
                    {
                        string key = "Continue";
                        bool currentEnabled = orangeImg.enabled;

                        if (!highlightStates.ContainsKey(key))
                        {
                            highlightStates[key] = currentEnabled;
                        }
                        else if (!highlightStates[key] && currentEnabled)
                        {
                            highlightStates[key] = currentEnabled;

                            if (lastAnnouncedTheme != "Next")
                            {
                                lastAnnouncedTheme = "Next";
                                Speak("Next button");
                                Logger.LogInfo("Announced: Next button");
                            }
                        }
                        else if (highlightStates[key] != currentEnabled)
                        {
                            highlightStates[key] = currentEnabled;
                        }
                    }
                }
            }

            // Check theme packs
            Transform content = canvasPacks.transform.Find("Packs/PackHolder/First_Move/Invectory/Viewport/Content");
            if (content == null) return;

            foreach (Transform child in content)
            {
                if (!child.name.StartsWith("Pack_")) continue;
                if (!child.gameObject.activeInHierarchy) continue;

                Transform highlight = child.Find("Highlight");
                if (highlight == null) continue;

                Image img = highlight.GetComponent<Image>();
                if (img == null) continue;

                string key = child.name;
                bool currentEnabled = img.enabled;

                if (!highlightStates.ContainsKey(key))
                {
                    highlightStates[key] = currentEnabled;
                    continue;
                }

                if (!highlightStates[key] && currentEnabled)
                {
                    highlightStates[key] = currentEnabled;

                    Text nameText = child.Find("Text_Pack")?.GetComponent<Text>();
                    string themeName = nameText != null ? nameText.text : child.name;

                    if (themeName != lastAnnouncedTheme)
                    {
                        lastAnnouncedTheme = themeName;

                        string announcement = GetThemeAnnouncement(child);
                        Speak(announcement);
                        Logger.LogInfo($"Announced: {announcement}");
                    }
                }
                else if (highlightStates[key] != currentEnabled)
                {
                    highlightStates[key] = currentEnabled;
                }
            }
        }

        private string GetThemeAnnouncement(Transform packTransform)
        {
            Text nameText = packTransform.Find("Text_Pack")?.GetComponent<Text>();
            string themeName = nameText != null ? nameText.text : packTransform.name;

            string announcement = themeName;

            Transform holder = packTransform.Find("Holder");
            if (holder != null)
            {
                Image selectedIcon = holder.Find("Selected")?.GetComponent<Image>();
                if (selectedIcon != null && selectedIcon.enabled)
                {
                    announcement += ", selected";
                }
                else
                {
                    announcement += ", not selected";
                }

                Image lockIcon = holder.Find("Lock")?.GetComponent<Image>();
                if (lockIcon != null && lockIcon.enabled)
                {
                    announcement += ", locked";
                }
            }

            Transform dlcStar = packTransform.Find("DLC STAR");
            if (dlcStar != null && dlcStar.gameObject.activeSelf)
            {
                announcement += ", premium";
            }

            return announcement;
        }

        public void OnThemeScreenEntered(GameObject canvasObject)
        {
            Logger.LogInfo("Theme selection screen detected!");
            isScreenActive = true;

            highlightStates.Clear();
            lastAnnouncedTheme = "";

            Transform content = canvasObject.transform.Find("Packs/PackHolder/First_Move/Invectory/Viewport/Content");
            int themeCount = 0;
            if (content != null)
            {
                foreach (Transform child in content)
                {
                    if (child.name.StartsWith("Pack_") && child.gameObject.activeInHierarchy)
                        themeCount++;
                }
            }

            string announcement = $"{themeCount} items";
            Speak(announcement);
            Logger.LogInfo($"Theme screen: {announcement}");

            // Force check first focused item
            CheckForFocusChanges();
        }

        public void OnThemeScreenExited()
        {
            Logger.LogInfo("Theme selection screen exited");
            isScreenActive = false;
            highlightStates.Clear();
            lastAnnouncedTheme = "";
        }

        public void Speak(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            if (!nvdaReady)
            {
                Logger.LogInfo($"[SPEAK] {text}");
                return;
            }

            try
            {
                nvdaController_speakText(text);
            }
            catch (Exception e)
            {
                Logger.LogError($"Speech error: {e.Message}");
                nvdaReady = false;
            }
        }

        void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }

    [HarmonyPatch]
    public class ThemeScreenDetector
    {
        [HarmonyPatch(typeof(GameObject), "SetActive")]
        [HarmonyPostfix]
        static void OnSetActive(GameObject __instance, bool value)
        {
            if (!value) return;

            if (__instance.name == "Canvas_Packs")
            {
                var plugin = ThemeSelectionAccessibility.Instance;
                if (plugin != null)
                {
                    plugin.OnThemeScreenEntered(__instance);
                }
            }
        }
    }
}