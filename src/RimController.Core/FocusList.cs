using System;
using System.Collections.Generic;
using System.Linq;

namespace RimController.Core
{
    public readonly struct FocusItem
    {
        public string Id { get; }
        public bool Enabled { get; }
        public string DisabledReason { get; }
        public FocusItem(string id, bool enabled = true, string disabledReason = "")
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Focus ID is required.", nameof(id));
            if (!enabled && string.IsNullOrWhiteSpace(disabledReason)) throw new ArgumentException("Disabled items need a reason.");
            Id = id; Enabled = enabled; DisabledReason = disabledReason;
        }
    }
    public sealed class FocusList
    {
        private FocusItem[] items = Array.Empty<FocusItem>();
        public int Index { get; private set; } = -1;
        public FocusItem? Current => Index >= 0 ? items[Index] : (FocusItem?)null;
        public void SetItems(IEnumerable<FocusItem> next)
        {
            var replacement = next.ToArray();
            if (replacement.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != replacement.Length)
                throw new ArgumentException("Duplicate focus IDs.");
            string? old = Current?.Id;
            items = replacement;
            int found = Array.FindIndex(items, x => x.Id == old);
            Index = items.Length == 0 ? -1 : found >= 0 ? found : Math.Max(0, Math.Min(Index, items.Length - 1));
        }
        // Disabled items remain focusable so their reason can be read.
        public void Move(int delta)
        {
            if (items.Length > 0) Index = (int)Math.Max(0L, Math.Min(items.Length - 1L, (long)Index + delta));
        }
        public bool CanConfirm => Current?.Enabled == true;
    }
}
