using System.Collections;
using UnityEngine;

namespace ShadowTrace
{
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerController2D))]
    public sealed class PlayerLife : MonoBehaviour
    {
        [Min(0.05f)] public float effectDuration = 0.6f;
        public bool IsDead { get; private set; }
        public event System.Action Died;
        private bool ownsPause;
        private float previousTimeScale;

        public void Die()
        {
            if (IsDead) return;
            IsDead = true;
            var player = GetComponent<PlayerController2D>();
            var ability = GetComponent<ShadowTraceAbility>();
            if (ability != null)
            {
                ability.CancelTrace(); ability.enabled = false;
                if (ability.manager != null) ability.manager.enabled = false;
            }
            player.enabled = false;
            player.Body.velocity = Vector2.zero;
            player.Body.simulated = false;
            var animation = GetComponent<ActorAnimationDriver>();
            if (animation != null) animation.enabled = false;
            var animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.enabled = false;
            previousTimeScale = Time.timeScale;
            ownsPause = previousTimeScale > 0;
            Time.timeScale = 0;
            StartCoroutine(DeathEffect());
            Died?.Invoke();
        }

        private IEnumerator DeathEffect()
        {
            var sprite = GetComponentInChildren<SpriteRenderer>();
            if (sprite == null) yield break;
            Color original = sprite.color;
            float elapsed = 0;
            while (elapsed < effectDuration)
            {
                float progress = elapsed / effectDuration;
                Color tint = (int)(elapsed * 12) % 2 == 0 ? Color.red : original;
                tint.a = original.a * (1 - progress);
                sprite.color = tint;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            sprite.color = new Color(original.r, original.g, original.b, 0);
        }

        private void OnDisable()
        {
            // Scene unload / stopping Play must not leave the next run globally paused.
            if (ownsPause) { Time.timeScale = previousTimeScale; ownsPause = false; }
        }
    }
}
