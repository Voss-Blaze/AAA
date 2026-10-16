using UnityEngine;

namespace ShadowTrace
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class EndGameTrigger : MonoBehaviour
    {
        public LevelStoryController story;
        private void Reset() { GetComponent<BoxCollider2D>().isTrigger = true; }
        private void OnTriggerEnter2D(Collider2D other) { Check(other); }
        private void OnTriggerStay2D(Collider2D other) { Check(other); }
        private void Check(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerController2D>();
            if (player == null || !player.isActiveAndEnabled || Time.timeScale <= 0) return;
            if (story == null) story = FindObjectOfType<LevelStoryController>();
            if (story != null && (story.checkpoint == null || story.checkpoint.player == player)) story.BeginEnding();
        }
    }
}
