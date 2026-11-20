# AC Rally StageSwitch

Sets Assetto Corsa Rally's in-game **FFB gain** (optionally per car) and **render resolution scale** automatically per stage,
and shows a popup over the game for 10 seconds when it changes them.

## Use

1. Run `ACRallyStageSwitch.exe` (it lives in the system tray; closing the window keeps it running — right-click the tray icon → Exit to quit).
2. Set a value (0–200) per stage. The bold rally row sets all stages of that rally at once.
   Untick a stage to leave the game's gain alone there.
   Optional: ⚙ → **Different gain per car** adds a “+ Car” button to each stage, so a car can get its
   own value there. Cars without their own value use the stage value. Off by default.
3. Start AC Rally and load a stage: the gain is set and a popup shows the new value.
   Changing the value of the stage you're on in the app applies it immediately.

## How it works

- **Stage list** – read from the game's asset index (`acr\Content\Paks\*.utoc`): pacenote tables `DT_Pacenote<Stage>…`
  and layouts `DL_<Stage><Variant>GameplayActors`. Circuits without a rally (the Livigno ice circuit) become their own
  location; rally stages only appear once they have pacenotes (unreleased layouts such as Col de la Charbonnière are skipped).
  Rescanned at app start and whenever the game starts, so stages added by game updates appear automatically.
- **Stage detection** – the game's shared memory (`Local\acpmf_static`, ACC layout) signals that a stage
  loaded and names the rally plus an area (e.g. `Wales Cwmbiga`, which is really Hafren North). The exact stage
  is then read from the save file, where the game writes the event you start (e.g. `WelesS3HafrenNorthFullForward`).
  If that fails, the rally's value is used.
- **Cars** – the car list comes from `acr\Content\LiveryTemplates` (one folder per car); the car you drive is
  reported by shared memory, and its name from the game replaces the folder name. Flags and car-make logos
  are simplified versions drawn in code (the game's own artwork is packed and not readable).
- **Changing the gain** – the game keeps its settings in memory in the same layout it writes to
  `%LOCALAPPDATA%\acr\Saved\SaveGames\PlayerDataSaveSlot.sav`. The app uses the bytes around the gain in that
  file as a fingerprint, finds the copies in the game's memory and writes the new value (150 = 1.5).
  The force changes immediately and the settings menu shows the new value.
  If a game update changes that layout, the popup will say the gain was **not** changed.
- **Resolution scale** (33–200 %, "—" = keep the game's own setting) – written live to the engine's
  `r.ScreenPercentage` console variable, found by its help text, plus the game's menu copies so the graphics menu
  shows the same value. Setting a stage back to "—" restores the value from the game's menu.
  The upscaler shown in the column title (TSR / DLSS / XeSS / FSR = 2 / 3 / 4 / 5) is read from the save file.
  With DLSS the game itself rounds menu values to DLSS presets (e.g. 63 % → 66.7 %); the app writes the exact value.

Settings and log: `%APPDATA%\AC Rally StageSwitch\` (`settings.json`, `log.txt`).
Settings from before the rename (`%APPDATA%\AcrFfbGain\`) are copied over on first start; the old folder is left as a backup.

## Build

```powershell
.\build.ps1
```

Uses the C# compiler that ships with Windows (.NET Framework 4.x); no SDK needed.
