// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Martin Amareld">
//   Copyright(c) 2017 Martin Amareld. All rights reserved. 
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace EdNetApi.Generator
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;

    using EdNetApi.Generator.Journal;

    using Newtonsoft.Json;

    internal class Program
    {
        private static void Main()
        {
            const bool Reset = true; // Ignore cached items

            // Prevent accidental runs
            Debugger.Break();

            const string ManualEntriesFilename = "ManualEntries.txt";

            List<ManualEntry> manualEntries;
            if (File.Exists(ManualEntriesFilename))
            {
                manualEntries =
                    JsonConvert.DeserializeObject<List<ManualEntry>>(File.ReadAllText(ManualEntriesFilename));
            }
            else
            {
                if (!JournalManualManager.ParseManual(out manualEntries))
                {
                    return;
                }

                var json = JsonConvert.SerializeObject(manualEntries, Formatting.Indented);
                File.WriteAllText(ManualEntriesFilename, json);
            }

            manualEntries = manualEntries.GroupBy(me => me.Name).Select(g => g.First()).ToList();

            JournalEntryManager.RegenerateJournalEntryClasses(
                @"C:\Users\Martin\Saved Games\Frontier Developments\Elite Dangerous",
                manualEntries,
                Reset);
        }
    }
}