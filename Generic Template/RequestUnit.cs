using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;
using HBS.Collections;
using BattleTech;
using BattleTech.Data;
using BattleTech.Framework;

namespace FullXotlTables
{
    public static class RequestUnit
    {
        // Apply all to hit based quirks
        [HarmonyPatch(typeof(UnitSpawnPointOverride), "RequestUnit")]
        public static class UnitSpawnPointOverride_RequestPilot
        {
            static bool Prefix(UnitSpawnPointOverride __instance, LoadRequest request, MetadataDatabase mdd, string lanceName, string lanceDefId, string unitName, int unitIndex, DateTime? currentDate, TagSet companyTags)
            {
                if (__instance.IsUnitDefTagged &&
                    __instance.unitTagSet.Contains("unit_mech") &&
                    currentDate != null)
                {
                    __instance.selectedUnitDefId = Core.xotlTables.RequestUnit(currentDate.Value, __instance.unitTagSet, __instance.unitExcludedTagSet, companyTags);
                    __instance.selectedUnitType = UnitType.Mech;

                    request.AddBlindLoadRequest(BattleTechResourceType.MechDef, __instance.selectedUnitDefId, new bool?(false));
                    return false;
                }
                else return true;
            }
        }
    }
}
