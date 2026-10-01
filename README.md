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
- **Forge of Potential lowers the level instead of destroying gear**: a failed upgrade drops the
  item one level. Only a level-1 item, which has no lower level, is destroyed. The success chance
  is unchanged.

## Building

Requires the .NET SDK and a Steam install of Valheim. BepInEx is taken from the game
folder or from the Thunderstore Mod Manager `Default` profile.

```
dotnet build DanMod/DanMod.csproj -c Release
```

The build does two things:

- Packages `Thunderstore\DanMod-<version>.zip` (manifest, icon, README and DLL).
- Updates the copy of `DanMod.dll` that Thunderstore Mod Manager installed in the `Default` profile
  (`BepInEx\plugins\<Author>-DanMod`). If the mod is disabled in the mod manager, it updates
  the disabled copy (`DanMod.dll.old`) instead. It never creates a copy of its own, because a
  copy the mod manager doesn't know about would load alongside the managed one and couldn't be
  toggled off.

The first time, install the mod through the mod manager so it shows up there and can be toggled on
and off:

1. Build once so the zip exists. The build warns that DanMod isn't installed yet.
2. In the mod manager, go to Settings → Import local mod, pick the zip and enter `Dan` as the Author.

After that, builds just update the installed copy. If the mod gets imported twice (e.g. under two
different Authors), the build warns about it; uninstall the extra copy in the mod manager.

### Releasing a new version

The version is set only by `<Version>` in `DanMod/DanMod.csproj`. The build puts it in the plugin
and in `Thunderstore/manifest.json`, and names the zip after it. To release:

1. Bump `<Version>`: the patch number for fixes, the minor number for new features.
2. Build, then commit the csproj and the updated `Thunderstore/manifest.json`.
3. Import the new zip in the mod manager, again with Author `Dan`. Until then the manager keeps
   showing the old version (builds only replace the DLL), and the build warns about it.

Useful overrides:

- `-p:GamePath="D:\path\to\Valheim"` if the game install is not found automatically
- `-p:ProfilePath="..."` to use a different mod manager profile
- `-p:PluginDeployPath="..."` to deploy into a specific folder instead
- `-p:SkipDeploy=true` to build without deploying
