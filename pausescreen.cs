using System;
using System.Runtime.InteropServices;
using BepInEx;
using UnityEngine;

namespace MillionaireAccessibility
{
    [BepInPlugin("com.mike.millionaire.pause_v2", "Millionaire Pause Access V2", "2.0.0")]
    public class PauseAccessV2 : BaseUnityPlugin
    {
        // --- NVDA BRIDGE ---
        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode)]
        public static extern int nvdaController_speakText(string text);

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode)]
        public static extern int nvdaController_testIfRunning();

        // --- REFERENCES ---
        private GameObject _targetPauseCanvas; // Objek "Canvas_Pause"
        private GameObject _targetLeavePopup;  // Objek "PopUpLeave"

        // --- STATE TRACKING ---
        private bool _isPaused = false;
        private bool _isLeavePopupActive = false;
        private float _scanTimer = 0f;

        void Awake()
        {
            Logger.LogInfo("Plugin Millionaire Pause V2: Active & Searching...");
        }

        void Update()
        {
            // 1. Fasa Pencarian (Jika reference belum jumpa)
            if (_targetPauseCanvas == null)
            {
                _scanTimer += Time.deltaTime;
                if (_scanTimer > 2.0f) // Scan setiap 2 saat supaya tak berat
                {
                    AttemptToFindUI();
                    _scanTimer = 0f;
                }
                return;
            }

            // 2. Fasa Pengecekan Status (Real-time monitoring)
            CheckPauseStatus();
        }

        void AttemptToFindUI()
        {
            // Cari SEMUA objek Canvas yang aktif
            Canvas[] allCanvases = FindObjectsOfType<Canvas>();

            foreach (Canvas c in allCanvases)
            {
                // Taktik: Kita cari Canvas yang ada anak bernama "Resizer"
                // Ini membezakan Main Canvas dengan UI Shell/Loading lain
                Transform resizer = c.transform.Find("Resizer");

                if (resizer != null)
                {
                    // Jumpa laluan yang betul! Sekarang cari Canvas_Pause
                    Transform canvasPauseTr = resizer.Find("Canvas_Pause");
                    if (canvasPauseTr != null)
                    {
                        _targetPauseCanvas = canvasPauseTr.gameObject;
                        Logger.LogInfo("SUCCESS: Canvas_Pause found!");

                        // Drill down cari Popup Leave
                        // Path: PauseMenu -> PopupPause -> PopUpLeave
                        Transform pauseMenu = canvasPauseTr.Find("PauseMenu");
                        if (pauseMenu)
                        {
                            Transform popupPause = pauseMenu.Find("PopupPause");
                            if (popupPause)
                            {
                                Transform popupLeave = popupPause.Find("PopUpLeave");
                                if (popupLeave)
                                {
                                    _targetLeavePopup = popupLeave.gameObject;
                                    Logger.LogInfo("SUCCESS: PopUpLeave found!");
                                }
                            }
                        }
                    }
                    break; // Dah jumpa, berhenti loop
                }
            }
        }

        void CheckPauseStatus()
        {
            // Cek status aktif di hierarchy
            bool currentlyPaused = _targetPauseCanvas.activeInHierarchy;

            // LOGIKA PAUSE UTAMA
            if (currentlyPaused != _isPaused)
            {
                _isPaused = currentlyPaused;

                if (_isPaused)
                {
                    Speak("Game Paused");
                }
                else
                {
                    Speak("Game Resumed");
                    _isLeavePopupActive = false; // Reset flag popup bila resume
                }
            }

            // LOGIKA POPUP "LEAVE GAME" (Hanya cek bila sedang Pause)
            if (_isPaused && _targetLeavePopup != null)
            {
                bool currentlyLeaving = _targetLeavePopup.activeInHierarchy;

                if (currentlyLeaving != _isLeavePopupActive)
                {
                    _isLeavePopupActive = currentlyLeaving;

                    if (_isLeavePopupActive)
                    {
                        Speak("Return to the main menu? Yes or No.");
                    }
                    else
                    {
                        // Jika popup tutup tapi masih pause, bermakna user tekan "No"
                        Speak("Back to Pause Menu");
                    }
                }
            }
        }

        void Speak(string msg)
        {
            if (nvdaController_testIfRunning() == 0)
            {
                nvdaController_speakText(msg);
            }
        }
    }
}