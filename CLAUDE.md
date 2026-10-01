# DanMod

Valheim mod: a BepInEx 5 plugin that uses Harmony to patch the game. This repo started
as a blank template; the actual mod still has to be written.

## Layout

- `DanMod/DanMod.csproj`: SDK-style project that targets net48 but compiles against the
  game's own `mscorlib`/`netstandard` (Valheim 1.0 uses Unity 6 with a netstandard 2.1
  surface). Game assemblies are publicized at build time, so private members are accessible.
- `DanMod/DanMod.cs`: the plugin entry point (`BaseUnityPlugin`). `Awake` calls
  `Harmony.PatchAll()`, so any `[HarmonyPatch]` class in the assembly is applied.
- `Thunderstore/`: package metadata. A release zip needs `manifest.json`, `icon.png`
  (256x256), `README.md` and the built DLL; the build's `Package` target makes
  `Thunderstore/DanMod-<Version>.zip` (git-ignored).

## Build and test

- `dotnet build DanMod/DanMod.csproj -c Release` builds the DLL and updates the copy the
  Thunderstore Mod Manager installed in the `Default` profile: the `BepInEx/plugins/*-DanMod`
  folder that holds a `manifest.json`, created by importing the package zip (Settings → Import
  local mod, Author `Dan`). The manager disables a mod by renaming its files to `*.old`; the
  deploy then updates `DanMod.dll.old` so the mod stays disabled. The build never creates a
  plugin folder itself (a copy the manager doesn't know about loads twice and can't be toggled),
  and it warns when DanMod is not installed or is installed more than once.
- To test, launch the game through Thunderstore Mod Manager (modded), then read
  `%APPDATA%\Thunderstore Mod Manager\DataFolder\Valheim\profiles\Default\BepInEx\LogOutput.log`.
- Game code can be read by decompiling `valheim_Data/Managed/assembly_valheim.dll`
  (e.g. with ILSpy / `ilspycmd`).

## Conventions

- Tabs for indentation in C#.
- The version lives only in `<Version>` in the csproj. The build generates `ModInfo.Version`
  (used by `ModVersion` in `DanMod.cs`) and writes `version_number` into
  `Thunderstore/manifest.json`; commit that file along with the bump. Bump it for every release
  (patch for fixes, minor for new features). The mod manager keeps showing the imported version
  until the new zip is imported (Author `Dan`), and the build warns until then.
- If the mod needs another Unity/game assembly, add a `<Reference>` with `Private="false"`
  pointing into `$(_Managed)`.
- When renaming the mod, update the folder and csproj name, `AssemblyName`, `RootNamespace`,
  `ModName`, `ModGUID`, the manifest and the README.
