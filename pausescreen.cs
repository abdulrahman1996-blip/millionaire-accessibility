using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Runtime.InteropServices;

[BepInPlugin("wwtbam.pause.screen", "WWTBAM Pause Screen Reader", "1.1.0")]
public class WWTBAMPauseScreen : BaseUnityPlugin
{
    static bool active = false;
    static bool announced = false;
    static int cursor = 0; // 0 = YES, 1 = NO

    static Text questionText;
    static Text yesText;
    static Text noText;

    void Awake()
    {
        new Harmony("wwtbam.pause.screen.harmony").PatchAll();
        Logger.LogInfo("PauseScreen Reader loaded");
    }

    void Update()
    {
        if (!active) return;

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            cursor = 0;
            Speak("Yes");
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            cursor = 1;
            Speak("No");
        }

        if (Input.GetKeyDown(KeyCode.Return))
        {
            Speak(cursor == 0 ? "Yes selected" : "No selected");
        }
    }

    [HarmonyPatch(typeof(UnityEngine.EventSystems.EventSystem), "Update")]
    class PauseProbe
    {
        static void Postfix()
        {
            var texts = Object.FindObjectsOfType<Text>(true);

            questionText = texts.FirstOrDefault(t =>
                t.pathContains("Canvas/Resizer/PopUp/Text"));

            yesText = texts.FirstOrDefault(t =>
                t.pathContains("Canvas/Resizer/PopUp/Image_Yes/Text"));

            noText = texts.FirstOrDefault(t =>
                t.pathContains("Canvas/Resizer/PopUp/Image_No/Text"));

            if (questionText && yesText && noText)
            {
                active = true;

                if (!announced)
                {
                    announced = true;
                    cursor = 0;
                    Speak("Pause menu. " + questionText.text + " Yes.");
                }
            }
            else
            {
                active = false;
                announced = false;
            }
        }
    }

    // ===== NVDA =====
    [DllImport("nvdaControllerClient64.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern int nvdaController_speakText(
        [MarshalAs(UnmanagedType.LPWStr)] string text);

    static void Speak(string text)
    {
        nvdaController_speakText(text);
    }
}

// ===== helper =====
static class TextExt
{
    public static bool pathContains(this Text t, string path)
    {
        return t && t.transform && t.transform.GetHierarchyPath().Contains(path);
    }

    public static string GetHierarchyPath(this Transform t)
    {
        string p = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            p = t.name + "/" + p;
        }
        return p;
    }
}
