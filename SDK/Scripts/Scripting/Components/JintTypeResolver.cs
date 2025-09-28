using System;
using System.Globalization;
using System.Linq;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;
using Jint.Native.Object;

namespace MetaverseCloudEngine.Unity.Scripting.Components
{
    /// <summary>
    /// Utility helpers for resolving <see cref="Type"/> objects from Jint values.
    /// </summary>
    internal static class JintTypeResolver
    {
        /// <summary>
        /// Attempts to resolve a <see cref="Type"/> from a value provided by Jint.
        /// </summary>
        /// <param name="candidate">The candidate value supplied from JavaScript.</param>
        /// <param name="resolved">The resolved managed type.</param>
        /// <returns><c>true</c> if the type could be resolved; otherwise <c>false</c>.</returns>
        public static bool TryResolveType(object candidate, out Type resolved)
        {
            resolved = null!;

            if (candidate == null)
            {
                return false;
            }

            if (candidate is Type directType)
            {
                resolved = directType;
                return true;
            }

            if (candidate is string typeName)
            {
                var resolvedType = ResolveFromString(typeName);
                if (resolvedType == null)
                {
                    return false;
                }

                resolved = resolvedType;
                return true;
            }

            if (candidate is TypeReference typeReference)
            {
                Type? referenceType = typeReference.ReferenceType;
                if (referenceType == null)
                {
                    return false;
                }

                resolved = referenceType!;
                return true;
            }

            if (candidate is Delegate del)
            {
                if (del.Target is TypeReference delegateTypeReference)
                {
                    Type? referenceType = delegateTypeReference.ReferenceType;
                    if (referenceType == null)
                    {
                        return false;
                    }

                    resolved = referenceType!;
                    return true;
                }

                if (del.Target is Type targetType)
                {
                    resolved = targetType;
                    return true;
                }

                var returnType = del.Method?.ReturnType;
                if (returnType != null && typeof(Type).IsAssignableFrom(returnType))
                {
                    resolved = returnType;
                    return true;
                }
            }

            if (candidate is ObjectWrapper wrapper && wrapper.Target is Type wrapperType)
            {
                resolved = wrapperType;
                return true;
            }

            if (candidate is JsValue jsValue)
            {
                return TryResolveFromJsValue(jsValue, out resolved);
            }

            return false;
        }

        private static bool TryResolveFromJsValue(JsValue value, out Type resolved)
        {
            resolved = null!;

            if (value.Type == Types.Null || value.Type == Types.Undefined)
            {
                return false;
            }

            if (value is TypeReference typeReference)
            {
                resolved = typeReference.ReferenceType;
                return resolved != null;
            }

            var unwrapped = value.ToObject();

            if (unwrapped == null)
            {
                return false;
            }

            if (unwrapped is ObjectWrapper wrapper && wrapper.Target is Type wrapperType)
            {
                resolved = wrapperType;
                return true;
            }

            return TryResolveType(unwrapped, out resolved);
        }

        private static Type? ResolveFromString(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            var normalized = typeName.Trim();
            if (normalized.EndsWith("[]", true, CultureInfo.InvariantCulture))
            {
                var elementTypeName = normalized.Substring(0, normalized.Length - 2);
                var elementType = ResolveFromString(elementTypeName);
                return elementType?.MakeArrayType();
            }

            var type = Type.GetType(normalized, throwOnError: false);
            if (type != null)
            {
                return type;
            }

            // Search assemblies permitted by the runtime.
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(a => !a.IsDynamic)
                .Select(a => a.GetType(normalized, throwOnError: false))
                .FirstOrDefault(t => t != null);
        }
    }
}
