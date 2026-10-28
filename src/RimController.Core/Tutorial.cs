using System;
using System.Collections.Generic;
using System.Linq;

namespace RimController.Core
{
    public enum LessonStatus { Pending, Completed, Skipped }

    public sealed class TutorialLesson<TState>
    {
        public string Id { get; }
        public string TextKey { get; }
        public ControllerAction Action { get; }
        public InputContext Context { get; }
        internal Func<TState, bool> Condition { get; }
        public TutorialLesson(string id, string textKey, ControllerAction action,
            InputContext context, Func<TState, bool> condition)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Lesson ID required.", nameof(id));
            if (string.IsNullOrWhiteSpace(textKey)) throw new ArgumentException("Text key required.", nameof(textKey));
            Id = id; TextKey = textKey; Action = action; Context = context;
            Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }
        // Resolve at display time so hints cannot retain stale button assignments.
        public string ButtonLabel(Bindings bindings) => PlayStationGlyphs.Label(bindings.ButtonFor(Context, Action));
    }

    // Game adapters supply observed state. Button presses alone never complete lessons.
    public sealed class Tutorial<TState>
    {
        private readonly TutorialLesson<TState>[] lessons;
        private readonly Dictionary<string, LessonStatus> progress = new(StringComparer.Ordinal);
        public bool Enabled { get; set; } = true;
        public bool ControllerMode { get; set; }
        public TutorialLesson<TState>? Current => Enabled && ControllerMode
            ? lessons.FirstOrDefault(x => progress[x.Id] == LessonStatus.Pending) : null;
        public Tutorial(IEnumerable<TutorialLesson<TState>> lessons)
        {
            this.lessons = lessons?.ToArray() ?? throw new ArgumentNullException(nameof(lessons));
            foreach (var lesson in this.lessons)
            {
                if (lesson == null || progress.ContainsKey(lesson.Id)) throw new ArgumentException("Unique, non-null lessons required.");
                progress.Add(lesson.Id, LessonStatus.Pending);
            }
        }
        public bool Observe(TState state)
        {
            var current = Current;
            if (current == null || !current.Condition(state)) return false;
            progress[current.Id] = LessonStatus.Completed;
            return true; // At most one lesson per observation; no cascade through an existing colony.
        }
        public bool Skip()
        {
            var current = Current;
            if (current == null) return false;
            progress[current.Id] = LessonStatus.Skipped;
            return true;
        }
        public void Replay(string id)
        {
            if (!progress.ContainsKey(id)) throw new ArgumentException("Unknown lesson.", nameof(id));
            progress[id] = LessonStatus.Pending;
        }
        public LessonStatus Status(string id) => progress[id];
        // Detached data for a future mod-profile serializer. Never stored in a game component.
        public Dictionary<string, LessonStatus> Export() => new(progress, StringComparer.Ordinal);
        public void Restore(IReadOnlyDictionary<string, LessonStatus> saved)
        {
            if (saved == null) throw new ArgumentNullException(nameof(saved));
            foreach (var lesson in lessons)
                progress[lesson.Id] = saved.TryGetValue(lesson.Id, out var status) && Enum.IsDefined(typeof(LessonStatus), status)
                    ? status : LessonStatus.Pending;
        }
    }
}
