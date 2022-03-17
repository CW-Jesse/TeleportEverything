using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ServerSync;
using UnityEngine;

namespace TeleportEverything
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    internal partial class Plugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.kpro.TeleportEverything";
        public const string PluginName = "TeleportEverything";
        public const string PluginVersion = "1.4.0";
        private static string ConfigFileName = PluginGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;

        private static ConfigEntry<bool>? _serverConfigLocked;

        // General
        public static ConfigEntry<bool> EnableMod;
        public static ConfigEntry<string> TeleportMode;
        public static ConfigEntry<string> MessageMode;

        // Transport Allies
        public static bool TransportAllies;
        public static ConfigEntry<bool> TransportBoar;
        public static ConfigEntry<bool> TransportWolves;
        public static ConfigEntry<bool> TransportLox;
        public static ConfigEntry<string> TransportMask;
        public static ConfigEntry<float> TransportRadius;
        public static ConfigEntry<float> TransportVerticalTolerance;
        public static ConfigEntry<float> SpawnForwardOffset;
        public static bool IncludeTamed;
        public static bool IncludeNamed;
        public static bool IncludeWild;
        public static bool IncludeFollow;
        public static bool ExcludeNamed;
        public static ConfigEntry<string> IncludeMode;

        //Teleport Self
        public static ConfigEntry<float> SearchRadius;
        public static ConfigEntry<float> MaximumDisplacement;
        public static List<Character> enemies;
        public static List<Character> allies;

        //Items
        public static ConfigEntry<bool> RemoveItemsRestriction;
        public static ConfigEntry<bool> TransportDragonEggs;
        public static ConfigEntry<bool> TransportOres;
        public static ConfigEntry<int> TransportFee;
        public static bool hasOre;

        private readonly Harmony harmony = new Harmony(PluginGUID);

        public static readonly ManualLogSource TeleportEverythingLogger =
            BepInEx.Logging.Logger.CreateLogSource(PluginName);

        private static readonly ConfigSync ConfigSync = new(PluginGUID) { DisplayName = PluginName, CurrentVersion = PluginVersion, MinimumRequiredVersion = PluginVersion };

        private void Awake()
        {
            harmony.PatchAll();
            CreateConfigValues();
            SetupWatcher();

            enemies = new List<Character>();
            allies = new List<Character>();

            ClearIncludeVars();
        }

        private void OnDestroy()
        {
            harmony.UnpatchSelf();
        }


        private void CreateConfigValues()
        {
            _serverConfigLocked = config("General", "Force Server Config", true, "Force Server Config");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            // General
            EnableMod = config("Mod", "Enable Mod", true, "Enable/Disable mod");
            MessageMode = config("Mod", "Message Mode", "No messages",
                new ConfigDescription("Ally Mode",
                    new AcceptableValueList<string>("No messages", "top left", "centered")),
                false);


            // Transport

            TransportBoar = config("Transport", "Transport Boar", false, "");
            TransportWolves = config("Transport", "Transport Wolves", false, "");
            TransportLox = config("Transport", "Transport Lox", false, "");
            TransportMask = config("Transport", "Transport Mask", "", "");

            IncludeMode = config("Transport", "Ally Mode", "No Allies",
                new ConfigDescription("Ally Mode",
                    new AcceptableValueList<string>("No Allies", "All tamed", "Only Follow",
                        "All tamed except Named", "Only Named")), false);

            TransportRadius = config("Transport", "Transport Radius", 10f, "");
            TransportVerticalTolerance =
                config("Transport", "Transport Vertical Tolerance", 2f, "");
            SpawnForwardOffset = config("Transport", "Spawn forward Tolerance", .5f, "");

            // Transport.Items
            RemoveItemsRestriction = config("Transport Items", "Remove Items Restriction", false, "Allows transporting all items.");
            TransportDragonEggs = config("Transport Items", "Transport Dragon Eggs", false, "");
            TransportOres = config("Transport Items", "Transport Ores", false, "Allows transporting ores, ingots and other restricted items.");
            TransportFee = config("Transport Items Config", "Transport fee", 10,
                new ConfigDescription("Transport fee in (%) ore",
                new AcceptableValueRange<int>(0, 100)));

            // Teleport Self
            SearchRadius = config("Teleport Self", "Search Radius", 10f, "");
            MaximumDisplacement = config("Teleport Self", "Max Enemy Displacement", 5f, "");
            TeleportMode = config("Teleport Self", "Teleport Mode", "Standard",
                new ConfigDescription("Teleport Mode",
                    new AcceptableValueList<string>("Standard", "Vikings Don't Run",
                        "Take Them With You")));
        }

        private static void ClearIncludeVars()
        {
            TransportAllies = false;
            IncludeTamed = false;
            IncludeNamed = false;
            IncludeWild = false;
            IncludeFollow = false;
            ExcludeNamed = false;
        }

        public static void SetIncludeMode()
        {
            ClearIncludeVars();

            if (!IncludeMode.Value.Contains("No Allies"))
            {
                TransportAllies = true;
            }

            if (IncludeMode.Value.Equals("All tamed"))
            {
                IncludeTamed = true;
                IncludeNamed = true;
                IncludeFollow = true;
            }

            if (IncludeMode.Value.Contains("Only Follow"))
            {
                IncludeFollow = true;
            }

            if (IncludeMode.Value.Contains("All tamed except Named"))
            {
                IncludeTamed = true;
                ExcludeNamed = true;
                IncludeFollow = true;
            }

            if (IncludeMode.Value.Contains("Only Named"))
            {
                IncludeNamed = true;
            }
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                TeleportEverythingLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                TeleportEverythingLogger.LogError($"There was an issue loading your {ConfigFileName}");
                TeleportEverythingLogger.LogError("Please check your config entries for spelling and format!");
            }
        }


        #region ConfigOptions

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true) => config(group, name, value, new ConfigDescription(description), synchronizedSetting);

        #endregion 
    }
}