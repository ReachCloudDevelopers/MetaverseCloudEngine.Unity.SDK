// This was written by the folks at Exit Games for PUN 2, so all rights and credits to them.
// It should be noted that this class will ONLY serve as a wrapper class, and it has been modified slightly.
using System.Collections.Generic;
using MetaverseCloudEngine.Unity.Attributes;
using TriInspectorMVCE;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MetaverseCloudEngine.Unity.Networking.Components
{
    /// <summary>
    ///     Represents the cull area used for network culling.
    /// </summary>
    [Experimental]
    [HideMonoScript]
    [DefaultExecutionOrder(ExecutionOrder.PreInitialization)]
    public class NetworkCullingArea : TriInspectorMonoBehaviour
    {
        private const int MAX_NUMBER_OF_ALLOWED_CELLS = 250;

        public const int MAX_NUMBER_OF_SUBDIVISIONS = 3;

        /// <summary>
        ///     This represents the first ID which is assigned to the first created cell.
        ///     If you already have some interest groups blocking this first ID, fell free to change it.
        ///     However increasing the first group ID decreases the maximum amount of allowed cells.
        ///     Allowed values are in range from 1 to 250.
        /// </summary>
        public readonly byte FIRST_GROUP_ID = 1;

        /// <summary>
        ///     This represents the order in which updates are sent. 
        ///     The number represents the subdivision of the cell hierarchy:
        ///     - 0: message is sent to all players
        ///     - 1: message is sent to players who are interested in the matching cell of the first subdivision
        ///     If there is only one subdivision we are sending one update to all players
        ///     before sending three consequent updates only to players who are in the same cell
        ///     or interested in updates of the current cell.
        /// </summary>
        public readonly int[] SUBDIVISION_FIRST_LEVEL_ORDER = new int[4] { 0, 1, 1, 1 };

        /// <summary>
        ///     This represents the order in which updates are sent.
        ///     The number represents the subdivision of the cell hierarchy:
        ///     - 0: message is sent to all players
        ///     - 1: message is sent to players who are interested in the matching cell of the first subdivision
        ///     - 2: message is sent to players who are interested in the matching cell of the second subdivision
        ///     If there are two subdivisions we are sending every second update only to players
        ///     who are in the same cell or interested in updates of the current cell.
        /// </summary>
        public readonly int[] SUBDIVISION_SECOND_LEVEL_ORDER = new int[8] { 0, 2, 1, 2, 0, 2, 1, 2 };

        /// <summary>
        ///     This represents the order in which updates are sent.
        ///     The number represents the subdivision of the cell hierarchy:
        ///     - 0: message is sent to all players
        ///     - 1: message is sent to players who are interested in the matching cell of the first subdivision
        ///     - 2: message is sent to players who are interested in the matching cell of the second subdivision
        ///     - 3: message is sent to players who are interested in the matching cell of the third subdivision
        ///     If there are two subdivisions we are sending every second update only to players
        ///     who are in the same cell or interested in updates of the current cell.
        /// </summary>
        public readonly int[] SUBDIVISION_THIRD_LEVEL_ORDER = new int[12] { 0, 3, 2, 3, 1, 3, 2, 3, 1, 3, 2, 3 };

        public Vector2 Center;
        public Vector2 Size = new Vector2(25.0f, 25.0f);
        public Vector2[] Subdivisions = new Vector2[MAX_NUMBER_OF_SUBDIVISIONS];
        public int NumberOfSubdivisions;
        public bool YIsUpAxis = false;
        public bool RecreateCellHierarchy = false;

        public int CellCount { get; private set; }
        public CellTree CellTree { get; private set; }
        public Dictionary<int, GameObject> Map { get; private set; }
        public static NetworkCullingArea Instance { get; private set; }

        private byte idCounter;

        /// <summary>
        ///     Creates the cell hierarchy at runtime.
        /// </summary>
        private void Awake()
        {
            Instance = this;
            
            this.idCounter = this.FIRST_GROUP_ID;

            this.CreateCellHierarchy();
        }

        /// <summary>
        ///     Creates the cell hierarchy in editor and draws the cell view.
        /// </summary>
        public void OnDrawGizmos()
        {
            this.idCounter = this.FIRST_GROUP_ID;

            if (this.RecreateCellHierarchy)
            {
                this.CreateCellHierarchy();
            }

            this.DrawCells();
        }

        /// <summary>
        ///     Creates the cell hierarchy.
        /// </summary>
        private void CreateCellHierarchy()
        {
            if (!this.IsCellCountAllowed())
            {
                if (Debug.isDebugBuild)
                {
                    Debug.LogError("There are too many cells created by your subdivision options. Maximum allowed number of cells is " + (MAX_NUMBER_OF_ALLOWED_CELLS - this.FIRST_GROUP_ID) +
                                   ". Current number of cells is " + this.CellCount + ".");
                    return;
                }
                else
                {
                    Application.Quit();
                }
            }

            CellTreeNode rootNode = new CellTreeNode(this.idCounter++, CellTreeNode.ENodeType.Root, null);

            if (this.YIsUpAxis)
            {
                this.Center = new Vector2(transform.position.x, transform.position.y);
                this.Size = new Vector2(transform.localScale.x, transform.localScale.y);

                rootNode.Center = new Vector3(this.Center.x, this.Center.y, 0.0f);
                rootNode.Size = new Vector3(this.Size.x, this.Size.y, 0.0f);
                rootNode.TopLeft = new Vector3((this.Center.x - (this.Size.x / 2.0f)), (this.Center.y - (this.Size.y / 2.0f)), 0.0f);
                rootNode.BottomRight = new Vector3((this.Center.x + (this.Size.x / 2.0f)), (this.Center.y + (this.Size.y / 2.0f)), 0.0f);
            }
            else
            {
                this.Center = new Vector2(transform.position.x, transform.position.z);
                this.Size = new Vector2(transform.localScale.x, transform.localScale.z);

                rootNode.Center = new Vector3(this.Center.x, 0.0f, this.Center.y);
                rootNode.Size = new Vector3(this.Size.x, 0.0f, this.Size.y);
                rootNode.TopLeft = new Vector3((this.Center.x - (this.Size.x / 2.0f)), 0.0f, (this.Center.y - (this.Size.y / 2.0f)));
                rootNode.BottomRight = new Vector3((this.Center.x + (this.Size.x / 2.0f)), 0.0f, (this.Center.y + (this.Size.y / 2.0f)));
            }

            this.CreateChildCells(rootNode, 1);

            this.CellTree = new CellTree(rootNode);

            this.RecreateCellHierarchy = false;
        }

        /// <summary>
        ///     Creates all child cells.
        /// </summary>
        /// <param name="parent">The current parent node.</param>
        /// <param name="cellLevelInHierarchy">The cell level within the current hierarchy.</param>
        private void CreateChildCells(CellTreeNode parent, int cellLevelInHierarchy)
        {
            if (cellLevelInHierarchy > this.NumberOfSubdivisions)
            {
                return;
            }

            int rowCount = (int)this.Subdivisions[(cellLevelInHierarchy - 1)].x;
            int columnCount = (int)this.Subdivisions[(cellLevelInHierarchy - 1)].y;

            float startX = parent.Center.x - (parent.Size.x / 2.0f);
            float width = parent.Size.x / rowCount;

            for (int row = 0; row < rowCount; ++row)
            {
                for (int column = 0; column < columnCount; ++column)
                {
                    float xPos = startX + (row * width) + (width / 2.0f);

                    CellTreeNode node = new CellTreeNode(this.idCounter++, (this.NumberOfSubdivisions == cellLevelInHierarchy) ? CellTreeNode.ENodeType.Leaf : CellTreeNode.ENodeType.Node, parent);

                    if (this.YIsUpAxis)
                    {
                        float startY = parent.Center.y - (parent.Size.y / 2.0f);
                        float height = parent.Size.y / columnCount;
                        float yPos = startY + (column * height) + (height / 2.0f);

                        node.Center = new Vector3(xPos, yPos, 0.0f);
                        node.Size = new Vector3(width, height, 0.0f);
                        node.TopLeft = new Vector3(xPos - (width / 2.0f), yPos - (height / 2.0f), 0.0f);
                        node.BottomRight = new Vector3(xPos + (width / 2.0f), yPos + (height / 2.0f), 0.0f);
                    }
                    else
                    {
                        float startZ = parent.Center.z - (parent.Size.z / 2.0f);
                        float depth = parent.Size.z / columnCount;
                        float zPos = startZ + (column * depth) + (depth / 2.0f);

                        node.Center = new Vector3(xPos, 0.0f, zPos);
                        node.Size = new Vector3(width, 0.0f, depth);
                        node.TopLeft = new Vector3(xPos - (width / 2.0f), 0.0f, zPos - (depth / 2.0f));
                        node.BottomRight = new Vector3(xPos + (width / 2.0f), 0.0f, zPos + (depth / 2.0f));
                    }

                    parent.AddChild(node);

                    this.CreateChildCells(node, (cellLevelInHierarchy + 1));
                }
            }
        }

        /// <summary>
        ///     Draws the cells.
        /// </summary>
        private void DrawCells()
        {
            if ((this.CellTree != null) && (this.CellTree.RootNode != null))
            {
                this.CellTree.RootNode.Draw();
            }
            else
            {
                this.RecreateCellHierarchy = true;
            }
        }

        /// <summary>
        ///     Checks if the cell count is allowed.
        /// </summary>
        /// <returns>True if the cell count is allowed, false if the cell count is too large.</returns>
        private bool IsCellCountAllowed()
        {
            int horizontalCells = 1;
            int verticalCells = 1;

            foreach (Vector2 v in this.Subdivisions)
            {
                horizontalCells *= (int)v.x;
                verticalCells *= (int)v.y;
            }

            this.CellCount = horizontalCells * verticalCells;

            return (this.CellCount <= (MAX_NUMBER_OF_ALLOWED_CELLS - this.FIRST_GROUP_ID));
        }

        /// <summary>
        ///     Gets a list of all cell IDs the player is currently inside or nearby.
        /// </summary>
        /// <param name="position">The current position of the player.</param>
        /// <returns>A list containing all cell IDs the player is currently inside or nearby.</returns>
        public List<byte> GetActiveCells(Vector3 position)
        {
            List<byte> activeCells = new List<byte>(0);
            this.CellTree.RootNode.GetActiveCells(activeCells, this.YIsUpAxis, position);

            // it makes sense to sort the "nearby" cells. those are in the list in positions after the subdivisions the point is inside. 2 subdivisions result in 3 areas the point is in.
            int cellsActive = this.NumberOfSubdivisions + 1;
            int cellsNearby = activeCells.Count - cellsActive;
            if (cellsNearby > 0)
            {
                activeCells.Sort(cellsActive, cellsNearby, new ByteComparer());
            }
            return activeCells;
        }
    }

    /// <summary>
    ///     Represents the tree accessible from its root node.
    /// </summary>
    public class CellTree
    {
        /// <summary>
        ///     Represents the root node of the cell tree.
        /// </summary>
        public CellTreeNode RootNode { get; private set; }

        /// <summary>
        ///     Default constructor.
        /// </summary>
        public CellTree()
        {
        }

        /// <summary>
        ///     Constructor to define the root node.
        /// </summary>
        /// <param name="root">The root node of the tree.</param>
        public CellTree(CellTreeNode root)
        {
            this.RootNode = root;
        }
    }

    /// <summary>
    ///     Represents a single node of the tree.
    /// </summary>
    public class CellTreeNode
    {
        public enum ENodeType : byte
        {
            Root = 0,
            Node = 1,
            Leaf = 2
        }

        /// <summary>
        ///     Represents the unique ID of the cell.
        /// </summary>
        public byte Id;

        /// <summary>
        ///     Represents the center, top-left or bottom-right position of the cell
        ///     or the size of the cell.
        /// </summary>
        public Vector3 Center, Size, TopLeft, BottomRight;

        /// <summary>
        ///     Describes the current node type of the cell tree node.
        /// </summary>
        public ENodeType NodeType;

        /// <summary>
        ///     Reference to the parent node.
        /// </summary>
        public CellTreeNode Parent;

        /// <summary>
        ///     A list containing all child nodes.
        /// </summary>
        public List<CellTreeNode> Childs;

        /// <summary>
        ///     The max distance the player can have to the center of the cell for being 'nearby'.
        ///     This is calculated once at runtime.
        /// </summary>
        private float maxDistance;

        /// <summary>
        ///     Default constructor.
        /// </summary>
        public CellTreeNode()
        {
        }

        /// <summary>
        ///     Constructor to define the ID and the node type as well as setting a parent node.
        /// </summary>
        /// <param name="id">The ID of the cell is used as the interest group.</param>
        /// <param name="nodeType">The node type of the cell tree node.</param>
        /// <param name="parent">The parent node of the cell tree node.</param>
        public CellTreeNode(byte id, ENodeType nodeType, CellTreeNode parent)
        {
            this.Id = id;

            this.NodeType = nodeType;

            this.Parent = parent;
        }

        /// <summary>
        ///     Adds the given child to the node.
        /// </summary>
        /// <param name="child">The child which is added to the node.</param>
        public void AddChild(CellTreeNode child)
        {
            if (this.Childs == null)
            {
                this.Childs = new List<CellTreeNode>(1);
            }

            this.Childs.Add(child);
        }

        /// <summary>
        ///     Draws the cell in the editor.
        /// </summary>
        public void Draw()
        {
#if UNITY_EDITOR
        if (this.Childs != null)
        {
            foreach (CellTreeNode node in this.Childs)
            {
                node.Draw();
            }
        }

        Gizmos.color = new Color((this.NodeType == ENodeType.Root) ? 1 : 0, (this.NodeType == ENodeType.Node) ? 1 : 0, (this.NodeType == ENodeType.Leaf) ? 1 : 0);
        Gizmos.DrawWireCube(this.Center, this.Size);

        byte offset = (byte)this.NodeType;
        GUIStyle gs = new GUIStyle() { fontStyle = FontStyle.Bold };
        gs.normal.textColor = Gizmos.color;
        UnityEditor.Handles.Label(this.Center+(Vector3.forward*offset*1f), this.Id.ToString(), gs);
#endif
        }

        /// <summary>
        ///     Gathers all cell IDs the player is currently inside or nearby.
        /// </summary>
        /// <param name="activeCells">The list to add all cell IDs to the player is currently inside or nearby.</param>
        /// <param name="yIsUpAxis">Describes if the y-axis is used as up-axis.</param>
        /// <param name="position">The current position of the player.</param>
        public void GetActiveCells(List<byte> activeCells, bool yIsUpAxis, Vector3 position)
        {
            if (this.NodeType != ENodeType.Leaf)
            {
                foreach (CellTreeNode node in this.Childs)
                {
                    node.GetActiveCells(activeCells, yIsUpAxis, position);
                }
            }
            else
            {
                if (this.IsPointNearCell(yIsUpAxis, position))
                {
                    if (this.IsPointInsideCell(yIsUpAxis, position))
                    {
                        activeCells.Insert(0, this.Id);

                        CellTreeNode p = this.Parent;
                        while (p != null)
                        {
                            activeCells.Insert(0, p.Id);

                            p = p.Parent;
                        }
                    }
                    else
                    {
                        activeCells.Add(this.Id);
                    }
                }
            }
        }

        /// <summary>
        ///     Checks if the given point is inside the cell.
        /// </summary>
        /// <param name="yIsUpAxis">Describes if the y-axis is used as up-axis.</param>
        /// <param name="point">The point to check.</param>
        /// <returns>True if the point is inside the cell, false if the point is not inside the cell.</returns>
        public bool IsPointInsideCell(bool yIsUpAxis, Vector3 point)
        {
            if ((point.x < this.TopLeft.x) || (point.x > this.BottomRight.x))
            {
                return false;
            }

            if (yIsUpAxis)
            {
                if ((point.y >= this.TopLeft.y) && (point.y <= this.BottomRight.y))
                {
                    return true;
                }
            }
            else
            {
                if ((point.z >= this.TopLeft.z) && (point.z <= this.BottomRight.z))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Checks if the given point is near the cell.
        /// </summary>
        /// <param name="yIsUpAxis">Describes if the y-axis is used as up-axis.</param>
        /// <param name="point">The point to check.</param>
        /// <returns>True if the point is near the cell, false if the point is too far away.</returns>
        public bool IsPointNearCell(bool yIsUpAxis, Vector3 point)
        {
            if (this.maxDistance == 0.0f)
            {
                this.maxDistance = (this.Size.x + this.Size.y + this.Size.z) / 2.0f;
            }

            return ((point - this.Center).sqrMagnitude <= (this.maxDistance * this.maxDistance));
        }
    }

    public class ByteComparer : IComparer<byte>
    {
        /// <inheritdoc />
        public int Compare(byte x, byte y)
        {
            return x == y ? 0 : x < y ? -1 : 1;
        }
    }
    
#if UNITY_EDITOR
    [CanEditMultipleObjects]
    [CustomEditor(typeof(NetworkCullingArea))]
    public class NetworkCullAreaEditor : Editor
    {
        private bool _alignEditorCamera;
        private NetworkCullingArea _cullingArea;

        private enum UP_AXIS_OPTIONS
        {
            SideScrollerMode = 0,
            TopDownOr3DMode = 1
        }

        private UP_AXIS_OPTIONS upAxisOptions;

        public void OnEnable()
        {
            _cullingArea = (NetworkCullingArea)target;

            // Destroying the newly created cull area if there is already one existing
            if (FindObjectsOfType<NetworkCullingArea>().Length > 1)
            {
                Debug.LogWarning("Destroying newly created cull area because there is already one existing in the scene.");

                DestroyImmediate(_cullingArea);

                return;
            }

            // Prevents the dropdown from resetting
            if (_cullingArea != null)
            {
                upAxisOptions = _cullingArea.YIsUpAxis ? UP_AXIS_OPTIONS.SideScrollerMode : UP_AXIS_OPTIONS.TopDownOr3DMode;
            }
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.BeginVertical();

            if (Application.isEditor && !Application.isPlaying)
            {
                OnInspectorGUIEditMode();
            }
            else
            {
                OnInspectorGUIPlayMode();
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        ///     Represents the inspector GUI when edit mode is active.
        /// </summary>
        private void OnInspectorGUIEditMode()
        {
            EditorGUI.BeginChangeCheck();

            #region DEFINE_UP_AXIS

            {
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("Select game type", EditorStyles.boldLabel);
                upAxisOptions = (UP_AXIS_OPTIONS)EditorGUILayout.EnumPopup("Game type", upAxisOptions);
                _cullingArea.YIsUpAxis = (upAxisOptions == UP_AXIS_OPTIONS.SideScrollerMode);
                EditorGUILayout.EndVertical();
            }

            #endregion

            EditorGUILayout.Space();

            #region SUBDIVISION

            {
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("Set the number of subdivisions", EditorStyles.boldLabel);
                _cullingArea.NumberOfSubdivisions = EditorGUILayout.IntSlider("Number of subdivisions", _cullingArea.NumberOfSubdivisions, 0, NetworkCullingArea.MAX_NUMBER_OF_SUBDIVISIONS);
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space();

                if (_cullingArea.NumberOfSubdivisions != 0)
                {
                    for (int index = 0; index < _cullingArea.Subdivisions.Length; ++index)
                    {
                        if ((index + 1) <= _cullingArea.NumberOfSubdivisions)
                        {
                            string countMessage = (index + 1) + ". Subdivision: row / column count";

                            EditorGUILayout.BeginVertical();
                            _cullingArea.Subdivisions[index] = EditorGUILayout.Vector2Field(countMessage, _cullingArea.Subdivisions[index]);
                            EditorGUILayout.EndVertical();

                            EditorGUILayout.Space();
                        }
                        else
                        {
                            _cullingArea.Subdivisions[index] = new Vector2(1, 1);
                        }
                    }
                }
            }

            #endregion

            EditorGUILayout.Space();

            #region UPDATING_MAIN_CAMERA

            {
                EditorGUILayout.BeginVertical();

                EditorGUILayout.LabelField("View and camera options", EditorStyles.boldLabel);
                _alignEditorCamera = EditorGUILayout.Toggle("Automatically align editor view with grid", _alignEditorCamera);

                if (Camera.main != null)
                {
                    if (GUILayout.Button("Align main camera with grid"))
                    {
                        Undo.RecordObject(Camera.main.transform, "Align main camera with grid.");

                        float yCoord = _cullingArea.YIsUpAxis ? _cullingArea.Center.y : Mathf.Max(_cullingArea.Size.x, _cullingArea.Size.y);
                        float zCoord = _cullingArea.YIsUpAxis ? -Mathf.Max(_cullingArea.Size.x, _cullingArea.Size.y) : _cullingArea.Center.y;

                        Camera.main.transform.position = new Vector3(_cullingArea.Center.x, yCoord, zCoord);
                        Camera.main.transform.LookAt(_cullingArea.transform.position);
                    }

                    EditorGUILayout.LabelField("Current main camera position is " + Camera.main.transform.position.ToString());
                }

                EditorGUILayout.EndVertical();
            }

            #endregion

            if (EditorGUI.EndChangeCheck())
            {
                _cullingArea.RecreateCellHierarchy = true;

                AlignEditorView();
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
        }

        /// <summary>
        ///     Represents the inspector GUI when play mode is active.
        /// </summary>
        private void OnInspectorGUIPlayMode()
        {
            EditorGUILayout.LabelField("No changes allowed when game is running. Please exit play mode first.", EditorStyles.boldLabel);
        }

        public void OnSceneGUI()
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(Screen.width - 110, Screen.height - 90, 100, 60));

            if (GUILayout.Button("Reset position"))
            {
                _cullingArea.transform.position = Vector3.zero;
            }

            if (GUILayout.Button("Reset scaling"))
            {
                _cullingArea.transform.localScale = new Vector3(25.0f, 25.0f, 25.0f);
            }

            GUILayout.EndArea();
            Handles.EndGUI();

            // Checking for changes of the transform
            if (_cullingArea.transform.hasChanged)
            {
                // Resetting position
                float posX = _cullingArea.transform.position.x;
                float posY = _cullingArea.YIsUpAxis ? _cullingArea.transform.position.y : 0.0f;
                float posZ = !_cullingArea.YIsUpAxis ? _cullingArea.transform.position.z : 0.0f;

                _cullingArea.transform.position = new Vector3(posX, posY, posZ);

                // Resetting scaling
                if (_cullingArea.Size.x < 1.0f || _cullingArea.Size.y < 1.0f)
                {
                    float scaleX = (_cullingArea.transform.localScale.x < 1.0f) ? 1.0f : _cullingArea.transform.localScale.x;
                    float scaleY = (_cullingArea.transform.localScale.y < 1.0f) ? 1.0f : _cullingArea.transform.localScale.y;
                    float scaleZ = (_cullingArea.transform.localScale.z < 1.0f) ? 1.0f : _cullingArea.transform.localScale.z;

                    _cullingArea.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);

                    Debug.LogWarning("Scaling on a single axis can not be lower than 1. Resetting...");
                }

                _cullingArea.RecreateCellHierarchy = true;

                AlignEditorView();
            }
        }

        /// <summary>
        ///     Aligns the editor view with the created grid.
        /// </summary>
        private void AlignEditorView()
        {
            if (!_alignEditorCamera)
            {
                return;
            }

            // This creates a temporary game object in order to align the editor view.
            // The created game object is destroyed afterwards.
            GameObject tmpGo = new GameObject();

            float yCoord = _cullingArea.YIsUpAxis ? _cullingArea.Center.y : Mathf.Max(_cullingArea.Size.x, _cullingArea.Size.y);
            float zCoord = _cullingArea.YIsUpAxis ? -Mathf.Max(_cullingArea.Size.x, _cullingArea.Size.y) : _cullingArea.Center.y;

            tmpGo.transform.position = new Vector3(_cullingArea.Center.x, yCoord, zCoord);
            tmpGo.transform.LookAt(_cullingArea.transform.position);

            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.AlignViewToObject(tmpGo.transform);
            }

            DestroyImmediate(tmpGo);
        }
    }
#endif
}