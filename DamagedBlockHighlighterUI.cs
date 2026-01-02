using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
                        if (!IsBlockInFrustum(frustumPlanes, blockWorldCenter))
                            continue;

                        // Check if block is damaged
                        BlockValue blockValue = GameManager.Instance.World.GetBlock(blockPos);

                        if (blockValue.damage > 0)
                        {
                            currentDamagedBlocks.Add(blockPos);
                        }
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
            // Create a cube at the block position
            GameObject highlightObject = GameObject.CreatePrimitive(PrimitiveType.Cube);

            // Remove the collider so it doesn't interfere with gameplay
            Collider collider = highlightObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            // Position the highlight (blocks are 1x1x1 units)
            Vector3 worldPos = new Vector3(blockPos.x + 0.5f, blockPos.y + 0.5f, blockPos.z + 0.5f);
            highlightObject.transform.position = worldPos - Origin.position;

            // Make it slightly larger than the block to be visible
            highlightObject.transform.localScale = Vector3.one * config.HighlightScale;

            // Apply the highlight material
            Renderer renderer = highlightObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = highlightMaterial;
            }

            // Store the highlight
            highlightObjects[blockPos] = highlightObject;
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

        private Plane[] GetClampedFrustumPlanes(Camera cam, float maxDistance)
        {
            // Get standard frustum planes (these are in Unity world space)
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);

            // Replace far plane with distance-clamped plane
            Vector3 camPos = cam.transform.position;
            Vector3 forward = cam.transform.forward;

            // Create a near plane at the clamped distance
            planes[5] = new Plane(-forward, camPos + forward * maxDistance);

            return planes;
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

        private bool IsBlockInFrustum(Plane[] planes, Vector3 blockCenter)
        {
            // blockCenter is already in Unity world space (with Origin offset applied)
            Bounds bounds = new Bounds(blockCenter, Vector3.one);
            return GeometryUtility.TestPlanesAABB(planes, bounds);
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
        }

        private IEnumerator ScanRoutine()
        {
            while (true)
            {
                ScanDamagedBlocksBox();
                yield return new WaitForSeconds(config.ScanInterval);
            }
        }
    }
}