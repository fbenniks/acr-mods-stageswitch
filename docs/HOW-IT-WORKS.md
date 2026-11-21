# How StageSwitch works

## Stages and cars

- **Stage list.** StageSwitch reads the game's asset index (`acr\Content\Paks\*.utoc`) for pacenote tables
  and layouts. Rally stages appear once their pacenotes ship. Circuits such as the Livigno ice circuit get their
  own location. The stage list is rescanned when StageSwitch starts and when it detects the game starting.
- **Stage detection.** The game's shared memory (`Local\acpmf_static`, ACC layout) reports the rally and area
  when a stage loads. StageSwitch uses the event name in the game's save to identify the exact stage. For
  example, telemetry can say `Wales Cwmbiga` while the selected event is Hafren North. If only the rally can be
  identified, StageSwitch uses the rally values.
- **Car list.** StageSwitch lists the folders in `acr\Content\LiveryTemplates` and uses telemetry to identify
  the current car. The in-game name replaces the folder name when it becomes available. The flags and car
  badges are simplified drawings rendered by the app.

## Applying and learning settings

- **FFB gain.** The app identifies the running game's settings using the layout of
  `%LOCALAPPDATA%\acr\Saved\SaveGames\PlayerDataSaveSlot.sav`. It updates the in-memory gain, represented
  as 1.5 for a gain of 150. If a game update changes the layout and the gain cannot be written, StageSwitch
  reports the failure in its status and in the popup when enabled.
- **Render resolution.** When enabled, StageSwitch sets the engine's `r.ScreenPercentage` and updates the
  game's menu copies. New stages start from the game's resolution scale or an existing rally value. The upscaler
  is read from the save file (TSR = 2, DLSS = 3, XeSS = 4, FSR = 5). With DLSS the value is snapped to a preset.
  For example, 67% in the app is written as the 66.7% DLSS Quality ratio.
- **Learning.** While on a known stage, the app watches for the game to update its save file and picks up
  supported changes made through the game's settings menu. FFB can be stored for the current car on the stage.
  Resolution is stored per stage when resolution scaling is enabled.

## MozaHud connection

[MozaHud](https://github.com/fbenniks/acr-mods-moza-hud) connects to the local named pipe
`ACRallyStageSwitch`. It sends one tab-separated request line and reads one reply line.

| Request | What it does |
|---|---|
| `HELLO` | Checks that StageSwitch is available. |
| `GET rally stage car` | Looks up the current rally, stage, and car values. |
| `SETGAIN scope rally stage car gain` | Writes the gain to the chosen scope. |

The available scopes are `car` (this car on this stage), `rallycar` (this car across the rally), `stage` (the
stage default), and `rally` (all stage defaults in the rally). The two car scopes turn on **Different gain per
car** if needed. The stage and rally scopes clear overrides for the car from the selected run on the affected
stages, so it uses the new default. Requests enable affected stages, save the result, refresh the list and
trigger a reapply if the game is on a stage.

## Settings and logs

StageSwitch stores `settings.json` and `log.txt` in `%APPDATA%\AC Rally StageSwitch`. On the first start after
the rename it copies older settings from `%APPDATA%\AcrFfbGain` and leaves the old folder as a backup.

## Build

```powershell
.\build.ps1
```

The script builds `ACRallyStageSwitch.exe` with the 64-bit C# compiler included with .NET Framework 4.x on
Windows. No separate SDK is needed.
