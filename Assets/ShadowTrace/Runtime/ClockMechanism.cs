using System;
using UnityEngine;

namespace ShadowTrace
{
    public enum ClockTriggerMode { Latched, WhilePressed }

    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
    public sealed class ClockMechanism : MonoBehaviour
    {
        [Serializable] public sealed class Hand
        {
            public Rigidbody2D pivot;
            [Tooltip("顺时针角速度，单位：度/秒。")]
            [Min(0)] public float speed;
        }

        public Hand hour = new Hand { speed = 3 };
        public Hand minute = new Hand { speed = 6 };
        public Hand second = new Hand { speed = 12 };
        public ClockTriggerMode mode = ClockTriggerMode.Latched;
        public bool playerCanPress = true;
        public bool shadowCanPress;
        public BoxCollider2D pressArea;
        public Sprite idleSprite;
        public Sprite pressedSprite;
        public bool IsPressed { get; private set; }
        public bool IsRunning { get; private set; }
        private bool activated;
        private SpriteRenderer sprite;
        private readonly Collider2D[] overlaps = new Collider2D[32];

        private void Awake()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
            if (pressArea == null) pressArea = GetComponent<BoxCollider2D>();
            pressArea.isTrigger = true;
            sprite = GetComponent<SpriteRenderer>();
            if (idleSprite == null) idleSprite = sprite.sprite;
        }

        private void FixedUpdate()
        {
            Refresh();
            Rotate(hour); Rotate(minute); Rotate(second);
        }

        public void Refresh()
        {
            bool pressed = false;
            if (isActiveAndEnabled && pressArea != null && pressArea.enabled && pressArea.gameObject.activeInHierarchy)
            {
                int count = pressArea.OverlapCollider(new ContactFilter2D().NoFilter(), overlaps);
                for (int i = 0; i < count; i++)
                {
                    var player = playerCanPress ? overlaps[i].GetComponentInParent<PlayerController2D>() : null;
                    var shadow = shadowCanPress ? overlaps[i].GetComponentInParent<ShadowActor>() : null;
                    if ((player != null && player.isActiveAndEnabled && player.Body.simulated &&
                        (player.GetComponent<PlayerLife>() == null || !player.GetComponent<PlayerLife>().IsDead)) ||
                        (shadow != null && shadow.isActiveAndEnabled && shadow.Body != null && shadow.Body.simulated))
                    { pressed = true; break; }
                }
            }
            if (pressed && !IsPressed) GameAudioManager.Instance?.PlaySound(GameSound.Plate);
            IsPressed = pressed;
            activated |= pressed;
            IsRunning = mode == ClockTriggerMode.Latched ? activated : pressed;
            var frame = pressed ? pressedSprite : idleSprite;
            if (frame != null) sprite.sprite = frame;
        }

        private void Rotate(Hand hand)
        {
            if (hand == null || hand.pivot == null) return;
            if (!IsRunning || hand.speed <= 0) { hand.pivot.angularVelocity = 0; return; }
            hand.pivot.MoveRotation(hand.pivot.rotation - Mathf.Max(0, hand.speed) * Time.fixedDeltaTime);
        }

        private void OnDisable()
        {
            IsPressed = false; IsRunning = false;
            Stop(hour); Stop(minute); Stop(second);
        }

        private static void Stop(Hand hand) { if (hand != null && hand.pivot != null) hand.pivot.angularVelocity = 0; }
    }
}
