// --------------------------------------------------------------------------------------------------------------------
// <copyright file="JournalClass.cs" company="Martin Amareld">
//   Copyright(c) 2017 Martin Amareld. All rights reserved. 
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace EdNetApi.Generator.Journal
{
    using System.Collections.Generic;

    internal class JournalClass
    {
        public JournalClass()
        {
            Properties = new List<JournalProperty>();
        }

        public string FilePath { get; set; }

        public string Name { get; set; }

        public string Type { get; set; }

        public string Event { get; set; }

        public bool Timestamp { get; set; }

        public List<JournalProperty> Properties { get; set; }
    }
}