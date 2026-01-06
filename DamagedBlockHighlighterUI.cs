using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XMLEditing;

namespace DamagedBlockHighlighter
{
    public class DamagedBlockHighlighterUI : MonoBehaviour
    {
        private static DamagedBlockHighlighterUI instance;
        public static DamagedBlockHighlighterUI Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("DamagedBlockHighlighterUI");
                    instance = go.AddComponent<DamagedBlockHighlighterUI>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        private Dictionary<Vector3i, GameObject> highlightObjects = new Dictionary<Vector3i, GameObject>();
        private Dictionary<(int blockType, byte meta), Mesh> meshCache = new Dictionary<(int, byte), Mesh>();
        private Material highlightMaterial;
        private DamagedBlockHighlighterConfig config;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this.gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(this.gameObject);

            config = ConfigLoader.Load();

            // Create highlight material
            highlightMaterial = new Material(Shader.Find("Transparent/Diffuse"));
            highlightMaterial.color = config.HighlightColor;
            highlightMaterial.renderQueue = 3001;
            highlightMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        }

        private void OnEnable()
        {
            StartCoroutine(ScanRoutine());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            ClearAllHighlights();
        }

        private void OnDestroy()
        {
            ClearAllHighlights();
            foreach (var cachedMesh in meshCache.Values)
            {
                if (cachedMesh != null) Destroy(cachedMesh);
            }
            meshCache.Clear();
        }

        private IEnumerator ScanRoutine()
        {
            while (true)
            {
                ScanDamagedBlocksBox();
                yield return new WaitForSeconds(config.ScanInterval);
            }
        }

        public void ScanDamagedBlocksBox()
        {
            EntityPlayerLocal player = GameManager.Instance?.World?.GetPrimaryPlayer();
            if (player == null) return;

            // Only continue if the player is holding a repair tool
            if (!IsHoldingRepairTool(player) && config.ScanOnlyWithRepairTool)
            {
                ClearAllHighlights();
                return;
            }

            Camera cam = player.playerCamera;
            if (cam == null) return;

            HashSet<Vector3i> currentDamagedBlocks = new HashSet<Vector3i>();

            // Get frustum planes in Unity world space (accounting for Origin offset)
            Plane[] frustumPlanes = GetClampedFrustumPlanes(cam, config.ScanRange);

            // Get iteration bounds based on camera frustum
            GetIterationBounds(cam, config.ScanRange, out Vector3i min, out Vector3i max);

            // Scan all blocks in the bounding box
            for (int x = min.x; x <= max.x; x++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    for (int z = min.z; z <= max.z; z++)
                    {
                        Vector3i blockPos = new Vector3i(x, y, z);

                        // Convert block position to Unity world space (with Origin offset)
                        Vector3 blockWorldCenter = new Vector3(
                            blockPos.x + 0.5f,
                            blockPos.y + 0.5f,
                            blockPos.z + 0.5f
                        ) - Origin.position;

                        // Check if block is in camera frustum
                        if (!IsBlockInFrustum(frustumPlanes, blockWorldCenter)) continue;

                        // Check if block damage exceeds threshold
                        BlockValue blockValue = GameManager.Instance.World.GetBlock(blockPos);
                        int maxDamage = blockValue.Block.MaxDamage;
                        float damagePercent = (float)blockValue.damage / maxDamage;
                        if (damagePercent <= config.ScanDamageThreshold) continue;

                        // Check if terrain blocks should be ignored
                        if (config.ScanIgnoreTerrain && blockValue.Block.shape.IsTerrain()) continue;

                        currentDamagedBlocks.Add(blockPos);
                    }
                }
            }

            // Update the highlights
            UpdateHighlights(currentDamagedBlocks);
        }

        private bool IsHoldingRepairTool(EntityPlayerLocal player)
        {
            ItemValue holdingItem = player.inventory.holdingItemItemValue;
            if (holdingItem.IsEmpty()) return false;

            ItemClass itemClass = holdingItem.ItemClass;
            if (itemClass == null) return false;

            // Check if tool has RepairAction
            ItemAction[] actions = itemClass.Actions;
            if (actions != null)
            {
                foreach (ItemAction action in actions)
                {
                    if (action is ItemActionRepair) return true;
                }
            }

            return false;
        }

        private void ClearAllHighlights()
        {
            foreach (var kvp in highlightObjects)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value);
                }
            }
            highlightObjects.Clear();
        }

        private void UpdateHighlights(HashSet<Vector3i> damagedBlocks)
        {
            // Remove highlights for blocks that are no longer damaged or visible
            List<Vector3i> toRemove = new List<Vector3i>();
            foreach (var kvp in highlightObjects)
            {
                if (!damagedBlocks.Contains(kvp.Key))
                {
                    Destroy(kvp.Value);
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var pos in toRemove)
            {
                highlightObjects.Remove(pos);
            }

            // Add highlights for new damaged blocks
            foreach (var blockPos in damagedBlocks)
            {
                if (!highlightObjects.ContainsKey(blockPos))
                {
                    HighlightBlock(blockPos);
                }
            }
        }

        private void HighlightBlock(Vector3i blockPos)
        {
            BlockValue blockValue = GameManager.Instance.World.GetBlock(blockPos);
            if (blockValue.isair) return;
            var cacheKey = (blockValue.type, blockValue.meta);

            GameObject highlightObject = new GameObject("BlockHighlight_" + blockPos);
            MeshFilter meshFilter = highlightObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = highlightObject.AddComponent<MeshRenderer>();
            meshRenderer.material = highlightMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            Vector3 worldPos = new Vector3(blockPos.x + 0.5f, blockPos.y + 0.5f, blockPos.z + 0.5f) - Origin.position; // Position at block center (Unity world space)
            highlightObject.transform.position = worldPos;
            highlightObject.transform.rotation = blockValue.Block.shape.GetRotation(blockValue); // Apply block rotation
            if (!meshCache.TryGetValue(cacheKey, out Mesh mesh)) // Get cached or generate mesh
            {
                mesh = GetBlockMesh(blockValue);
                if (mesh != null) meshCache[cacheKey] = mesh;
                else mesh = GetCubeMesh(); // Fallback to cube if mesh gen fails
            }
            meshFilter.mesh = mesh;
            highlightObjects[blockPos] = highlightObject;
        }

        private Mesh GetBlockMesh(BlockValue blockValue)
        {
            try
            {
                Block block = blockValue.Block;
                BlockShape shape = block.shape;

                if (shape is BlockShapeNew bsn)
                {
                    Mesh mesh = new Mesh();
                    mesh.Clear();
                    List<Vector3> verts = new List<Vector3>();
                    List<Vector3> norms = new List<Vector3>();
                    List<int> tris = new List<int>(); // Use int[] for SetTriangles

                    for (int f = 0; f < 7; f++) // Combine all visual meshes (faces 0-6)
                    {
                        BlockShapeNew.MySimpleMesh sm = bsn.visualMeshes[f];
                        if (sm == null) continue;
                        int offset = verts.Count;
                        verts.AddRange(sm.Vertices);
                        norms.AddRange(sm.Normals);
                        foreach (ushort idx in sm.Indices) tris.Add(offset + idx); // Convert ushort indices to int and offset
                    }

                    if (verts.Count == 0) // If no visuals, try colliders
                    {
                        for (int f = 0; f < 7; f++)
                        {
                            BlockShapeNew.MySimpleMesh sm = bsn.colliderMeshes[f];
                            if (sm == null) continue;
                            int offset = verts.Count;
                            verts.AddRange(sm.Vertices);
                            norms.AddRange(sm.Normals);
                            foreach (ushort idx in sm.Indices) tris.Add(offset + idx);
                        }
                    }

                    Vector3 offsetVec = new Vector3(0.5f, 0.5f, 0.5f); // Center the vertices by subtracting 0.5 in x, y and z
                    for (int i = 0; i < verts.Count; i++) verts[i] -= offsetVec;

                    mesh.SetVertices(verts);
                    mesh.SetNormals(norms);
                    mesh.SetTriangles(tris, 0); // submesh 0
                    mesh.RecalculateBounds();
                    mesh.MarkDynamic(); // Optional for performance

                    if (mesh != null && mesh.vertices.Length > 0)
                    {
                        Vector3[] vertices = mesh.vertices;
                        Vector3[] normals = mesh.normals;
                        for (int i = 0; i < vertices.Length; i++) vertices[i] += normals[i] * 0.001f; // Small offset to push highlight outside
                        mesh.vertices = vertices;
                        mesh.RecalculateBounds();
                    }

                    return mesh;
                }
                else return null;
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Error generating block mesh: " + ex.Message);
                return null;
            }
        }

        //private Mesh GetModelBlockProxyMesh(BlockValue blockValue)
        //{
        //    Block block = blockValue.Block;

        //    // Use block bounds if available
        //    Vector3 size = Vector3.one;

        //    if (block is BlockModel bm)
        //    {
        //        size = bm.GetModelBounds().size;
        //    }

        //    Mesh cube = GetCubeMesh();

        //    Vector3[] verts = cube.vertices;
        //    for (int i = 0; i < verts.Length; i++)
        //    {
        //        verts[i] = Vector3.Scale(verts[i], size);
        //    }

        //    cube.vertices = verts;
        //    cube.RecalculateBounds();

        //    return cube;
        //}

        private Mesh GetCubeMesh()
        {
            GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh cubeMesh = Instantiate(tempCube.GetComponent<MeshFilter>().sharedMesh); // Instantiate to own it
            DestroyImmediate(tempCube);
            Vector3[] vertices = cubeMesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] *= 1.001f; // Scale the cube slightly to avoid z-fighting
            cubeMesh.vertices = vertices;
            cubeMesh.RecalculateBounds();
            return cubeMesh;
        }

        private Plane[] GetClampedFrustumPlanes(Camera cam, float maxDistance)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam); // Get standard frustum planes (these are in Unity world space)
            Vector3 camPos = cam.transform.position; // Replace far plane with distance-clamped plane
            Vector3 forward = cam.transform.forward;
            planes[5] = new Plane(-forward, camPos + forward * maxDistance); // Create a near plane at the clamped distance
            return planes;
        }

        private bool IsBlockInFrustum(Plane[] planes, Vector3 blockCenter)
        {
            // blockCenter is already in Unity world space (with Origin offset applied)
            Bounds bounds = new Bounds(blockCenter, Vector3.one);
            return GeometryUtility.TestPlanesAABB(planes, bounds);
        }

        private void GetIterationBounds(Camera cam, float range, out Vector3i min, out Vector3i max)
        {
            // Camera position in Unity world space
            Vector3 camPos = cam.transform.position;
            Vector3 forward = cam.transform.forward;
            Vector3 right = cam.transform.right;
            Vector3 up = cam.transform.up;

            // Calculate frustum dimensions at max range
            float halfHeight = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * range;
            float halfWidth = halfHeight * cam.aspect;

            // Calculate the 8 corners of the frustum at max range
            Vector3 center = camPos + forward * range;
            Vector3[] corners = new Vector3[8];

            // Near plane corners (at camera)
            corners[0] = camPos + up * halfHeight * 0.1f + right * halfWidth * 0.1f;
            corners[1] = camPos + up * halfHeight * 0.1f - right * halfWidth * 0.1f;
            corners[2] = camPos - up * halfHeight * 0.1f + right * halfWidth * 0.1f;
            corners[3] = camPos - up * halfHeight * 0.1f - right * halfWidth * 0.1f;

            // Far plane corners
            corners[4] = center + up * halfHeight + right * halfWidth;
            corners[5] = center + up * halfHeight - right * halfWidth;
            corners[6] = center - up * halfHeight + right * halfWidth;
            corners[7] = center - up * halfHeight - right * halfWidth;

            // Find min/max across all corners, then convert to block coordinates
            Vector3 minWorld = corners[0];
            Vector3 maxWorld = corners[0];

            for (int i = 1; i < 8; i++)
            {
                minWorld = Vector3.Min(minWorld, corners[i]);
                maxWorld = Vector3.Max(maxWorld, corners[i]);
            }

            // Convert from Unity world space to block coordinates
            // Add Origin.position back because block coordinates are in "world" space
            minWorld += Origin.position;
            maxWorld += Origin.position;

            min = new Vector3i(
                Mathf.FloorToInt(minWorld.x),
                Mathf.FloorToInt(minWorld.y),
                Mathf.FloorToInt(minWorld.z)
            );

            max = new Vector3i(
                Mathf.CeilToInt(maxWorld.x),
                Mathf.CeilToInt(maxWorld.y),
                Mathf.CeilToInt(maxWorld.z)
            );
        }
    }
}