using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Runtime.InteropServices;
using Image = UnityEngine.UI.Image;
using Text = UnityEngine.UI.Text;

namespace MillionaireAccessibility
{
    [BepInPlugin("com.accessibility.shoppopup", "Shop Popup Accessibility", "1.0.0")]
    public class ShopPopupAccessibility : BaseUnityPlugin
    {
        public static ShopPopupAccessibility Instance;

        [DllImport("nvdaControllerClient64.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern int nvdaController_testIfRunning();

        [DllImport("nvdaControllerClient64.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern int nvdaController_speakText([MarshalAs(UnmanagedType.LPWStr)] string text);

        private bool nvdaReady = false;
        private Harmony harmony;

        private bool isPopupActive = false;
        private string lastPopupButton = "";
        
        private float lastCheckTime = 0f;
        private const float CHECK_INTERVAL = 0.1f;

        void Awake()
        {
            Instance = this;
            Logger.LogInfo("Shop Popup Accessibility loaded!");

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

            harmony = new Harmony("com.accessibility.shoppopup");
            harmony.PatchAll();
        }

        void Update()
        {
            if (Time.time - lastCheckTime > CHECK_INTERVAL)
            {
                lastCheckTime = Time.time;
                
                // ONLY check popup if we're actually in shop screen!
                if (IsInShopScreen())
                {
                    CheckPopup();
                }
                else
                {
                    // Not in shop, reset popup state
                    if (isPopupActive)
                    {
                        isPopupActive = false;
                        lastPopupButton = "";
                        Logger.LogInfo("Popup reset - not in shop screen");
                    }
                }
            }
        }

        private bool IsInShopScreen()
        {
            try
            {
                GameObject canvasShop = GameObject.Find("Canvas/ResizedBase/Canvas_Shop");
                return (canvasShop != null && canvasShop.activeInHierarchy);
            }
            catch
            {
                return false;
            }
        }

        private void CheckPopup()
        {
            GameObject canvasShop = GameObject.Find("Canvas/ResizedBase/Canvas_Shop");
            if (canvasShop == null)
            {
                if (isPopupActive)
                {
                    isPopupActive = false;
                    lastPopupButton = "";
                    Logger.LogInfo("Popup closed (no canvas)");
                }
                return;
            }

            Transform popup = canvasShop.transform.Find("Panel_Shop/FadePopup/PopUp");
            if (popup == null)
            {
                if (isPopupActive)
                {
                    isPopupActive = false;
                    lastPopupButton = "";
                    Logger.LogInfo("Popup closed (no popup)");
                }
                return;
            }

            // Check if popup REALLY active by checking Highlight enabled
            bool popupCurrentlyActive = false;
            
            Transform yesHighlight = popup.Find("Image_Yes/Highlight");
            if (yesHighlight != null)
            {
                Image yesImg = yesHighlight.GetComponent<Image>();
                if (yesImg != null && yesImg.enabled)
                {
                    popupCurrentlyActive = true;
                }
            }
            
            if (!popupCurrentlyActive)
            {
                Transform noHighlight = popup.Find("Image_No/Highlight");
                if (noHighlight != null)
                {
                    Image noImg = noHighlight.GetComponent<Image>();
                    if (noImg != null && noImg.enabled)
                    {
                        popupCurrentlyActive = true;
                    }
                }
            }

            // Popup state changed
            if (popupCurrentlyActive && !isPopupActive)
            {
                // Popup just opened
                isPopupActive = true;
                lastPopupButton = "";
                
                Logger.LogInfo("Popup opened!");
                
                Text priceText = GameObject.Find("Canvas/ResizedBase/Canvas_Shop/Panel_Shop/Price/Hold/Price/Price")?.GetComponent<Text>();
                string price = priceText != null ? priceText.text : "";
                
                string announcement = "Are you sure?";
                if (!string.IsNullOrEmpty(price))
                {
                    announcement += $" Price {price} neurons";
                }
                
                Speak(announcement);
                CheckPopupButtons(popup);
            }
            else if (!popupCurrentlyActive && isPopupActive)
            {
                // Popup just closed
                isPopupActive = false;
                lastPopupButton = "";
                Logger.LogInfo("Popup closed");
            }

            // If popup active, monitor buttons
            if (popupCurrentlyActive)
            {
                CheckPopupButtons(popup);
            }
        }

        private void CheckPopupButtons(Transform popup)
        {
            // Check YES button
            Transform yesButton = popup.Find("Image_Yes");
            if (yesButton != null)
            {
                Transform highlight = yesButton.Find("Highlight");
                if (highlight != null)
                {
                    Image img = highlight.GetComponent<Image>();
                    if (img != null && img.enabled)
                    {
                        if (lastPopupButton != "YES")
                        {
                            lastPopupButton = "YES";
                            Speak("YES");
                            Logger.LogInfo("Popup button: YES");
                        }
                        return;
                    }
                }
            }

            // Check NO button
            Transform noButton = popup.Find("Image_No");
            if (noButton != null)
            {
                Transform highlight = noButton.Find("Highlight");
                if (highlight != null)
                {
                    Image img = highlight.GetComponent<Image>();
                    if (img != null && img.enabled)
                    {
                        if (lastPopupButton != "NO")
                        {
                            lastPopupButton = "NO";
                            Speak("NO");
                            Logger.LogInfo("Popup button: NO");
                        }
                        return;
                    }
                }
            }
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
}
