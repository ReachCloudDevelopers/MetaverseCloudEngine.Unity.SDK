using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.FixingOtherPeoplesCode
{
    public class FixGeospatialCreatorOriginNotUpdatingAtRuntime : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        [InitializeOnLoadMethod]
        private static void PatchCode()
        {
            const string path = "./Library/PackageCache";
            if (!System.IO.Directory.Exists(path)) return;
            var files = System.IO.Directory.GetFiles(path, "ARGeospatialCreatorOrigin.cs", System.IO.SearchOption.AllDirectories);
            if (files.Length == 0) return;
            var file = files.FirstOrDefault(x => x.Replace("\\", "/").StartsWith("./Library/PackageCache/com.google.ar.core.arfoundation.extensions") && x.Replace("\\", "/").EndsWith("ARGeospatialCreatorOrigin.cs"));
            if (file == null) return;
            var text = System.IO.File.ReadAllText(file);
            const string oldValue = 
@"#if UNITY_EDITOR
        // Manages the coupling between _originPoint and this GameObject's subcomponent that also";
            if (text.Contains(oldValue) && !text.Contains("public void SetOriginPointWithoutUpdatingAdapter(double latitude, double longitude, double altitude)"))
            {
                var newText = text.Replace(oldValue, 
@"        /// <summary>
        /// Sets the origin point at runtime, however this will NOT update the origin point in the adapter.
        /// </summary>
        /// <param name=""latitude"">Latitude for the origin, in decimal degrees.</param>
        /// <param name=""longitude"">Longitude for the origin, in decimal degrees.</param>
        /// <param name=""altitude"">Altitude for the origin, meters according to WGS84.</param>
        public void SetOriginPointWithoutUpdatingAdapter(double latitude, double longitude, double altitude)
        {
            _originPoint = new GeoCoordinate(latitude, longitude, altitude);
        }

" + oldValue);
                newText = newText.Replace("// SetOriginPoint() method.", "// SetOriginPoint() method in Editor or SetOriginPointWithoutUpdatingAdapter() method for updating at runtime.");
                newText = newText.Replace("origin at runtime is not supported.", "origin along with the component adapter at runtime is not supported. Use SetOriginPointWithoutUpdatingAdapter() method instead.");
                newText = newText.Replace(
@"#if UNITY_EDITOR
            private set;
#endif",
"            private set;");
                System.IO.File.WriteAllText(file, newText);
                UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                Debug.Log("Fixed ARGeospatialCreatorOrigin.cs");
            }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            PatchCode();
        }
    }
}