using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShadowTrace
{
    public sealed class LevelCheckpointController : MonoBehaviour
    {
        public PlayerController2D player;
        public PlayerLife life;
        public ShadowLamp[] lamps;
        public Image deathBlackout;
        public string menuScenePath;
        /// <summary>终局返回的目标场景路径，供 LevelStoryController 做黑屏异步转场。</summary>
        public string MenuScenePath => menuScenePath;
        public ShadowTraceHUD hud;
        [Min(0)] public float deathFadeDuration = 0.35f;
        [Min(0)] public float deathBlackDuration = 0.35f;
        [Tooltip("进入 Log 后，从黑屏恢复画面的时长（秒）。")]
        [Min(0)] public float deathRevealDuration = 0.6f;
        [Min(0.1f)] public float groundSearchDistance = 20;
        private bool returningToMenu;

        private void Awake() { EnsureDeathBlackout(); }

        private void EnsureDeathBlackout()
        {
            if (deathBlackout == null || deathBlackout.GetComponentInChildren<Button>(true) == null) return;
            // Legacy scenes used the pause background for death too. Never activate its menu children.
            var parent = deathBlackout.transform.parent;
            var root = new GameObject("Death Blackout", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            deathBlackout = root.GetComponent<Image>();
            deathBlackout.color = new Color(0, 0, 0, 0);
            deathBlackout.raycastTarget = true;
            root.SetActive(false);
        }

        private void Update() { if (Input.GetKeyDown(KeyCode.Q)) ExitLevel(); }

        public void ExitLevel()
        {
            if (returningToMenu || LevelStoryController.InputBlocked || (life != null && life.IsDead)) return;
            returningToMenu = true;
            var ability = player != null ? player.GetComponent<ShadowTraceAbility>() : null;
            if (ability != null) ability.CancelTrace();
            ReturnToMenu();
        }

        public void ReloadLevel(bool fromCheckpoint)
        {
            if (returningToMenu || LevelStoryController.InputBlocked || (life != null && life.IsDead)) return;
            bool canResume = false;
            if (fromCheckpoint && SaveGameService.Current.TryRead(out var data) && data.scenePath == gameObject.scene.path)
            {
                if (lamps != null) foreach (var lamp in lamps)
                    if (lamp != null && lamp.checkpointId == data.checkpointId && SafePosition(data.position, out _))
                    { canResume = true; break; }
            }
            if (canResume)
            {
                if (!SaveGameService.Current.BeginResume(gameObject.scene.path)) return;
            }
            else if (!SaveGameService.Current.BeginNewGame()) return;
            returningToMenu = true;
            if (player != null) player.GetComponent<ShadowTraceAbility>()?.CancelTrace();
            GameAudioManager.Instance?.StopGameplaySounds();
            Time.timeScale = 1;
            SceneManager.LoadScene(gameObject.scene.path);
        }

        private void OnEnable()
        {
            if (life != null) life.Died += OnDeath;
            if (lamps != null) foreach (var lamp in lamps) if (lamp != null) lamp.Lit += SaveCheckpoint;
        }

        /// <summary>
        /// 终局清档：只负责抹除存档并停掉游戏音效、恢复时间流动；
        /// 场景跳转交给 LevelStoryController 在黑屏下异步完成。返回 false 表示清档失败、可重试。
        /// </summary>
        public bool ClearGameProgress()
        {
            if (returningToMenu || !SaveGameService.Current.BeginNewGame()) return false;
            returningToMenu = true;
            GameAudioManager.Instance?.StopGameplaySounds();
            Time.timeScale = 1;
            return true;
        }

        private void OnDisable()
        {
            if (life != null) life.Died -= OnDeath;
            if (lamps != null) foreach (var lamp in lamps) if (lamp != null) lamp.Lit -= SaveCheckpoint;
            StopAllCoroutines();
        }

        private void Start()
        {
            if (!SaveGameService.Current.ConsumeResume(gameObject.scene.path, out var data)) return;
            ShadowLamp savedLamp = null;
            if (lamps != null)
                foreach (var lamp in lamps)
                    if (lamp != null && lamp.checkpointId == data.checkpointId) { savedLamp = lamp; break; }
            if (savedLamp == null || !SafePosition(data.position, out var position))
            {
                SaveGameService.Current.ShowNewGameHint = true;
                ReturnToMenu();
                return;
            }
            player.transform.position = position;
            player.Body.position = position;
            player.Body.velocity = Vector2.zero;
            Physics2D.SyncTransforms();
            savedLamp.RestoreLit();
            var camera = Camera.main;
            var follow = camera != null ? camera.GetComponent<TraceCameraFollow>() : null;
            if (follow != null && follow.target == player.transform)
                camera.transform.position = new Vector3(position.x + follow.offset.x, position.y + follow.offset.y, camera.transform.position.z);
        }

        public bool SafePosition(Vector3 origin, out Vector3 position)
        {
            position = origin;
            if (player == null) return false;
            var shape = player.Shape != null ? player.Shape : player.GetComponent<BoxCollider2D>();
            if (shape == null) return false;
            Vector2 scale = player.transform.lossyScale;
            Vector2 size = Vector2.Scale(shape.size, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
            Vector2 offset = Vector2.Scale(shape.offset, scale);
            Physics2D.SyncTransforms();
            if (!TracePhysics.SpawnPosition(origin, ShadowKind.Stay, size, offset,
                player.worldMask, groundSearchDistance, out var grounded)) return false;
            foreach (var collider in Physics2D.OverlapBoxAll(grounded + offset, size * 0.96f, 0))
                if (collider.GetComponentInParent<SpikeHazard>() != null) return false;
            position = new Vector3(grounded.x, grounded.y, origin.z);
            return true;
        }

        private void SaveCheckpoint(ShadowLamp lamp)
        {
            if (returningToMenu || life == null || life.IsDead || string.IsNullOrEmpty(lamp.checkpointId)) return;
            var story = GetComponent<LevelStoryController>();
            if (story != null && story.EndingStarted) return;
            if (!SafePosition(lamp.RespawnPosition, out var position))
            {
                Debug.LogWarning("Checkpoint respawn point is blocked or has no safe ground: " + lamp.name, lamp);
                return;
            }
            bool saved = SaveGameService.Current.TrySave(new SaveGameService.Checkpoint
            {
                scenePath = gameObject.scene.path,
                checkpointId = lamp.checkpointId,
                position = position
            });
            if (saved && hud != null && hud.manager != null && hud.manager.uiSettings != null)
            {
                var settings = hud.manager.uiSettings;
                hud.ShowPriorityMessage(settings.saveSucceeded, settings.saveNoticeDuration);
            }
        }

        private void OnDeath()
        {
            if (returningToMenu) return;
            returningToMenu = true;
            GameAudioManager.Instance?.StopGameplaySounds();
            StartCoroutine(DeathTransition());
        }

        private IEnumerator DeathTransition()
        {
            EnsureDeathBlackout();
            if (deathBlackout != null)
            {
                deathBlackout.transform.SetAsLastSibling();
                deathBlackout.gameObject.SetActive(true);
                deathBlackout.raycastTarget = true;
            }
            float elapsed = 0;
            while (elapsed < deathFadeDuration)
            {
                if (deathBlackout != null) deathBlackout.color = new Color(0, 0, 0, Mathf.Clamp01(elapsed / deathFadeDuration));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            if (deathBlackout != null) deathBlackout.color = Color.black;
            yield return new WaitForSecondsRealtime(deathBlackDuration);
            Time.timeScale = 1;
            SceneFadeOverlay.LoadThroughBlack(menuScenePath, deathRevealDuration);
        }

        private void ReturnToMenu()
        {
            GameAudioManager.Instance?.StopGameplaySounds();
            Time.timeScale = 1;
            SceneManager.LoadScene(menuScenePath);
        }
    }
}
