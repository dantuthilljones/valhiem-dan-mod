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

	public void Awake()
	{
		Log = Logger;
		new Harmony(ModGUID).PatchAll();
		Logger.LogInfo($"{ModName} {ModVersion} loaded");
	}
}
