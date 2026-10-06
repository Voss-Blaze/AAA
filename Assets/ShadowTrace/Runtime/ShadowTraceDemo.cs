using UnityEngine;

namespace ShadowTrace
{
    /// <summary>Builds only the isolated demo scene; ordinary levels wire the components directly.</summary>
    public sealed class ShadowTraceDemo : MonoBehaviour
    {
        public PlayerController2D playerPrefab;
        public ShadowActor shadowPrefab;
        public GameObject boxPrefab;
        public Sprite blockSprite;

        private void Start()
        {
            if (playerPrefab == null || shadowPrefab == null || boxPrefab == null || blockSprite == null)
            {
                Debug.LogError("Demo assets missing. Run Tools > Shadow Trace > Create Demo Assets.", this);
                return;
            }
            Block("Ground", new Vector2(4, -1.5f), new Vector2(40, 1));
            Block("Left wall", new Vector2(-15.5f, 2), new Vector2(1, 8));
            Block("Right wall", new Vector2(23.5f, 2), new Vector2(1, 8));
            Block("Platform", new Vector2(12, 1.8f), new Vector2(5, 0.4f));
            Block("Push stop wall", new Vector2(7, 0), new Vector2(0.6f, 2));
            Block("Upper step", new Vector2(-9, 0.5f), new Vector2(3, 0.5f));
            Instantiate(boxPrefab, new Vector3(4.5f, -0.45f, 0), Quaternion.identity);
            var player = Instantiate(playerPrefab, new Vector3(-3, 0, 0), Quaternion.identity);
            var manager = gameObject.AddComponent<ShadowManager>();
            manager.player = player; manager.shadowPrefab = shadowPrefab;
            var ability = player.GetComponent<ShadowTraceAbility>();
            ability.manager = manager;
            var hud = gameObject.AddComponent<ShadowTraceHUD>();
            hud.ability = ability; hud.manager = manager;
            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }
            camera.orthographic = true; camera.orthographicSize = 6;
            camera.backgroundColor = new Color(0.06f, 0.08f, 0.14f);
            camera.transform.position = new Vector3(0, 2, -10);
            var follow = camera.gameObject.AddComponent<TraceCameraFollow>();
            follow.target = player.transform;
        }

        private void Block(string objectName, Vector2 position, Vector2 size)
        {
            var block = new GameObject(objectName);
            block.transform.position = position;
            var renderer = block.AddComponent<SpriteRenderer>();
            renderer.sprite = blockSprite;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;
            renderer.color = new Color(0.22f, 0.29f, 0.4f);
            block.AddComponent<BoxCollider2D>().size = size;
        }
    }
}
