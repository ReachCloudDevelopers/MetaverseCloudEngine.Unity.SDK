using System;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Scripting.Components
{
    /// <summary>
    /// Extension helpers that provide JavaScript-friendly overloads for common Unity component APIs.
    /// </summary>
    internal static class ComponentJintExtensions
    {
        public static Component[] GetComponentsInChildren(this Component component, object typeIdentifier)
        {
            return GetComponentsInChildren(component, typeIdentifier, includeInactive: false);
        }

        public static Component[] GetComponentsInChildren(this Component component, object typeIdentifier, bool includeInactive)
        {
            if (!component)
            {
                return Array.Empty<Component>();
            }

            if (!JintTypeResolver.TryResolveType(typeIdentifier, out var resolvedType))
            {
                throw new ArgumentException($"Cannot resolve component type from value '{typeIdentifier ?? "null"}'.", nameof(typeIdentifier));
            }

            return component.GetComponentsInChildren(resolvedType, includeInactive);
        }

        public static Component[] GetComponentsInChildren(this GameObject gameObject, object typeIdentifier)
        {
            return GetComponentsInChildren(gameObject, typeIdentifier, includeInactive: false);
        }

        public static Component[] GetComponentsInChildren(this GameObject gameObject, object typeIdentifier, bool includeInactive)
        {
            if (!gameObject)
            {
                return Array.Empty<Component>();
            }

            if (!JintTypeResolver.TryResolveType(typeIdentifier, out var resolvedType))
            {
                throw new ArgumentException($"Cannot resolve component type from value '{typeIdentifier ?? "null"}'.", nameof(typeIdentifier));
            }

            return gameObject.GetComponentsInChildren(resolvedType, includeInactive);
        }
    }
}
