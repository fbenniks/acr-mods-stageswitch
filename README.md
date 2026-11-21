# AC Rally StageSwitch

**Set your force feedback once for each stage, then let StageSwitch load it every time you drive.**

AC Rally StageSwitch remembers the **FFB gain** you want on each stage in Assetto Corsa Rally. Keep one value for a
whole rally, fine-tune individual stages, or give a car its own gain on a particular stage. If you want to tune
graphics too, StageSwitch can load a different **render resolution scale** for every stage.

> **A hobby project.** StageSwitch is an unofficial tool for Assetto Corsa Rally fans. See [Disclaimer](#disclaimer).

![StageSwitch with Monte-Carlo expanded and different FFB gains for La Bollène-Vésubie and Sisteron](docs/images/overview.png)

---

## Why you'd want this

The same force feedback setting can feel different on a fast gravel stage, a tight tarmac stage, or in another car.
If you find yourself changing the gain in the game menu before each run, StageSwitch lets you save those values
once. When you load a stage, it applies the value you picked for it.

You can do the same with graphics. A lower render resolution scale can help on a demanding stage, and you can use a
higher one where your PC has room to spare.

## What you get

### A gain for the whole rally or one stage

StageSwitch finds the stages in your game files and checks for new ones when you start StageSwitch or the game.
Click a rally to open its stage list. Set the bold rally row to give all its stages the same starting value, then
adjust any stage you want. Use **−** and **+**, or click the number to type a gain from **0 to 200**. Turn off a stage's
switch to skip automatic changes there.

![Wales with Hafren North enabled and Hafren South switched off](docs/images/stage-gain.png)

### A gain for a particular car

Open **Settings** with the gear icon and turn on **Different gain per car**. Click **+ Car** on a stage and pick
the car you want to tune. In this example, the Subaru Impreza S3 uses **135** on Sisteron and cars without their
own setting use **150**. Click **×** beside a car to go back to the stage value.

![Sisteron at 150 for the stage, with a Subaru Impreza S3 override of 135](docs/images/car-gain.png)

### A different render resolution for each stage

Open Settings and turn on **Resolution scale per stage** (off by default) to show the **Res scale** column.
Set a value for the whole rally or for each stage. With TSR, FSR or XeSS you can enter a value from **33% to 200%**.
With DLSS the controls step through the game's presets, such as **58% Balanced**, **67% Quality** and **100% DLAA**.

![Monte-Carlo with DLSS at 58% on La Bollène-Vésubie and 67% on Sisteron](docs/images/resolution-scale.png)

If the game's own DLSS setting is **100% (DLAA)**, lower it to **67%** in the game and press **Apply** so
StageSwitch can switch between presets. You can still choose **100%** for a stage in StageSwitch.

![StageSwitch's DLAA warning with the two steps to enable per-stage DLSS changes](docs/images/dlaa-warning.png)

### A popup when your settings are applied

When a stage loads, a popup confirms the gain and shows the resolution scale if you enabled it. The popup lasts
**10 seconds** by default. It works in borderless or windowed mode, and you can turn it off with **Show popup in
game** in Settings. If you edit the active stage in StageSwitch, the change applies during that run.

![Example stage-load popup showing FFB gain 135 and 67% DLSS Quality on Sisteron](docs/images/stage-popup.png)

### Learn from changes you make in the game

With **Learn from in-game changes** on (the default), you can fine-tune FFB in the game's own menu during a run.
StageSwitch saves the new gain for that stage, or for the car you're driving on that stage if per-car gains are
on. It can also learn resolution changes when **Resolution scale per stage** is enabled. It watches for changes
while you're on a stage, so you can still adjust your general settings from the game's main menu.

![Example learning popup confirming FFB 145 was saved for the Subaru on Sisteron](docs/images/learned-popup.png)

### Save a recommendation from MozaHud

[MozaHud](https://github.com/fbenniks/acr-mods-moza-hud) is StageSwitch's companion app for MOZA wheelbases.
It measures clipping as you drive and suggests an FFB gain after the stage. Keep both apps open, select the run in
MozaHud and click **Apply in StageSwitch…**. You can use the gain for this car on this stage,
this car across the rally, the stage default, or all stage defaults in the rally.

StageSwitch saves your choice and uses it the next time you load the stage. You can see the new value in the
stage list. If you're already on the affected stage, it can apply the value during the run. Choosing a default
clears any overrides for the car from that run on the affected stages so it uses the new default.

![MozaHud's Apply in StageSwitch dialog showing the four places to use the recommended gain](docs/images/apply-in-stageswitch.png)

---

## Getting started

**You need** a 64-bit Windows PC and Assetto Corsa Rally. MozaHud is optional.

1. **Download** [ACRallyStageSwitch.exe](https://github.com/fbenniks/acr-mods-stageswitch/raw/refs/heads/main/ACRallyStageSwitch.exe)
   and put it in a folder of its own. It's a single file with no installer.
2. **Run StageSwitch.** It looks for your AC Rally install and loads the stage list. If the list is empty, open
   **Settings → Game folder → Change…** and pick the **Assetto Corsa Rally** install folder.
3. **Open a rally and set a gain** for the stage you want to drive. Then start the game and load that stage.
4. **Optional.** Open **Settings** to enable car-specific gains, per-stage resolution, **Start with Windows**, or
   **Start minimized to tray**.

![StageSwitch Settings showing the FFB, popup, Windows startup and folder controls](docs/images/settings.png)

Closing or minimizing the window keeps StageSwitch running in the Windows system tray. Double-click its icon to
bring it back, or right-click and choose **Exit** to quit.

![StageSwitch tray menu showing Open, Settings and Exit](docs/images/tray-menu.png)

## Good to know

- **Starting values.** Check the gains before your first run. On a fresh scan the app starts Wales at 200,
  Monte-Carlo at 160 and the other rallies at 100. Adjust them for your wheel and taste.
- **Stage switched off?** StageSwitch skips automatic changes there. If learning is on and you change a
  supported setting in the game's menu while driving that stage, StageSwitch saves it and turns the stage back on.
- **Where are my settings?** **Settings → Settings folder → Open** takes you to the local folder with
  **settings.json** and **log.txt**. Your stage settings stay on your PC.
- **After a game update.** If StageSwitch has trouble applying a setting, check the status in the main window
  or the log in the settings folder.

## How it works

For stage detection, settings storage, the MozaHud connection and build instructions, see
[How it works](docs/HOW-IT-WORKS.md).

## Disclaimer

AC Rally StageSwitch is an unofficial hobby project for sim-rally fans. It is independent of the makers and
publishers of Assetto Corsa Rally and MOZA Racing. It is provided as is, without warranty. Product names and
logos belong to their respective owners. The screenshots show example settings from the app.
