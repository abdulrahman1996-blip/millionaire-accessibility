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
    [BepInPlugin("com.accessibility.shopmenu", "Shop Menu Accessibility", "1.0.0")]
    public class ShopMenuAccessibility : BaseUnityPlugin
    {
        public static ShopMenuAccessibility Instance;

        [DllImport("nvdaControllerClient64.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern int nvdaController_testIfRunning();

        [DllImport("nvdaControllerClient64.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern int nvdaController_speakText([MarshalAs(UnmanagedType.LPWStr)] string text);

        private bool nvdaReady = false;
        private Harmony harmony;
        public bool isScreenActive = false;

        private Dictionary<string, bool> orangeStates = new Dictionary<string, bool>();
        private float lastCheckTime = 0f;
        private const float CHECK_INTERVAL = 0.1f;
        private string lastAnnouncedItem = "";

        void Awake()
        {
            Instance = this;
            Logger.LogInfo("Shop Menu Accessibility loaded!");

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

            harmony = new Harmony("com.accessibility.shopmenu");
            harmony.PatchAll();
        }

        void Update()
        {
            GameObject canvasShop = GameObject.Find("Canvas/ResizedBase/Canvas_Shop");

            if (canvasShop != null && canvasShop.activeInHierarchy && !isScreenActive)
            {
                OnShopMenuEntered(canvasShop);
            }
            else if ((canvasShop == null || !canvasShop.activeInHierarchy) && isScreenActive)
            {
                OnShopMenuExited();
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
            GameObject canvasShop = GameObject.Find("Canvas/ResizedBase/Canvas_Shop");
            if (canvasShop == null) return;

            Transform content = canvasShop.transform.Find("Panel_Shop/Menu/GameMode/Invectory/Viewport/Content");
            if (content == null) return;

            foreach (Transform child in content)
            {
                if (!IsShopItem(child.name)) continue;
                if (!child.gameObject.activeInHierarchy) continue;

                Transform orange = child.Find("Orange");
                if (orange == null) continue;

                Image img = orange.GetComponent<Image>();
                if (img == null) continue;

                string key = child.name;
                bool currentEnabled = img.enabled;

                if (!orangeStates.ContainsKey(key))
                {
                    orangeStates[key] = currentEnabled;

                    // If Orange already enabled during init, announce it!
                    if (currentEnabled)
                    {
                        Text nameText = child.Find("Text_Options")?.GetComponent<Text>();
                        string itemName = nameText != null ? nameText.text : child.name;

                        if (itemName != lastAnnouncedItem)
                        {
                            lastAnnouncedItem = itemName;

                            string announcement = GetItemAnnouncement(child);
                            Speak(announcement);
                            Logger.LogInfo($"Announced (init): {announcement}");
                        }
                    }

                    continue;
                }

                if (!orangeStates[key] && currentEnabled)
                {
                    orangeStates[key] = currentEnabled;

                    Text nameText = child.Find("Text_Options")?.GetComponent<Text>();
                    string itemName = nameText != null ? nameText.text : child.name;

                    if (itemName != lastAnnouncedItem)
                    {
                        lastAnnouncedItem = itemName;

                        string announcement = GetItemAnnouncement(child);
                        Speak(announcement);
                        Logger.LogInfo($"Announced: {announcement}");
                    }
                }
                else if (orangeStates[key] != currentEnabled)
                {
                    orangeStates[key] = currentEnabled;
                }
            }
        }

        private bool IsShopItem(string name)
        {
            return name.StartsWith("Pack_") ||
                   name == "Football" ||
                   name == "Disney" ||
                   name == "Star_Wars" ||
                   name == "Harry_Potter" ||
                   name == "Mangas" ||
                   name == "TV_Show" ||
                   name == "World_S_Food" ||
                   name == "Super_Hero" ||
                   name == "Bkg_BD";
        }

        private string GetItemAnnouncement(Transform itemTransform)
        {
            Text nameText = itemTransform.Find("Text_Options")?.GetComponent<Text>();
            string itemName = nameText != null ? nameText.text : itemTransform.name;

            string announcement = itemName;

            Transform dlcStar = itemTransform.Find("Select_1");
            if (dlcStar != null && dlcStar.gameObject.activeSelf)
            {
                announcement += ", DLC";
            }

            Image greyOut = itemTransform.Find("GreyOut")?.GetComponent<Image>();
            if (greyOut != null && !greyOut.enabled)
            {
                announcement += ", owned";
            }
            else
            {
                announcement += ", available for purchase";
            }

            return announcement;
        }

        public void OnShopMenuEntered(GameObject canvasObject)
        {
            Logger.LogInfo("=== SHOP MENU ENTERED ===");
            isScreenActive = true;

            orangeStates.Clear();
            lastAnnouncedItem = "";

            Transform content = canvasObject.transform.Find("Panel_Shop/Menu/GameMode/Invectory/Viewport/Content");
            int itemCount = 0;
            if (content != null)
            {
                foreach (Transform child in content)
                {
                    if (IsShopItem(child.name) && child.gameObject.activeInHierarchy)
                    {
                        itemCount++;
                    }
                }
            }

            string announcement = $"{itemCount} items";
            Speak(announcement);
            Logger.LogInfo($"Shop menu: {announcement}");

            CheckForFocusChanges();
        }

        public void OnShopMenuExited()
        {
            Logger.LogInfo("=== SHOP MENU EXITED ===");
            isScreenActive = false;
            orangeStates.Clear();
            lastAnnouncedItem = "";
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
    public class ShopScreenDetector
    {
        [HarmonyPatch(typeof(GameObject), "SetActive")]
        [HarmonyPostfix]
        static void OnSetActive(GameObject __instance, bool value)
        {
            if (!value) return;

            if (__instance.name == "Canvas_Shop")
            {
                var plugin = ShopMenuAccessibility.Instance;
                if (plugin != null)
                {
                    plugin.OnShopMenuEntered(__instance);
                }
            }
        }
    }
}