// --------------------------------------------------------------------------------------------------------------------
// <copyright file="JournalEntryManager.cs" company="Martin Amareld">
//   Copyright(c) 2017 Martin Amareld. All rights reserved. 
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace EdNetApi.Generator.Journal
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;

    using EdNetApi.Common;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal static class JournalEntryManager
    {
        public static void RegenerateJournalEntryClasses(
            string journalFolderPath,
            List<ManualEntry> manualEntries,
            bool reset)
        {
            const string GeneratedClassesFilename = "GeneratedClasses.txt";

            Dictionary<string, JournalClass> generatedClasses;
            if (reset)
            {
                foreach (var file in Directory.GetFiles(GetClassFolderPath()).ToList())
                {
                    File.Delete(file);
                }

                generatedClasses = new Dictionary<string, JournalClass>();
            }
            else
            {
                if (File.Exists(GeneratedClassesFilename))
                {
                    generatedClasses = JsonConvert
                        .DeserializeObject<List<JournalClass>>(File.ReadAllText(GeneratedClassesFilename))
                        .ToDictionary(j => j.Name, j => j);
                }
                else
                {
                    generatedClasses = new Dictionary<string, JournalClass>();
                }
            }

            var propertyEnumConnections = GetPropertyEnumConnections();

            var journalFiles = Directory.GetFiles(journalFolderPath, "*.log");
            var errors = new List<string>();
            foreach (var journalFile in journalFiles)
            {
                GenerateFromJournalFile(generatedClasses, manualEntries, propertyEnumConnections, errors, journalFile);
            }

            if (propertyEnumConnections.Any(pec => !pec.IsUsed))
            {
                foreach (var propertyEnumConnection in propertyEnumConnections.Where(pec => !pec.IsUsed))
                {
                    errors.Add(
                        $"PropertyEnumConnection unused: {propertyEnumConnection.EventName}.{propertyEnumConnection.PropertyName}");
                }
            }

            if (errors.Any())
            {
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }

                return;
            }

            File.WriteAllText(
                GeneratedClassesFilename,
                JsonConvert.SerializeObject(generatedClasses.Values.ToList(), Formatting.Indented));

            GenerateAndOutputJournalEventType(manualEntries);

            var enumClassNames = GetEnumClassNames();
            foreach (var generatedClass in generatedClasses.Values)
            {
                UpdateLocalisedString(generatedClass, errors);
                OutputClass(enumClassNames, generatedClass);
            }
        }

        private static PropertyEnumConnection CreateConnection(string eventName, string propertyName, string enumName)
        {
            return new PropertyEnumConnection
            {
                EventName = eventName,
                PropertyName = propertyName,
                EnumName = enumName
            };
        }

        private static void GenerateAndOutputJournalEventType(List<ManualEntry> manualEntries)
        {
            var filePath = Path.Combine(Path.GetDirectoryName(GetClassFolderPath()), "JournalEventType.cs");

            var classBuilder = new StringBuilder();
            classBuilder.Append(GetHeader("JournalEventType"));
            classBuilder.AppendLine("namespace EdNetApi.Journal");
            classBuilder.AppendLine("{");
            classBuilder.AppendLine("    using System.ComponentModel;");
            classBuilder.AppendLine();
            classBuilder.AppendLine("    public enum JournalEventType");
            classBuilder.AppendLine("    {");
            classBuilder.AppendLine(@"        [Description(""Unknown - journal entry failed to parse"")]");
            classBuilder.AppendLine("        UnknownValue = -1,");
            classBuilder.AppendLine();
            classBuilder.AppendLine(@"        [Description(""File header"")]");
            classBuilder.AppendLine("        Fileheader = 0,");

            foreach (var manualEntry in manualEntries)
            {
                classBuilder.AppendLine();
                classBuilder.AppendLine($@"        [Description(""{manualEntry.Description.Replace("\"", "\\\"")}"")]");
                classBuilder.AppendLine($"        {manualEntry.Name},");
            }

            classBuilder.AppendLine("    }");
            classBuilder.AppendLine("}");

            File.WriteAllText(filePath, classBuilder.ToString());
        }

        private static void GenerateFromJournalFile(
            Dictionary<string, JournalClass> generatedClasses,
            List<ManualEntry> manualEntries,
            List<PropertyEnumConnection> propertyEnumConnections,
            List<string> errors,
            string journalFilePath)
        {
            try
            {
                using (var journalStreamReader = new StreamReader(journalFilePath))
                {
                    string journalJson;
                    var lineNumber = 0;
                    while ((journalJson = journalStreamReader.ReadLine()) != null)
                    {
                        lineNumber++;
                        var journalEntry = JObject.Parse(journalJson);
                        var journalEntryEvent = journalEntry["event"]?.Value<string>();
                        if (string.IsNullOrWhiteSpace(journalEntryEvent))
                        {
                            throw new ApplicationException(
                                $"Unknown journal entry in file {journalFilePath} on line {lineNumber}");
                        }

                        var eventName = journalEntry["event"].Value<string>().ToPascalCase();
                        var className = $"{eventName}JournalEntry";

                        var journalClass = GetOrCreateJournalClass(
                            generatedClasses,
                            className,
                            "JournalEntry",
                            eventName,
                            true);

                        var manualParameters =
                            manualEntries.FirstOrDefault(
                                me => me.Name.Equals(eventName, StringComparison.OrdinalIgnoreCase))?.Parameters
                            ?? new List<ManualParameter>();

                        UpdateProperties(
                            generatedClasses,
                            propertyEnumConnections,
                            journalClass,
                            eventName,
                            manualParameters,
                            journalEntry);
                    }
                }
            }
            catch (Exception exception)
            {
                errors.Add(exception.GetBaseException().Message);
            }
        }

        private static void GeneratePartTypeClass(
            Dictionary<string, JournalClass> generatedClasses,
            List<PropertyEnumConnection> propertyEnumConnections,
            string className,
            string eventName,
            List<ManualParameter> manualParameters,
            JArray array)
        {
            if (array.First == null)
            {
                return;
            }

            var journalClass = GetOrCreateJournalClass(generatedClasses, className);
            UpdateProperties(
                generatedClasses,
                propertyEnumConnections,
                journalClass,
                eventName,
                manualParameters,
                (JObject)array.First);
        }

        private static string GetClassFolderPath([CallerFilePath] string sourceFilePath = "")
        {
            var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            var solutionFolderPath = sourceFilePath.Substring(
                0,
                sourceFilePath.IndexOf(assemblyName, StringComparison.InvariantCulture) - 1);
            var repositoryFolderPath = Path.GetDirectoryName(solutionFolderPath);
            var classFolderPath = Path.Combine(repositoryFolderPath, "ednetapi", "EdNetApi", "Journal", "JournalEntries");
            if (classFolderPath == null)
            {
                throw new ApplicationException("Failed to get source file path folder");
            }

            return classFolderPath;
        }

        private static List<string> GetEnumClassNames()
        {
            var contextFolderPath = Path.GetDirectoryName(GetClassFolderPath());
            var enumFolderPath = Path.Combine(contextFolderPath, "Enums");
            var enumClassNames = Directory.GetFiles(enumFolderPath).Select(Path.GetFileNameWithoutExtension).ToList();
            return enumClassNames;
        }

        private static string GetHeader(string className)
        {
            var headerBuilder = new StringBuilder();

            headerBuilder.AppendLine(
                "// --------------------------------------------------------------------------------------------------------------------");
            headerBuilder.AppendLine($@"// <copyright file=""{className}.cs"" company=""Martin Amareld"">");
            headerBuilder.AppendLine("//   Copyright(c) 2017 Martin Amareld. All rights reserved. ");
            headerBuilder.AppendLine("// </copyright>");
            headerBuilder.AppendLine(
                "// --------------------------------------------------------------------------------------------------------------------");
            headerBuilder.AppendLine();
            return headerBuilder.ToString();
        }

        private static JournalClass GetOrCreateJournalClass(
            Dictionary<string, JournalClass> generatedClasses,
            string className,
            string type = null,
            string eventName = null,
            bool timestamp = false)
        {
            if (!generatedClasses.ContainsKey(className))
            {
                var classFilePath = Path.Combine(GetClassFolderPath(), $"{className}.cs");
                var journalClass = new JournalClass
                {
                    FilePath = classFilePath,
                    Name = className,
                    Type = type,
                    Event = eventName,
                    Timestamp = timestamp
                };
                generatedClasses.Add(className, journalClass);
            }

            return generatedClasses[className];
        }

        private static string GetPartTypeName(JTokenType journalEntryPointType)
        {
            switch (journalEntryPointType)
            {
                case JTokenType.Integer:
                    return "int";
                case JTokenType.Float:
                    return "double";
                case JTokenType.String:
                    return "string";
                case JTokenType.Boolean:
                    return "bool";
                case JTokenType.Date:
                    return "DateTime";

                ////case JTokenType.Raw:
                ////case JTokenType.Bytes:
                ////case JTokenType.Guid:
                ////case JTokenType.Uri:
                ////case JTokenType.Array:
                ////case JTokenType.Comment:
                ////case JTokenType.Constructor:
                ////case JTokenType.None:
                ////case JTokenType.Null:
                ////case JTokenType.Object:
                ////case JTokenType.Property:
                ////case JTokenType.TimeSpan:
                ////case JTokenType.Undefined:
                default:
                    throw new ArgumentOutOfRangeException($"{journalEntryPointType} is not supported");
            }
        }

        private static List<PropertyEnumConnection> GetPropertyEnumConnections()
        {
            var propertyEnumConnections = new List<PropertyEnumConnection>();

            // Startup
            propertyEnumConnections.Add(CreateConnection("Loadout", "Ship", "ShipType"));
            propertyEnumConnections.Add(CreateConnection("LoadGame", "Ship", "ShipType"));
            propertyEnumConnections.Add(CreateConnection("Rank", "Combat", "CombatRank"));
            propertyEnumConnections.Add(CreateConnection("Rank", "Trade", "TradeRank"));
            propertyEnumConnections.Add(CreateConnection("Rank", "Explore", "ExplorationRank"));
            propertyEnumConnections.Add(CreateConnection("Rank", "Empire", "EmpireRank"));
            propertyEnumConnections.Add(CreateConnection("Rank", "Federation", "FederationRank"));
            propertyEnumConnections.Add(CreateConnection("Rank", "Cqc", "CqcRank"));

            // Travel
            propertyEnumConnections.Add(CreateConnection("DockingDenied", "Reason", "DockingDeniedType"));
            propertyEnumConnections.Add(CreateConnection("FsdJump", "PowerplayState", "PowerplayState"));
            propertyEnumConnections.Add(CreateConnection("Location", "PowerplayState", "PowerplayState"));
            propertyEnumConnections.Add(CreateConnection("StartJump", "JumpType", "JumpType"));

            // Combat
            // propertyEnumConnections.Add(CreateConnection("PVPKill", "CombatRank", "CombatRank"));
            propertyEnumConnections.Add(CreateConnection("ShieldState", "ShieldsUp", "ShieldState"));

            // Exploration
            // propertyEnumConnections.Add(CreateConnection("Scan", "TerraformState", "TerraformState"));
            // propertyEnumConnections.Add(CreateConnection("Scan", "ReserveLevel", "ReserveLevel"));
            propertyEnumConnections.Add(CreateConnection("MaterialCollected", "Category", "MaterialType"));

            // Station Services
            // propertyEnumConnections.Add(CreateConnection("CrewHire", "CombatRank", "CombatRank"));
            propertyEnumConnections.Add(CreateConnection("EngineerContribution", "Type", "ContributionType"));
            propertyEnumConnections.Add(CreateConnection("EngineerProgress", "Progress", "ProgressState"));
            propertyEnumConnections.Add(CreateConnection("FetchRemoteModule", "Ship", "ShipType"));
            propertyEnumConnections.Add(CreateConnection("MissionAccepted", "Influence", "EffectType"));
            propertyEnumConnections.Add(CreateConnection("MissionAccepted", "Reputation", "EffectType"));

            // propertyEnumConnections.Add(CreateConnection("MissionAccepted", "PassengerType", "PassengerType"));
            propertyEnumConnections.Add(CreateConnection("ModuleBuy", "Ship", "ShipType"));
            propertyEnumConnections.Add(CreateConnection("ModuleRetrieve", "Ship", "ShipType"));
            propertyEnumConnections.Add(CreateConnection("ModuleSell", "Ship", "ShipType"));
            propertyEnumConnections.Add(CreateConnection("ModuleSellRemote", "Ship", "ShipType"));
            propertyEnumConnections.Add(CreateConnection("ModuleStore", "Ship", "ShipType"));
            propertyEnumConnections.Add(CreateConnection("ModuleSwap", "Ship", "ShipType"));
            propertyEnumConnections.Add(CreateConnection("RedeemVoucher", "Type", "VoucherType"));
            propertyEnumConnections.Add(CreateConnection("Repair", "Item", "RepairType"));
            propertyEnumConnections.Add(CreateConnection("SetUserShipName", "Ship", "ShipType"));
            propertyEnumConnections.Add(CreateConnection("ShipyardBuy", "ShipType", "ShipType"));
            propertyEnumConnections.Add(CreateConnection("ShipyardNew", "ShipType", "ShipType"));
            propertyEnumConnections.Add(CreateConnection("ShipyardSell", "ShipType", "ShipType"));
            propertyEnumConnections.Add(CreateConnection("ShipyardTransfer", "ShipType", "ShipType"));
            propertyEnumConnections.Add(CreateConnection("ShipyardSwap", "ShipType", "ShipType"));

            // Powerplay

            // Other Events
            // propertyEnumConnections.Add(CreateConnection("ChangeCrewRole", "Role", "CrewRole"));
            // propertyEnumConnections.Add(CreateConnection("CrewMemberRoleChange", "Role", "CrewRole"));
            propertyEnumConnections.Add(CreateConnection("DataScanned", "Type", "DataLinkType"));
            propertyEnumConnections.Add(CreateConnection("Friends", "Status", "FriendStatus"));

            // propertyEnumConnections.Add(CreateConnection("Promotion", "Combat", "CombatRank"));
            propertyEnumConnections.Add(CreateConnection("Promotion", "Trade", "TradeRank"));
            propertyEnumConnections.Add(CreateConnection("Promotion", "Explore", "ExplorationRank"));
            propertyEnumConnections.Add(CreateConnection("Promotion", "Empire", "EmpireRank"));
            propertyEnumConnections.Add(CreateConnection("Promotion", "Federation", "FederationRank"));

            // propertyEnumConnections.Add(CreateConnection("Promotion", "CQC", "CqcRank"));
            // propertyEnumConnections.Add(CreateConnection("RecieveText", "Channel", "ChannelType"));
            propertyEnumConnections.Add(CreateConnection("Scanned", "ScanType", "ScanType"));

            // propertyEnumConnections.Add(CreateConnection("VehicleSwitch", "To", "VehicleSwitchType"));
            return propertyEnumConnections;
        }

        private static void OutputClass(List<string> enumClassNames, JournalClass journalClass)
        {
            var typeText = journalClass.Type != null ? $" : {journalClass.Type}" : string.Empty;

            var classBuilder = new StringBuilder();
            classBuilder.Append(GetHeader(journalClass.Name));
            classBuilder.AppendLine("namespace EdNetApi.Journal.JournalEntries");
            classBuilder.AppendLine("{");

            var systemAdded = false;
            if (journalClass.Timestamp || journalClass.Properties.Any(p => p.Type.Contains("DateTime")))
            {
                classBuilder.AppendLine("    using System;");
                systemAdded = true;
            }

            classBuilder.AppendLine("    using System.ComponentModel;");

            if (journalClass.Properties.Any(p => p.Type.Contains("List<")))
            {
                classBuilder.AppendLine("    using System.Collections.Generic;");
                systemAdded = true;
            }

            var enumAdded = false;
            if (journalClass.Properties.Any(p => enumClassNames.Contains(p.Type)))
            {
                if (systemAdded)
                {
                    classBuilder.AppendLine();
                }

                classBuilder.AppendLine("    using EdNetApi.Common;");
                classBuilder.AppendLine("    using EdNetApi.Journal.Enums;");
                enumAdded = true;
            }

            if (systemAdded || enumAdded)
            {
                classBuilder.AppendLine();
            }

            classBuilder.AppendLine("    using Newtonsoft.Json;");
            classBuilder.AppendLine();
            classBuilder.AppendLine($"    public class {journalClass.Name}{typeText}");
            classBuilder.AppendLine("    {");

            if (journalClass.Event != null)
            {
                classBuilder.AppendLine(
                    $"        public const JournalEventType EventConst = JournalEventType.{journalClass.Event};");
                classBuilder.AppendLine();
            }

            classBuilder.AppendLine($"        internal {journalClass.Name}()");
            classBuilder.AppendLine("        {");
            classBuilder.AppendLine("        }");

            if (journalClass.Event != null)
            {
                classBuilder.AppendLine();
                classBuilder.AppendLine("        [JsonIgnore]");
                classBuilder.AppendLine("        public override JournalEventType Event => EventConst;");
            }

            if (journalClass.Timestamp)
            {
                classBuilder.AppendLine();
                classBuilder.AppendLine(@"        [JsonProperty(""timestamp"")]");
                classBuilder.AppendLine("        public override DateTime Timestamp { get; internal set; }");
            }

            foreach (var journalClassProperty in journalClass.Properties)
            {
                classBuilder.AppendLine();
                if (journalClassProperty.JsonName != null)
                {
                    classBuilder.AppendLine($@"        [JsonProperty(""{journalClassProperty.JsonName}"")]");
                }
                else
                {
                    classBuilder.AppendLine("        [JsonIgnore]");
                }

                classBuilder.AppendLine($@"        [Description(""{journalClassProperty.Description}"")]");
                classBuilder.AppendLine(
                    $"        public {journalClassProperty.Type} {journalClassProperty.Name} {journalClassProperty.Accessors}");
            }

            classBuilder.AppendLine("    }");
            classBuilder.Append("}");

            File.WriteAllText(journalClass.FilePath, classBuilder.ToString());
        }

        private static void UpdateLocalisedString(JournalClass journalClass, List<string> errors)
        {
            var localisedProperties =
                journalClass.Properties.Where(p => p.Name.EndsWith("_Localised", StringComparison.OrdinalIgnoreCase));
            foreach (var localisedProperty in localisedProperties)
            {
                var name = localisedProperty.Name.Substring(
                    0,
                    localisedProperty.Name.IndexOf("_", StringComparison.Ordinal));
                var match = journalClass.Properties.SingleOrDefault(p => p.Name == name);
                if (match == null)
                {
                    errors.Add($"{journalClass.Name} has orphan localised property {localisedProperty.Name}");
                    continue;
                }

                match.Name += "Id";
                localisedProperty.Name = name;
            }
        }

        private static void UpdateProperties(
            Dictionary<string, JournalClass> generatedClasses,
            List<PropertyEnumConnection> propertyEnumConnections,
            JournalClass journalClass,
            string eventName,
            List<ManualParameter> manualParameters,
            JObject source)
        {
            foreach (var part in source)
            {
                var jsonName = part.Key;
                var value = part.Value;

                if (jsonName == "event" || jsonName == "timestamp")
                {
                    continue;
                }

                var name = jsonName.ToPascalCase();
                string type;
                JournalProperty enumAccessor = null;
                if (value.Type == JTokenType.Array)
                {
                    var array = (JArray)value;
                    if (array.First == null)
                    {
                        continue;
                    }

                    if (array.First.Type == JTokenType.Object)
                    {
                        var arrayType = $"{eventName}{jsonName.TrimEnd('s')}";
                        GeneratePartTypeClass(
                            generatedClasses,
                            propertyEnumConnections,
                            arrayType,
                            eventName,
                            manualParameters,
                            array);
                        type = $"List<{arrayType}>";
                    }
                    else
                    {
                        var arrayType = GetPartTypeName(array.First.Type);
                        type = $"List<{arrayType}>";
                    }

                    name = name + "List";
                }
                else
                {
                    type = GetPartTypeName(value.Type);

                    var propertyEnumConnection =
                        propertyEnumConnections.FirstOrDefault(p => p.EventName == eventName && p.PropertyName == name);
                    if (propertyEnumConnection != null)
                    {
                        enumAccessor =
                            new JournalProperty
                            {
                                Type = propertyEnumConnection.EnumName,
                                Name = name,
                                Accessors = $@"=> {name}Raw.GetEnumValue<{
                                            propertyEnumConnection.EnumName
                                        }>();"
                            };
                        name += "Raw";
                        propertyEnumConnection.IsUsed = true;
                    }
                }

                var existingProperty = journalClass.Properties.FirstOrDefault(p => p.Name == name);
                if (existingProperty != null)
                {
                    if (existingProperty.Type == type)
                    {
                        continue;
                    }

                    existingProperty.JsonName = jsonName;
                    existingProperty.Type = type;
                    continue;
                }

                var manualParameter = manualParameters.FirstOrDefault(
                    p => p.Name.Equals(jsonName, StringComparison.OrdinalIgnoreCase));
                var description = manualParameter?.Description?.Replace("\"", "\\\"").Replace("\r", string.Empty)
                    .Replace("\n", " - ").Trim();

                var journalProperty = new JournalProperty
                {
                    JsonName = jsonName,
                    Description = description,
                    Type = type,
                    Name = name,
                    Accessors = "{ get; internal set; }"
                };
                journalClass.Properties.Add(journalProperty);

                if (enumAccessor != null)
                {
                    enumAccessor.Description = description;
                    journalClass.Properties.Add(enumAccessor);
                }
            }
        }
    }
}