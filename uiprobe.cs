using BepInEx;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TMPro;

namespace WWTBAM.Accessibility
{
    [BepInPlugin("com.accessibility.wwtbam.uiprobe", "WWTBAM UI Probe Enhanced", "2.1.0")]
    public class UIProbeEnhanced : BaseUnityPlugin
    {
        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_testIfRunning();

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_speakText([MarshalAs(UnmanagedType.LPWStr)] string text);

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_cancelSpeech();

        private bool nvdaAvailable = false;
        private bool enhancementEnabled = true;
        private bool filterEnabled = false;
        private bool includeInactive = false;
        private bool watchModeEnabled = false;

        private string outputPath = "";

        private const float WATCH_INTERVAL = 2.0f;
        private float timeSinceLastWatch = 0f;

        void Awake()
        {
            Logger.LogInfo("=== WWTBAM UI Probe Enhanced v2.1 ===");
            Logger.LogInfo("Hotkeys:");
            Logger.LogInfo("  F6: Toggle Watch Mode (auto-dump every 2 seconds)");
            Logger.LogInfo("  F7: Toggle Enhancement (MonoBehaviour detection)");
            Logger.LogInfo("  F8: Toggle Filter");
            Logger.LogInfo("  F9: Dump UI to file");
            Logger.LogInfo("  F10: Toggle Include Inactive");

            outputPath = Path.Combine(Paths.PluginPath, "UIProbe");
            Directory.CreateDirectory(outputPath);

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
                    Logger.LogWarning("NVDA not running");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize NVDA: {ex.Message}");
                nvdaAvailable = false;
            }

            LogCurrentState();
        }

        void Update()
        {
            // F6: Toggle Watch Mode
            if (Input.GetKeyDown(KeyCode.F6))
            {
                watchModeEnabled = !watchModeEnabled;
                string state = watchModeEnabled ? "ON" : "OFF";
                Logger.LogInfo($"Watch Mode: {state}");
                Speak($"Watch mode {state}");

                if (watchModeEnabled)
                {
                    timeSinceLastWatch = 0f;
                    Logger.LogInfo("Watch mode started - dumping UI every 2 seconds");
                }
            }

            // Watch mode auto-dump
            if (watchModeEnabled)
            {
                timeSinceLastWatch += Time.unscaledDeltaTime;
                if (timeSinceLastWatch >= WATCH_INTERVAL)
                {
                    timeSinceLastWatch = 0f;
                    DumpUI();
                }
            }

            // F7: Toggle Enhancement
            if (Input.GetKeyDown(KeyCode.F7))
            {
                enhancementEnabled = !enhancementEnabled;
                string state = enhancementEnabled ? "ON" : "OFF";
                Logger.LogInfo($"Enhancement (MonoBehaviour): {state}");
                Speak($"Enhancement {state}");
            }

            // F8: Toggle Filter
            if (Input.GetKeyDown(KeyCode.F8))
            {
                filterEnabled = !filterEnabled;
                string state = filterEnabled ? "ON" : "OFF";
                Logger.LogInfo($"Filter: {state}");
                Speak($"Filter {state}");
            }

            // F9: Dump UI
            if (Input.GetKeyDown(KeyCode.F9))
            {
                DumpUI();
            }

            // F10: Toggle Include Inactive
            if (Input.GetKeyDown(KeyCode.F10))
            {
                includeInactive = !includeInactive;
                string state = includeInactive ? "OFF" : "ON";
                Logger.LogInfo($"Inactive filter: {state}");
                Speak($"Inactive filter {state}");
            }
        }

        private void LogCurrentState()
        {
            Logger.LogInfo($"Current state:");
            Logger.LogInfo($"  Watch Mode: {(watchModeEnabled ? "ON" : "OFF")}");
            Logger.LogInfo($"  Enhancement: {(enhancementEnabled ? "ON" : "OFF")}");
            Logger.LogInfo($"  Filter: {(filterEnabled ? "ON" : "OFF")}");
            Logger.LogInfo($"  Include Inactive: {(includeInactive ? "YES" : "NO")}");
        }

        private void DumpUI()
        {
            try
            {
                Logger.LogInfo("=== Starting UI Dump ===");

                // Only speak if not in watch mode
                if (!watchModeEnabled)
                {
                    Speak("Dumping UI");
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string watchSuffix = watchModeEnabled ? "_watch" : "";
                string enhancementSuffix = enhancementEnabled ? "_enhanced" : "";
                string filterSuffix = filterEnabled ? "_filtered" : "";
                string inactiveSuffix = includeInactive ? "_with_inactive" : "";

                string filename = $"ui_probe{watchSuffix}{enhancementSuffix}{filterSuffix}{inactiveSuffix}_{timestamp}.txt";
                string fullPath = Path.Combine(outputPath, filename);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("=== WWTBAM UI PROBE DUMP ===");
                sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Watch Mode: {(watchModeEnabled ? "ON" : "OFF")}");
                sb.AppendLine($"Enhancement: {(enhancementEnabled ? "ON" : "OFF")}");
                sb.AppendLine($"Filter: {(filterEnabled ? "ON" : "OFF")}");
                sb.AppendLine($"Include Inactive: {(includeInactive ? "YES" : "NO")}");
                sb.AppendLine();

                // Find all Canvas components
                Canvas[] allCanvas = FindObjectsOfType<Canvas>(includeInactive);
                Logger.LogInfo($"Found {allCanvas.Length} Canvas objects");

                foreach (Canvas canvas in allCanvas)
                {
                    if (!includeInactive && !canvas.gameObject.activeInHierarchy)
                        continue;

                    DumpCanvas(canvas, sb);
                }

                File.WriteAllText(fullPath, sb.ToString());

                Logger.LogInfo($"=== UI Dump Complete ===");
                Logger.LogInfo($"Saved to: {fullPath}");
                Logger.LogInfo($"File size: {new FileInfo(fullPath).Length} bytes");

                // Only speak if not in watch mode
                if (!watchModeEnabled)
                {
                    Speak($"UI dumped to {filename}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error dumping UI: {ex.Message}");
                Logger.LogError($"Stack trace: {ex.StackTrace}");
                Speak("Dump failed");
            }
        }

        private void DumpCanvas(Canvas canvas, StringBuilder sb)
        {
            try
            {
                sb.AppendLine("================================================================================");
                sb.AppendLine($"CANVAS: {canvas.gameObject.name}");
                sb.AppendLine($"  Path: {GetGameObjectPath(canvas.gameObject)}");
                sb.AppendLine($"  ActiveInHierarchy: {canvas.gameObject.activeInHierarchy}");
                sb.AppendLine($"  Enabled: {canvas.enabled}");
                sb.AppendLine($"  RenderMode: {canvas.renderMode}");
                sb.AppendLine($"  SortingLayerID: {canvas.sortingLayerID} | SortingOrder: {canvas.sortingOrder}");
                sb.AppendLine();

                // Dump UI.Text components
                sb.AppendLine("  >>> UI.Text <<<");
                Text[] texts = canvas.GetComponentsInChildren<Text>(includeInactive);
                foreach (Text text in texts)
                {
                    if (!includeInactive && !text.gameObject.activeInHierarchy)
                        continue;

                    if (filterEnabled && ShouldFilterOut(text.gameObject))
                        continue;

                    DumpText(text, sb);
                }

                // Dump TextMeshProUGUI components
                sb.AppendLine("  >>> TextMeshProUGUI <<<");
                TextMeshProUGUI[] tmpTexts = canvas.GetComponentsInChildren<TextMeshProUGUI>(includeInactive);
                foreach (TextMeshProUGUI tmpText in tmpTexts)
                {
                    if (!includeInactive && !tmpText.gameObject.activeInHierarchy)
                        continue;

                    if (filterEnabled && ShouldFilterOut(tmpText.gameObject))
                        continue;

                    DumpTMPText(tmpText, sb);
                }

                // Dump Image components
                sb.AppendLine("  >>> UI.Image <<<");
                Image[] images = canvas.GetComponentsInChildren<Image>(includeInactive);
                foreach (Image image in images)
                {
                    if (!includeInactive && !image.gameObject.activeInHierarchy)
                        continue;

                    if (filterEnabled && ShouldFilterOut(image.gameObject))
                        continue;

                    DumpImage(image, sb);
                }

                // Dump Button components
                sb.AppendLine("  >>> UI.Button <<<");
                Button[] buttons = canvas.GetComponentsInChildren<Button>(includeInactive);
                foreach (Button button in buttons)
                {
                    if (!includeInactive && !button.gameObject.activeInHierarchy)
                        continue;

                    if (filterEnabled && ShouldFilterOut(button.gameObject))
                        continue;

                    DumpButton(button, sb);
                }

                // Enhancement: Dump custom MonoBehaviour components
                if (enhancementEnabled)
                {
                    sb.AppendLine("  >>> MonoBehaviour (Enhanced) <<<");
                    MonoBehaviour[] behaviours = canvas.GetComponentsInChildren<MonoBehaviour>(includeInactive);

                    HashSet<string> processedTypes = new HashSet<string>();

                    foreach (MonoBehaviour behaviour in behaviours)
                    {
                        if (behaviour == null)
                            continue;

                        if (!includeInactive && !behaviour.gameObject.activeInHierarchy)
                            continue;

                        if (filterEnabled && ShouldFilterOut(behaviour.gameObject))
                            continue;

                        string typeName = behaviour.GetType().Name;

                        // Skip Unity built-in components already dumped
                        if (typeName == "Text" || typeName == "Image" || typeName == "Button" ||
                            typeName == "TextMeshProUGUI" || typeName == "Canvas" ||
                            typeName == "CanvasRenderer" || typeName == "CanvasScaler" ||
                            typeName == "GraphicRaycaster" || typeName == "RectTransform" ||
                            typeName == "Shadow" || typeName == "Outline")
                            continue;

                        DumpMonoBehaviour(behaviour, sb, processedTypes);
                    }
                }

                sb.AppendLine();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error dumping canvas {canvas.gameObject.name}: {ex.Message}");
                sb.AppendLine($"  ERROR: {ex.Message}");
            }
        }

        private void DumpText(Text text, StringBuilder sb)
        {
            sb.AppendLine("  [-] ITEM");
            sb.AppendLine($"    Name: {text.gameObject.name}");
            sb.AppendLine($"    Type: UI.Text | Enabled={text.enabled} | Active={text.gameObject.activeInHierarchy}");
            sb.AppendLine($"    Path: {GetGameObjectPath(text.gameObject)}");
            sb.AppendLine($"    Text: {text.text}");
            sb.AppendLine($"    Color: {text.color} | FontSize: {text.fontSize} | Align: {text.alignment}");
            sb.AppendLine();
        }

        private void DumpTMPText(TextMeshProUGUI tmpText, StringBuilder sb)
        {
            sb.AppendLine("  [-] ITEM");
            sb.AppendLine($"    Name: {tmpText.gameObject.name}");
            sb.AppendLine($"    Type: TextMeshProUGUI | Enabled={tmpText.enabled} | Active={tmpText.gameObject.activeInHierarchy}");
            sb.AppendLine($"    Path: {GetGameObjectPath(tmpText.gameObject)}");
            sb.AppendLine($"    Text: {tmpText.text}");
            sb.AppendLine($"    Color: {tmpText.color} | FontSize: {tmpText.fontSize} | Align: {tmpText.alignment}");
            sb.AppendLine();
        }

        private void DumpImage(Image image, StringBuilder sb)
        {
            sb.AppendLine("  [-] ITEM");
            sb.AppendLine($"    Name: {image.gameObject.name}");
            sb.AppendLine($"    Type: Image | Enabled={image.enabled} | Active={image.gameObject.activeInHierarchy}");
            sb.AppendLine($"    Path: {GetGameObjectPath(image.gameObject)}");
            sb.AppendLine($"    Sprite: {(image.sprite != null ? image.sprite.name : "None")}");
            sb.AppendLine($"    Color: {image.color} | RaycastTarget: {image.raycastTarget}");
            sb.AppendLine($"    ImageType: {image.type} | PreserveAspect: {image.preserveAspect}");
            sb.AppendLine();
        }

        private void DumpButton(Button button, StringBuilder sb)
        {
            sb.AppendLine("  [-] ITEM");
            sb.AppendLine($"    Name: {button.gameObject.name}");
            sb.AppendLine($"    Type: Button | Enabled={button.enabled} | Active={button.gameObject.activeInHierarchy}");
            sb.AppendLine($"    Path: {GetGameObjectPath(button.gameObject)}");
            sb.AppendLine($"    Interactable: {button.interactable}");

            // Try to get button text
            Text buttonText = button.GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                sb.AppendLine($"    ButtonText: {buttonText.text}");
            }

            TextMeshProUGUI tmpButtonText = button.GetComponentInChildren<TextMeshProUGUI>();
            if (tmpButtonText != null)
            {
                sb.AppendLine($"    ButtonText (TMP): {tmpButtonText.text}");
            }

            sb.AppendLine();
        }

        private void DumpMonoBehaviour(MonoBehaviour behaviour, StringBuilder sb, HashSet<string> processedTypes)
        {
            string typeName = behaviour.GetType().Name;
            string fullTypeName = behaviour.GetType().FullName;

            sb.AppendLine("  [-] ITEM");
            sb.AppendLine($"    Name: {behaviour.gameObject.name}");
            sb.AppendLine($"    Type: {typeName} | Enabled={behaviour.enabled} | Active={behaviour.gameObject.activeInHierarchy}");
            sb.AppendLine($"    Path: {GetGameObjectPath(behaviour.gameObject)}");
            sb.AppendLine($"    FullType: {fullTypeName}");
            sb.AppendLine();
        }

        private bool ShouldFilterOut(GameObject go)
        {
            string path = GetGameObjectPath(go);

            // Filter out common Unity internal objects
            if (path.Contains("EventSystem") ||
                path.Contains("InputModule") ||
                path.Contains("UICamera"))
            {
                return true;
            }

            return false;
        }

        private string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform parent = obj.transform.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
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
            Logger.LogInfo("WWTBAM UI Probe Enhanced Plugin unloaded");
        }
    }
}