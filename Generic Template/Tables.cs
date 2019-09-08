using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HBS.Collections;
using BattleTech.Data;

namespace FullXotlTables
{
    public class WeightedContainer
    {
        private int GetDateIndex(DateTime currentDate, List<DateTime> dates)
        {
            int dateIndex = 0;

            while (dateIndex < dates.Count - 1 &&
                   dates[dateIndex + 1] < currentDate)
                dateIndex++;

            return dateIndex;
        }

        private float GetInterpolateFraction(DateTime currentDate, DateTime firstDate, DateTime secondDate)
        {
            TimeSpan fromEarlierDate = currentDate.Subtract(firstDate);
            TimeSpan fromLaterDate = secondDate.Subtract(currentDate);

            int daysFromEarlier = fromEarlierDate.Days;
            int daysFromLater = fromLaterDate.Days;

            if(daysFromEarlier + daysFromLater == 0)
            {
                return 0f;
            }
            else return Math.Max(daysFromEarlier / (daysFromEarlier + daysFromLater), 1.0f);
        }

        public Dictionary<string, int> ApplyAnySmoothing(Dictionary<string, int> listToSmooth)
        {
            Dictionary<string, int> smoothedList = new Dictionary<string, int>();

            if (Core.Settings.CommonMechFlattenFactor != 1.0f)
            {
                foreach (string key in listToSmooth.Keys)
                {
                    int smoothedValue = listToSmooth[key];

                    if(smoothedValue > Core.Settings.CommonMechFlattenFloor)
                    {
                        smoothedValue = Core.Settings.CommonMechFlattenFloor + (int)((smoothedValue - Core.Settings.CommonMechFlattenFloor) * Core.Settings.CommonMechFlattenFactor);
                    }

                    smoothedList.Add(key, smoothedValue);
                }

                return smoothedList;
            }
            return listToSmooth;
        }


        public Dictionary<string, int> InterpolateWeight(DateTime currentDate, Dictionary<string, List<WeightValue>> weightList, List<DateTime> dates)
        {
            int dateIndex = GetDateIndex(currentDate, dates);
            Dictionary<string, int> interpolatedList = new Dictionary<string, int>();

            if (dateIndex < dates.Count - 1)
            {
                float fraction = 0f;

                foreach (string key in weightList.Keys)
                {
                    if (weightList[key][dateIndex].Value == 0 &&
                        weightList[key][dateIndex + 1].HasStart)
                    {
                        if (currentDate > weightList[key][dateIndex + 1].StartDate)
                        {
                            if (weightList[key][dateIndex + 1].StartIsJumpValue)
                            {
                                interpolatedList.Add(key, weightList[key][dateIndex + 1].Value);
                            }
                            else
                            {
                                fraction = GetInterpolateFraction(currentDate, weightList[key][dateIndex + 1].StartDate, dates[dateIndex + 1]);

                                interpolatedList.Add(key, Math.Min(1, (int)(weightList[key][dateIndex + 1].Value * fraction)));
                            }
                        }
                    }
                    else if (weightList[key][dateIndex + 1].Value == 0 &&
                             weightList[key][dateIndex].HasStop)
                    {
                        if (currentDate < weightList[key][dateIndex].StopDate)
                        {
                            if (weightList[key][dateIndex].StopIsJumpValue)
                            {
                                interpolatedList.Add(key, weightList[key][dateIndex].Value);
                            }
                            else
                            {
                                fraction = GetInterpolateFraction(currentDate, dates[dateIndex], weightList[key][dateIndex].StopDate);

                                interpolatedList.Add(key, Math.Min(1, (int)(weightList[key][dateIndex].Value * (1.0f - fraction))));
                            }
                        }
                    }
                    else
                    {
                        if (weightList[key][dateIndex].StopIsJumpValue)
                        {
                            if (currentDate < weightList[key][dateIndex].StopDate)
                            {
                                interpolatedList.Add(key, weightList[key][dateIndex].Value);
                            }
                            else
                            {
                                interpolatedList.Add(key, weightList[key][dateIndex + 1].Value);
                            }
                        }
                        else
                        {
                            fraction = GetInterpolateFraction(currentDate, dates[dateIndex], dates[dateIndex + 1]);

                            interpolatedList.Add(key, Math.Min(1,
                                weightList[key][dateIndex].Value +
                                        (int)((weightList[key][dateIndex + 1].Value - weightList[key][dateIndex].Value) * fraction)));
                        }
                    }
                }
            }
            else
            {
                // There is only 1 date in this table, output values as they are
                foreach (string key in weightList.Keys)
                {
                    if (weightList[key][dateIndex].Value != 0)
                    {
                        interpolatedList.Add(key, weightList[key][dateIndex].Value);
                    }
                }
            }

            return interpolatedList;
        }
    }

    public class XotlTable
    {
        private Dictionary<string, FactionTable> factions = new Dictionary<string, FactionTable>(StringComparer.OrdinalIgnoreCase);
        private List<string> lastThreeMechs = new List<string>();

        public Dictionary<string, FactionTable> Factions { get => factions; set => factions = value; }

        public string RequestUnit(DateTime currentDate, TagSet includeTags, TagSet excludeTags, TagSet companyTags)
        {
            TagSet trimmedIncludes = includeTags;
            TagSet trimmedExcludes = excludeTags;

            string unit = "mechdef_griffin_GRF-1N";
            string faction = "General";

            foreach (string tag in excludeTags)
            {
                if (tag.Contains("unit_none_"))
                {
                    faction = tag.Substring(10, tag.Length - 10);
                    break;
                }
            }

            Logger.Log($"RequestUnit: Faction = {faction}");
            string tableToUse = faction;
            string finalTable = faction;

            if (!factions.Keys.Contains(faction))
            {
                Logger.Log($"RequestUnit: Switching to general table as we don't have a table for that faction");
                tableToUse = "General";
                finalTable = "General";
            }

            // Keep rolling first if we should be looking at a different collection, then after that salvage from there
            while (tableToUse != "" &&
                   factions.Keys.Contains(tableToUse))
            {
                while (tableToUse != "" &&
                       factions.Keys.Contains(tableToUse))
                {
                    finalTable = tableToUse;
                    tableToUse = factions[tableToUse].RollCollection(currentDate);
                    Logger.Log($"RequestUnit: Rolled New Collection = {tableToUse}");
                }

                tableToUse = factions[finalTable].RollSalvage(currentDate);
                Logger.Log($"RequestUnit: Rolled New Salvage = {tableToUse}");
            }

            Logger.Log($"RequestUnit: Final table to use = {finalTable}");

            // If we don't have a table for this faction use the general one
            if (!factions.Keys.Contains(finalTable))
            {
                Logger.Log($"RequestUnit: Pulling unit from general table as we don't have a table for where we ended up");
                finalTable = "General";
            }

            Dictionary<string, int> interpolateList = null;


            // Determine a weight class and grab a list.  If it can be several weight classes it will be done by excludes and randomly pick evenly based on a shuffle.
            if (includeTags.Contains("unit_light"))
            {
                interpolateList = factions[finalTable].InterpolateWeight(currentDate, factions[finalTable].Mechs.Lights);
            }
            else if (includeTags.Contains("unit_medium"))
            {
                interpolateList = factions[finalTable].InterpolateWeight(currentDate, factions[finalTable].Mechs.Mediums);
            }
            else if (includeTags.Contains("unit_heavy"))
            {
                interpolateList = factions[finalTable].InterpolateWeight(currentDate, factions[finalTable].Mechs.Heavies);
            }
            else if (includeTags.Contains("unit_assault"))
            {
                interpolateList = factions[finalTable].InterpolateWeight(currentDate, factions[finalTable].Mechs.Assaults);
            }
            else
            {
                string[] weights = { "unit_light", "unit_medium", "unit_heavy", "unit_assault" };
                List<string> possibleWeights = new List<string>(weights);

                if (excludeTags.Contains("unit_light"))
                {
                    possibleWeights.Remove("unit_light");
                }
                if (excludeTags.Contains("unit_medium"))
                {
                    possibleWeights.Remove("unit_medium");
                }
                if (excludeTags.Contains("unit_heavy"))
                {
                    possibleWeights.Remove("unit_heavy");
                }
                if (excludeTags.Contains("unit_assault"))
                {
                    possibleWeights.Remove("unit_assault");
                }

                possibleWeights.Shuffle();
                if (possibleWeights[0].Contains("unit_light"))
                {
                    interpolateList = factions[finalTable].InterpolateWeight(currentDate, factions[finalTable].Mechs.Lights);
                }
                else if (possibleWeights[0].Contains("unit_medium"))
                {
                    interpolateList = factions[finalTable].InterpolateWeight(currentDate, factions[finalTable].Mechs.Mediums);
                }
                else if (possibleWeights[0].Contains("unit_heavy"))
                {
                    interpolateList = factions[finalTable].InterpolateWeight(currentDate, factions[finalTable].Mechs.Heavies);
                }
                else if (possibleWeights[0].Contains("unit_assault"))
                {
                    interpolateList = factions[finalTable].InterpolateWeight(currentDate, factions[finalTable].Mechs.Assaults);
                }
                else
                {
                    Logger.Log("Error: Totally shouldn't have arrived here. Couldn't determine what weight to use, using Lights");
                    interpolateList = factions[finalTable].InterpolateWeight(currentDate, factions[finalTable].Mechs.Lights);
                }
            }

            int totalWeightOfList = 0;

            foreach (string key in interpolateList.Keys)
            {
                totalWeightOfList += interpolateList[key];
            }

            bool unitSatisfiesTags = false;
            int infiniteLoopBreaker = 0;
            
            // Keep rolling on the same table until the tags are fine for that unit
            // Has an infinite loop breaker just in case the lance units tags are too restrictive for the mechs available for the faction
            while (infiniteLoopBreaker < 10000 &&
                !unitSatisfiesTags)
            {
                infiniteLoopBreaker++;

                int finalRoll = UnityEngine.Random.Range(1, totalWeightOfList);

                foreach (string key in interpolateList.Keys)
                {
                    if (finalRoll <= interpolateList[key])
                    {
                        unit = key;
                        break;
                    }
                    finalRoll -= interpolateList[key];
                }

                unitSatisfiesTags = CheckUnitAgainstTags(unit, includeTags, excludeTags, currentDate, companyTags);

                if (Core.Settings.IgnoreRepetitionOnce)
                {
                    if (lastThreeMechs.Contains(unit))
                    {
                        unitSatisfiesTags = false;
                        lastThreeMechs.Remove(unit);
                    }
                    else
                    {
                        if (lastThreeMechs.Count >= 3)
                        {
                            lastThreeMechs.RemoveAt(0);
                        }
                        lastThreeMechs.Add(unit);
                    }
                }
            }

            if(infiniteLoopBreaker >= 10000)
            {
                Logger.Log("Error: Couldn't find a mech that matched the tags defined by the lance in 10,000 tries.  Lance file is too restrictive or faction table is too limited.");
                unit = "mechdef_griffin_GRF-1N";
            }

            return unit;
        }

        // Check everything, does unit exist in the mod, does it meet the tags requirements
        private bool CheckUnitAgainstTags(string unit, TagSet includes, TagSet excludes, DateTime currentDate, TagSet companyTags)
        {
            bool unitChecksOut = false;
            TagSet trimmedIncludes = new TagSet();
            TagSet trimmedExcludes = new TagSet();

            string[] aiRoles = { "unit_role_brawler", "unit_role_sniper", "unit_role_scout" };
            string[] lanceRoles = { "unit_lance_support", "unit_lance_assassin", "unit_lance_vanguard", "unit_lance_tank" };
            string[] unitTraits = { "unit_indirectFire", "unit_jumpOK", "unit_range_long", "unit_range_medium", "unit_range_short", "unit_armor_high", "unit_armor_low", "unit_speed_high", "unit_speed_low", "unit_hot" };

            if (Core.Settings.RespectUnitAIRole)
            {
                foreach (string thisAiRole in aiRoles)
                {
                    if (includes.Contains(thisAiRole))
                    {
                        trimmedIncludes.Add(thisAiRole);
                    }
                    else if (excludes.Contains(thisAiRole))
                    {
                        trimmedExcludes.Add(thisAiRole);
                    }
                }
            }

            if (Core.Settings.RespectUnitLanceRole)
            {
                foreach (string thisLanceRole in lanceRoles)
                {
                    if (includes.Contains(thisLanceRole))
                    {
                        trimmedIncludes.Add(thisLanceRole);
                    }
                    else if (excludes.Contains(thisLanceRole))
                    {
                        trimmedExcludes.Add(thisLanceRole);
                    }
                }
            }

            if (Core.Settings.RespectUnitTraits)
            {
                foreach (string thisUnitTrait in unitTraits)
                {
                    if (includes.Contains(thisUnitTrait))
                    {
                        trimmedIncludes.Add(thisUnitTrait);
                    }
                    else if (excludes.Contains(thisUnitTrait))
                    {
                        trimmedExcludes.Add(thisUnitTrait);
                    }
                }
            }

            List<UnitDef_MDD> list = MetadataDatabase.Instance.GetMatchingUnitDefs(trimmedIncludes, trimmedExcludes, true, currentDate, companyTags);

            if(list.Count > 0)
            {
                foreach(UnitDef_MDD unitDef in list)
                {
                    if(unit == unitDef.UnitDefID)
                    {
                        unitChecksOut = true;
                    }
                }
            }
            else
            {
                Logger.Log("Error: Total fail, the lance units tags can't possibly get us a unit in this mod, checking if unit exists ignoring lance tags.");
                // Clear the current unit for use because the tags are messed up anyway
                trimmedIncludes = new TagSet();
                trimmedExcludes = new TagSet();
                list = MetadataDatabase.Instance.GetMatchingUnitDefs(trimmedIncludes, trimmedExcludes, true, currentDate, companyTags);

                if (list.Count > 0)
                {
                    foreach (UnitDef_MDD unitDef in list)
                    {
                        if (unit == unitDef.UnitDefID)
                        {
                            unitChecksOut = true;
                        }
                    }
                }
                else
                {
                    Logger.Log("Error: Database failure, can't find any units.");
                }
            }

            return unitChecksOut;
        }
    }

    public class FactionTable : WeightedContainer
    {
        private List<DateTime> dates = new List<DateTime>();
        private Dictionary<string, List<WeightValue>> collection = new Dictionary<string, List<WeightValue>>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, List<WeightValue>> salvage = new Dictionary<string, List<WeightValue>>(StringComparer.OrdinalIgnoreCase);
        private MechTable mechs = new MechTable();

        public List<DateTime> Dates { get => dates; set => dates = value; }
        public Dictionary<string, List<WeightValue>> Collection { get => collection; set => collection = value; }
        public Dictionary<string, List<WeightValue>> Salvage { get => salvage; set => salvage = value; }
        public MechTable Mechs { get => mechs; set => mechs = value; }

        public string RollCollection(DateTime currentDate)
        {
            string chosenCollection = "";

            if (collection.Count > 0)
            {
                Dictionary<string, int> collectionList = InterpolateWeight(currentDate, collection, dates);

                int randomRoll = UnityEngine.Random.Range(1, 1000);

                foreach (string key in collectionList.Keys)
                {
                    if (randomRoll <= collectionList[key])
                    {
                        chosenCollection = key;
                        break;
                    }
                    else randomRoll -= collectionList[key];
                }
            }

            return chosenCollection;
        }

        public string RollSalvage(DateTime currentDate)
        {
            string chosenSalvage = "";

            if (salvage.Count > 0)
            {
                Dictionary<string, int> salvageList = InterpolateWeight(currentDate, salvage, dates);

                int randomRoll = UnityEngine.Random.Range(1, 1000);

                foreach (string key in salvageList.Keys)
                {
                    if (randomRoll <= salvageList[key])
                    {
                        chosenSalvage = key;
                        break;
                    }
                    else randomRoll -= salvageList[key];
                }
            }

            return chosenSalvage;
        }


        public Dictionary<string, int> InterpolateWeight(DateTime currentDate, Dictionary<string, List<WeightValue>> weightList)
        {
            return ApplyAnySmoothing(InterpolateWeight(currentDate, weightList, dates));
        }
    }

    public class MechTable
    {
        private Dictionary<string, List<WeightValue>> lights = new Dictionary<string, List<WeightValue>>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, List<WeightValue>> mediums = new Dictionary<string, List<WeightValue>>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, List<WeightValue>> heavies = new Dictionary<string, List<WeightValue>>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, List<WeightValue>> assaults = new Dictionary<string, List<WeightValue>>(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, List<WeightValue>> Lights { get => lights; set => lights = value; }
        public Dictionary<string, List<WeightValue>> Mediums { get => mediums; set => mediums = value; }
        public Dictionary<string, List<WeightValue>> Heavies { get => heavies; set => heavies = value; }
        public Dictionary<string, List<WeightValue>> Assaults { get => assaults; set => assaults = value; }
    }

    public class WeightValue
    {
        private int value = 0;
        private DateTime startDate = new DateTime(2000,1,1);
        private DateTime stopDate = new DateTime(2000, 1, 1);
        private bool startIsJumpValue = false;
        private bool stopIsJumpValue = false;
        private bool hasStart = false;
        private bool hasStop = false;

        public int Value { get => value; set => this.value = value; }
        public DateTime StartDate { get => startDate; set => startDate = value; }
        public DateTime StopDate { get => stopDate; set => stopDate = value; }
        public bool StartIsJumpValue { get => startIsJumpValue; set => startIsJumpValue = value; }
        public bool StopIsJumpValue { get => stopIsJumpValue; set => stopIsJumpValue = value; }
        public bool HasStart { get => hasStart; set => hasStart = value; }
        public bool HasStop { get => hasStop; set => hasStop = value; }
    }
}
