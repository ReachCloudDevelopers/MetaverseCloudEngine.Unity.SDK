using System.Linq;
using UnityEditor;
using UnityEngine;
using MetaverseCloudEngine.Unity.Scripting.Components;
using UnityEngine.UIElements;

namespace MetaverseCloudEngine.Unity.Editors
{
    /// <summary>
    /// Handles drag and drop of JavaScript files onto GameObjects in the Scene View
    /// to automatically add MetaverseScript components.
    /// </summary>
    [InitializeOnLoad]
    internal static class JavaScriptDragDropHandler
    {
        private static readonly System.Collections.Generic.HashSet<EditorWindow> HookedInspectors = new();
        private static bool _inspectorDragActive;
        private static Vector2 _inspectorLastPos;
        
        static JavaScriptDragDropHandler()
        {
            // Subscribe to Scene View GUI events
            SceneView.duringSceneGui += OnSceneGUI;
            // Hook inspector UIElements callbacks
            EditorApplication.delayCall += EnsureInspectorHooks;
            EditorApplication.update += EnsureInspectorHooks;
        }


        private static void OnSceneGUI(SceneView sceneView)
        {
            HandleDragAndDrop();
        }

        private static void EnsureInspectorHooks()
        {
            var inspectorType = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            if (inspectorType == null) return;
            var inspectors = Resources.FindObjectsOfTypeAll(inspectorType);
            foreach (var obj in inspectors)
            {
                if (obj is not EditorWindow win) continue;
                if (HookedInspectors.Contains(win)) continue;

                var root = win.rootVisualElement;
                if (root == null) continue;

                root.RegisterCallback<MouseUpEvent>(OnInspectorMouseUp, TrickleDown.TrickleDown);
                root.RegisterCallback<PointerUpEvent>(OnInspectorPointerUp, TrickleDown.TrickleDown);
                root.RegisterCallback<DragEnterEvent>(OnInspectorDragEnter, TrickleDown.TrickleDown);
                root.RegisterCallback<DragLeaveEvent>(OnInspectorDragLeave, TrickleDown.TrickleDown);
                root.RegisterCallback<DragExitedEvent>(OnInspectorDragExited, TrickleDown.TrickleDown);
                root.RegisterCallback<DragUpdatedEvent>(OnInspectorDragUpdated, TrickleDown.TrickleDown);
                root.RegisterCallback<DragPerformEvent>(OnInspectorDragPerform, TrickleDown.TrickleDown);

                HookedInspectors.Add(win);
            }
        }

        private static void OnInspectorMouseUp(MouseUpEvent e) { }
        private static void OnInspectorPointerUp(PointerUpEvent e) { }
        
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
            // Fallback: Some Unity versions never fire DragPerform for Inspector.
            // If we were actively dragging JS over the Inspector, treat exit as perform.
            if (_inspectorDragActive && HasJavaScriptInDrag())
            {
                _inspectorDragActive = false;
                var js = ResolveJsFromDrag();
                EditorApplication.delayCall += () => AssignJsToSelected(js);
            }
        }

        private static void OnInspectorDragUpdated(DragUpdatedEvent e)
        {
            _inspectorDragActive = true;
            _inspectorLastPos = e.mousePosition;
            if (!HasJavaScriptInDrag()) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            e.StopPropagation();
        }

        private static void OnInspectorDragPerform(DragPerformEvent e)
        {
            if (!HasJavaScriptInDrag()) return;
            var js = ResolveJsFromDrag();
            if (!js) return;
            DragAndDrop.AcceptDrag();
            _inspectorDragActive = false;
            EditorApplication.delayCall += () => AssignJsToSelected(js);
            e.StopPropagation();
        }

        private static TextAsset ResolveJsFromDrag()
        {
            // From object refs first
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is TextAsset ta && AssetDatabase.GetAssetPath(ta).EndsWith(".js"))
                    return ta;
            }
            // Then from paths
            if (DragAndDrop.paths is { Length: > 0 })
            {
                foreach (var p in DragAndDrop.paths)
                {
                    if (!string.IsNullOrEmpty(p) && p.EndsWith(".js"))
                    {
                        var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(p);
                        if (ta) return ta;
                    }
                }
            }
            return null;
        }

        private static void AssignJsToSelected(TextAsset jsAsset)
        {
            if (!jsAsset) return;
            var targetGameObject = Selection.activeGameObject;
            if (!targetGameObject) return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Add MetaverseScript (Drag & Drop)");
            int group = Undo.GetCurrentGroup();

            var script = targetGameObject.GetComponent<MetaverseScript>();
            if (!script)
                script = Undo.AddComponent<MetaverseScript>(targetGameObject);

            Undo.RecordObject(script, "Assign JavaScript File");
            script.javascriptFile = jsAsset;
            EditorUtility.SetDirty(script);
            EditorUtility.SetDirty(targetGameObject);

            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = targetGameObject;
        }

        private static bool HasJavaScriptInDrag()
        {
            var refs = DragAndDrop.objectReferences;
            if (refs == null || refs.Length == 0) return false;
            foreach (var r in refs)
                if (r is TextAsset ta && AssetDatabase.GetAssetPath(ta).EndsWith(".js"))
                    return true;
            return false;
        }

        private static bool IsMouseOverInspector(Vector2 mousePosition)
        {
            // Retained for possible future IMGUI paths; not used with UIElements callbacks
            var inspectorType = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            var inspectors = Resources.FindObjectsOfTypeAll(inspectorType);
            foreach (var obj in inspectors)
                if (obj is EditorWindow w && w.position.Contains(mousePosition))
                    return true;
            return false;
        }


        private static void HandleDragAndDrop()
        {
            Event evt = Event.current;

            // Only handle drag events
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
                return;

            // Check if any of the dragged objects are JavaScript files
            bool hasJavaScriptFile = false;
            foreach (Object draggedObject in DragAndDrop.objectReferences)
            {
                if (draggedObject is TextAsset textAsset)
                {
                    string assetPath = AssetDatabase.GetAssetPath(textAsset);
                    if (assetPath.EndsWith(".js"))
                    {
                        hasJavaScriptFile = true;
                        break;
                    }
                }
            }

            if (!hasJavaScriptFile)
                return;

            // Set visual mode to show copy cursor
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            // Handle the drop
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();

                // Get the GameObject under the mouse cursor
                GameObject targetGameObject = GetGameObjectUnderMouse();
                
                if (targetGameObject != null)
                {
                    // Check if MetaverseScript component already exists
                    MetaverseScript existingScript = targetGameObject.GetComponent<MetaverseScript>();
                    
                    foreach (Object draggedObject in DragAndDrop.objectReferences)
                    {
                        if (draggedObject is TextAsset textAsset && 
                            AssetDatabase.GetAssetPath(textAsset).EndsWith(".js"))
                        {
                            if (existingScript == null)
                            {
                                // Add new MetaverseScript component
                                existingScript = targetGameObject.AddComponent<MetaverseScript>();
                            }
                            
                            // Assign the JavaScript file
                            existingScript.javascriptFile = textAsset;
                            
                            // Mark the object as dirty so Unity saves the changes
                            EditorUtility.SetDirty(targetGameObject);
                            
                            // Break after first JavaScript file to avoid multiple assignments
                            break;
                        }
                    }
                    
                    // Select the target GameObject to show the new component in inspector
                    Selection.activeGameObject = targetGameObject;
                }
            }

            evt.Use();
        }

        private static GameObject GetGameObjectUnderMouse()
        {
            // Get the mouse position in screen coordinates
            Vector2 mousePosition = Event.current.mousePosition;
            
            // Convert to world ray
            Camera sceneCamera = SceneView.lastActiveSceneView?.camera;
            if (sceneCamera == null)
                return null;

            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            
            // Perform raycast
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                return hit.collider.gameObject;
            }
            
            // If no physics raycast hit, try to find objects by checking all GameObjects
            // This is a fallback for objects without colliders
            GameObject[] allObjects = Object.FindObjectsOfType<GameObject>();
            
            GameObject closestObject = null;
            float closestDistance = float.MaxValue;
            
            foreach (GameObject obj in allObjects)
            {
                // Skip inactive objects
                if (!obj.activeInHierarchy)
                    continue;
                    
                // Check if mouse is over the object's bounds
                Bounds bounds = GetObjectBounds(obj);
                
                if (bounds.Contains(sceneCamera.WorldToScreenPoint(bounds.center)))
                {
                    float distance = Vector3.Distance(sceneCamera.transform.position, bounds.center);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestObject = obj;
                    }
                }
            }
            
            return closestObject;
        }

        private static Bounds GetObjectBounds(GameObject obj)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                return renderer.bounds;
            }
            
            // Fallback to transform position with a small bounds
            return new Bounds(obj.transform.position, Vector3.one * 0.1f);
        }
    }

}
