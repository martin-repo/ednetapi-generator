// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ManualEntry.cs" company="Martin Amareld">
//   Copyright(c) 2017 Martin Amareld. All rights reserved. 
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace EdNetApi.Generator.Journal
{
    using System.Collections.Generic;

    internal class ManualEntry
    {
        public ManualEntry()
        {
            Parameters = new List<ManualParameter>();
        }

        public string Category { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public List<ManualParameter> Parameters { get; set; }

        public string Example { get; set; }

    }
}