# Changelog

All notable changes to the WWTBAM Accessibility Project will be documented in this file.

## [1.0.0] - 2026-01-28

### 🚀 New Features

#### **Core System & Menus**
- **NVDA Integration:** Full integration with `nvdaControllerClient64.dll` for screen reader feedback across all supported screens.
- **Main Menu Accessibility:** Added voice navigation for the Main Menu ( automatic announcement of Country Selection.
- **Game Setup:**
  - **Game Mode:** Announces selected mode (-
  - **Difficulty:** Announces difficulty levels (
  - **Character Selection:** Reads character names as you navigate the roster.
  - **Theme Selection:** Added accessibility for the Question Pack/Theme selection screen.
- **Options & Settings:**
  - Full support for the Settings menu including Audio, Graphics, Game, and Controls tabs.
  - Announces option names and their current values (e.g., "Music Volume: 80").
- **Shop System:**
  - **Store Navigation:** Reads item names and details in the Shop grid.
  - **Purchase Popups:** Accessibility support for Shop confirmation dialogs (Yes/No prompts).

#### **Gameplay Accessibility**
- **Question & Answers:**
  - **Question Text:** Automatically reads the question text when it appears on screen.
  - **Answer Reading:** Announces answer choices (A, B, C, D) when highlighted.
  - **Status Indicators:** Detects and announces "Final Answer" state  and "Correct Answer" state (-
  - **Timer Accessibility:**
  - **Manual Announcement:** Press **T** to hear remaining time.
  - **Visibility Toggle:** Press **H** to toggle the visual timer (hides the halo effect).
  - **Auto-Announce:** Optional setting to announce time every 5 seconds.
- **Lifelines:**
  - **Menu Navigation:** Reads status of all lifelines (-
  - **Ask the Audience:** Specifically reads out the percentage votes for each answer.
  - **Phone-a-Friend:** Reads the selected friend's Name and Job title.
- **Game Flow:**
  - **Pause Menu:**  yes no navigation.
  - - **Walk Away:** Accessibility support for the "Walk Away" confirmation popup.
  - **Earnings Screen:** Announces the total money won on the earnings summary screen.

#### **Utilities & Tools**
- **Game Freeze (F5):** Added a dedicated freeze function to pause the game logic completely, allowing blind players time to think without timer pressure.
  
