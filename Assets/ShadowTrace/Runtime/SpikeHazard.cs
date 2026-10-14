using UnityEngine;

namespace ShadowTrace
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
    public sealed class SpikeHazard : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other) { Hit(other); }
        private void OnTriggerStay2D(Collider2D other) { Hit(other); }
        private void Hit(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerController2D>();
            if (player != null && player.isActiveAndEnabled)
            {
                var life = player.GetComponent<PlayerLife>();
                if (life != null) life.Die();
            }
        }
    }
}
