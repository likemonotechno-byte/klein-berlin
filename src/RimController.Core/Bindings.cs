using System;
using System.Collections.Generic;
using System.Linq;

namespace RimController.Core
{
    public sealed class Bindings
    {
        private readonly Dictionary<InputContext, Dictionary<PadButton, ControllerAction>> maps = new();
        private readonly PadCapabilities capabilities;
        public int Revision { get; private set; }
        public Bindings(PadCapabilities capabilities)
        {
            this.capabilities = capabilities;
            foreach (InputContext context in Enum.GetValues(typeof(InputContext)))
            {
                var map = new Dictionary<PadButton, ControllerAction>
                {
                    [PadButton.Cross] = ControllerAction.Confirm, [PadButton.Circle] = ControllerAction.Cancel,
                    [PadButton.Square] = ControllerAction.Context, [PadButton.Triangle] = ControllerAction.Overview,
                    [PadButton.L1] = ControllerAction.Previous, [PadButton.R1] = ControllerAction.Next
                };
                bool menu = context == InputContext.Menu || context == InputContext.Modal;
                map[PadButton.Up] = menu ? ControllerAction.Up : ControllerAction.Alerts;
                map[PadButton.Down] = menu ? ControllerAction.Down : ControllerAction.Build;
                map[PadButton.Left] = menu ? ControllerAction.Left : ControllerAction.SpeedDown;
                map[PadButton.Right] = menu ? ControllerAction.Right : ControllerAction.SpeedUp;
                if (menu)
                {
                    map[PadButton.L2] = ControllerAction.PagePrevious;
                    map[PadButton.R2] = ControllerAction.PageNext;
                }
                else
                {
                    if ((capabilities & PadCapabilities.TouchpadClick) != 0)
                        map[PadButton.Touchpad] = ControllerAction.Pause;
                    else if ((capabilities & PadCapabilities.R3) != 0)
                        map[PadButton.R3] = ControllerAction.Pause;
                    map[PadButton.Options] = ControllerAction.GameMenu;
                }
                if (context == InputContext.Building)
                {
                    map[PadButton.L1] = ControllerAction.RotateLeft;
                    map[PadButton.R1] = ControllerAction.RotateRight;
                }
                maps[context] = map;
            }
        }
        public IReadOnlyDictionary<PadButton, ControllerAction> For(InputContext context) => maps[context];
        public PadButton ButtonFor(InputContext context, ControllerAction action)
        {
            foreach (var pair in maps[context]) if (pair.Value == action) return pair.Key;
            return PadButton.None;
        }
        public bool Available(PadButton button)
        {
            ulong bits = (ulong)button;
            return bits != 0 && (bits & (bits - 1)) == 0 && Enum.IsDefined(typeof(PadButton), button) &&
                (button != PadButton.Touchpad || (capabilities & PadCapabilities.TouchpadClick) != 0) &&
                (button != PadButton.R3 || (capabilities & PadCapabilities.R3) != 0);
        }
        public BindingOverride[] Export() => maps.SelectMany(context => context.Value.Select(pair =>
            new BindingOverride(context.Key, pair.Value, pair.Key))).ToArray();

        public static Bindings Restore(PadCapabilities capabilities, IEnumerable<BindingOverride> entries, ICollection<string> issues)
        {
            var result = new Bindings(capabilities);
            var all = entries.ToArray();
            var valid = new List<BindingOverride>();
            foreach (var entry in all)
            {
                if (!result.maps.TryGetValue(entry.Context, out var map) || !map.ContainsValue(entry.Action))
                { issues.Add("RC_ProfileUnknown"); continue; }
                if (!result.Available(entry.Button)) { issues.Add("RC_ProfileUnavailable"); continue; }
                if (all.Count(x => x.Context == entry.Context && x.Action == entry.Action) != 1)
                { issues.Add("RC_ProfileConflict"); continue; }
                valid.Add(entry);
            }
            // Resolve complete mappings atomically: valid permutations must survive serialization.
            // Reject all overrides involved in a collision, then reconsider their restored defaults.
            while (true)
            {
                var proposed = result.Export().Select(b => valid.FirstOrDefault(x => x.Context == b.Context && x.Action == b.Action) ?? b).ToArray();
                var collisions = proposed.GroupBy(x => (x.Context, x.Button)).Where(g => g.Count() > 1)
                    .SelectMany(g => g).ToArray();
                var rejected = valid.Where(x => collisions.Any(c => c.Context == x.Context && c.Action == x.Action)).ToArray();
                if (rejected.Length == 0) break;
                foreach (var entry in rejected) { valid.Remove(entry); issues.Add("RC_ProfileConflict"); }
            }
            foreach (var entry in valid)
            {
                var map = result.maps[entry.Context];
                foreach (var key in map.Where(x => x.Value == entry.Action).Select(x => x.Key).ToArray()) map.Remove(key);
            }
            foreach (var entry in valid) result.maps[entry.Context][entry.Button] = entry.Action;
            return result;
        }
        public bool TryRebind(InputContext context, ControllerAction action, PadButton button)
        {
            ulong bits = (ulong)button;
            if (bits == 0 || (bits & (bits - 1)) != 0 || !Enum.IsDefined(typeof(PadButton), button)) return false;
            if (!Available(button) || !maps.TryGetValue(context, out var map)) return false;
            if (!map.ContainsValue(action)) return false;
            if (map.TryGetValue(button, out var occupied) && occupied != action) return false;
            var old = map.Where(x => x.Value == action).Select(x => x.Key).ToArray();
            foreach (var key in old) map.Remove(key);
            map[button] = action;
            Revision++;
            return true;
        }
    }
}
