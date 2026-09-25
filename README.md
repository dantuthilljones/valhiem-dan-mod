# DanMod

A Valheim mod (BepInEx plugin). TODO: describe what it does.

## Building

Requires the .NET SDK and a Steam install of Valheim. BepInEx is taken from the game
folder or from the Thunderstore Mod Manager `Default` profile.

```
dotnet build DanMod/DanMod.csproj -c Release
```

The build copies `DanMod.dll` into
`%APPDATA%\Thunderstore Mod Manager\DataFolder\Valheim\profiles\Default\BepInEx\plugins\DanMod`.

Useful overrides:

- `-p:GamePath="D:\path\to\Valheim"` if the game install is not found automatically
- `-p:ProfilePath="..."` to deploy into a different mod manager profile
- `-p:SkipDeploy=true` to build without deploying
