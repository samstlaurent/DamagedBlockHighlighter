using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Windows;

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

        private GameObject highlightObject;
        private Material highlightMaterial;
        private Vector3i lastHighlightedBlock = Vector3i.zero;

        private void Awake()
        {
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

        public void RaycastAndLogBlock()
        {
            EntityPlayerLocal player = GameManager.Instance?.World?.GetPrimaryPlayer();
            if (player == null)
                return;

            Vector3 origin = player.getHeadPosition();
            Vector3 direction = player.GetLookVector();

            Ray ray = new Ray(origin, direction);
            float maxDistance = 20f;

            //int hitMask = Voxel.HM_NotMoveable | Voxel.HM_Moveable | Voxel.HM_Transparent;
            int hitMask = Voxel.HM_Melee;

            if (Voxel.Raycast(GameManager.Instance.World, ray, maxDistance, hitMask, 0f))
            {
                WorldRayHitInfo hit = Voxel.voxelRayHitInfo;
                Vector3i blockPos = hit.hit.blockPos;
                BlockValue blockValue = hit.hit.blockValue;
                if (blockValue.damage > 0)
                {
                    if (blockPos != lastHighlightedBlock)
                    {
                        lastHighlightedBlock = blockPos;
                        HighlightBlock(blockPos);
                    }
                }
                else
                {
                    ClearHighlight();
                    lastHighlightedBlock = Vector3i.zero;
                }
            }
            else
            {
                ClearHighlight();
                lastHighlightedBlock = Vector3i.zero;
            }
        }

        private void HighlightBlock(Vector3i blockPos)
        {
            // Clear previous highlight
            ClearHighlight();

            // Create a cube at the block position
            highlightObject = GameObject.CreatePrimitive(PrimitiveType.Cube);

            // Remove the collider so it doesn't interfere with gameplay
            Collider collider = highlightObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            // Position the highlight (blocks are 1x1x1 units)
            // Add 0.5 to center it on the block
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
        }

        private void ClearHighlight()
        {
            if (highlightObject != null)
            {
                Destroy(highlightObject);
                highlightObject = null;
            }
        }

        [SerializeField] private float interval = 0.1f; // seconds

        private void OnEnable()
        {
            StartCoroutine(FireEveryNSeconds());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        private IEnumerator FireEveryNSeconds()
        {
            while (true)
            {
                RaycastAndLogBlock();
                yield return new WaitForSeconds(interval);
            }
        }
    }
}
