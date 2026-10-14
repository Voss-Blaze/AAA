using System.Collections.Generic;
using UnityEngine;

namespace ShadowTrace
{
    public enum DoorRequirementMode { All, Any }

    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
    public sealed class InteractiveDoor : MonoBehaviour
    {
        public DoorRequirementMode requirementMode = DoorRequirementMode.All;
        public PlayerController2D player;
        public BoxCollider2D interactionArea;
        public Sprite closedSprite;
        public Sprite openSprite;
        public bool PassageGranted => passer != null;
        public bool IsUnlocked
        {
            get
            {
                if (plates.Count == 0) return true;
                foreach (var plate in plates)
                {
                    bool active = plate != null && plate.isActiveAndEnabled && plate.targetDoor == this && plate.IsActive;
                    if (requirementMode == DoorRequirementMode.All && !active) return false;
                    if (requirementMode == DoorRequirementMode.Any && active) return true;
                }
                return requirementMode == DoorRequirementMode.All;
            }
        }
        private readonly List<ShadowPressurePlate> plates = new List<ShadowPressurePlate>();
        private BoxCollider2D blocker;
        private SpriteRenderer sprite;
        private PlayerController2D passer;

        private void Awake()
        {
            blocker = GetComponent<BoxCollider2D>(); sprite = GetComponent<SpriteRenderer>();
            foreach (var plate in FindObjectsOfType<ShadowPressurePlate>(true))
                if (plate.targetDoor == this) Register(plate);
            if (player == null) player = FindObjectOfType<PlayerController2D>();
            RefreshAppearance();
        }

        public void Register(ShadowPressurePlate plate)
        { if (plate != null && !plates.Contains(plate)) plates.Add(plate); }
        public void Unregister(ShadowPressurePlate plate) { plates.Remove(plate); }

        public bool Contains(PlayerController2D visitor) => isActiveAndEnabled && visitor != null &&
            visitor.Shape != null && interactionArea != null && interactionArea.enabled &&
            interactionArea.Distance(visitor.Shape).isOverlapped;

        public bool TryPass(PlayerController2D visitor)
        {
            if (Time.timeScale <= 0 || visitor == null || !visitor.isActiveAndEnabled ||
                !Contains(visitor) || passer != null) return false;
            RefreshPlates();
            if (!IsUnlocked) return false;
            passer = visitor;
            Physics2D.IgnoreCollision(blocker, visitor.Shape, true);
            var ability = visitor.GetComponent<ShadowTraceAbility>();
            if (ability != null)
            {
                ability.CancelTrace();
                if (ability.manager != null) ability.manager.ClearAll();
            }
            // Re-query after clearing: sustained plates release without revoking this safe passage.
            RefreshPlates();
            RefreshAppearance();
            return true;
        }

        private void Update()
        {
            if (passer != null && (!passer.isActiveAndEnabled || !Contains(passer))) RestoreCollision();
            RefreshAppearance();
            if (player != null && Input.GetKeyDown(KeyCode.F)) TryPass(player);
        }

        private void RefreshAppearance()
        {
            var frame = IsUnlocked ? openSprite : closedSprite;
            if (frame != null && sprite != null) sprite.sprite = frame;
        }

        private void RefreshPlates()
        {
            for (int i = plates.Count - 1; i >= 0; i--)
                if (plates[i] != null) plates[i].Refresh();
        }

        private void RestoreCollision()
        {
            if (passer != null && passer.Shape != null && blocker != null)
                Physics2D.IgnoreCollision(blocker, passer.Shape, false);
            passer = null;
        }

        private void OnDisable() { RestoreCollision(); }
    }
}
