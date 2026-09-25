using BepInEx;
using HarmonyLib;

namespace DanMod;

[BepInPlugin(ModGUID, ModName, ModVersion)]
public class DanMod : BaseUnityPlugin
{
	private const string ModName = "DanMod";
	private const string ModVersion = "0.1.0";
	private const string ModGUID = "dantuthilljones.DanMod";

	public void Awake()
	{
		new Harmony(ModGUID).PatchAll();
		Logger.LogInfo($"{ModName} {ModVersion} loaded");
	}
}
