using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace DanMod;

// At the Forge of Potential (a CraftingStation with m_upgrader), InventoryGui.DoCrafting rolls once: the upgrade
// succeeds when roll <= m_upgradeChance, otherwise the item is destroyed when m_breakChance >= 1 - roll and
// given back one level lower when it isn't. Swap the break chance the game reads for one that never destroys
// the item, except at level 1, where there is no lower level to drop to.
[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
internal static class NoUpgradeBreak
{
	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		var matcher = new CodeMatcher(instructions).MatchForward(true,
			new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(ItemDrop.ItemData.SharedData), nameof(ItemDrop.ItemData.SharedData.m_breakChance))));
		if (matcher.IsInvalid)
		{
			DanMod.Log.LogWarning("Could not find the break chance in InventoryGui.DoCrafting, failed upgrades will still destroy items");
			return matcher.InstructionEnumeration();
		}
		return matcher.Advance(1).Insert(
			new CodeInstruction(OpCodes.Ldarg_0),
			new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NoUpgradeBreak), nameof(BreakChance)))).InstructionEnumeration();
	}

	// The item breaks when this is at least 1 - roll, with roll in [0, 1]: -1 never breaks it, 1 always does.
	private static float BreakChance(float chance, InventoryGui gui) => gui.m_craftUpgradeItem switch
	{
		null => chance,
		{ m_quality: > 1 } => -1f,
		_ => 1f,
	};
}

// The game's translations have no text for the message the level-lowering branch shows ($msg_upgrader_failed),
// so it came out as "[msg_upgrader_failed]". Every language load (startup, or a language change, which clears
// the translations first) goes through SetupLanguage; add English text afterwards unless the game has its own.
[HarmonyPatch(typeof(Localization), nameof(Localization.SetupLanguage))]
internal static class UpgradeFailedMessage
{
	private const string Key = "msg_upgrader_failed";

	// $1 is the item, $2 its new level; worded like the game's "$1 refinement failed and broke on level $2".
	private static void Postfix(Localization __instance)
	{
		if (!__instance.m_translations.ContainsKey(Key))
			__instance.AddWord(Key, "$1 refinement failed and dropped to level $2");
	}
}
