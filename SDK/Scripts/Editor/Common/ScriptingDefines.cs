#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace MetaverseCloudEngine.Unity.Installer.Editor
{
    public static class ScriptingDefines
    {
        public const string DefaultSymbols = "METAVERSE_CLOUD_ENGINE";

        private const char DefSeparator = ';';
        private static readonly List<string> Defs = new();

        public static void Add(string[] symbols)
        {
            var symbol = string.Join(DefSeparator, symbols);
            var groups = GetSupportedBuildTargetGroups();
            foreach (var g in groups)
                Add(g, symbol.Split(DefSeparator));
        }

        public static void Remove(string[] symbols)
        {
            var symbol = string.Join(DefSeparator, symbols);
            var groups = GetSupportedBuildTargetGroups();
            foreach (var g in groups)
                Remove(g, symbol.Split(DefSeparator));
        }

        public static void Add(BuildTargetGroup group, string[] defines)
        {
            var currentDefines = GetAll(group);
            if (currentDefines.All(defines.Contains))
                return;

            Defs.Clear();
            Defs.AddRange(GetDefines(group));
            Defs.AddRange(defines.Except(Defs));
            UpdateDefines(group, Defs);
        }

        public static void Remove(BuildTargetGroup group, string[] defines)
        {
            Defs.Clear();
            Defs.AddRange(GetDefines(group).Except(defines));
            UpdateDefines(group, Defs);
        }

        public static void Clear(BuildTargetGroup group)
        {
            Defs.Clear();
            UpdateDefines(group, Defs);
        }

        public static string[] GetAll()
        {
            var groups = GetSupportedBuildTargetGroups();
            var defines = new List<string>();
            foreach (var g in groups)
                defines.AddRange(GetDefines(g));
            return defines.Distinct().ToArray();
        }

        public static string[] GetAll(BuildTargetGroup group)
        {
            return GetDefines(group).ToArray();
        }

        private static IEnumerable<string> GetDefines(BuildTargetGroup group)
        {
            return PlayerSettings.GetScriptingDefineSymbolsForGroup(group).Split(DefSeparator).ToList();
        }

        private static void UpdateDefines(BuildTargetGroup group, List<string> allDefines)
        {
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group,
                string.Join(DefSeparator.ToString(), allDefines.Distinct().ToArray()));
        }

        private static IEnumerable<BuildTargetGroup> GetSupportedBuildTargetGroups()
        {
            return ((BuildTarget[]) Enum.GetValues(typeof(BuildTarget)))
                .Select(x => new {group = BuildPipeline.GetBuildTargetGroup(x), target = x})
                .Where(x => BuildPipeline.IsBuildTargetSupported(x.group, x.target))
                .Select(x => x.group)
                .Distinct()
                .ToArray();
        }
    }
}

#endif