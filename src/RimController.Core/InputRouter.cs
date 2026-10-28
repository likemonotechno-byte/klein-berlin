using System;
using System.Collections.Generic;

namespace RimController.Core
{
    public sealed class RoutedFrame
    {
        public HashSet<ControllerAction> Actions { get; } = new();
        public bool SafetyPause { get; internal set; }
        public bool Armed { get; internal set; }
        public Stick Camera { get; internal set; }
        public Stick Cursor { get; internal set; }
        public float Zoom { get; internal set; }
    }
    // Poll in Update, never in OnGUI: Unity can draw several times in one frame.
    public sealed class InputRouter
    {
        private long lastFrame = -1;
        private double lastTime;
        private bool usable, armed;
        private ulong device;
        private InputContext context;
        private object? owner;
        private PadButton previous;
        private readonly Dictionary<PadButton, double> repeatAt = new();
        public float Deadzone { get; set; } = 0.18f;
        public float Sensitivity { get; set; } = 1;
        public bool InvertY { get; set; }
        public Bindings Bindings { get; private set; } = new(PadCapabilities.None);
        private PadCapabilities capabilities;
        public ControllerProfiles? Profiles { get; set; }
        public IReadOnlyList<string> BindingIssues { get; private set; } = Array.Empty<string>();
        private ControllerProfile? appliedProfile;
        private int profileRevision = -1, bindingRevision;
        private PadModel model;
        private void RefreshBindings(ControllerSnapshot pad)
        {
            if (!pad.Connected) return;
            var profile = Profiles?.For(pad.Model);
            if (capabilities != pad.Capabilities || model != pad.Model || !ReferenceEquals(appliedProfile, profile) ||
                profileRevision != (profile?.Revision ?? -1))
            {
                capabilities = pad.Capabilities; model = pad.Model; appliedProfile = profile;
                profileRevision = profile?.Revision ?? -1;
                var issues = new List<string>();
                Bindings = profile?.Resolve(capabilities, issues) ?? new Bindings(capabilities);
                BindingIssues = issues;
                Reset();
            }
            if (bindingRevision != Bindings.Revision) Reset();
            bindingRevision = Bindings.Revision;
        }
        public void Reset()
        {
            armed = false;
            previous = PadButton.None;
            repeatAt.Clear();
        }
        public RoutedFrame Update(long frame, double now, ControllerSnapshot pad, bool active, bool focused, InputContext nextContext, object? nextOwner = null)
        {
            var result = new RoutedFrame();
            if (frame <= lastFrame) return result;
            lastFrame = frame;
            RefreshBindings(pad);
            if (double.IsNaN(now) || double.IsInfinity(now) || now < lastTime)
            {
                Reset();
                return result;
            }
            lastTime = now;
            bool ready = active && focused && pad.Connected;
            bool replaced = usable && ready && device != pad.DeviceId;
            result.SafetyPause = usable && (!ready || replaced);
            if (!ready)
            {
                usable = false;
                Reset();
                return result;
            }
            if (!usable || replaced || !ReferenceEquals(owner, nextOwner) || context != nextContext || capabilities != pad.Capabilities) Reset();
            usable = true;
            device = pad.DeviceId;
            context = nextContext;
            owner = nextOwner;
            float deadzone = Stick.Finite(Deadzone) ? Math.Max(0, Math.Min(0.9f, Deadzone)) : 0.18f;
            if (!armed)
            {
                armed = pad.IsNeutral(deadzone);
                result.Armed = armed;
                return result;
            }
            result.Armed = true;
            Stick left = pad.LeftStick.Filter(deadzone, Sensitivity, InvertY);
            Stick right = pad.RightStick.Filter(deadzone, Sensitivity, InvertY);
            bool menu = context == InputContext.Menu || context == InputContext.Modal;
            PadButton held = pad.Buttons;
            Stick navigation = pad.LeftStick.Filter(deadzone);
            if (menu && navigation.Magnitude > 0.55f)
                held |= Bindings.ButtonFor(context, Math.Abs(navigation.X) > Math.Abs(navigation.Y)
                    ? (navigation.X > 0 ? ControllerAction.Right : ControllerAction.Left)
                    : (navigation.Y > 0 ? ControllerAction.Up : ControllerAction.Down));
            foreach (var pair in Bindings.For(context))
            {
                bool down = (held & pair.Key) != 0;
                bool wasDown = (previous & pair.Key) != 0;
                bool repeatable = menu && (pair.Value == ControllerAction.Up || pair.Value == ControllerAction.Down ||
                    pair.Value == ControllerAction.Left || pair.Value == ControllerAction.Right);
                if (down && !wasDown)
                {
                    result.Actions.Add(pair.Value);
                    if (repeatable) repeatAt[pair.Key] = now + 0.35;
                }
                else if (down && repeatable && repeatAt.TryGetValue(pair.Key, out double next) && now >= next)
                {
                    result.Actions.Add(pair.Value);
                    repeatAt[pair.Key] = now + 0.09;
                }
                if (!down) repeatAt.Remove(pair.Key);
            }
            previous = held;
            if (!menu)
            {
                result.Camera = left;
                result.Cursor = right;
                result.Zoom = SafeTrigger(pad.RightTrigger) - SafeTrigger(pad.LeftTrigger);
            }
            else result.Cursor = right;
            return result;
        }
        private static float SafeTrigger(float n) => Stick.Finite(n) ? Math.Max(0, Math.Min(1, n)) : 0;
    }
}
