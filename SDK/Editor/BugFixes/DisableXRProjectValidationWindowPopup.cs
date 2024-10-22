using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors.BugFixes
{
    public class DisableXRProjectValidationWindowPopup
    {
        [UnityEditor.InitializeOnLoadMethod]
        private static void PatchCode()
        {
            var path = "./";
            if (!System.IO.Directory.Exists(path)) return;
            var changed = false;
            // Scan all .cs files in the PackageCache directory
            foreach (var file in System.IO.Directory.GetFiles(path, "*.cs", System.IO.SearchOption.AllDirectories))
            {
                if (!file.StartsWith("./Assets") && !file.StartsWith("./Library/PackageCache"))
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
                    changed = true;
                }
            }

            if (changed)
            {
                UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                Debug.Log("Disabled XR Project Validation Window Popup");
            }
        }
    }
}