using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;

namespace FullXotlTables
{
    public static class GenerateTables
    {
        public static XotlTable GenerateFromFiles()
        {
            string currentDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName;
            string tablesFolder = Path.Combine(currentDirectory, "XotlTables");
            string[] filePaths = Directory.GetFiles(tablesFolder, "*.csv");

            XotlTable generatedTable = new XotlTable();

            foreach (string path in filePaths)
            {
                FactionTable factionTable = new FactionTable();
                string factionName = null;

                using (var reader = new StreamReader(path))
                {
                    if (reader != null)
                    {
                        var firstLine = reader.ReadLine();
                        var firstValues = firstLine.Split(',');

                        factionName = firstValues[0];

                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();
                            var values = line.Split(',');

                            if (values[0].StartsWith("Collection:"))
                            {
                                factionTable.Collection.Add(values[0].TrimStart('C', 'o', 'l', 'e', 'c', 't', 'i', 'o', 'n', ':'), ReadWeightLine(values));
                            }
                            else if (values[0].StartsWith("Salvage:"))
                            {
                                factionTable.Salvage.Add(values[0].TrimStart('S', 'a', 'l', 'v', 'a', 'g', 'e', ':'), ReadWeightLine(values));
                            }
                            else if (values[0].Contains("Dates"))
                            {
                                for (int element = 1; element < values.Length; element += 2)
                                {
                                    factionTable.Dates.Add(DateTime.Parse(values[element]));
                                }
                            }
                            else if (values[0].Contains("Lights"))
                            {
                                while (!reader.EndOfStream &&
                                    !values[0].Contains("Mediums"))
                                {
                                    line = reader.ReadLine();
                                    values = line.Split(',');
                                    if (values[0] != "" &&
                                        !values[0].Contains("Mediums"))
                                    {
                                        factionTable.Mechs.Lights.Add(values[0], ReadWeightLine(values));
                                    }
                                }
                                while (!reader.EndOfStream &&
                                    !values[0].Contains("Heavies"))
                                {
                                    line = reader.ReadLine();
                                    values = line.Split(',');
                                    if (values[0] != "" &&
                                        !values[0].Contains("Heavies"))
                                    {
                                        factionTable.Mechs.Mediums.Add(values[0], ReadWeightLine(values));
                                    }
                                }
                                while (!reader.EndOfStream &&
                                    !values[0].Contains("Assaults"))
                                {
                                    line = reader.ReadLine();
                                    values = line.Split(',');
                                    if (values[0] != "" &&
                                        !values[0].Contains("Assaults"))
                                    {
                                        factionTable.Mechs.Heavies.Add(values[0], ReadWeightLine(values));
                                    }
                                }
                                while (!reader.EndOfStream)
                                {
                                    line = reader.ReadLine();
                                    values = line.Split(',');
                                    if (values[0] != "")
                                    {
                                        factionTable.Mechs.Assaults.Add(values[0], ReadWeightLine(values));
                                    }
                                }
                            }
                        }
                    }
                }

                if (factionName != null)
                {
                    generatedTable.Factions.Add(factionName, factionTable);
                }
            }

            return generatedTable;
        }

        private static List<WeightValue> ReadWeightLine(string[] lineToRead)
        {
            /*
            Logger.Log("Reading Weight Line:");
            foreach(string stringFromLine in lineToRead)
            {
                Logger.Log("   " + stringFromLine);
            }
            */
            List<WeightValue> weightRead = new List<WeightValue>();
            WeightValue weight;

            for (int weightIndex = 1; weightIndex < lineToRead.Length; weightIndex += 2)
            {
                weight = new WeightValue();
                weight.Value = int.Parse(lineToRead[weightIndex]);

                // weightIndex 1 will be our first date so don't check for intermediate date before
                if (weightIndex != 1)
                {
                    if (lineToRead[weightIndex - 1] != "")
                    {
                        if (lineToRead[weightIndex - 1].StartsWith("Jump:"))
                        {
                            weight.StartIsJumpValue = true;
                        }
                        weight.StartDate = DateTime.Parse(lineToRead[weightIndex - 1].TrimStart('J', 'u', 'm', 'p', ':'));
                        weight.HasStart = true;
                    }
                }

                if (weightIndex != lineToRead.Length - 1)
                {
                    if (lineToRead[weightIndex + 1] != "")
                    {
                        if (lineToRead[weightIndex + 1].StartsWith("Jump:"))
                        {
                            weight.StopIsJumpValue = true;
                        }
                        weight.StopDate = DateTime.Parse(lineToRead[weightIndex + 1].TrimStart('J', 'u', 'm', 'p', ':'));
                        weight.HasStop = true;
                    }
                }

                weightRead.Add(weight);
            }

            return weightRead;
        }
    }
}
