using HarmonyLib;

namespace DanMod;

// Re-eating a food the player already has resets its timer to the food's full duration, losing the time
// that was left. Remember that time before eating and add it back afterwards.
[HarmonyPatch(typeof(Player), nameof(Player.EatFood))]
internal static class StackFoodTime
{
	private static void Prefix(Player __instance, ItemDrop.ItemData item, out float __state) =>
		__state = FindFood(__instance, item)?.m_time ?? 0f;

	private static void Postfix(Player __instance, ItemDrop.ItemData item, bool __result, float __state)
	{
		if (__result && __state > 0f && FindFood(__instance, item) is { } food)
			food.m_time += __state;
	}

	private static Player.Food? FindFood(Player player, ItemDrop.ItemData item) =>
		player.m_foods.Find(food => food.m_item.m_shared.m_name == item.m_shared.m_name);
}
