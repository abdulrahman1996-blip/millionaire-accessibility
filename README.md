# Who Wants to Be a Millionaire – Accessibility Mod

Welcome to the **Who Wants to Be a Millionaire Accessibility Mod**. This mod is built using **BepInEx** with **Harmony** integration, adding comprehensive screen reader support to enhance accessibility for visually impaired players.

## Features

Currently, the mod supports the following features:

1. **Country selection** (language of the show)
2. **Main menu** navigation
3. **Shop menu**
4. **Options** menu
5. **Credits** (note: this menu is not fully readable yet)
6. **Game modes** selection
7. **Difficulties** selection
8. **Character selection**
9. **Questions and answers** – full reading and navigation support
10. **Lifelines** – Phone a Friend and Ask the Audience are fully supported
11. **Earning screen**
12. **Answer revealed** screen

## Installation

To install this plugin, you must first install **BepInEx**. Follow the steps below:

### Step 1: Download BepInEx
Download BepInEx from the official repository:  
[https://github.com/BepInEx/BepInEx/releases](https://github.com/BepInEx/BepInEx/releases)  
Choose the `.zip` file matching your operating system.

### Step 2: Extract BepInEx
Extract the downloaded archive to a convenient location.

### Step 3: Copy BepInEx to the Game Folder
1. Open the extracted BepInEx folder.
2. Copy **all files and folders** into the **game's root folder**.
   - For Steam users, the default installation path is:  
     `C:\Program Files (x86)\Steam\steamapps\common\Who Wants To Be A Millionaire`

### Step 4: Copy NVDA Controller DLL
Copy the file `nvdaControllerClient64.dll` into the same **game root folder**.

### Step 5: Run the Game Once
Launch the game. This will generate the necessary folder structure inside the `BepInEx` directory, including the `plugins` folder.

### Step 6: Install the Mod
1. Navigate to the `BepInEx` folder inside your game's root directory.
2. Open the `plugins` subfolder.
3. Copy and paste `millionaire_accessibility.dll` into the `plugins` folder.

### Step 7: Launch and Enjoy
Restart the game if it's still running. The accessibility features should now be active.

## Contribution

Contributions are welcome!  
The source code will be released on GitHub for developers who wish to help improve the mod, fix bugs, or add new features.

This mod has been tested with **NVDA** and is confirmed to be working as intended. However, some bugs may still exist. Feel free to report issues, suggest improvements, or submit pull requests.

---

**Note**: This mod is designed for accessibility purposes and is not affiliated with the original game developers.
