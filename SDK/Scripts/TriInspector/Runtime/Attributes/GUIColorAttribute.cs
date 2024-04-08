﻿using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method |
                    AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class GUIColorAttribute : Attribute
    {
        public float R { get; }
        public float G { get; }
        public float B { get; }
        public float A { get; }

        public GUIColorAttribute(float r, float g, float b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }
    }
}