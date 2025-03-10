using System.Linq;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.FixingOtherPeoplesCode
{
    public class DisableXRProjectValidationWindowPopup
    {
        [UnityEditor.InitializeOnLoadMethod]
        private static void PatchCode()
        {
            const string path = "Library/PackageCache";
            if (!System.IO.Directory.Exists("Library/PackageCache") ||
                !System.IO.Directory.Exists("Assets")) return;
            var changed = 0;
            // Scan all .cs files in the PackageCache directory
            var files = System.IO.Directory.GetFiles(path, "*.cs", System.IO.SearchOption.AllDirectories).Concat(
                System.IO.Directory.GetFiles("Assets", "*.cs", System.IO.SearchOption.AllDirectories)).ToArray();
            foreach (var file in files) 
            {
                if (!file.StartsWith("Library/PackageCache") && !file.StartsWith("Assets"))
                    continue;
                var text = System.IO.File.ReadAllText(file);
                var regex = new System.Text.RegularExpressions.Regex(@"SettingsService\.OpenProjectSettings\([^)]+\);");
                if (text.Contains("using Unity.XR.CoreUtils") && text.Contains("BuildValidator") && regex.IsMatch(text))
                {
                    // Ok, this is a file that uses build validation rules. Let's axe
                    // the dang thing that opens the freaking validation rule window every
                    // time the domain is reloaded.
                    var newText = regex.Replace(text, "((System.Action)(() => {}))();"); // Replace with a lambda that does nothing
                    System.IO.File.WriteAllText(file, newText);
                    changed++;
                }
            }

            if (changed > 0)
            {
                UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                Debug.Log("Updated " + changed + " files to disable XR build validation window popup.");
            }
        }
    }
}