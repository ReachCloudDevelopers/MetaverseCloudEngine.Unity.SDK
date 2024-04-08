﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TriInspectorMVCE;
using TriInspectorMVCE.TypeProcessors;
using TriInspectorMVCE.Utilities;

[assembly: RegisterTriTypeProcessor(typeof(TriRegisterShownByTriPropertiesTypeProcessor), 1)]

namespace TriInspectorMVCE.TypeProcessors
{
    public class TriRegisterShownByTriPropertiesTypeProcessor : TriTypeProcessor
    {
        public override void ProcessType(Type type, List<TriPropertyDefinition> properties)
        {
            const int propertiesOffset = 10001;

            properties.AddRange(TriReflectionUtilities
                .GetAllInstancePropertiesInDeclarationOrder(type)
                .Where(IsSerialized)
                .Select((it, ind) => TriPropertyDefinition.CreateForPropertyInfo(ind + propertiesOffset, it)));
        }

        private static bool IsSerialized(PropertyInfo propertyInfo)
        {
            return propertyInfo.GetCustomAttribute<ShowInInspectorAttribute>() != null;
        }
    }
}