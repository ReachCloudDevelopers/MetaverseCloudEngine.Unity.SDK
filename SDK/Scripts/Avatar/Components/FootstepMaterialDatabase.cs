using System;
using System.Collections.Generic;
using System.Linq;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity
{
    [HideMonoScript]
    public class FootstepMaterialDatabase : TriInspectorMonoBehaviour
    {
        private static FootstepMaterialDatabase _instance;
        private static FootstepMaterialDatabase Instance
        {
            get
            {
                if (_instance)
                    return _instance;
                _instance = FindObjectOfType<FootstepMaterialDatabase>();
                return _instance ? _instance : null;
            }
        }

        /// <summary>
        /// A record within the material database.
        /// </summary>
        [Serializable]
        public class FootstepMaterialDatabaseRecord
        {
            public string name;
            
            [Header("Lookup")] 
            public Material[] materials;
            public Texture2D[] textures;

            [Header("Audio")]
            public AudioClip[] audioClips;
        }

        [Tooltip("The records within the material database.")] [SerializeField]
        private FootstepMaterialDatabaseRecord[] records;

        private Dictionary<string, FootstepMaterialDatabaseRecord> _database = new();
        private Dictionary<Material, FootstepMaterialDatabaseRecord> _materialLookup = new();
        private Dictionary<Texture2D, FootstepMaterialDatabaseRecord> _textureLookup = new();
        private static readonly RaycastHit[] s_HitsNonAlloc = new RaycastHit[16];

        public static FootstepMaterialDatabaseRecord[] Records =>
            Instance ? Instance.records : Array.Empty<FootstepMaterialDatabaseRecord>();

        private void OnValidate()
        {
            InitializeDictionaryInternal();
        }

        private void Awake()
        {
            InitializeDictionaryInternal();
        }

        private void InitializeDictionaryInternal()
        {
            if (records == null || records.Length == 0)
                return;

            if (Application.isPlaying)
            {
                foreach (var rec in records)
                {
                    if (rec == null) continue;
                    rec.materials = rec.materials.Where(x => x).ToArray();
                }
            }

            MakeSureNamesAreUnique();
            MakeSureNoDuplicateTexturesOrMaterials();

            _database = records.ToDictionary(x => x.name, y => y);
            _materialLookup = records.SelectMany(x => x.materials.Select(y => new { y, x }))
                .ToDictionary(x => x.y, y => y.x);
            _textureLookup = records.SelectMany(x => x.textures.Select(y => new { y, x }))
                .ToDictionary(x => x.y, y => y.x);
        }

        private void MakeSureNoDuplicateTexturesOrMaterials()
        {
            var textures = new HashSet<Texture2D>();
            var materials = new HashSet<Material>();
            foreach (var rec in records)
            {
                if (rec == null) continue;
                if (rec.textures != null)
                {
                    rec.textures = rec.textures.Where(x => x).ToArray();
                    foreach (var tex in rec.textures)
                    {
                        if (textures.Contains(tex))
                        {
                            Debug.LogWarning($"Texture {tex.name} is used in multiple records.");
                            rec.textures = rec.textures.Where(x => x != tex).ToArray();
                        }
                        else
                            textures.Add(tex);
                    }
                }

                if (rec.materials != null)
                {
                    rec.materials = rec.materials.Where(x => x).ToArray();
                    foreach (var mat in rec.materials)
                    {
                        if (materials.Contains(mat))
                        {
                            Debug.LogWarning($"Material {mat.name} is used in multiple records.");
                            rec.materials = rec.materials.Where(x => x != mat).ToArray();
                        }
                        else
                            materials.Add(mat);
                    }
                }
            }
        }

        private void MakeSureNamesAreUnique()
        {
            var names = new HashSet<string>();
            var counter = new Dictionary<string, int>();
            foreach (var rec in records)
            {
                if (rec == null) continue;
                if (names.Contains(rec.name))
                {
                    counter.TryAdd(rec.name, 1);
                    rec.name += $" ({counter[rec.name]++})";
                }
                else
                {
                    names.Add(rec.name);
                }
            }
        }

        public static void DeleteRecord(string recordName)
        {
            if (!Instance)
                return;

            if (!TryFind(recordName, out var record))
                return;

            Instance.records = Instance.records.Where(x => x != record).ToArray();
            Instance.InitializeDictionaryInternal();
        }

        public static void DeleteRecord(Material material)
        {
            // Delete the material from all available records.
            foreach (var record in Records)
            {
                if (record.materials == null || record.materials.Length == 0)
                    continue;

                record.materials = record.materials.Where(x => x != material).ToArray();
            }

            Instance.InitializeDictionaryInternal();
        }

        /// <summary>
        /// Registers a material to a record. If the record does not exist, it will be created.
        /// </summary>
        /// <param name="recordName">The name of the record.</param>
        /// <param name="material">The material to register.</param>
        public static void UpsertMaterial(string recordName, Material material)
        {
            if (!Instance)
                return;

            if (!TryFind(recordName, out var record))
            {
                record = new FootstepMaterialDatabaseRecord
                {
                    name = recordName,
                    materials = new[] { material }
                };
                DeleteRecord(material);
                AddRecord(record);
                return;
            }

            if (record.materials.Contains(material))
                return;

            DeleteRecord(material);
            record.materials = record.materials.Append(material).ToArray();
            Instance.InitializeDictionaryInternal();
        }

        /// <summary>
        /// Registers a texture to a record. If the record does not exist, it will be created.
        /// </summary>
        /// <param name="recordName">The name of the record.</param>
        /// <param name="texture">The texture to register.</param>
        public static void UpsertTexture(string recordName, Texture2D texture)
        {
            if (!Instance)
                return;

            if (!TryFind(recordName, out var record))
            {
                record = new FootstepMaterialDatabaseRecord
                {
                    name = recordName,
                    textures = new[] { texture }
                };

                AddRecord(record);
                return;
            }

            if (record.textures.Contains(texture))
                return;

            record.textures = record.textures.Append(texture).ToArray();
            Instance.InitializeDictionaryInternal();
        }

        /// <summary>
        /// Shoots a ray and returns the first material database record hit.
        /// </summary>
        /// <param name="ray">The ray to shoot.</param>
        /// <param name="hitInfo">The hit info of the ray.</param>
        /// <param name="maxDistance">The maximum distance to shoot the ray.</param>
        /// <param name="layerMask">The layer mask to use.</param>
        /// <param name="record">The record that was hit.</param>
        /// <param name="triggerInteraction">The trigger interaction to use.</param>
        /// <returns>True if a record was hit, false otherwise.</returns>
        public static bool Query(
            Ray ray,
            out FootstepMaterialDatabaseRecord record,
            out RaycastHit hitInfo,
            float maxDistance,
            LayerMask layerMask,
            QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.UseGlobal)
        {
            if (!Instance)
            {
                record = null;
                hitInfo = default;
                return false;
            }

            var hits = Physics.RaycastNonAlloc(ray, s_HitsNonAlloc, maxDistance, layerMask, triggerInteraction);
            if (hits > 0)
            {
                for (var i = 0; i < hits; i++)
                {
                    hitInfo = s_HitsNonAlloc[i];
                    if (hitInfo.collider.TryGetComponent(out Renderer ren))
                    {
                        var mat = ren.sharedMaterial;
                        if (TryFind(mat, out record))
                            return true;
                    }

                    if (hitInfo.collider.TryGetComponent(out Terrain terrain))
                    {
                        var textureAtPoint = GetTextureAtPoint(terrain, hitInfo.point);
                        if (TryFind(textureAtPoint, out record))
                            return true;
                    }
                }
            }

            record = null;
            hitInfo = default;
            return false;
        }

        /// <summary>
        /// Attempts to find a record by material.
        /// </summary>
        /// <param name="material">The material to find.</param>
        /// <param name="record">The record that was found.</param>
        /// <returns>True if a record was found, false otherwise.</returns>
        public static bool TryFind(Material material, out FootstepMaterialDatabaseRecord record)
        {
            if (!Instance || !material)
            {
                record = null;
                return false;
            }

            return Instance._materialLookup.TryGetValue(material, out record);
        }

        /// <summary>
        /// Attempts to find a record by texture.
        /// </summary>
        /// <param name="texture">The texture to find.</param>
        /// <param name="record">The record that was found.</param>
        /// <returns>True if a record was found, false otherwise.</returns>
        public static bool TryFind(Texture2D texture, out FootstepMaterialDatabaseRecord record)
        {
            if (!Instance || !texture)
            {
                record = null;
                return false;
            }

            return Instance._textureLookup.TryGetValue(texture, out record);
        }

        /// <summary>
        /// Attempts to find a record by name.
        /// </summary>
        /// <param name="recordName">The name of the record to find.</param>
        /// <param name="record">The record that was found.</param>
        /// <returns>True if a record was found, false otherwise.</returns>
        public static bool TryFind(string recordName, out FootstepMaterialDatabaseRecord record)
        {
            if (!Instance || string.IsNullOrEmpty(recordName))
            {
                record = null;
                return false;
            }

            return Instance._database.TryGetValue(recordName, out record);
        }

        private static void AddRecord(FootstepMaterialDatabaseRecord record)
        {
            if (!Instance)
                return;

            var list = Instance.records.ToList();
            list.Add(record);
            Instance.records = list.ToArray();
            Instance.InitializeDictionaryInternal();
        }

        private static Texture2D GetTextureAtPoint(Terrain terrain, Vector3 worldPoint)
        {
            var terrainData = terrain.terrainData;
            var terrainPos = terrain.transform.position;
            var terrainLocalPos = worldPoint - terrainPos;
            var normalizedPos = new Vector3(terrainLocalPos.x / terrainData.size.x, 0,
                terrainLocalPos.z / terrainData.size.z);
            var terrainX = (int)(normalizedPos.x * terrainData.alphamapWidth);
            var terrainZ = (int)(normalizedPos.z * terrainData.alphamapHeight);
            var splatmapData = terrainData.GetAlphamaps(terrainX, terrainZ, 1, 1);
            var textureIndex = 0;
            var highestOpacity = 0f;
            for (var i = 0; i < splatmapData.Length; i++)
            {
                if (!(splatmapData[i, 0, 0] > highestOpacity))
                    continue;
                textureIndex = i;
                highestOpacity = splatmapData[i, 0, 0];
            }

            if (textureIndex >= terrainData.terrainLayers.Length)
                return null;
            var terrainSplat = terrainData.terrainLayers[textureIndex].diffuseTexture;
            return terrainSplat;
        }
    }
}