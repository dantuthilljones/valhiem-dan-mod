using HarmonyLib;

namespace DanMod;

// s_bypassCheatChecks is the game's own switch for ignoring cheats. With it on, the game stops marking
// creatures, pieces and loot as cheated, and Achievements.CanGetAchievements always allows achievements,
// which it otherwise refuses for any modded game (Game.isModded), cheated item or cheat world modifier.
[HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.s_bypassCheatChecks), MethodType.Getter)]
internal static class BypassCheatChecks
{
	private static void Postfix(ref bool __result) => __result = true;
}

// Cheat console commands wait for "confirmcheats" until the profile is marked as having used cheats.
// NoCheatFlags stops that mark from ever being set, so the prompt would never go away. Run cheat commands
// as ordinary ones instead; whether they are allowed at all (devcommands) is checked before RunAction.
[HarmonyPatch(typeof(Terminal.ConsoleCommand), nameof(Terminal.ConsoleCommand.RunAction))]
internal static class NoCheatConfirmation
{
	private static void Prefix(Terminal.ConsoleCommand __instance, out bool __state)
	{
		__state = __instance.IsCheat;
		__instance.IsCheat = false;
	}

	private static void Finalizer(Terminal.ConsoleCommand __instance, bool __state) =>
		__instance.IsCheat = __state;
}
