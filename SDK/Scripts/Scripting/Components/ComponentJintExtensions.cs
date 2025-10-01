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

        public static Component GetComponentInParent(this Component component, object typeIdentifier, bool includeInactive)
        {
            if (!component)
            {
                return null;
            }

            if (!JintTypeResolver.TryResolveType(typeIdentifier, out var resolvedType))
            {
                throw new ArgumentException($"Cannot resolve component type from value '{typeIdentifier ?? "null"}'.", nameof(typeIdentifier));
            }

            return component.GetComponentInParent(resolvedType, includeInactive);
        }

        public static Component GetComponentInParent(this GameObject gameObject, object typeIdentifier)
        {
            return GetComponentInParent(gameObject, typeIdentifier, includeInactive: false);
        }

        public static Component GetComponentInParent(this GameObject gameObject, object typeIdentifier, bool includeInactive)
        {
            if (!gameObject)
            {
                return null;
            }

            if (!JintTypeResolver.TryResolveType(typeIdentifier, out var resolvedType))
            {
                throw new ArgumentException($"Cannot resolve component type from value '{typeIdentifier ?? "null"}'.", nameof(typeIdentifier));
            }

            return gameObject.GetComponentInParent(resolvedType, includeInactive);
        }

        public static Component GetComponentInParent(this Component component, object typeIdentifier)
        {
            return GetComponentInParent(component, typeIdentifier, includeInactive: false);
        }

        public static Component[] GetComponentsInParent(this Component component, object typeIdentifier, bool includeInactive)
        {
            if (!component)
            {
                return Array.Empty<Component>();
            }

            if (!JintTypeResolver.TryResolveType(typeIdentifier, out var resolvedType))
            {
                throw new ArgumentException($"Cannot resolve component type from value '{typeIdentifier ?? "null"}'.", nameof(typeIdentifier));
            }

            return component.GetComponentsInParent(resolvedType, includeInactive);
        }

        public static Component[] GetComponentsInParent(this Component component, object typeIdentifier)
        {
            return GetComponentsInParent(component, typeIdentifier, includeInactive: false);
        }

        public static Component[] GetComponentsInParent(this GameObject gameObject, object typeIdentifier, bool includeInactive)
        {
            if (!gameObject)
            {
                return Array.Empty<Component>();
            }

            if (!JintTypeResolver.TryResolveType(typeIdentifier, out var resolvedType))
            {
                throw new ArgumentException($"Cannot resolve component type from value '{typeIdentifier ?? "null"}'.", nameof(typeIdentifier));
            }

            return gameObject.GetComponentsInParent(resolvedType, includeInactive);
        }

        public static Component[] GetComponentsInParent(this GameObject gameObject, object typeIdentifier)
        {
            return GetComponentsInParent(gameObject, typeIdentifier, includeInactive: false);
        }

        public static Component AddComponent(this GameObject gameObject, object typeIdentifier)
        {
            if (!gameObject)
            {
                return null;
            }

            if (!JintTypeResolver.TryResolveType(typeIdentifier, out var resolvedType))
            {
                throw new ArgumentException($"Cannot resolve component type from value '{typeIdentifier ?? "null"}'.", nameof(typeIdentifier));
            }

            if (!typeof(Component).IsAssignableFrom(resolvedType))
            {
                throw new ArgumentException($"Type '{resolvedType.FullName}' is not a Unity component type.", nameof(typeIdentifier));
            }

            return gameObject.AddComponent(resolvedType);
        }

        public static Component GetComponent(this GameObject gameObject, string typeName)
        {
            if (!gameObject)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            return gameObject.GetComponent(typeName);
        }
        
        public static Component GetComponent(this Component component, string typeName)
        {
            if (!component)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            return component.GetComponent(typeName);
        }
    }
}
