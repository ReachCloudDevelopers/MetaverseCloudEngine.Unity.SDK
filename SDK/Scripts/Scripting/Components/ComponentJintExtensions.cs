using System;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Scripting.Components
{
    /// <summary>
    /// Extension helpers that provide JavaScript-friendly overloads for common Unity component APIs.
    /// </summary>
    internal static class ComponentJintExtensions
    {
        public static Component GetComponent(this Component component, object typeIdentifier)
        {
            if (!component)
            {
                return null;
            }

            if (!JintTypeResolver.TryResolveType(typeIdentifier, out var resolvedType))
            {
                throw new ArgumentException($"Cannot resolve component type from value '{typeIdentifier ?? "null"}'.", nameof(typeIdentifier));
            }

            return component.GetComponent(resolvedType);
        }

        public static Component GetComponentInChildren(this Component component, object typeIdentifier)
        {
            return GetComponentInChildren(component, typeIdentifier, includeInactive: false);
        }

        public static Component GetComponentInChildren(this Component component, object typeIdentifier, bool includeInactive)
        {
            if (!component)
            {
                return null;
            }

            if (!JintTypeResolver.TryResolveType(typeIdentifier, out var resolvedType))
            {
                throw new ArgumentException($"Cannot resolve component type from value '{typeIdentifier ?? "null"}'.", nameof(typeIdentifier));
            }

            return component.GetComponentInChildren(resolvedType, includeInactive);
        }

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

        public static Component GetComponent(this GameObject gameObject, object typeIdentifier)
        {
            if (!gameObject)
            {
                return null;
            }

            if (!JintTypeResolver.TryResolveType(typeIdentifier, out var resolvedType))
            {
                throw new ArgumentException($"Cannot resolve component type from value '{typeIdentifier ?? "null"}'.", nameof(typeIdentifier));
            }

            return gameObject.GetComponent(resolvedType);
        }

        public static Component GetComponentInChildren(this GameObject gameObject, object typeIdentifier)
        {
            return GetComponentInChildren(gameObject, typeIdentifier, includeInactive: false);
        }

        public static Component GetComponentInChildren(this GameObject gameObject, object typeIdentifier, bool includeInactive)
        {
            if (!gameObject)
            {
                return null;
            }

            if (!JintTypeResolver.TryResolveType(typeIdentifier, out var resolvedType))
            {
                throw new ArgumentException($"Cannot resolve component type from value '{typeIdentifier ?? "null"}'.", nameof(typeIdentifier));
            }

            return gameObject.GetComponentInChildren(resolvedType, includeInactive);
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
