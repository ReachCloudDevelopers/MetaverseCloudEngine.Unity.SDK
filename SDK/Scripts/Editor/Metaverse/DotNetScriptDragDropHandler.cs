using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using MetaverseCloudEngine.Unity.Scripting.Components;

namespace MetaverseCloudEngine.Unity.Editors
{
    [InitializeOnLoad]
    internal static class DotNetScriptDragDropHandler
    {
        private static readonly HashSet<EditorWindow> HookedInspectors = new();
        private static bool _inspectorDragActive;

        static DotNetScriptDragDropHandler()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.delayCall += EnsureInspectorHooks;
            EditorApplication.update += EnsureInspectorHooks;
        }

        private static void OnSceneGUI(SceneView view)
        {
            HandleSceneDragAndDrop();
        }

        private static void EnsureInspectorHooks()
        {
            var inspectorType = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            if (inspectorType == null)
                return;

            var inspectors = Resources.FindObjectsOfTypeAll(inspectorType);
            foreach (var obj in inspectors)
            {
                if (obj is not EditorWindow win)
                    continue;
                if (HookedInspectors.Contains(win))
                    continue;

                var root = win.rootVisualElement;
                if (root == null)
                    continue;

                root.RegisterCallback<DragEnterEvent>(OnInspectorDragEnter, TrickleDown.TrickleDown);
                root.RegisterCallback<DragLeaveEvent>(OnInspectorDragLeave, TrickleDown.TrickleDown);
                root.RegisterCallback<DragExitedEvent>(OnInspectorDragExited, TrickleDown.TrickleDown);
                root.RegisterCallback<DragUpdatedEvent>(OnInspectorDragUpdated, TrickleDown.TrickleDown);
                root.RegisterCallback<DragPerformEvent>(OnInspectorDragPerform, TrickleDown.TrickleDown);

                HookedInspectors.Add(win);
            }
        }

        private static void OnInspectorDragEnter(DragEnterEvent e)
        {
            _inspectorDragActive = true;
        }

        private static void OnInspectorDragLeave(DragLeaveEvent e)
        {
            _inspectorDragActive = false;
        }

        private static void OnInspectorDragExited(DragExitedEvent e)
        {
            if (!_inspectorDragActive || !HasDotNetScriptInDrag())
                return;

            _inspectorDragActive = false;
            var scriptPath = ResolveScriptPathFromDrag();
            if (!string.IsNullOrEmpty(scriptPath))
                EditorApplication.delayCall += () => AssignDotNetToSelected(scriptPath);
        }

        private static void OnInspectorDragUpdated(DragUpdatedEvent e)
        {
            _inspectorDragActive = true;
            if (!HasDotNetScriptInDrag())
                return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            e.StopPropagation();
        }

        private static void OnInspectorDragPerform(DragPerformEvent e)
        {
            if (!HasDotNetScriptInDrag())
                return;

            var scriptPath = ResolveScriptPathFromDrag();
            if (string.IsNullOrEmpty(scriptPath))
                return;

            DragAndDrop.AcceptDrag();
            _inspectorDragActive = false;
            EditorApplication.delayCall += () => AssignDotNetToSelected(scriptPath);
            e.StopPropagation();
        }

        private static bool HasDotNetScriptInDrag()
        {
            // Only treat the drag as a Metaverse .NET script drag if at least one of the
            // dragged C# files actually declares a MetaverseDotNetScriptBase/IMetaverseDotNetScript
            // type. This avoids hijacking Unity's default drag-and-drop behaviour for
            // arbitrary C# scripts.
            return TryGetMetaverseDotNetScriptPathFromDrag(out _);
        }

        private static string ResolveScriptPathFromDrag()
        {
            return TryGetMetaverseDotNetScriptPathFromDrag(out var scriptPath) ? scriptPath : null;
        }

        /// <summary>
        /// Returns true if the current drag contains a C# file that declares a Metaverse
        /// .NET script (class deriving from MetaverseDotNetScriptBase or implementing
        /// IMetaverseDotNetScript) and outputs its asset path.
        /// </summary>
        private static bool TryGetMetaverseDotNetScriptPathFromDrag(out string scriptPath)
        {
            scriptPath = null;

            var refs = DragAndDrop.objectReferences;
            if (refs != null && refs.Length > 0)
            {
                foreach (var r in refs)
                {
                    var path = AssetDatabase.GetAssetPath(r);
                    if (IsMetaverseDotNetScriptSource(path))
                    {
                        scriptPath = path;
                        return true;
                    }
                }
            }

            var paths = DragAndDrop.paths;
            if (paths != null && paths.Length > 0)
            {
                foreach (var p in paths)
                {
                    if (IsMetaverseDotNetScriptSource(p))
                    {
                        scriptPath = p;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsMetaverseDotNetScriptSource(string scriptAssetPath)
        {
            if (string.IsNullOrEmpty(scriptAssetPath) || !scriptAssetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                return false;

            // Use the same detection logic as the MetaverseDotNetScriptEditor: we only
            // care about scripts that declare a class deriving from MetaverseDotNetScriptBase
            // or implementing IMetaverseDotNetScript.
            var typeName = MetaverseDotNetScriptEditor.TryExtractDotNetScriptTypeNameFromSource(scriptAssetPath);
            return !string.IsNullOrEmpty(typeName);
        }

        private static void AssignDotNetToSelected(string scriptAssetPath)
        {
            var go = Selection.activeGameObject;
            if (!go)
                return;

            AssignDotNetToGameObject(go, scriptAssetPath);
        }

        private static void AssignDotNetToGameObject(GameObject go, string scriptAssetPath)
        {
            if (!go || string.IsNullOrEmpty(scriptAssetPath))
                return;

            if (!MetaverseDotNetScriptEditor.TryConfigureFromScriptPath(scriptAssetPath, out var assemblyAsset, out var fullTypeName))
                return;

            // Match JavaScript behaviour:
            // 1. Prefer an existing MetaverseDotNetScript with no assembly assigned (a "blank" slot).
            // 2. If none exists, ALWAYS add a new MetaverseDotNetScript component.
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Add Metaverse .NET Script (Drag & Drop)");
            var group = Undo.GetCurrentGroup();

            var scripts = go.GetComponents<MetaverseDotNetScript>();
            MetaverseDotNetScript target = null;

            if (scripts != null && scripts.Length > 0)
            {
                foreach (var s in scripts)
                {
                    if (s != null && s.AssemblyAsset == null)
                    {
                        target = s;
                        break;
                    }
                }
            }

            if (target == null)
            {
                target = Undo.AddComponent<MetaverseDotNetScript>(go);
            }

            if (target == null)
            {
                Debug.LogError("[METAVERSE_DOTNET_SCRIPT] Failed to create or find MetaverseDotNetScript component for drag-and-drop.");
                Undo.CollapseUndoOperations(group);
                return;
            }

            Undo.RecordObject(target, "Assign Metaverse .NET Script");
            target.AssemblyAsset = assemblyAsset;
            target.ClassName = fullTypeName;
            EditorUtility.SetDirty(target);
            EditorUtility.SetDirty(go);

            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = go;
        }

        private static void HandleSceneDragAndDrop()
        {
            var evt = Event.current;
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
                return;

            if (!HasDotNetScriptInDrag())
                return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                var scriptPath = ResolveScriptPathFromDrag();
                if (!string.IsNullOrEmpty(scriptPath))
                {
                    var go = GetGameObjectUnderMouse();
                    if (go)
                        AssignDotNetToGameObject(go, scriptPath);
                }
            }

            evt.Use();
        }

        private static GameObject GetGameObjectUnderMouse()
        {
            var sceneView = SceneView.lastActiveSceneView;
            var camera = sceneView != null ? sceneView.camera : null;
            if (!camera)
                return null;

            var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            return Physics.Raycast(ray, out var hit) ? hit.collider.gameObject : null;
        }
    }
}

