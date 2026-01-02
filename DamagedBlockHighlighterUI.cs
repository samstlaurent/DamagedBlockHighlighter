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

        [SerializeField] private float interval = 0.1f; // Check less frequently since we're doing more raycasts
        [SerializeField] private float scanDistance = 20f;
        [SerializeField] private float scanWidth = 20f;
        [SerializeField] private float scanHeight = 20f;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this.gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(this.gameObject);

            // Create highlight material
            highlightMaterial = new Material(Shader.Find("Transparent/Diffuse"));
            highlightMaterial.color = new Color(1f, 0f, 0f, 0.2f);
        }

        public void ScanDamagedBlocksBox()
        {
            EntityPlayerLocal player = GameManager.Instance?.World?.GetPrimaryPlayer();
            if (player == null) return;

            // Only continue if the player is holding a repair tool
            if (!IsHoldingRepairTool(player))
            {
                // Clear all highlights if not holding repair tool
                ClearAllHighlights();
                return;
            }

            Vector3 playerPos = player.getHeadPosition();
            Vector3 forward = player.GetLookVector().normalized;

            HashSet<Vector3i> currentDamagedBlocks = new HashSet<Vector3i>();

            // Compute the center of the box in front of the player
            Vector3 boxCenter = playerPos + forward * (scanDistance * 0.5f);

            // Half extents of the box (width, height, depth)
            Vector3 halfExtents = new Vector3(scanWidth * 0.5f, scanHeight * 0.5f, scanDistance * 0.5f);

            // Convert box bounds to voxel coordinates
            int minX = Mathf.FloorToInt(boxCenter.x - halfExtents.x);
            int maxX = Mathf.CeilToInt(boxCenter.x + halfExtents.x);
            int minY = Mathf.FloorToInt(boxCenter.y - halfExtents.y);
            int maxY = Mathf.CeilToInt(boxCenter.y + halfExtents.y);
            int minZ = Mathf.FloorToInt(boxCenter.z - halfExtents.z);
            int maxZ = Mathf.CeilToInt(boxCenter.z + halfExtents.z);

            // Scan all blocks in the box
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        Vector3i blockPos = new Vector3i(x, y, z);
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
            // Get the item the player is currently holding
            ItemValue holdingItem = player.inventory.holdingItemItemValue;

            if (holdingItem.IsEmpty()) return false;

            // Get the item class
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
            highlightObject.transform.localScale = new Vector3(1.02f, 1.02f, 1.02f);

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
                yield return new WaitForSeconds(interval);
            }
        }
    }
}
