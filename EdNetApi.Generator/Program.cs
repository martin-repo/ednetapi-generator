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

    using EdNetApi.Common;
    using EdNetApi.Generator.Journal;

    using Newtonsoft.Json;

    internal class Program
    {
        private static void Main()
        {
            const bool Reset = true; // Ignore cached items

            // Prevent accidental runs
            Debugger.Break();

            const string GeneratedDescriptionsFilename = "GeneratedDescriptions.txt";

            Dictionary<string, string> generatedDescriptions;
            if (File.Exists(GeneratedDescriptionsFilename))
            {
                generatedDescriptions =
                    JsonConvert.DeserializeObject<Dictionary<string, string>>(
                        File.ReadAllText(GeneratedDescriptionsFilename));
            }
            else
            {
                if (!JournalManualManager.ParseManual(out List<ManualEntry> manualEntries))
                {
                    return;
                }

                generatedDescriptions = new Dictionary<string, string>();

                foreach (var manualEntry in manualEntries)
                {
                    var name = manualEntry.Name.ToPascalCase();
                    if (generatedDescriptions.ContainsKey(name))
                    {
                        continue;
                    }

                    generatedDescriptions.Add(name, manualEntry.Description);
                }

                var json = JsonConvert.SerializeObject(generatedDescriptions, Formatting.Indented);
                File.WriteAllText(GeneratedDescriptionsFilename, json);
            }

            JournalEntryManager.RegenerateJournalEntryClasses(
                @"C:\Users\Martin\Saved Games\Frontier Developments\Elite Dangerous",
                generatedDescriptions,
                reset: Reset);
        }
    }
}