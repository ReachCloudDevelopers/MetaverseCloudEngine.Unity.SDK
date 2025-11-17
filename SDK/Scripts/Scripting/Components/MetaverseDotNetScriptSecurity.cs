using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MetaverseCloudEngine.Unity.Scripting.Components
{
    public static class MetaverseDotNetScriptSecurity
    {
        public static bool Enabled { get; set; } = true;

        static readonly string[] DefaultBlockedNamespacePrefixes = new[]
        {
            "System.Reflection",
            "System.IO",
            "System.Net",
            "System.Diagnostics",
            "Microsoft.Win32",
            "System.Security"
        };

        static readonly string[] DefaultBlockedTypeFullNames = new[]
        {
            "System.Diagnostics.Process"
        };

        static readonly HashSet<string> ExtraBlockedNamespaces = new();
        static readonly HashSet<string> ExtraBlockedTypes = new();
        static readonly HashSet<string> ExtraAllowedTypes = new();
        static readonly object Gate = new();

        public static void AddBlockedNamespace(string nsPrefix)
        {
            if (string.IsNullOrWhiteSpace(nsPrefix)) return;
            lock (Gate) ExtraBlockedNamespaces.Add(nsPrefix);
        }

        public static void AddBlockedType(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return;
            lock (Gate) ExtraBlockedTypes.Add(fullName);
        }

        public static void AddAllowedType(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return;
            lock (Gate) ExtraAllowedTypes.Add(fullName);
        }

        public static bool ValidateAssembly(Assembly assembly, out string message)
        {
            message = null;
            if (!Enabled || assembly == null)
                return true;

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                var first = ex.LoaderExceptions?.FirstOrDefault();
                message =
                    $"Security validation failed while loading types from assembly '{assembly.FullName}': {first?.GetType().Name}: {first?.Message ?? ex.Message}";
                return false;
            }
            catch (Exception ex)
            {
                message =
                    $"Security validation failed while enumerating types in assembly '{assembly.FullName}': {ex.GetType().Name}: {ex.Message}";
                return false;
            }

            foreach (var type in types)
            {
                try
                {
                    if (!ValidateType(type, ref message))
                        return false;
                }
                catch (Exception ex)
                {
                    var typeName = type?.FullName ?? "<unknown>";
                    message =
                        $"Security validation failed while inspecting type '{typeName}' in assembly '{assembly.FullName}': {ex.GetType().Name}: {ex.Message}";
                    return false;
                }
            }

            return true;
        }

        public static bool ValidateAssemblyBytes(byte[] assemblyBytes, out string message)
        {
            message = null;

            if (!Enabled)
                return true;

            if (assemblyBytes == null || assemblyBytes.Length == 0)
                return true;

            Assembly assembly;
            try
            {
                assembly = Assembly.Load(assemblyBytes);
            }
            catch (Exception ex)
            {
                message =
                    $"Security validation failed: could not load assembly bytes: {ex.GetType().Name}: {ex.Message}";
                return false;
            }

            return ValidateAssembly(assembly, out message);
        }

        static bool ValidateType(Type type, ref string message)
        {
            if (type == null)
                return true;

            if (IsTypeBlockedDeep(type, out var reason))
            {
                message = $"Type '{type.FullName}' uses blocked API: {reason}";
                return false;
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (var field in type.GetFields(flags))
            {
                if (IsTypeBlockedDeep(field.FieldType, out reason))
                {
                    message = $"Field '{field.Name}' on type '{type.FullName}' uses blocked API: {reason}";
                    return false;
                }
            }

            foreach (var prop in type.GetProperties(flags))
            {
                if (IsTypeBlockedDeep(prop.PropertyType, out reason))
                {
                    message = $"Property '{prop.Name}' on type '{type.FullName}' uses blocked API: {reason}";
                    return false;
                }
            }

            foreach (var method in type.GetMethods(flags))
            {
                if (!ValidateMethod(type, method, ref message))
                    return false;
            }

            foreach (var ctor in type.GetConstructors(flags))
            {
                if (!ValidateMethod(type, ctor, ref message))
                    return false;
            }

            return MetaverseDotNetScriptILScanner.ValidateTypeIL(type, ref message);
        }

        static bool ValidateMethod(Type type, MethodBase method, ref string message)
        {
            if (method == null)
                return true;

            if (method is MethodInfo mi && IsTypeBlockedDeep(mi.ReturnType, out var reason))
            {
                message = $"Method '{type.FullName}.{method.Name}' has blocked return type: {reason}";
                return false;
            }

            foreach (var p in method.GetParameters())
            {
                if (IsTypeBlockedDeep(p.ParameterType, out reason))
                {
                    message = $"Method '{type.FullName}.{method.Name}' has blocked parameter '{p.Name}': {reason}";
                    return false;
                }
            }

            return true;
        }

        internal static bool IsTypeBlockedDeep(Type type, out string reason)
        {
            reason = null;
            if (type == null)
                return false;

            if (type.IsArray || type.IsByRef || type.IsPointer)
                return IsTypeBlockedDeep(type.GetElementType(), out reason);

            if (type.IsGenericType)
            {
                foreach (var arg in type.GetGenericArguments())
                {
                    if (IsTypeBlockedDeep(arg, out reason))
                        return true;
                }
            }

            return IsTypeBlockedSimple(type, out reason);
        }

        static bool IsTypeBlockedSimple(Type type, out string reason)
        {
            reason = null;
            var fullName = type.FullName ?? type.Name;

            lock (Gate)
            {
                if (ExtraAllowedTypes.Contains(fullName))
                    return false;

                if (DefaultBlockedTypeFullNames.Contains(fullName) || ExtraBlockedTypes.Contains(fullName))
                {
                    reason = $"type '{fullName}' is blocked";
                    return true;
                }

                var ns = type.Namespace ?? string.Empty;
                if (DefaultBlockedNamespacePrefixes.Any(p => ns.StartsWith(p, StringComparison.Ordinal)) || ExtraBlockedNamespaces.Any(p => ns.StartsWith(p, StringComparison.Ordinal)))
                {
                    reason = $"namespace '{ns}' is blocked";
                    return true;
                }
            }

            return false;
        }
    }
}

