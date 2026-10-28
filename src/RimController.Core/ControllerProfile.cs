using System;
using System.Collections.Generic;
using System.Linq;

namespace RimController.Core
{
    public sealed class BindingOverride
    {
        public InputContext Context { get; }
        public ControllerAction Action { get; }
        public PadButton Button { get; }
        public BindingOverride(InputContext context, ControllerAction action, PadButton button)
        { Context = context; Action = action; Button = button; }
    }

    public sealed class ControllerProfile
    {
        private BindingOverride[] overrides = Array.Empty<BindingOverride>();
        public PadModel Model { get; }
        public int Revision { get; private set; }
        public ControllerProfile(PadModel model)
        {
            if (model != PadModel.DualSense && model != PadModel.DualShock4) throw new ArgumentException("Unsupported model.");
            Model = model;
        }
        public BindingOverride[] Export() => overrides.ToArray();
        public void Restore(IEnumerable<BindingOverride> entries)
        {
            overrides = entries.ToArray();
            Revision++;
        }
        public Bindings Resolve(PadCapabilities capabilities, ICollection<string> issues)
            => Bindings.Restore(capabilities, overrides, issues);
        public bool TryRebind(PadCapabilities capabilities, InputContext context, ControllerAction action,
            PadButton button, out string issue)
        {
            var effective = Resolve(capabilities, new List<string>());
            if (!effective.TryRebind(context, action, button))
            { issue = "RC_ProfileConflict"; return false; }
            // Retain overrides for inputs temporarily absent on this connection.
            overrides = overrides.Where(x => x.Context != context || x.Action != action)
                .Concat(new[] { new BindingOverride(context, action, button) }).ToArray();
            Revision++;
            issue = "";
            return true;
        }
        public void Reset() { overrides = Array.Empty<BindingOverride>(); Revision++; }
    }

    public sealed class ControllerProfiles
    {
        public ControllerProfile DualSense { get; } = new(PadModel.DualSense);
        public ControllerProfile DualShock4 { get; } = new(PadModel.DualShock4);
        public bool TutorialEnabled { get; set; } = true;
        private readonly Dictionary<string, LessonStatus> lessons = new(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, LessonStatus> Lessons => new Dictionary<string, LessonStatus>(lessons);
        public List<string> LoadIssues { get; } = new();
        public ControllerProfile? For(PadModel model) => model == PadModel.DualSense ? DualSense :
            model == PadModel.DualShock4 ? DualShock4 : null;
        public void RestoreLessons(IEnumerable<KeyValuePair<string, LessonStatus>> entries)
        {
            lessons.Clear();
            foreach (var pair in entries)
                if (!string.IsNullOrWhiteSpace(pair.Key) && Enum.IsDefined(typeof(LessonStatus), pair.Value))
                    lessons[pair.Key] = pair.Value;
        }
        public void LoadTutorial<T>(Tutorial<T> tutorial)
        { tutorial.Enabled = TutorialEnabled; tutorial.Restore(Lessons); }
        public void SaveTutorial<T>(Tutorial<T> tutorial)
        {
            TutorialEnabled = tutorial.Enabled;
            // Preserve lessons owned by other courses.
            foreach (var pair in tutorial.Export()) lessons[pair.Key] = pair.Value;
        }
    }
}
