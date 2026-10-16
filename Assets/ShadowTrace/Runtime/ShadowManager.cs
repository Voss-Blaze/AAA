using System;
using UnityEngine;

namespace ShadowTrace
{
    public sealed class ShadowManager : MonoBehaviour
    {
        public PlayerController2D player;
        public ShadowActor shadowPrefab;
        public ShadowUISettings uiSettings;
        public event Action SlotsChanged;
        public event Action<ShadowNotice> NoticeShown;
        [Min(1), Tooltip("同时存在的影子数量上限；在进入 Play 前设置，运行中修改需重新运行生效。")]
        public int maxShadows = 8;
        [Min(0.1f)] public float groundSearchDistance = 20;
        [Min(0.05f)] public float doubleClickWindow = 0.3f;
        public int SelectedSlot { get; private set; } = -1;
        public string Message { get; private set; }
        public float MessageUntil { get; private set; }
        public int Count => slots.Count;
        public int Capacity => actors.Length;
        public bool Full => Count >= actors.Length;
        public ShadowActor[] Actors => actors;
        private ShadowActor[] actors;
        private ShadowSlots slots;
        private bool pendingDelete;
        private float deleteTime;
        private ShadowActor deleteTarget;

        private void Awake()
        {
            maxShadows = Mathf.Max(1, maxShadows);
            actors = new ShadowActor[maxShadows];
            slots = new ShadowSlots(maxShadows);
        }

        private void OnValidate() { maxShadows = Mathf.Max(1, maxShadows); }

        private void Update()
        {
            if (Time.timeScale <= 0) return;
            for (int i = 0; i < Mathf.Min(actors.Length, 9); i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) Select(i);
            if (actors.Length > 9 && Input.GetKeyDown(KeyCode.Tab)) CycleSelection();

            float now = Time.time;
            // Commit an expired first click before treating a new click as another single click.
            if (pendingDelete && now - deleteTime > doubleClickWindow) CommitDelete();
            if (!Input.GetKeyDown(KeyCode.R)) return;
            if (pendingDelete)
            {
                pendingDelete = false; deleteTarget = null;
                ClearAll();
                ShowNotice(ShadowNotice.Cleared);
            }
            else
            {
                pendingDelete = true; deleteTime = now;
                deleteTarget = SelectedSlot >= 0 ? actors[SelectedSlot] : null;
            }
        }

        private void CommitDelete()
        {
            pendingDelete = false;
            if (deleteTarget != null) Remove(deleteTarget);
            deleteTarget = null;
        }

        public bool TrySpawn(ShadowKind kind, Vector2 origin, int direction)
        {
            if (Time.timeScale <= 0) return false;
            if (Full) { ShowNotice(ShadowNotice.LimitReached); return false; }
            if (player == null || shadowPrefab == null)
            { ShowNotice(ShadowNotice.MissingReferences); return false; }
            var shape = shadowPrefab.GetComponent<BoxCollider2D>();
            Vector2 scale = shadowPrefab.transform.lossyScale;
            Vector2 size = Vector2.Scale(shape.size, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
            Vector2 centerOffset = Vector2.Scale(shape.offset, scale);
            Physics2D.SyncTransforms();
            if (!TracePhysics.SpawnPosition(origin, kind, size, centerOffset, shadowPrefab.worldMask,
                groundSearchDistance, out var position))
            { ShowNotice(ShadowNotice.SpawnBlocked); return false; }
            int slot = slots.Allocate();
            var actor = Instantiate(shadowPrefab, position, Quaternion.identity);
            actor.name = "Shadow " + (slot + 1) + " - " + kind;
            actor.Initialize(kind, slot + 1, direction, player, this);
            foreach (var other in actors)
                if (other != null) Physics2D.IgnoreCollision(actor.Shape, other.Shape, true);
            actors[slot] = actor;
            SlotsChanged?.Invoke();
            ShowNotice(ShadowNotice.Spawned);
            return true;
        }

        public void Select(int slot)
        {
            if (Time.timeScale <= 0) return;
            if (SelectedSlot >= 0 && actors[SelectedSlot] != null) actors[SelectedSlot].SetSelected(false);
            SelectedSlot = slot >= 0 && slot < actors.Length && actors[slot] != null ? slot : -1;
            if (SelectedSlot >= 0) actors[SelectedSlot].SetSelected(true);
        }

        public void CycleSelection()
        {
            for (int offset = 1; offset <= actors.Length; offset++)
            {
                int slot = (SelectedSlot + offset) % actors.Length;
                if (actors[slot] != null) { Select(slot); return; }
            }
            Select(-1);
        }

        public void Remove(ShadowActor actor)
        {
            if (actor == null) return;
            int slot = actor.Number - 1;
            if (slot < 0 || slot >= actors.Length || actors[slot] != actor) return;
            NotifyDestroyed(actor);
            // Disable immediately; Destroy is deferred until the end of the frame.
            actor.gameObject.SetActive(false);
            Destroy(actor.gameObject);
        }

        public void NotifyDestroyed(ShadowActor actor)
        {
            int slot = actor.Number - 1;
            if (slot < 0 || slot >= actors.Length || actors[slot] != actor) return;
            if (SelectedSlot == slot) SelectedSlot = -1;
            actors[slot] = null;
            slots.Release(slot);
            SlotsChanged?.Invoke();
        }

        public void ClearAll()
        {
            for (int i = 0; i < actors.Length; i++) if (actors[i] != null) Remove(actors[i]);
        }

        public void ShowMessage(string message)
        { Message = message; MessageUntil = Time.time + (uiSettings != null ? uiSettings.noticeDuration : 0); }

        public void ShowNotice(ShadowNotice notice)
        {
            ShowMessage(uiSettings != null ? uiSettings.Notice(notice) : string.Empty);
            NoticeShown?.Invoke(notice);
        }

        private void OnDisable() { pendingDelete = false; deleteTarget = null; }
    }
}
