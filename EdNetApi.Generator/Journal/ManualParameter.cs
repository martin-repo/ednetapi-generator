// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ManualParameter.cs" company="Martin Amareld">
//   Copyright(c) 2017 Martin Amareld. All rights reserved. 
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace EdNetApi.Generator.Journal
{
    using System.Collections.Generic;

    internal class ManualParameter
    {
        public ManualParameter()
        {
            SubParameters = new List<ManualParameter>();
        }

        public string Name { get; set; }

        public string Description { get; set; }

        public List<ManualParameter> SubParameters { get; set; }
    }
}