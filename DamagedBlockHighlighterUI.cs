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
        [SerializeField] private float maxDistance = 20f;
        [SerializeField] private int gridSize = 20; // Number of rays in each direction (20x20 = 400 rays)
        [SerializeField] private float fieldOfView = 60f; // Field of view to scan

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
            highlightMaterial = new Material(Shader.Find("Standard"));
            highlightMaterial.color = new Color(1f, 0f, 0f, 0.7f); // Red with transparency
            highlightMaterial.SetFloat("_Mode", 3); // Transparent rendering mode
            highlightMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            highlightMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            highlightMaterial.SetInt("_ZWrite", 0);
            highlightMaterial.DisableKeyword("_ALPHATEST_ON");
            highlightMaterial.EnableKeyword("_ALPHABLEND_ON");
            highlightMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            highlightMaterial.renderQueue = 3000;
        }

        public void ScanForDamagedBlocks()
        {
            EntityPlayerLocal player = GameManager.Instance?.World?.GetPrimaryPlayer();
            if (player == null)
                return;

            Vector3 origin = player.getHeadPosition();
            Vector3 forward = player.GetLookVector();

            // Calculate camera's up and right vectors
            Vector3 up = Vector3.up;
            Vector3 right = Vector3.Cross(forward, up).normalized;
            up = Vector3.Cross(right, forward).normalized;

            // Store found damaged blocks in this scan
            HashSet<Vector3i> currentDamagedBlocks = new HashSet<Vector3i>();

            int hitMask = Voxel.HM_Melee;

            // Cast rays in a grid pattern
            float halfFOV = fieldOfView / 2f;

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    // Calculate the angle offset for this ray
                    float horizontalAngle = Mathf.Lerp(-halfFOV, halfFOV, x / (float)(gridSize - 1));
                    float verticalAngle = Mathf.Lerp(-halfFOV, halfFOV, y / (float)(gridSize - 1));

                    // Create the ray direction
                    Vector3 direction = forward;
                    direction = Quaternion.AngleAxis(horizontalAngle, up) * direction;
                    direction = Quaternion.AngleAxis(verticalAngle, right) * direction;
                    direction.Normalize();

                    Ray ray = new Ray(origin, direction);

                    // Perform raycast
                    if (Voxel.Raycast(GameManager.Instance.World, ray, maxDistance, hitMask, 0f))
                    {
                        WorldRayHitInfo hit = Voxel.voxelRayHitInfo;
                        Vector3i blockPos = hit.hit.blockPos;
                        BlockValue blockValue = hit.hit.blockValue;

                        // Check if block is damaged
                        if (blockValue.damage > 0)
                        {
                            currentDamagedBlocks.Add(blockPos);
                        }
                    }
                }
            }

            // Update highlights based on scan results
            UpdateHighlights(currentDamagedBlocks);
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
                ScanForDamagedBlocks();
                yield return new WaitForSeconds(interval);
            }
        }
    }
}