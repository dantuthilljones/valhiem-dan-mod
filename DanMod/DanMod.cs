using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace DanMod;

[BepInPlugin(ModGUID, ModName, ModVersion)]
public class DanMod : BaseUnityPlugin
{
	private const string ModName = "DanMod";
	private const string ModVersion = ModInfo.Version;
	private const string ModGUID = "dantuthilljones.DanMod";

	internal static ManualLogSource Log = null!;

	private FileSystemWatcher? _configWatcher;

	public void Awake()
	{
		Log = Logger;
		MapPins.Init(Config);
		WatchConfig();
		new Harmony(ModGUID).PatchAll();
		Logger.LogInfo($"{ModName} {ModVersion} loaded");
	}

	public void Update() => MapPins.Update();

	// BepInEx reads the config file only at startup. Reload it when it changes, so settings edited while the game
	// runs (e.g. in the mod manager's config editor) apply straight away. The handlers run on the main thread.
	private void WatchConfig()
	{
		_configWatcher = new FileSystemWatcher(Path.GetDirectoryName(Config.ConfigFilePath), Path.GetFileName(Config.ConfigFilePath))
		{
			NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
			SynchronizingObject = ThreadingHelper.SynchronizingObject,
		};
		_configWatcher.Changed += ReloadConfig;
		_configWatcher.Created += ReloadConfig;
		_configWatcher.Renamed += ReloadConfig;
		_configWatcher.EnableRaisingEvents = true;
	}

	private void ReloadConfig(object sender, FileSystemEventArgs e)
	{
		if (!File.Exists(Config.ConfigFilePath))
			return;
		try
		{
			Config.Reload();
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"Could not reload {Config.ConfigFilePath}: {ex.Message}");
		}
	}
}
