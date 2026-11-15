using System;
using System.Linq;
using UnityEngine;

namespace ShadowTrace
{
    [CreateAssetMenu(menuName = "Shadow Trace/Tutorial Lessons")]
    public sealed class TutorialLessonSettings : ScriptableObject
    {
        [Serializable] public sealed class Lesson
        {
            public TutorialStep step;
            public string title;
            [TextArea(1, 3)] public string instruction;
            [TextArea(1, 3)] public string recordingInstruction;
        }
        public Lesson[] lessons;
        public string preparingFormat = "准备中 · {time} 秒（E 取消）";
        public string recordingFormat = "记录中 · {time} 秒";
        public string needTwoShadows = "再生成一个影子，再快速按两次 R。";
        public string approachWatching = "观察影子：遇到障碍后，它会停下。";
        public string doorUnlocked = "靠近大门，按 F 通行。";
        public string doorCountFormat = "需要 {count} 个机关 · 影子踩住机关";
        public string retryHint = "还没成功？可点击“重试本步”。";
        public string wrongType = "类型不符，按当前提示再试一次。";
        public string spikeRetry = "影子没碰到地刺，请重试本步。";
        public string spikeResult = "地刺会消除影子，也会伤害你。";
        public string doorResult = "所有影子已清除，大门永久开启。";
        public string retryResult = "已回到安全练习点，请再试一次。";
        [TextArea(10, 20)] public string handbook;
        public Lesson Find(TutorialStep step) => lessons != null ? lessons.FirstOrDefault(l => l.step == step) : null;
    }
}
