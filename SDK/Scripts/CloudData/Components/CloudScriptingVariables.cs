using System;
using System.Collections.Generic;
using System.IO;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.Forms;
using TriInspectorMVCE;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;

namespace MetaverseCloudEngine.Unity.CloudData.Components
{
    [Serializable]
    public class CloudScriptingVariableValues
    {
        public string[] variableNames;
        public bool useSceneVariables;
        [HideIf(nameof(useSceneVariables))]
        public Variables variablesReference;
        public VariableDeclarations Declarations => 
            useSceneVariables && Variables.ExistInActiveScene 
                ? Variables.ActiveScene 
                : variablesReference.declarations;
    }
    
    public class CloudScriptingVariables : CloudDataRecordBase<CloudScriptingVariableValues>
    {
        public CloudScriptingVariableValues values = new();
        
        public override CloudScriptingVariableValues ParseData(CloudDataRecordDto record)
        {
            if (!fsJsonParser.Parse(record.StringValue, out var data).Succeeded)
                return null;

            var parsed = data.AsDictionary;
            var declarations = values.Declarations;
            if (declarations is null)
                return null;
            
            foreach (var variableName in values.variableNames)
            {
                if (!declarations.IsDefined(variableName) || !parsed.TryGetValue(variableName, out var fsData))
                    continue;

                switch (fsData.Type)
                {
                    case fsDataType.Double:
                        declarations.Set(variableName, (float)fsData.AsDouble);
                        break;
                    case fsDataType.Int64:
                        declarations.Set(variableName, (int)fsData.AsInt64);
                        break;
                    case fsDataType.Boolean:
                        declarations.Set(variableName, fsData.AsBool);
                        break;
                    case fsDataType.String:
                        declarations.Set(variableName, fsData.AsString);
                        break;
                    case fsDataType.Null:
                        declarations.Set(variableName, null);
                        break;
                }
            }

            return values;
        }

        public override void WriteData(CloudDataRecordUpsertForm form)
        {
            var dict = new Dictionary<string, fsData>();
            var declarations = values.Declarations;
            if (declarations is null)
                return;

            foreach (var variableName in values.variableNames)
            {
                switch (declarations.Get(variableName))
                {
                    case int intValue:
                        dict.Add(variableName, new fsData(intValue));
                        break;
                    case float floatValue:
                        dict.Add(variableName, new fsData(floatValue));
                        break;
                    case bool boolValue:
                        dict.Add(variableName, new fsData(boolValue));
                        break;
                    case string stringValue:
                        dict.Add(variableName, new fsData(stringValue));
                        break;
                    case null:
                        dict.Add(variableName, new fsData());
                        break;
                }
            }
            
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream);
            fsJsonPrinter.CompressedJson(new fsData(dict), writer);
            writer.Flush();
            
            form.StringValue = Convert.ToBase64String(stream.ToArray());
        }
    }
}