using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTrace
{
    public sealed class LevelStoryController : MonoBehaviour
    {
        [Serializable] public sealed class DoorStory
        {
            public InteractiveDoor door;
            public Text text;
        }

        public GameObject talkPanel;
        public GameObject endingPanel;
        [Tooltip("在 Text 中用手动换行分隔每次显示的内容；空行会被忽略。")]
        public Text introduction;
        public DoorStory[] doorStories;
        public Text endingTalk;
        [Tooltip("最终剧情同样按手动换行逐段显示，最后一段确认后清档并返回菜单。")]
        public Text endingWords;
        public PlayerLife life;
        public LevelCheckpointController checkpoint;
        public GamePauseController pause;
        [Min(0)] public float introductionDelay = 2;
        [Min(0)] public float fadeDuration = 0.3f;
        [Tooltip("终局黑屏白字结束后，从纯黑淡开到 Log 场景的时长（秒）。")]
        [Min(0)] public float endingRevealDuration = 0.6f;

        public static bool InputBlocked => owner != null;
        public bool IsPlaying => owner == this;
        public bool EndingStarted { get; private set; }
        public bool WaitingForAdvance { get; private set; }
        public Text CurrentText { get; private set; }
        private static LevelStoryController owner;
        private readonly Dictionary<Text, string[]> lines = new Dictionary<Text, string[]>();
        private readonly HashSet<Text> requested = new HashSet<Text>();
        private readonly Queue<Text> pending = new Queue<Text>();
        private Coroutine playback;
        private CanvasGroup currentTextGroup;
        private bool advance;
        private int readyFrame;
        private float previousTimeScale;
        private bool previousButtonEnabled;
        private bool skipIntroduction;

        public static string[] SplitLines(string text)
        {
            var result = new List<string>();
            foreach (var line in (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
                if (!string.IsNullOrWhiteSpace(line)) result.Add(line.Trim());
            return result.ToArray();
        }

        private void Awake()
        {
            // Capture at entry: lighting a lamp during a new run must not cancel its introduction.
            skipIntroduction = SaveGameService.Current.TryRead(out var saved) && saved.scenePath == gameObject.scene.path;
            Cache(introduction); Cache(endingTalk); Cache(endingWords);
            if (doorStories != null) foreach (var binding in doorStories) Cache(binding.text);
            if (talkPanel != null) talkPanel.SetActive(false);
            if (endingPanel != null) endingPanel.SetActive(false);
        }

        private void Cache(Text text)
        {
            if (text == null || lines.ContainsKey(text)) return;
            lines.Add(text, SplitLines(text.text));
            text.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (doorStories != null) foreach (var binding in doorStories)
                if (binding.door != null) binding.door.Passed += OnDoorPassed;
            if (life != null) life.Died += Abort;
        }

        private IEnumerator Start()
        {
            if (skipIntroduction) yield break;
            float elapsed = 0;
            while (elapsed < introductionDelay)
            {
                if (!InputBlocked && Time.timeScale > 0) elapsed += Time.deltaTime;
                yield return null;
            }
            Request(introduction);
        }

        private void Update()
        {
            if (playback == null && pending.Count > 0 && Time.timeScale > 0 && !InputBlocked)
                playback = StartCoroutine(PlayQueue());
            if (Input.GetKeyDown(KeyCode.F) || Input.GetMouseButtonDown(0)) Advance();
        }

        private void OnDoorPassed(InteractiveDoor door)
        {
            if (EndingStarted || doorStories == null) return;
            foreach (var binding in doorStories) if (binding.door == door) Request(binding.text);
        }

        public void Request(Text text)
        {
            if (text == null || (life != null && life.IsDead) || !requested.Add(text)) return;
            pending.Enqueue(text);
        }

        public void BeginEnding()
        {
            if (EndingStarted || (life != null && life.IsDead)) return;
            EndingStarted = true;
            var ability = checkpoint != null && checkpoint.player != null
                ? checkpoint.player.GetComponent<ShadowTraceAbility>() : null;
            if (ability != null) ability.CancelTrace();
            Request(endingTalk);
            // Lock in this physics callback, before another gameplay input can run.
            if (playback == null && !InputBlocked && Time.timeScale > 0)
                playback = StartCoroutine(PlayQueue());
        }

        public void Advance()
        {
            if (!WaitingForAdvance || Time.frameCount <= readyFrame) return;
            WaitingForAdvance = false;
            advance = true;
        }

        private IEnumerator PlayQueue()
        {
            owner = this;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0;
            GameAudioManager.Instance?.SetGameplayPaused(true);
            if (pause != null && pause.button != null)
            {
                previousButtonEnabled = pause.button.interactable;
                pause.button.interactable = false;
            }
            while (pending.Count > 0)
            {
                var text = pending.Dequeue();
                if (EndingStarted && text == endingTalk)
                {
                    // 第一段（普通对白框）正常淡入淡出。
                    yield return PlayText(text, talkPanel);
                    // 第二段是 EndWord 黑底白字：结束时白字淡出，但黑底保持全黑不退场，
                    // 交给跨场景常驻黑幕接管，避免露出游戏画面那一闪。
                    yield return PlayText(endingWords, endingPanel, keepPanelOpaque: true);
                    // 清档失败时允许重试：把白字淡回来等玩家确认，再淡走重试。
                    while (checkpoint != null && !checkpoint.ClearGameProgress())
                    {
                        yield return Fade(currentTextGroup, 1);
                        WaitingForAdvance = true; readyFrame = Time.frameCount; advance = false;
                        yield return new WaitUntil(() => advance);
                        yield return Fade(currentTextGroup, 0);
                    }
                    // 屏幕此刻仍是 EndWord 纯黑：常驻黑幕在肉眼无变化的情况下接管，异步进 Log 后淡开。
                    SceneFadeOverlay.LoadThroughBlack(
                        checkpoint != null ? checkpoint.MenuScenePath : null, endingRevealDuration);
                    yield break;
                }
                yield return PlayText(text, talkPanel);
            }
            Release();
            playback = null;
        }

        private IEnumerator PlayText(Text text, GameObject panel, bool keepPanelOpaque = false)
        {
            if (text == null || panel == null || !lines.TryGetValue(text, out var entries)) yield break;
            CurrentText = text;
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            var panelFade = Group(panel);
            panelFade.blocksRaycasts = true;
            panelFade.alpha = 0;
            text.gameObject.SetActive(true);
            var textFade = Group(text.gameObject);
            currentTextGroup = textFade;
            textFade.alpha = 1;
            if (entries.Length > 0) text.text = entries[0];
            else text.text = string.Empty;
            yield return Fade(panelFade, 1);
            for (int i = 0; i < entries.Length; i++)
            {
                advance = false; readyFrame = Time.frameCount; WaitingForAdvance = true;
                yield return new WaitUntil(() => advance);
                if (i + 1 < entries.Length)
                {
                    yield return Fade(textFade, 0);
                    text.text = entries[i + 1];
                    yield return Fade(textFade, 1);
                }
            }
            if (keepPanelOpaque)
            {
                // 终局：白字淡出即可，黑底 alpha 保持 1、面板保持激活，后续转场黑幕从纯黑接管。
                yield return Fade(textFade, 0);
                text.gameObject.SetActive(false);
                CurrentText = null;
                yield break;
            }
            yield return Fade(panelFade, 0);
            text.gameObject.SetActive(false);
            panel.SetActive(false);
            CurrentText = null;
        }

        private static CanvasGroup Group(GameObject target)
        {
            var group = target.GetComponent<CanvasGroup>();
            return group != null ? group : target.AddComponent<CanvasGroup>();
        }

        private IEnumerator Fade(CanvasGroup group, float target)
        {
            float initial = group.alpha;
            float elapsed = 0;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(initial, target, Mathf.Clamp01(elapsed / fadeDuration));
                yield return null;
            }
            group.alpha = target;
        }

        private void Release()
        {
            WaitingForAdvance = false;
            if (owner != this) return;
            owner = null;
            if (life == null || !life.IsDead) Time.timeScale = previousTimeScale;
            GameAudioManager.Instance?.SetGameplayPaused(false);
            if (pause != null && pause.button != null) pause.button.interactable = previousButtonEnabled;
        }

        private void Abort()
        {
            StopAllCoroutines(); playback = null; pending.Clear();
            if (talkPanel != null) talkPanel.SetActive(false);
            if (endingPanel != null) endingPanel.SetActive(false);
            Release();
        }

        private void OnDisable()
        {
            if (doorStories != null) foreach (var binding in doorStories)
                if (binding.door != null) binding.door.Passed -= OnDoorPassed;
            if (life != null) life.Died -= Abort;
            Abort();
        }
    }
}
