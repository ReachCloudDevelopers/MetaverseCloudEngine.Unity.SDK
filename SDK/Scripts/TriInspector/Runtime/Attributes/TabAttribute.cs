﻿using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class TabAttribute : Attribute
    {
        public TabAttribute(string tab)
        {
            TabName = tab;
        }

        public string TabName { get; }
    }
}