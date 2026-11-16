using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Splatform;
using UnityEngine;

namespace DanMod;

// Pins the things players build and then lose track of (portals, ships, carts, sap extractors, ...) on the map,
// each with its own build icon, shown and hidden with a hotkey. Pieces only exist as GameObjects near a player,
// so they are found among the world's ZDOs instead. In single player or when hosting that is everything in the
// world; a client of someone else's server only has the ZDOs the server sent it, i.e. what it has been near
// this session. Only pieces a player built count (Piece.IsPlacedByPlayer), so world objects that happen to
// share a component, like a wild beehive, are left out.
// The pins have m_save off, so they are never written to the map, shared at a cartography table, or
// clickable (the map's click and remove handling only looks at saved pins).
internal static class MapPins
{
	private const string Section = "Map pins";
	// Like ZDOMan.GetAllZDOsWithPrefabIterative: walk this many occupied sectors per frame.
	private const int SectorsPerFrame = 400;
	// Seconds between scans while the pins are shown, to pick up things that were built, removed or moved.
	private const float RescanInterval = 5f;

	// A kind of piece to pin: the prefabs with this component. A prefab that has several of the components
	// belongs to the first category that lists one.
	private sealed class Category(string key, Type component, string singular, string plural, bool pinByDefault,
		string description, bool labelWithTag = false)
	{
		public readonly Type Component = component;
		public readonly string Singular = singular;
		public readonly string Plural = plural;
		// Portals: the pin is labelled with the portal's tag.
		public readonly bool LabelWithTag = labelWithTag;
		public ConfigEntry<bool> Enabled = null!;

		public void Bind(ConfigFile config) => Enabled = config.Bind(Section, key, pinByDefault, description);
	}

	// Things that get lost are pinned by default; the base's crafting stations would mostly crowd the pins there.
	private static readonly Category[] s_categories =
	[
		new("Portals", typeof(TeleportWorld), "portal", "portals", true, "Pin portals, labelled with their tag.",
			labelWithTag: true),
		new("Ships", typeof(Ship), "ship", "ships", true, "Pin rafts, karves, longships and drakkars."),
		new("Carts", typeof(Vagon), "cart", "carts", true, "Pin carts."),
		new("SapExtractors", typeof(SapCollector), "sap extractor", "sap extractors", true,
			"Pin sap extractors (placed on the ancient roots in the Mistlands)."),
		new("Beehives", typeof(Beehive), "beehive", "beehives", false, "Pin beehives."),
		new("Fermenters", typeof(Fermenter), "fermenter", "fermenters", false, "Pin fermenters."),
		new("Smelters", typeof(Smelter), "smelter", "smelters", false,
			"Pin smelters, blast furnaces, charcoal kilns, windmills, spinning wheels, eitr refineries and the other " +
			"stations that turn one item into another over time."),
	];

	private static ConfigEntry<bool> s_show = null!;
	private static ConfigEntry<KeyboardShortcut> s_toggleKey = null!;

	// What each pinnable prefab (by hash) is and its build icon, from the current ZNetScene.
	private readonly struct PrefabInfo(Category category, Sprite? icon)
	{
		public readonly Category Category = category;
		public readonly Sprite? Icon = icon;
	}

	private static ZNetScene? s_scene;
	private static readonly Dictionary<int, PrefabInfo> s_prefabs = new();

	private static Minimap? s_minimap;
	private static readonly Dictionary<ZDOID, Minimap.PinData> s_pins = new();

	// What a scan found of one ZDO. The ZDOs themselves are pooled and reused once destroyed, so they aren't
	// kept between frames.
	private readonly struct Found(PrefabInfo prefab, Vector3 position, string label)
	{
		public readonly PrefabInfo Prefab = prefab;
		public readonly Vector3 Position = position;
		public readonly string Label = label;
	}

	// A scan in progress: the next sector to look at (-1 when none is running) and what it has found so far.
	private static int s_scanSector = -1;
	private static readonly Dictionary<ZDOID, Found> s_found = new();
	private static float s_nextScan;
	private static bool s_announce;

	public static void Init(ConfigFile config)
	{
		s_show = config.Bind(Section, "Show", false,
			"Show the pins. The toggle key switches this on and off in game.");
		s_toggleKey = config.Bind(Section, "ToggleKey", new KeyboardShortcut(KeyCode.F7),
			"Key that shows or hides the pins.");
		foreach (Category category in s_categories)
			category.Bind(config);
	}

	public static void Update()
	{
		// A new Minimap comes with every world load, and the old one took its pins with it.
		if (Minimap.instance != s_minimap)
		{
			s_minimap = Minimap.instance;
			s_pins.Clear();
			s_scanSector = -1;
			s_nextScan = 0f;
		}
		if (s_minimap == null || ZDOMan.instance == null || ZNetScene.instance == null)
			return;

		if (ToggleKeyPressed())
		{
			s_show.Value = !s_show.Value;
			s_announce = s_show.Value;
			s_nextScan = 0f;
			if (!s_show.Value)
				Message("Map pins hidden");
		}

		if (!s_show.Value)
		{
			s_scanSector = -1;
			ClearPins();
			return;
		}

		if (s_scanSector < 0)
		{
			if (Time.time < s_nextScan)
				return;
			LoadPrefabs();
			s_found.Clear();
			s_scanSector = 0;
		}
		if (Scan())
		{
			s_scanSector = -1;
			s_nextScan = Time.time + RescanInterval;
			UpdatePins();
		}
	}

	private static bool ToggleKeyPressed()
	{
		// The game reads keys through the new Input System, which BepInEx's KeyboardShortcut.IsDown can't see.
		KeyboardShortcut key = s_toggleKey.Value;
		if (key.MainKey == KeyCode.None || !ZInput.GetKeyDown(key.MainKey, logWarning: false)
			|| !key.Modifiers.All(modifier => ZInput.GetKey(modifier, logWarning: false)))
			return false;
		// Not while typing in the console, chat, a sign or a map pin's name, or with the menu open.
		return !(global::Console.IsVisible() || TextInput.IsVisible() || Minimap.InTextInput() || Menu.IsVisible()
			|| (Chat.instance != null && Chat.instance.HasFocus()));
	}

	private static void LoadPrefabs()
	{
		ZNetScene scene = ZNetScene.instance;
		if (scene == s_scene)
			return;
		s_scene = scene;
		s_prefabs.Clear();
		foreach (KeyValuePair<int, GameObject> prefab in scene.m_namedPrefabs)
		{
			if (prefab.Value == null)
				continue;
			Category? category = s_categories.FirstOrDefault(c => prefab.Value.GetComponent(c.Component) != null);
			if (category != null)
				s_prefabs.Add(prefab.Key, new PrefabInfo(category, prefab.Value.TryGetComponent(out Piece piece) ? piece.m_icon : null));
		}
		DanMod.Log.LogInfo("Map pin prefabs: " + string.Join("; ", s_categories.Select(category => category.Plural + " = " +
			string.Join(", ", s_prefabs.Where(prefab => prefab.Value.Category == category)
				.Select(prefab => scene.m_namedPrefabs[prefab.Key].name).OrderBy(name => name)))));
	}

	// Looks at the next slice of sectors; true once every sector has been seen.
	private static bool Scan()
	{
		List<ZDO>[] sectors = ZDOMan.instance.m_objectsBySector;
		int occupied = 0;
		for (; s_scanSector < sectors.Length && occupied < SectorsPerFrame; s_scanSector++)
		{
			List<ZDO> zdos = sectors[s_scanSector];
			if (zdos == null)
				continue;
			occupied++;
			foreach (ZDO zdo in zdos)
			{
				if (!s_prefabs.TryGetValue(zdo.GetPrefab(), out PrefabInfo prefab) || !prefab.Category.Enabled.Value
					|| zdo.GetLong(ZDOVars.s_creator) == 0L)
					continue;
				s_found[zdo.m_uid] = new Found(prefab, zdo.GetPosition(), prefab.Category.LabelWithTag ? Tag(zdo) : "");
			}
		}
		return s_scanSector >= sectors.Length;
	}

	// A portal's tag, filtered the way TeleportWorld.GetText shows it.
	private static string Tag(ZDO zdo)
	{
		string author = zdo.GetString(ZDOVars.s_tagauthor);
		return CensorShittyWords.FilterUGC(zdo.GetString(ZDOVars.s_tag), UGCType.Text,
			string.IsNullOrEmpty(author) ? PlatformUserID.None : new PlatformUserID(author));
	}

	private static void UpdatePins()
	{
		foreach (ZDOID gone in s_pins.Keys.Where(id => !s_found.ContainsKey(id)).ToList())
		{
			s_minimap!.RemovePin(s_pins[gone]);
			s_pins.Remove(gone);
		}
		foreach (KeyValuePair<ZDOID, Found> found in s_found)
		{
			// Loading the player's map data (Minimap.SetMapData) clears every pin, so a pin can be gone even though
			// its piece isn't. A pin's label is made with it, so a portal whose tag changed gets a new pin.
			if (s_pins.TryGetValue(found.Key, out Minimap.PinData existing))
			{
				if (s_minimap!.m_pins.Contains(existing) && existing.m_name == found.Value.Label)
				{
					existing.m_pos = found.Value.Position;  // ships and carts move
					continue;
				}
				s_minimap.RemovePin(existing);
			}
			Sprite? icon = found.Value.Prefab.Icon;
			// PinType.None has no sprite of its own (the map's location pins use it the same way).
			Minimap.PinData pin = s_minimap!.AddPin(found.Value.Position, icon != null ? Minimap.PinType.None : Minimap.PinType.Icon3,
				found.Value.Label, save: false, isChecked: false);
			if (icon != null)
				pin.m_icon = icon;
			s_pins[found.Key] = pin;
		}

		if (s_announce)
		{
			s_announce = false;
			IEnumerable<string> counts = s_categories
				.Select(category => (category, count: s_found.Values.Count(found => found.Prefab.Category == category)))
				.Where(c => c.count > 0)
				.Select(c => $"{c.count} {(c.count == 1 ? c.category.Singular : c.category.Plural)}");
			Message(s_found.Count == 0 ? "Nothing to pin on the map" : "Map pins: " + string.Join(", ", counts));
		}
	}

	private static void ClearPins()
	{
		foreach (Minimap.PinData pin in s_pins.Values)
			s_minimap!.RemovePin(pin);
		s_pins.Clear();
	}

	private static void Message(string text)
	{
		if (Player.m_localPlayer != null)
			Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, text);
	}
}
