using UnityEngine;

namespace ShadowTrace
{
    public sealed class ShadowManager : MonoBehaviour
    {
        public PlayerController2D player;
        public ShadowActor shadowPrefab;
        [Min(0.1f)] public float groundSearchDistance = 20;
        [Min(0.05f)] public float doubleClickWindow = 0.3f;
        public int SelectedSlot { get; private set; } = -1;
        public string Message { get; private set; }
        public float MessageUntil { get; private set; }
        public int Count => slots.Count;
        public bool Full => Count >= actors.Length;
        public ShadowActor[] Actors => actors;
        private readonly ShadowActor[] actors = new ShadowActor[8];
        private readonly ShadowSlots slots = new ShadowSlots(8);
        private bool pendingDelete;
        private float deleteTime;
        private ShadowActor deleteTarget;

        private void Update()
        {
            for (int i = 0; i < actors.Length; i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) Select(i);

            float now = Time.unscaledTime;
            // Commit an expired first click before treating a new click as another single click.
            if (pendingDelete && now - deleteTime > doubleClickWindow) CommitDelete();
            if (!Input.GetKeyDown(KeyCode.R)) return;
            if (pendingDelete)
            {
                pendingDelete = false; deleteTarget = null;
                ClearAll();
                ShowMessage("All shadows cleared.");
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
            if (Full) { ShowMessage("Limit reached: delete a shadow first."); return false; }
            if (player == null || shadowPrefab == null)
            { ShowMessage("Missing player or shadow prefab reference."); return false; }
            var shape = shadowPrefab.GetComponent<BoxCollider2D>();
            Vector2 size = Vector2.Scale(shape.size, shadowPrefab.transform.localScale);
            Physics2D.SyncTransforms();
            if (!TracePhysics.SpawnPosition(origin, kind, size, shadowPrefab.worldMask,
                groundSearchDistance, out var position))
            { ShowMessage("Cannot spawn: no ground or the starting position is blocked."); return false; }
            int slot = slots.Allocate();
            var actor = Instantiate(shadowPrefab, position, Quaternion.identity);
            actor.name = "Shadow " + (slot + 1) + " - " + kind;
            actor.Initialize(kind, slot + 1, direction, player, this);
            foreach (var other in actors)
                if (other != null) Physics2D.IgnoreCollision(actor.Shape, other.Shape, true);
            actors[slot] = actor;
            ShowMessage("Created #" + (slot + 1) + ": " + kind);
            return true;
        }

        public void Select(int slot)
        {
            if (SelectedSlot >= 0 && actors[SelectedSlot] != null) actors[SelectedSlot].SetSelected(false);
            SelectedSlot = slot >= 0 && slot < actors.Length && actors[slot] != null ? slot : -1;
            if (SelectedSlot >= 0) actors[SelectedSlot].SetSelected(true);
        }

        private void Remove(ShadowActor actor)
        {
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
        }

        public void ClearAll()
        {
            for (int i = 0; i < actors.Length; i++) if (actors[i] != null) Remove(actors[i]);
        }

        public void ShowMessage(string message)
        { Message = message; MessageUntil = Time.unscaledTime + 3; }

        private void OnDisable() { pendingDelete = false; deleteTarget = null; }
    }
}
