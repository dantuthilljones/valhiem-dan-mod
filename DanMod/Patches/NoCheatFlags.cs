using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace DanMod;

// Valheim marks items (ItemData.m_cheated) and the player profile (PlayerProfile.m_usedCheats) as cheated.
// Every game method that stores to either field is rewritten to store false. The save loaders are among
// them, so flags saved before the mod was installed are cleared too.
[HarmonyPatch]
internal static class NoCheatFlags
{
	private const byte Stfld = 0x7D;

	private static readonly FieldInfo[] CheatFields =
	[
		AccessTools.Field(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.m_cheated)),
		AccessTools.Field(typeof(PlayerProfile), nameof(PlayerProfile.m_usedCheats)),
	];

	// Found by scanning the game's IL rather than listed by hand, so writers added in game updates are covered.
	private static readonly List<MethodBase> Writers = FindWriters();

	private static bool Prepare()
	{
		DanMod.Log.LogInfo($"Clearing cheat flags in {Writers.Count} game methods");
		return Writers.Count > 0;
	}

	private static IEnumerable<MethodBase> TargetMethods() => Writers;

	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		foreach (CodeInstruction instruction in instructions)
		{
			if (CheatFields.Any(instruction.StoresField))
			{
				// Drop whatever value the game was about to store and store false instead.
				yield return new CodeInstruction(OpCodes.Pop).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
				yield return new CodeInstruction(OpCodes.Ldc_I4_0);
			}
			yield return instruction;
		}
	}

	private static List<MethodBase> FindWriters()
	{
		int[] tokens = CheatFields.Select(field => field.MetadataToken).ToArray();
		var writers = new List<MethodBase>();
		foreach (Type type in AccessTools.GetTypesFromAssembly(typeof(ItemDrop).Assembly))
		{
			try
			{
				foreach (MethodInfo method in AccessTools.GetDeclaredMethods(type))
				{
					if (!method.ContainsGenericParameters && MayStore(method, tokens) && Stores(method))
						writers.Add(method);
				}
			}
			catch (Exception e)
			{
				DanMod.Log.LogDebug($"Skipped {type} while looking for cheat flag writers: {e.Message}");
			}
		}
		return writers;
	}

	// Cheap filter over the raw IL bytes; it can give false positives, which Stores then rules out.
	private static bool MayStore(MethodInfo method, int[] tokens)
	{
		byte[]? il = method.GetMethodBody()?.GetILAsByteArray();
		if (il == null)
			return false;
		for (int i = 0; i + 4 < il.Length; i++)
		{
			if (il[i] == Stfld && tokens.Contains(BitConverter.ToInt32(il, i + 1)))
				return true;
		}
		return false;
	}

	private static bool Stores(MethodInfo method) =>
		PatchProcessor.GetOriginalInstructions(method).Any(instruction => CheatFields.Any(instruction.StoresField));
}
