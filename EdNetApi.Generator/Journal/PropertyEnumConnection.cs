// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PropertyEnumConnection.cs" company="Martin Amareld">
//   Copyright(c) 2017 Martin Amareld. All rights reserved. 
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace EdNetApi.Generator.Journal
{
    internal class PropertyEnumConnection
    {
        public string EventName { get; set; }

        public string PropertyName { get; set; }

        public string EnumName { get; set; }

        public bool IsUsed { get; set; }
    }
}