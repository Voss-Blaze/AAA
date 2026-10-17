using UnityEngine;
using UnityEngine.Rendering;

namespace ShadowTrace
{
    [DefaultExecutionOrder(50), DisallowMultipleComponent]
    public sealed class ShadowSelectionGlow : MonoBehaviour
    {
        public SpriteRenderer source;
        [Header("选中描边发光")]
        public bool selected;
        public bool outlineEnabled = true;
        [ColorUsage(true, true)] public Color outlineColor = new Color(1, 0.65f, 0.12f, 1);
        [Tooltip("描边宽度，单位：原始贴图像素。")]
        [Min(0)] public float outlineWidth = 10;
        public bool glowEnabled = true;
        [Tooltip("外扩光晕范围，单位：原始贴图像素。")]
        [Min(0)] public float glowRadius = 24;
        [Tooltip("HDR 发光强度；配合摄像机 Bloom 产生泛光。")]
        [Min(0)] public float glowIntensity = 3;
        [Range(0, 1)] public float glowOpacity = 0.5f;
        public bool inheritBodyOpacity = true;

        public MeshRenderer OutlineRenderer => overlay;
        private MeshRenderer overlay;
        private Mesh mesh;
        private MaterialPropertyBlock properties;
        private Sprite previousSprite;
        private bool previousFlipX, previousFlipY;
        private float previousPadding = -1;
        private Vector4 uvRect;

        private void Awake() { if (source == null) source = GetComponentInChildren<SpriteRenderer>(true); }

        public void SetSelected(bool value)
        {
            selected = value;
            Refresh();
        }

        private void LateUpdate() { Refresh(); }

        public void Refresh()
        {
            bool visible = isActiveAndEnabled && selected && source != null && source.enabled && source.sprite != null &&
                source.gameObject.activeInHierarchy && (outlineEnabled || glowEnabled);
            if (!visible) { if (overlay != null) overlay.enabled = false; return; }
            if (overlay == null)
            {
                var root = new GameObject("Selection Outline", typeof(MeshFilter), typeof(MeshRenderer));
                root.layer = source.gameObject.layer;
                root.transform.SetParent(source.transform, false);
                mesh = new Mesh { name = "Shadow Selection Quad" };
                mesh.MarkDynamic();
                root.GetComponent<MeshFilter>().sharedMesh = mesh;
                overlay = root.GetComponent<MeshRenderer>();
                overlay.shadowCastingMode = ShadowCastingMode.Off;
                overlay.receiveShadows = false;
                overlay.lightProbeUsage = LightProbeUsage.Off;
                properties = new MaterialPropertyBlock();
            }
            float width = outlineEnabled ? Mathf.Max(0, outlineWidth) : 0;
            float radius = glowEnabled ? Mathf.Max(0, glowRadius) : 0;
            float padding = width + radius + 1;
            if (previousSprite != source.sprite || previousFlipX != source.flipX || previousFlipY != source.flipY || !Mathf.Approximately(previousPadding, padding))
                Rebuild(padding);
            overlay.sharedMaterial = source.sharedMaterial;
            overlay.sortingLayerID = source.sortingLayerID;
            overlay.sortingOrder = source.sortingOrder - 1;
            overlay.gameObject.layer = source.gameObject.layer;
            properties.Clear();
            properties.SetTexture("_MainTex", source.sprite.texture);
            properties.SetFloat("_OutlineOnly", 1);
            properties.SetColor("_OutlineColor", outlineColor);
            properties.SetFloat("_OutlineWidth", width);
            properties.SetFloat("_GlowRadius", radius);
            properties.SetFloat("_GlowIntensity", glowEnabled ? Mathf.Max(0, glowIntensity) : 1);
            properties.SetFloat("_GlowOpacity", glowEnabled ? Mathf.Clamp01(glowOpacity) : 0);
            properties.SetFloat("_OutlineOpacity", inheritBodyOpacity ? source.color.a : 1);
            properties.SetVector("_SpriteUVRect", uvRect);
            overlay.SetPropertyBlock(properties);
            overlay.enabled = true;
        }

        private void Rebuild(float padding)
        {
            var sprite = source.sprite;
            var vertices = sprite.vertices;
            var uvs = sprite.uv;
            Vector2 axisU = Vector2.right, axisV = Vector2.up, origin = Vector2.zero;
            // Recover the local-to-texture affine map, including packed/rotated sprite rectangles.
            for (int i = 1; i + 1 < vertices.Length; i++)
            {
                Vector2 a = vertices[i] - vertices[0], b = vertices[i + 1] - vertices[0];
                float determinant = a.x * b.y - b.x * a.y;
                if (Mathf.Abs(determinant) < 0.000001f) continue;
                Vector2 du = uvs[i] - uvs[0], dv = uvs[i + 1] - uvs[0];
                axisU = (du * b.y - dv * a.y) / determinant;
                axisV = (dv * a.x - du * b.x) / determinant;
                origin = uvs[0] - axisU * vertices[0].x - axisV * vertices[0].y;
                break;
            }
            Vector2 uvMin = uvs[0], uvMax = uvs[0];
            foreach (var uv in uvs) { uvMin = Vector2.Min(uvMin, uv); uvMax = Vector2.Max(uvMax, uv); }
            uvRect = new Vector4(uvMin.x, uvMin.y, uvMax.x, uvMax.y);
            Vector2 minimum = -sprite.pivot / sprite.pixelsPerUnit - Vector2.one * (padding / sprite.pixelsPerUnit);
            Vector2 maximum = (sprite.rect.size - sprite.pivot) / sprite.pixelsPerUnit + Vector2.one * (padding / sprite.pixelsPerUnit);
            var corners = new[] { minimum, new Vector2(maximum.x, minimum.y), maximum, new Vector2(minimum.x, maximum.y) };
            var positions = new Vector3[4]; var textureUVs = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                textureUVs[i] = origin + axisU * corners[i].x + axisV * corners[i].y;
                positions[i] = new Vector3(corners[i].x * (source.flipX ? -1 : 1), corners[i].y * (source.flipY ? -1 : 1), 0);
            }
            mesh.Clear(); mesh.vertices = positions; mesh.uv = textureUVs;
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; mesh.RecalculateBounds();
            previousSprite = sprite; previousFlipX = source.flipX; previousFlipY = source.flipY; previousPadding = padding;
        }

        private void OnDisable() { if (overlay != null) overlay.enabled = false; }
        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
