using System;
using System.Collections.Generic;

namespace RimController.Core
{
    // Each instance is an input owner, even when two windows share the same context type.
    public sealed class InputScope
    {
        public InputContext Context { get; }
        public FocusList Focus { get; } = new();
        public InputScope(InputContext context) { Context = context; }
    }

    public sealed class InputContextStack
    {
        private readonly List<InputScope> scopes = new();
        public InputScope? Current => scopes.Count == 0 ? null : scopes[scopes.Count - 1];
        public void Push(InputScope scope)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            if (scopes.Contains(scope)) throw new ArgumentException("Scope already registered.", nameof(scope));
            scopes.Add(scope);
        }
        // A window may be removed beneath a modal. Never accidentally pop its child.
        public bool Remove(InputScope scope) => scopes.Remove(scope);
        public void Clear() => scopes.Clear();
        public RoutedFrame Route(InputRouter router, long frame, double now, ControllerSnapshot pad,
            bool controllerMode, bool focused)
        {
            var owner = Current;
            return router.Update(frame, now, pad, controllerMode && owner != null, focused,
                owner?.Context ?? InputContext.Menu, owner);
        }
    }
}
