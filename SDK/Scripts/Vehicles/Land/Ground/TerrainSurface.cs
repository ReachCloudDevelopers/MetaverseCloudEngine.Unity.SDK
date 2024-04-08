﻿using UnityEngine;

namespace MetaverseCloudEngine.Unity.Vehicles
{
    [RequireComponent(typeof(Terrain))]
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [AddComponentMenu(MetaverseConstants.MenuItems.MenuRootPath + "Vehicles/Land/Ground Surface/Vehicles - Terrain Surface", 2)]

    // Class for associating terrain textures with ground surface types
    public class TerrainSurface : MonoBehaviour
    {
        Transform tr;
        TerrainData terDat;
        float[,,] terrainAlphamap;
        public int[] surfaceTypes = new int[0];
        [System.NonSerialized]
        public float[] frictions;

        void Start() {
            tr = transform;
            if (GetComponent<Terrain>().terrainData) {
                terDat = GetComponent<Terrain>().terrainData;

                // Set frictions for each surface type
                if (Application.isPlaying) {
                    UpdateAlphamaps();
                    frictions = new float[surfaceTypes.Length];

                    for (int i = 0; i < frictions.Length; i++)
                    {
                        if (i >= GroundSurfaceMaster.Instance.surfaceTypes.Length)
                        {
                            frictions[i] = 1;
                            continue;
                        }
                        
                        if (GroundSurfaceMaster.Instance.surfaceTypes[surfaceTypes[i]].useColliderFriction) {
                            PhysicMaterial sharedMat = GetComponent<Collider>().sharedMaterial;
                            frictions[i] = sharedMat != null ? sharedMat.dynamicFriction * 2 : 1.0f;
                        }
                        else {
                            frictions[i] = GroundSurfaceMaster.Instance.surfaceTypes[surfaceTypes[i]].friction;
                        }
                    }
                }
            }
        }

        void Update() {
            if (!Application.isPlaying) {
                if (terDat) {
                    if (surfaceTypes.Length != terDat.terrainLayers.Length) {
                        ChangeSurfaceTypesLength();
                    }
                }
            }
        }

        // Updates the terrain alphamaps
        public void UpdateAlphamaps() {
            terrainAlphamap = terDat.GetAlphamaps(0, 0, terDat.alphamapWidth, terDat.alphamapHeight);
        }

        // Calculate the number of surface types based on the terrain layers
        void ChangeSurfaceTypesLength() {
            int[] tempVals = surfaceTypes;

            surfaceTypes = new int[terDat.terrainLayers.Length];

            for (int i = 0; i < surfaceTypes.Length; i++) {
                if (i >= tempVals.Length) {
                    break;
                }
                else {
                    surfaceTypes[i] = tempVals[i];
                }
            }
        }

        // Returns index of dominant surface type at point on terrain, relative to surface types array in GroundSurfaceMaster
        public int GetDominantSurfaceTypeAtPoint(Vector3 pos) {
            if (surfaceTypes.Length == 0) { return 0; }

            Vector2 coord = new Vector2(Mathf.Clamp01((pos.z - tr.position.z) / terDat.size.z), Mathf.Clamp01((pos.x - tr.position.x) / terDat.size.x));

            float maxVal = 0;
            int maxIndex = 0;
            float curVal = 0;

            for (int i = 0; i < terrainAlphamap.GetLength(2); i++) {
                curVal = terrainAlphamap[Mathf.FloorToInt(coord.x * (terDat.alphamapWidth - 1)), Mathf.FloorToInt(coord.y * (terDat.alphamapHeight - 1)), i];

                if (curVal > maxVal) {
                    maxVal = curVal;
                    maxIndex = i;
                }
            }

            return surfaceTypes[maxIndex];
        }

        // Gets the friction of the indicated surface type
        public float GetFriction(int sType) {
            float returnedFriction = 1;

            for (int i = 0; i < surfaceTypes.Length; i++) {
                if (sType == surfaceTypes[i]) {
                    returnedFriction = frictions[i];
                    break;
                }
            }

            return returnedFriction;
        }
    }
}