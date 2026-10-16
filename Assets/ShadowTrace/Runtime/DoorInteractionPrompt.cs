using UnityEngine;

namespace ShadowTrace
{
    [RequireComponent(typeof(InteractiveDoor))]
    public sealed class DoorInteractionPrompt : MonoBehaviour
    {
        public string promptText;
        public Font font;
        public Color color = Color.white;
        public Vector3 offset = new Vector3(0, 0.25f, 0);
        public int fontSize = 48;
        public float characterSize = 0.06f;
        public TextMesh Label { get; private set; }
        private InteractiveDoor door;
        private SpriteRenderer sprite;

        private void Awake()
        {
            door = GetComponent<InteractiveDoor>();
            sprite = GetComponent<SpriteRenderer>();
            var labelObject = new GameObject("Interaction Prompt");
            labelObject.transform.SetParent(transform, false);
            Label = labelObject.AddComponent<TextMesh>();
            Label.anchor = TextAnchor.LowerCenter;
            Label.alignment = TextAlignment.Center;
            Label.font = font; Label.fontSize = fontSize; Label.characterSize = characterSize;
            Label.color = color; Label.text = promptText;
            var renderer = Label.GetComponent<MeshRenderer>();
            if (font != null) renderer.sharedMaterial = font.material;
            if (sprite != null) { renderer.sortingLayerID = sprite.sortingLayerID; renderer.sortingOrder = sprite.sortingOrder + 10; }
            labelObject.SetActive(false);
        }

        private void LateUpdate()
        {
            var player = door.player;
            var life = player != null ? player.GetComponent<PlayerLife>() : null;
            bool visible = Time.timeScale > 0 && !LevelStoryController.InputBlocked &&
                (life == null || !life.IsDead) && door.Contains(player);
            Label.gameObject.SetActive(visible);
            if (!visible) return;
            var bounds = sprite != null ? sprite.bounds : GetComponent<BoxCollider2D>().bounds;
            Label.transform.position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) + offset;
            Label.transform.rotation = Quaternion.identity;
            var scale = transform.lossyScale;
            Label.transform.localScale = new Vector3(Mathf.Abs(scale.x) > 0.0001f ? 1 / scale.x : 1,
                Mathf.Abs(scale.y) > 0.0001f ? 1 / scale.y : 1, 1);
        }

        private void OnDisable() { if (Label != null) Label.gameObject.SetActive(false); }
    }
}
