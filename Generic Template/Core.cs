using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using Harmony;
using Newtonsoft.Json;
using static FullXotlTables.Logger;

namespace FullXotlTables
{
    public static class Core
    {
        public static XotlTable xotlTables = null;

        #region Init

        public static void Init(string modDir, string settings)
        {
            var harmony = HarmonyInstance.Create("BattleTech.Haree.FullXotlTables");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            // read settings
            try
            {
                Settings = JsonConvert.DeserializeObject<ModSettings>(settings);
                Settings.modDirectory = modDir;
            }
            catch (Exception)
            {
                Settings = new ModSettings();
            }

            // blank the logfile
            Clear();

            try
            {
                xotlTables = GenerateTables.GenerateFromFiles();
            }
            catch (Exception e)
            {
                Logger.Log("Failed to generate Xotl Tables");
                Logger.Error(e);
            }
            // PrintObjectFields(Settings, "Settings");

            foreach (KeyValuePair<string, FactionTable> kvp in xotlTables.Factions)
            {
                Logger.Log("Faction: " + kvp.Key);
                Logger.Log(" Number of Lights: " + xotlTables.Factions[kvp.Key].Mechs.Lights.Count.ToString());
                Logger.Log(" Number of Mediums: " + xotlTables.Factions[kvp.Key].Mechs.Mediums.Count.ToString());
                Logger.Log(" Number of Heavies: " + xotlTables.Factions[kvp.Key].Mechs.Heavies.Count.ToString());
                Logger.Log(" Number of Assaults: " + xotlTables.Factions[kvp.Key].Mechs.Assaults.Count.ToString());
            }
        }

        // logs out all the settings and their values at runtime
        internal static void PrintObjectFields(object obj, string name)
        {
            LogDebug($"[START {name}]");

            var settingsFields = typeof(ModSettings)
                .GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            foreach (var field in settingsFields)
            {
                if (field.GetValue(obj) is IEnumerable &&
                    !(field.GetValue(obj) is string))
                {
                    LogDebug(field.Name);
                    foreach (var item in (IEnumerable)field.GetValue(obj))
                    {
                        LogDebug("\t" + item);
                    }
                }
                else
                {
                    LogDebug($"{field.Name,-30}: {field.GetValue(obj)}");
                }
            }

            LogDebug($"[END {name}]");
        }

        #endregion

        internal static ModSettings Settings;
    }
    
}

