# DanMod

A Valheim mod (BepInEx plugin) with a few quality-of-life changes:

- **No cheated items or profile**: items are never flagged as cheated (so they no longer
  carry the flag around, show it in their tooltip or spread it when stacked, crafted or built with),
  and the character is never marked as having used cheats. Flags already in a save are cleared
  when it loads.
- **Achievements stay enabled**: turns on the game's own cheat-check bypass, so achievements
  still progress with mods loaded or after using console commands, and cheat commands run without
  the `confirmcheats` prompt.
- **No food decay**: food gives its full health, stamina and eitr until its timer runs out.
- **Re-eating adds time**: eating a food you already have adds its full duration to the time
  left instead of resetting the timer (e.g. 5 min of bread left + a 25 min bread = 30 min).

## Building

Requires the .NET SDK and a Steam install of Valheim. BepInEx is taken from the game
folder or from the Thunderstore Mod Manager `Default` profile.

```
dotnet build DanMod/DanMod.csproj -c Release
```

The build does two things:

- Copies `DanMod.dll` into
  `%APPDATA%\Thunderstore Mod Manager\DataFolder\Valheim\profiles\Default\BepInEx\plugins\dantuthilljones-DanMod`.
  If the mod is disabled in the mod manager, it updates the disabled copy (`DanMod.dll.old`) instead.
- Packages `Thunderstore\DanMod-<version>.zip` (manifest, icon, README and DLL).

To make the mod show up in Thunderstore Mod Manager so it can be toggled on and off, import the package once:

1. Build once so the zip exists.
2. In the mod manager, go to Settings → Import local mod and pick the zip.
3. Set **Author** to `dantuthilljones` so it installs into the same folder the build deploys to.

After that, builds just update the installed copy.

Useful overrides:

- `-p:GamePath="D:\path\to\Valheim"` if the game install is not found automatically
- `-p:ProfilePath="..."` to deploy into a different mod manager profile
- `-p:SkipDeploy=true` to build without deploying
