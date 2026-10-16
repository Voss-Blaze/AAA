using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShadowTrace
{
    /// <summary>
    /// 跨场景常驻的全屏黑幕：以纯黑状态盖住屏幕，在黑幕保护下异步加载目标场景，
    /// 场景就位后再用 unscaledDeltaTime 缓缓淡开，保证切换过程中任何一帧都不漏出旧场景。
    /// </summary>
    public sealed class SceneFadeOverlay : MonoBehaviour
    {
        private static SceneFadeOverlay current;
        private Image black;

        /// <summary>
        /// 在纯黑黑幕下异步切换到 scenePath；场景激活后黑幕用 revealDuration 秒淡出。
        /// 调用前屏幕应当已经是黑的（例如终局 EndWord 面板），这样黑幕出现时肉眼无变化。
        /// </summary>
        public static void LoadThroughBlack(string scenePath, float revealDuration)
        {
            if (current != null) return;
            if (string.IsNullOrEmpty(scenePath) || !Application.CanStreamedLevelBeLoaded(scenePath))
            {
                Debug.LogError("[SceneFadeOverlay] 无法加载场景：\"" + scenePath + "\"，请检查场景名与 Build Settings。");
                return;
            }

            var root = new GameObject("Scene Fade Overlay");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 永远盖在最上层，包括场景自带的 UI。
            canvas.sortingOrder = short.MaxValue;
            root.AddComponent<GraphicRaycaster>();

            var imageObject = new GameObject("Black");
            imageObject.transform.SetParent(root.transform, false);
            var image = imageObject.AddComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = true;
            var rect = (RectTransform)imageObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            DontDestroyOnLoad(root);
            var overlay = root.AddComponent<SceneFadeOverlay>();
            overlay.black = image;
            current = overlay;
            overlay.StartCoroutine(overlay.Transition(scenePath, Mathf.Max(0f, revealDuration)));
        }

        private IEnumerator Transition(string scenePath, float revealDuration)
        {
            var operation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
            operation.allowSceneActivation = false;
            // progress 到 0.9 表示除激活外全部就绪，等待期间屏幕始终纯黑。
            while (operation.progress < 0.9f) yield return null;
            operation.allowSceneActivation = true;
            while (!operation.isDone) yield return null;

            // 新场景已在黑幕后面就位：缓缓揭幕。
            float elapsed = 0f;
            while (elapsed < revealDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (black != null)
                    black.color = new Color(0f, 0f, 0f, 1f - Mathf.Clamp01(elapsed / revealDuration));
                yield return null;
            }
            current = null;
            Destroy(gameObject);
        }
    }
}
