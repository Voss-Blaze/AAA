using UnityEngine;

namespace ShadowTrace
{
    [DisallowMultipleComponent]
    public sealed class GameCursor : MonoBehaviour
    {
        public Texture2D texture;
        [Tooltip("点击热点，单位为导入后贴图的像素，从左上角开始计算。")]
        public Vector2 hotspot;
        public CursorMode mode = CursorMode.Auto;

        private void OnEnable() { Apply(); }
        private void OnApplicationFocus(bool focused) { if (focused) Apply(); }

        public void Apply()
        {
            if (texture == null) return;
            var point = new Vector2(Mathf.Clamp(hotspot.x, 0, texture.width - 1), Mathf.Clamp(hotspot.y, 0, texture.height - 1));
            Cursor.SetCursor(texture, point, mode);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnApplicationQuit()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
