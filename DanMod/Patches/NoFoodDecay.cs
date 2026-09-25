using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace DanMod;

// Player.UpdateFood scales each food's health, stamina and eitr by Pow(timeLeft / burnTime, 0.3f), so food
// weakens as it runs out. With an exponent of 0 the scale is always 1: full strength until the timer ends.
[HarmonyPatch(typeof(Player), nameof(Player.UpdateFood))]
internal static class NoFoodDecay
{
	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		var matcher = new CodeMatcher(instructions).MatchForward(false,
			new CodeMatch(OpCodes.Ldc_R4, 0.3f),
			new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Mathf), nameof(Mathf.Pow))));
		if (matcher.IsInvalid)
		{
			DanMod.Log.LogWarning("Could not find the food decay curve in Player.UpdateFood, food will still decay");
			return matcher.InstructionEnumeration();
		}
		return matcher.SetOperandAndAdvance(0f).InstructionEnumeration();
	}
}
