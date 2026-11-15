using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTrace
{
    [DisallowMultipleComponent]
    public sealed class FirstPlayTutorial : MonoBehaviour
    {
        public TutorialHUDView view;
        public TutorialLessonSettings settings;
        public TutorialArea area;
        public GameObject obstaclePrefab, climbStepPrefab;
        public Transform practiceAnchor;

        public TutorialStep Step => progress != null ? progress.step : TutorialStep.Introduction;
        public bool IsRunning => isActiveAndEnabled && initialized && Step != TutorialStep.Complete;
        public string CurrentInstruction => instruction != null ? instruction.text : string.Empty;
        public SpikeHazard TeachingSpike { get; private set; }
        public InteractiveDoor TeachingDoor { get; private set; }
        public ShadowLamp TeachingLamp { get; private set; }

        private LevelCheckpointController checkpoint;
        private LevelStoryController story;
        private PlayerController2D player;
        private ShadowTraceAbility ability;
        private ShadowManager manager;
        private TutorialProgress progress;
        private ShadowActor subject;
        private ShadowPressurePlate[] doorPlates;
        private GameObject canvasObject, panel, handbook, props;
        private Text title, instruction, feedback, pointer;
        private Button retry, next;
        private Transform focus;
        private Vector3 practicePosition;
        private float practiceFloor, enteredAt, lastProgressAt, feedbackUntil, initialZoom;
        private bool initialized, bound, movedLeft, movedRight, jumped, selectedSubject, sawMultiple;
        private int avoidDirection;
        private Vector2 followStart;
        private bool recovering;
        private Camera viewCamera;
        private readonly RaycastHit2D[] supportHits = new RaycastHit2D[32];

        private IEnumerator Start()
        {
            checkpoint = GetComponent<LevelCheckpointController>();
            story = GetComponent<LevelStoryController>();
            if (checkpoint == null || checkpoint.player == null || checkpoint.hud == null) { enabled = false; yield break; }
            player = checkpoint.player;
            ability = player.GetComponent<ShadowTraceAbility>();
            manager = checkpoint.hud.manager;
            if (ability == null || manager == null) { enabled = false; yield break; }
            // Checkpoint Start must finish consuming the resume request first.
            yield return null;
            progress = TutorialProgress.Read();
            if (!string.IsNullOrEmpty(progress.scenePath) && progress.scenePath != gameObject.scene.path)
                progress = new TutorialProgress();
            progress.scenePath = gameObject.scene.path;
            practicePosition = progress.hasPracticePosition ? progress.practicePosition :
                practiceAnchor != null ? practiceAnchor.position : player.transform.position;
            ResolveLandmarks();
            viewCamera = SceneComponents<Camera>().FirstOrDefault(c => c.GetComponent<TraceCameraFollow>() != null);
            if (view == null || settings == null || area == null || obstaclePrefab == null || climbStepPrefab == null)
            { Debug.LogError("Assign the authored tutorial HUD, settings, area and practice prefabs.", this); enabled = false; yield break; }
            BindUI();
            Bind();
            initialized = true;
            if (checkpoint.HasResumed)
            {
                if (Step == TutorialStep.Resume && checkpoint.ResumedCheckpointId == progress.checkpointId)
                    SetStep(TutorialStep.Zoom);
                else if (Step < TutorialStep.Resume)
                    SetStep(TutorialStep.Zoom); // Existing saves must never be dragged back to the practice area.
                else EnterStep();
            }
            else if (Step == TutorialStep.Resume || Step == TutorialStep.Zoom)
                SetStep(TutorialStep.Lamp); // Starting a new run is not a successful resume.
            else if (Step != TutorialStep.Introduction)
                SetStep(TutorialProgress.RestartableStep(Step));
            else EnterStep();
        }

        private IEnumerable<T> SceneComponents<T>() where T : Component => gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true));

        private void ResolveLandmarks()
        {
            TeachingSpike = SceneComponents<SpikeHazard>()
                .Where(s => s.isActiveAndEnabled && s.transform.position.x > practicePosition.x)
                .OrderBy(s => Mathf.Abs(s.transform.position.x - practicePosition.x) + Mathf.Abs(s.transform.position.y - practicePosition.y)).FirstOrDefault();
            if (!string.IsNullOrEmpty(checkpoint.tutorialSpikeName))
                TeachingSpike = SceneComponents<SpikeHazard>().FirstOrDefault(s => s.name == checkpoint.tutorialSpikeName);
            TeachingDoor = SceneComponents<InteractiveDoor>()
                .Where(d => d.transform.position.x > practicePosition.x)
                .OrderBy(d => Vector2.Distance(d.transform.position, practicePosition)).FirstOrDefault();
            if (!string.IsNullOrEmpty(checkpoint.tutorialDoorId))
                TeachingDoor = SceneComponents<InteractiveDoor>().FirstOrDefault(d => d.saveId == checkpoint.tutorialDoorId);
            doorPlates = SceneComponents<ShadowPressurePlate>().Where(p => p.targetDoor == TeachingDoor).ToArray();
            TeachingLamp = checkpoint.lamps == null ? null : checkpoint.lamps.Where(l => l != null &&
                (TeachingDoor == null || l.transform.position.x > TeachingDoor.transform.position.x))
                .OrderBy(l => Vector2.Distance(l.transform.position, TeachingDoor != null ? TeachingDoor.transform.position : practicePosition)).FirstOrDefault();
            if (!string.IsNullOrEmpty(checkpoint.tutorialLampId) && checkpoint.lamps != null)
                TeachingLamp = checkpoint.lamps.FirstOrDefault(l => l != null && l.checkpointId == checkpoint.tutorialLampId);
        }

        private void Bind()
        {
            manager.ActorSpawned += OnSpawned;
            manager.ActorRemoved += OnRemoved;
            manager.NoticeShown += OnNotice;
            player.JumpRequested += OnJump;
            checkpoint.CheckpointSaved += OnSaved;
            if (TeachingSpike != null) TeachingSpike.ShadowDestroyed += OnSpikeDestroyed;
            if (TeachingDoor != null) TeachingDoor.Passed += OnPassed;
            if (checkpoint.life != null) checkpoint.life.TryTutorialRecovery = TryRecover;
            bound = true;
        }

        private void Update()
        {
            if (!initialized) return;
            bool blocked = LevelStoryController.InputBlocked || (checkpoint.life != null && checkpoint.life.IsDead);
            canvasObject.SetActive(!blocked);
            panel.SetActive(IsRunning && Step != TutorialStep.Introduction && !blocked && Time.timeScale > 0);
            if (blocked || Time.timeScale <= 0) { pointer.gameObject.SetActive(false); return; }
            if (Step == TutorialStep.Introduction)
            {
                if (story == null || story.IntroductionFinished)
                {
                    if (practiceAnchor != null && checkpoint.SafePosition(practiceAnchor.position, out var safePractice))
                    {
                        practicePosition = safePractice;
                        player.transform.position = safePractice;
                        player.Body.position = safePractice;
                        player.Body.velocity = Vector2.zero;
                        Physics2D.SyncTransforms();
                    }
                    else practicePosition = player.transform.position;
                    progress.practicePosition = practicePosition;
                    progress.hasPracticePosition = true;
                    ResolveLandmarks();
                    SetStep(TutorialStep.Stay);
                }
                return;
            }
            if (!IsRunning) { pointer.gameObject.SetActive(false); return; }
            if (Time.time < enteredAt + 0.15f) return;
            if (Step <= TutorialStep.Sprint && player.Shape.bounds.min.y < practiceFloor - 5) TryRecover();
            if (Step == TutorialStep.DeleteOne && subject != null && manager.SelectedSlot == subject.Number - 1)
                selectedSubject = true;
            if (Step == TutorialStep.FollowPractice && subject != null)
            {
                movedLeft |= subject.Body.position.x < followStart.x - 0.25f;
                movedRight |= subject.Body.position.x > followStart.x + 0.25f;
                if (movedLeft && movedRight && jumped) SetStep(TutorialStep.Avoid);
            }
            if (Step == TutorialStep.AvoidObserve && subject != null)
            {
                int direction = (int)Mathf.Sign(subject.Body.velocity.x);
                if (Mathf.Abs(subject.Body.velocity.x) > 0.1f)
                {
                    if (avoidDirection != 0 && direction != avoidDirection) SetStep(TutorialStep.ClearAll);
                    else avoidDirection = direction;
                }
            }
            if (Step == TutorialStep.Approach && subject != null && subject.Stopped)
                SetStep(TutorialStep.Climb);
            if (Step == TutorialStep.Climb && subject != null && player.IsGrounded &&
                TracePhysics.TryGetSupport(player.Shape, player.worldMask, supportHits, out var support, out _) && support == subject.Shape)
                SetStep(TutorialStep.ApproachSpikes);
            if (Step == TutorialStep.ObserveSpikes && subject != null && Time.time - enteredAt > 15)
                Tell(settings.spikeRetry);
            if (Step == TutorialStep.Sprint && TeachingSpike != null && player.IsGrounded &&
                player.Shape.bounds.min.x > TeachingSpike.GetComponent<Collider2D>().bounds.max.x)
                SetStep(TutorialStep.Door);
            if (Step == TutorialStep.Door && TeachingDoor != null)
            {
                if (TeachingDoor.IsPermanentlyOpen) SetStep(TutorialStep.Lamp);
                else focus = TeachingDoor.IsUnlocked ? TeachingDoor.transform : doorPlates.FirstOrDefault(p => !p.IsActive)?.transform;
            }
            if (Step == TutorialStep.Zoom)
            {
                if (viewCamera != null && Mathf.Abs(viewCamera.orthographicSize - initialZoom) > 0.01f) SetStep(TutorialStep.Finish);
            }
            if (Step == TutorialStep.ClearAll) sawMultiple |= manager.Count >= 2;
            RefreshText();
            UpdatePointer();
        }

        private void SetStep(TutorialStep step)
        {
            progress.step = step;
            progress.Write();
            EnterStep();
        }

        private void EnterStep()
        {
            enteredAt = lastProgressAt = Time.time;
            selectedSubject = false;
            focus = null;
            if (Step >= TutorialStep.Stay && Step <= TutorialStep.ApproachSpikes)
                practiceFloor = progress.hasPracticePosition ? progress.practicePosition.y - (player.transform.position.y - player.Shape.bounds.min.y) : player.Shape.bounds.min.y;
            if (Step == TutorialStep.Avoid || Step == TutorialStep.Approach) CreatePracticeProps();
            if (Step == TutorialStep.AvoidObserve) avoidDirection = 0;
            if (Step == TutorialStep.ClearAll) sawMultiple = manager.Count >= 2;
            if (Step == TutorialStep.Climb) CreateClimbStep();
            if (Step == TutorialStep.ApproachSpikes || Step == TutorialStep.Sprint || Step == TutorialStep.Complete)
            {
                ClearProps();
                if (Step != TutorialStep.Complete) { ability.CancelTrace(); manager.ClearAll(); subject = null; }
            }
            if (Step == TutorialStep.FollowPractice)
            {
                movedLeft = movedRight = jumped = false;
                followStart = subject != null ? subject.Body.position : player.Body.position;
            }
            if (Step == TutorialStep.Door) focus = doorPlates.FirstOrDefault()?.transform;
            if (Step == TutorialStep.Lamp) focus = TeachingLamp != null ? TeachingLamp.transform : null;
            if (subject != null && (Step == TutorialStep.DeleteOne || Step == TutorialStep.FollowPractice ||
                Step == TutorialStep.AvoidObserve || Step == TutorialStep.Climb || Step == TutorialStep.ObserveSpikes))
                focus = subject.transform;
            if (Step == TutorialStep.Zoom)
            {
                initialZoom = viewCamera != null ? viewCamera.orthographicSize : 0;
            }
            retry.gameObject.SetActive(Step >= TutorialStep.Stay && Step <= TutorialStep.Sprint);
            next.gameObject.SetActive(Step == TutorialStep.Resume || Step == TutorialStep.Finish);
            next.GetComponentInChildren<Text>().text = Step == TutorialStep.Resume ? "稍后再试" : "开始探索";
            area.Configure(Step, player);
            RefreshText();
        }

        private void OnSpawned(ShadowActor actor)
        {
            if (!IsRunning || recovering) return;
            ShadowKind? expected = Step == TutorialStep.Stay ? ShadowKind.Stay : Step == TutorialStep.Follow ? ShadowKind.Follow :
                Step == TutorialStep.Avoid ? ShadowKind.Avoid : Step == TutorialStep.Approach || Step == TutorialStep.ApproachSpikes ? ShadowKind.Approach : (ShadowKind?)null;
            if (!expected.HasValue) return;
            if (actor.Kind != expected.Value)
            {
                Tell(settings.wrongType);
                return;
            }
            subject = actor;
            focus = actor.transform;
            if (Step == TutorialStep.Stay) SetStep(TutorialStep.DeleteOne);
            else if (Step == TutorialStep.Follow) SetStep(TutorialStep.FollowPractice);
            else if (Step == TutorialStep.Avoid) SetStep(TutorialStep.AvoidObserve);
            else if (Step == TutorialStep.ApproachSpikes) SetStep(TutorialStep.ObserveSpikes);
        }

        private void OnRemoved(ShadowActor actor)
        {
            if (recovering || actor != subject) return;
            if (Step == TutorialStep.DeleteOne && selectedSubject) SetStep(TutorialStep.Follow);
            else if (Step == TutorialStep.FollowPractice) SetStep(TutorialStep.Follow);
            else if (Step == TutorialStep.AvoidObserve) SetStep(TutorialStep.Avoid);
            else if (Step == TutorialStep.Climb || Step == TutorialStep.Approach) SetStep(TutorialStep.Approach);
            else if (Step == TutorialStep.ObserveSpikes) SetStep(TutorialStep.ApproachSpikes);
        }

        private void OnNotice(ShadowNotice notice)
        {
            if (Step == TutorialStep.ClearAll && sawMultiple && notice == ShadowNotice.Cleared && manager.Count == 0)
                SetStep(TutorialStep.Approach);
        }

        private void OnJump()
        {
            if (Step == TutorialStep.FollowPractice && player.IsGrounded) jumped = true;
        }

        private void OnSpikeDestroyed(ShadowActor actor)
        {
            if (Step == TutorialStep.ObserveSpikes && actor == subject)
            {
                subject = null;
                SetStep(TutorialStep.Sprint);
                Tell(settings.spikeResult, 5);
            }
        }

        private void OnPassed(InteractiveDoor door)
        {
            if (Step == TutorialStep.Door && door == TeachingDoor)
            {
                SetStep(TutorialStep.Lamp);
                Tell(settings.doorResult, 5);
            }
        }

        private void OnSaved(ShadowLamp lamp)
        {
            if (!IsRunning) return;
            if (Step >= TutorialStep.Resume)
            {
                progress.checkpointId = lamp.checkpointId;
                progress.Write();
                return;
            }
            if (lamp != TeachingLamp) return;
            progress.checkpointId = lamp.checkpointId;
            SetStep(TutorialStep.Resume);
        }

        public void Skip()
        {
            if (!initialized) return;
            SetStep(TutorialStep.Complete);
            Tell(string.Empty);
        }

        public void Continue()
        {
            if (Step == TutorialStep.Resume) SetStep(TutorialStep.Zoom);
            else if (Step == TutorialStep.Finish) SetStep(TutorialStep.Complete);
        }

        public void Retry()
        {
            if (!IsRunning || Step < TutorialStep.Stay || Step > TutorialStep.Sprint) return;
            RecoverPractice();
            Tell(settings.retryResult);
        }

        private bool TryRecover()
        {
            if (!IsRunning || Step < TutorialStep.Stay || Step > TutorialStep.Sprint || recovering) return false;
            RecoverPractice();
            Tell(settings.retryResult);
            return true;
        }

        private void RecoverPractice()
        {
            recovering = true;
            ability.CancelTrace(); manager.ClearAll(); subject = null;
            ClearProps();
            player.transform.position = practicePosition;
            player.Body.position = practicePosition;
            player.Body.velocity = Vector2.zero;
            Physics2D.SyncTransforms();
            player.RefreshGrounding();
            if (viewCamera != null)
            {
                var follow = viewCamera.GetComponent<TraceCameraFollow>();
                viewCamera.transform.position = new Vector3(practicePosition.x + follow.offset.x,
                    practicePosition.y + follow.offset.y, viewCamera.transform.position.z);
            }
            SetStep(TutorialProgress.RestartableStep(Step));
            recovering = false;
        }

        private void CreatePracticeProps()
        {
            ClearProps();
            props = new GameObject("Temporary Tutorial Props");
            props.transform.SetParent(transform, false);
            practiceFloor = player.Shape.bounds.min.y;
            if (checkpoint.SafePosition(practicePosition, out var safe))
                practiceFloor = safe.y - (player.transform.position.y - player.Shape.bounds.min.y) - 0.015f;
            CreateBlock("练习障碍", practicePosition.x + 6);
            CreateBlock("练习边界", practicePosition.x - 4);
            Physics2D.SyncTransforms();
        }

        private void CreateClimbStep()
        {
            if (subject == null || props == null) return;
            var bounds = subject.Shape.bounds;
            var stepShape = climbStepPrefab.GetComponent<BoxCollider2D>();
            float halfWidth = stepShape.size.x * Mathf.Abs(climbStepPrefab.transform.localScale.x) * 0.5f;
            // Approach shadows can stop against either practice obstacle. Put the
            // foothold on the player's side, rather than always to the left.
            float side = Mathf.Sign(player.Shape.bounds.center.x - bounds.center.x);
            if (Mathf.Abs(player.Shape.bounds.center.x - bounds.center.x) < 0.05f)
                side = -subject.AnimationDirection;
            float x = bounds.center.x + side * (bounds.extents.x + 0.12f + halfWidth);
            foreach (var obstacle in props.GetComponentsInChildren<BoxCollider2D>())
                if (x + halfWidth > obstacle.bounds.min.x && x - halfWidth < obstacle.bounds.max.x)
                {
                    side = -side;
                    x = bounds.center.x + side * (bounds.extents.x + 0.12f + halfWidth);
                    break;
                }
            x -= stepShape.offset.x * climbStepPrefab.transform.localScale.x;
            CreateBlock("辅助矮台", x);
            Physics2D.SyncTransforms();
        }

        private void CreateBlock(string name, float x)
        {
            var prefab = name == "辅助矮台" ? climbStepPrefab : obstaclePrefab;
            var block = Instantiate(prefab, props.transform);
            block.name = name;
            var collider = block.GetComponent<BoxCollider2D>();
            float scaleY = block.transform.lossyScale.y;
            block.transform.position = new Vector3(x, practiceFloor + collider.size.y * Mathf.Abs(scaleY) * 0.5f - collider.offset.y * scaleY, 0);
            // Keep the prefab's authored size, sprite, tint and outline editable.
        }

        private void ClearProps()
        {
            if (props != null) { props.SetActive(false); Destroy(props); props = null; }
        }

        private void Tell(string text, float duration = 5)
        {
            if (feedback == null) return;
            feedback.text = text;
            feedbackUntil = Time.time + duration;
        }

        private void RefreshText()
        {
            var lesson = settings.Find(Step);
            title.text = lesson != null ? lesson.title : string.Empty;
            string text = lesson != null ? lesson.instruction : string.Empty;
            string status = string.Empty;
            bool making = Step == TutorialStep.Stay || Step == TutorialStep.Follow || Step == TutorialStep.Avoid ||
                Step == TutorialStep.Approach || Step == TutorialStep.ApproachSpikes;
            if (making && ability.Phase == TracePhase.Preparing)
                text = settings.preparingFormat.Replace("{time}", Mathf.CeilToInt(ability.Remaining).ToString());
            else if (making && ability.Phase == TracePhase.Recording)
            {
                text = lesson != null ? lesson.recordingInstruction : text;
                status = settings.recordingFormat.Replace("{time}", Mathf.CeilToInt(ability.Remaining).ToString());
            }
            else if (Step == TutorialStep.Approach && subject != null) text = settings.approachWatching;
            if (Step == TutorialStep.Door)
            {
                if (TeachingDoor != null && TeachingDoor.IsUnlocked) text = settings.doorUnlocked;
                else status = settings.doorCountFormat.Replace("{count}", doorPlates.Length.ToString());
            }
            if (Step == TutorialStep.ClearAll && !sawMultiple) text = settings.needTwoShadows;
            if (Step == TutorialStep.FollowPractice)
                status = (movedLeft ? "✓" : "○") + " 向左   " + (movedRight ? "✓" : "○") + " 向右   " + (jumped ? "✓" : "○") + " 跳跃";
            instruction.text = text;
            view.status.text = status;
            if (Time.time > feedbackUntil)
                feedback.text = Time.time - lastProgressAt > 20 && Step <= TutorialStep.Sprint ? settings.retryHint : string.Empty;
            if (Step == TutorialStep.ApproachSpikes && TeachingSpike != null) focus = null;
        }

        private void UpdatePointer()
        {
            if (viewCamera == null || !panel.activeSelf) { pointer.gameObject.SetActive(false); return; }
            Vector3 position;
            if (Step == TutorialStep.ApproachSpikes && TeachingSpike != null)
                position = new Vector3(TeachingSpike.GetComponent<Collider2D>().bounds.min.x - 1, practiceFloor + 0.5f, 0);
            else
            {
                if (focus == null) { pointer.gameObject.SetActive(false); return; }
                var collider = focus.GetComponent<Collider2D>();
                position = collider != null ? new Vector3(collider.bounds.center.x, collider.bounds.max.y + 0.5f, 0) : focus.position + Vector3.up;
            }
            var screen = viewCamera.WorldToScreenPoint(position);
            pointer.gameObject.SetActive(screen.z > 0 && screen.x >= 0 && screen.x <= Screen.width && screen.y >= 0 && screen.y <= Screen.height);
            pointer.transform.position = screen + Vector3.up * (5 * Mathf.Sin(Time.unscaledTime * 4));
        }

        private void BindUI()
        {
            canvasObject = view.gameObject;
            panel = view.guidance; handbook = view.handbook;
            title = view.title; instruction = view.instruction; feedback = view.feedback; pointer = view.pointer;
            retry = view.retry; next = view.next;
            view.handbookText.text = settings.handbook;
            view.Bind(this);
        }

        private void OnDestroy()
        {
            if (bound)
            {
                if (manager != null) { manager.ActorSpawned -= OnSpawned; manager.ActorRemoved -= OnRemoved; manager.NoticeShown -= OnNotice; }
                if (player != null) player.JumpRequested -= OnJump;
                if (checkpoint != null) checkpoint.CheckpointSaved -= OnSaved;
                if (TeachingSpike != null) TeachingSpike.ShadowDestroyed -= OnSpikeDestroyed;
                if (TeachingDoor != null) TeachingDoor.Passed -= OnPassed;
                if (checkpoint != null && checkpoint.life != null && checkpoint.life.TryTutorialRecovery == TryRecover)
                    checkpoint.life.TryTutorialRecovery = null;
            }
            ClearProps();
            if (area != null) area.Configure(TutorialStep.Complete, null);
        }
    }
}
