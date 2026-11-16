using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace DanMod;

// Pins every sap extractor (a piece with a SapCollector, placed on an ancient root) on the map, shown and
// hidden with a hotkey. Pieces only exist as GameObjects near a player, so the extractors are found among the
// world's ZDOs instead. In single player or when hosting that is every extractor in the world; a client of
// someone else's server only has the ZDOs the server sent it, i.e. those it has been near this session.
// The pins have m_save off, so they are never written to the map, shared at a cartography table, or
// clickable (the map's click and remove handling only looks at saved pins).
internal static class SapExtractorPins
{
	private const string Section = "Sap extractor map";
	// Like ZDOMan.GetAllZDOsWithPrefabIterative: walk this many occupied sectors per frame.
	private const int SectorsPerFrame = 400;
	// Seconds between scans while the pins are shown, to pick up extractors that were built or removed.
	private const float RescanInterval = 5f;

	private static ConfigEntry<bool> s_show = null!;
	private static ConfigEntry<KeyboardShortcut> s_toggleKey = null!;

	// Prefab hashes of the SapCollector pieces and the build icon to pin them with, from the current ZNetScene.
	private static ZNetScene? s_scene;
	private static readonly HashSet<int> s_prefabs = new();
	private static Sprite? s_icon;

	private static Minimap? s_minimap;
	private static readonly Dictionary<ZDOID, Minimap.PinData> s_pins = new();

	// A scan in progress: the next sector to look at (-1 when none is running) and the extractors seen so far.
	private static int s_scanSector = -1;
	private static readonly Dictionary<ZDOID, Vector3> s_found = new();
	private static float s_nextScan;
	private static bool s_announce;

	public static void Init(ConfigFile config)
	{
		s_show = config.Bind(Section, "Show", false,
			"Pin every sap extractor on the map. The toggle key switches this on and off in game.");
		s_toggleKey = config.Bind(Section, "ToggleKey", new KeyboardShortcut(KeyCode.F7),
			"Key that shows or hides the sap extractor pins.");
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
				Message("Sap extractors hidden from the map");
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
		if (ZNetScene.instance == s_scene)
			return;
		s_scene = ZNetScene.instance;
		s_prefabs.Clear();
		s_icon = null;
		foreach (KeyValuePair<int, GameObject> prefab in s_scene.m_namedPrefabs)
		{
			if (prefab.Value == null || prefab.Value.GetComponent<SapCollector>() == null)
				continue;
			s_prefabs.Add(prefab.Key);
			if (s_icon == null && prefab.Value.TryGetComponent(out Piece piece))
				s_icon = piece.m_icon;
		}
		if (s_prefabs.Count == 0)
			DanMod.Log.LogWarning("Found no sap extractor prefab, so none can be pinned on the map");
	}

	// Looks at the next slice of sectors; true once every sector has been seen. The ZDOs themselves are pooled
	// and reused once destroyed, so only their IDs and positions are kept between frames.
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
				if (s_prefabs.Contains(zdo.GetPrefab()))
					s_found[zdo.m_uid] = zdo.GetPosition();
			}
		}
		return s_scanSector >= sectors.Length;
	}

	private static void UpdatePins()
	{
		foreach (ZDOID gone in s_pins.Keys.Where(id => !s_found.ContainsKey(id)).ToList())
		{
			s_minimap!.RemovePin(s_pins[gone]);
			s_pins.Remove(gone);
		}
		foreach (KeyValuePair<ZDOID, Vector3> extractor in s_found)
		{
			// Loading the player's map data (Minimap.SetMapData) clears every pin, so a pin can be gone even
			// though its extractor isn't.
			if (s_pins.TryGetValue(extractor.Key, out Minimap.PinData existing) && s_minimap!.m_pins.Contains(existing))
				continue;
			// PinType.None has no sprite of its own (the map's location pins use it the same way).
			Minimap.PinData pin = s_minimap!.AddPin(extractor.Value, s_icon != null ? Minimap.PinType.None : Minimap.PinType.Icon3,
				"", save: false, isChecked: false);
			if (s_icon != null)
				pin.m_icon = s_icon;
			s_pins[extractor.Key] = pin;
		}

		if (s_announce)
		{
			s_announce = false;
			Message(s_pins.Count switch
			{
				0 => "No sap extractors found",
				1 => "Showing 1 sap extractor on the map",
				int count => $"Showing {count} sap extractors on the map",
			});
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
			Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, text, 0, s_icon);
	}
}
