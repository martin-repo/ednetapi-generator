// --------------------------------------------------------------------------------------------------------------------
// <copyright file="JournalManualManager.cs" company="Martin Amareld">
//   Copyright(c) 2017 Martin Amareld. All rights reserved. 
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace EdNetApi.Generator.Journal
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    using EdNetApi.Common;

    using Microsoft.Office.Interop.Word;

    internal static class JournalManualManager
    {
        public static bool ParseManual(out List<ManualEntry> manualEntries)
        {
            var journalDocPath = GetJournalDocPath();

            Application word = null;
            Document document = null;

            try
            {
                word = new Application();
                document = word.Documents.Open(journalDocPath, ReadOnly: true);

                var index = 0;

                if (!SkipToOutlining(document, ref index, 1, text: "Startup"))
                {
                    manualEntries = null;
                    return false;
                }

                manualEntries = ParseManualEntries(document, ref index);
                return true;
            }
            catch
            {
                manualEntries = null;
                return false;
            }
            finally
            {
                document?.Close();
                word?.Quit();
            }
        }

        private static string GetJournalDocPath([CallerFilePath] string sourceFilePath = "")
        {
            var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            var solutionFolderPath = sourceFilePath.Substring(
                0,
                sourceFilePath.IndexOf(assemblyName, StringComparison.InvariantCulture));
            var journalFolderPath = Path.Combine(solutionFolderPath, assemblyName, "Resources", "JournalManual", "v13");
            var journalFilePath = Path.Combine(journalFolderPath, "Journal_Manual_v13.doc");
            return journalFilePath;
        }

        private static List<ManualEntry> ParseManualEntries(Document document, ref int index)
        {
            var manualEntries = new List<ManualEntry>();

            string currentCategory = null;

            while (index < document.Paragraphs.Count)
            {
                var range = document.Paragraphs[index].Range;
                if (IsOutlining(range, 1))
                {
                    Console.WriteLine($"{range.ListFormat.ListString} {range.Text.Trim()}");

                    currentCategory = range.Text.Trim();
                    if (!SkipToOutlining(document, ref index))
                    {
                        break;
                    }

                    if (currentCategory == "Appendix")
                    {
                        break;
                    }
                }

                Console.WriteLine($"{document.Paragraphs[index].Range.ListFormat.ListString} {range.Text.Trim()}");

                var manualEntry = ParseManualEntry(document, ref index, currentCategory);
                manualEntries.Add(manualEntry);
            }

            return manualEntries;
        }

        private static ManualEntry ParseManualEntry(Document document, ref int index, string currentCategory)
        {
            var entryName = document.Paragraphs[index].Range.Text.Trim().ToPascalCase();
            var description = document.Paragraphs[++index].Range.Text.Trim();
            if (description.StartsWith("When written:", StringComparison.InvariantCultureIgnoreCase))
            {
                description = description.Substring(13).Trim();
                if (!string.IsNullOrWhiteSpace(description))
                {
                    description = description.First().ToString().ToLowerInvariant() + description.Substring(1);
                    var spaceIndex = description.IndexOf(" ", StringComparison.Ordinal);
                    var addWhen = true;
                    if (spaceIndex > 0)
                    {
                        switch (description.Substring(0, spaceIndex))
                        {
                            case "at":
                            case "if":
                            case "when":
                                addWhen = false;
                                break;
                        }
                    }

                    description = $"Triggers {(addWhen ? "when " : null)}{description}";
                }
            }

            var manualEntry =
                new ManualEntry { Category = currentCategory, Name = entryName, Description = description };

            ManualParameter currentParameter = null;

            var range = document.Paragraphs[++index].Range;
            while (index < document.Paragraphs.Count && !IsOutlining(range))
            {
                if (IsBullet(range))
                {
                    var maualParameter = ParseBullet(range, ref currentParameter);
                    manualEntry.Parameters.Add(maualParameter);
                }
                else if (range.Text.Trim().StartsWith("example", StringComparison.InvariantCultureIgnoreCase))
                {
                    range = document.Paragraphs[++index].Range;
                    manualEntry.Example = range.Text.Trim();
                }
                else
                {
                    var text = range.Text.Trim();
                    var matchingParameters = manualEntry.Parameters.Where(p => text.Contains(p.Name)).ToList();
                    if (matchingParameters.Count == 1)
                    {
                        matchingParameters[0].Description += Environment.NewLine + text;
                    }
                }

                range = document.Paragraphs[++index].Range;
            }

            return manualEntry;
        }

        private static ManualParameter ParseBullet(Range range, ref ManualParameter currentParameter)
        {
            var text = range.Text.Trim();
            var colonIndex = text.IndexOf(":", StringComparison.InvariantCultureIgnoreCase);

            var name = colonIndex >= 0 ? text.Substring(0, colonIndex) : text;
            var description = colonIndex >= 0 ? text.Substring(colonIndex + 1).Trim() : string.Empty;

            var manualParameter = new ManualParameter { Name = name, Description = description };

            if (IsBullet(range, 1))
            {
                currentParameter = manualParameter;
                Console.WriteLine($"  {name}");
            }
            else
            {
                currentParameter.SubParameters.Add(manualParameter);
                Console.WriteLine($"    {name}");
            }

            return manualParameter;
        }

        private static bool SkipToOutlining(
            Document document,
            ref int index,
            int? level = null,
            int? value = null,
            string text = null)
        {
            while (index < document.Paragraphs.Count)
            {
                var range = document.Paragraphs[++index].Range;
                if (IsOutlining(range, level, value, text))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsOutlining(Range range, int? level = null, int? value = null, string text = null)
        {
            if (range.ListFormat.ListType != WdListType.wdListOutlineNumbering)
            {
                return false;
            }

            var listFormat = range.ListFormat;
            if (level.HasValue && listFormat.ListLevelNumber != level.Value)
            {
                return false;
            }

            if (value.HasValue && listFormat.ListValue != value.Value)
            {
                return false;
            }

            if (text != null && !string.Equals(range.Text.Trim(), text, StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static bool IsBullet(Range range, int? level = null)
        {
            if (range.ListFormat.ListType != WdListType.wdListBullet)
            {
                return false;
            }

            var listFormat = range.ListFormat;
            if (level.HasValue && listFormat.ListLevelNumber != level.Value)
            {
                return false;
            }

            return true;
        }
    }
}