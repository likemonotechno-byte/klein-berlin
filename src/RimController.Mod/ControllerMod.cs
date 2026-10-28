using System;
using System.Collections.Generic;
using System.Linq;
using RimController.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimController.Mod
{
    public sealed class ControllerSettings : ModSettings
    {
        public float Deadzone = 0.18f;
        public float Sensitivity = 1;
        public bool InvertY;
        public ControllerProfiles Profiles = new();
        private string profileData = "";
        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving) profileData = ProfileCodec.Encode(Profiles);
            Scribe_Values.Look(ref profileData, "controllerProfiles", "");
            if (Scribe.mode == LoadSaveMode.LoadingVars) Profiles = ProfileCodec.Decode(profileData);
            Scribe_Values.Look(ref Deadzone, "deadzone", 0.18f);
            Scribe_Values.Look(ref Sensitivity, "sensitivity", 1f);
            Scribe_Values.Look(ref InvertY, "invertY", false);
            if (float.IsNaN(Deadzone) || float.IsInfinity(Deadzone)) Deadzone = 0.18f;
            if (float.IsNaN(Sensitivity) || float.IsInfinity(Sensitivity)) Sensitivity = 1;
            Deadzone = Mathf.Clamp(Deadzone, 0.05f, 0.5f);
            Sensitivity = Mathf.Clamp(Sensitivity, 0.25f, 2);
            base.ExposeData();
        }
    }
    public sealed class ControllerMod : Verse.Mod
    {
        public static ControllerMod? Instance { get; private set; }
        public ControllerSettings Settings { get; }
        public ControllerRuntime? Runtime { get; private set; }
        public bool Compatible => VersionControl.CurrentVersionString.StartsWith("1.6.", StringComparison.Ordinal);
        public ControllerMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<ControllerSettings>();
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                if (!Compatible) return;
                var obj = new GameObject("RimController.InputDiagnostics");
                UnityEngine.Object.DontDestroyOnLoad(obj);
                Runtime = obj.AddComponent<ControllerRuntime>();
                Runtime.Initialize(content.RootDir, Settings);
            });
        }
        private Vector2 settingsScroll;
        private PadModel editModel = PadModel.DualSense;
        private InputContext editContext = InputContext.Menu;
        private ControllerAction editAction = ControllerAction.Confirm;
        private string bindingMessage = "";
        // Future game lessons use these entry points; no progress is stored in a savegame.
        public void LoadTutorial<T>(Tutorial<T> tutorial) => Settings.Profiles.LoadTutorial(tutorial);
        public void SaveTutorial<T>(Tutorial<T> tutorial)
        { Settings.Profiles.SaveTutorial(tutorial); WriteSettings(); }
        public override string SettingsCategory() => "RimController";
        public override void DoSettingsWindowContents(Rect rect)
        {
            var list = new Listing_Standard();
            var view = new Rect(0, 0, rect.width - 24, 1050);
            Widgets.BeginScrollView(rect, ref settingsScroll, view);
            list.Begin(view);
            list.Label("RC_Prototype".Translate());
            list.Gap();
            list.Label("RC_Deadzone".Translate() + $": {Settings.Deadzone:P0}");
            Settings.Deadzone = list.Slider(Settings.Deadzone, 0.05f, 0.5f);
            list.Label("RC_Sensitivity".Translate() + $": {Settings.Sensitivity:F2}");
            Settings.Sensitivity = list.Slider(Settings.Sensitivity, 0.25f, 2f);
            list.CheckboxLabeled("RC_InvertY".Translate(), ref Settings.InvertY);
            list.Label("RC_SteamInput".Translate());
            if (!Compatible) list.Label("RC_VersionUnsupported".Translate());
            else if (Runtime != null && list.ButtonText("RC_OpenDiagnostics".Translate()))
            {
                if (!Find.WindowStack.IsOpen<DiagnosticsWindow>()) Find.WindowStack.Add(new DiagnosticsWindow(Runtime));
            }
            DrawProfileEditor(list);
            list.End();
            Widgets.EndScrollView();
        }
        private void DrawProfileEditor(Listing_Standard list)
        {
            list.Gap();
            list.Label("RC_ProfileEditor".Translate());
            if (list.ButtonText("RC_Model".Translate() + ": " + editModel))
                Find.WindowStack.Add(new FloatMenu(new[] { PadModel.DualSense, PadModel.DualShock4 }
                    .Select(model => new FloatMenuOption(model.ToString(), () => { editModel = model; bindingMessage = ""; })).ToList()));
            if (list.ButtonText("RC_Context".Translate() + ": " + ("RC_Context_" + editContext).Translate()))
                Find.WindowStack.Add(new FloatMenu(Enum.GetValues(typeof(InputContext)).Cast<InputContext>()
                    .Select(context => new FloatMenuOption(("RC_Context_" + context).Translate(), () =>
                    { editContext = context; editAction = ControllerAction.Confirm; })).ToList()));
            var profile = Settings.Profiles.For(editModel)!;
            bool connected = Runtime?.Snapshot.Connected == true && Runtime.Snapshot.Model == editModel;
            var capabilities = connected ? Runtime!.Snapshot.Capabilities : PadCapabilities.None;
            // Without the selected device only known common buttons are offered; reset remains available.
            var issues = new List<string>();
            var effective = profile.Resolve(capabilities, issues);
            if (!effective.For(editContext).Values.Contains(editAction)) editAction = ControllerAction.Confirm;
            if (list.ButtonText("RC_Action".Translate() + ": " + ("RC_Action_" + editAction).Translate()))
                Find.WindowStack.Add(new FloatMenu(effective.For(editContext).Values.Distinct()
                    .Select(action => new FloatMenuOption(("RC_Action_" + action).Translate(), () => editAction = action)).ToList()));
            list.Label("RC_CurrentBinding".Translate() + ": " + PlayStationGlyphs.Label(effective.ButtonFor(editContext, editAction)));
            if (connected && list.ButtonText("RC_ChooseButton".Translate()))
                Find.WindowStack.Add(new FloatMenu(Enum.GetValues(typeof(PadButton)).Cast<PadButton>().Where(effective.Available)
                    .Select(button => new FloatMenuOption(PlayStationGlyphs.Label(button), () =>
                    {
                        var current = Runtime!.Snapshot;
                        if (!current.Connected || current.Model != editModel) { bindingMessage = "RC_ConnectModel"; return; }
                        bindingMessage = profile.TryRebind(current.Capabilities, editContext, editAction, button, out var issue)
                            ? "RC_ProfileSaved" : issue;
                        WriteSettings();
                    })).ToList()));
            if (!connected) list.Label("RC_ConnectModel".Translate());
            if (list.ButtonText("RC_ResetModel".Translate()))
            { profile.Reset(); bindingMessage = "RC_ProfileReset"; WriteSettings(); }
            bool enabled = Settings.Profiles.TutorialEnabled;
            list.CheckboxLabeled("RC_TutorialEnabled".Translate(), ref enabled);
            if (enabled != Settings.Profiles.TutorialEnabled)
            { Settings.Profiles.TutorialEnabled = enabled; WriteSettings(); }
            list.Label("RC_TutorialPending".Translate());
            if (bindingMessage.Length > 0) list.Label(bindingMessage.Translate());
            foreach (var issue in Settings.Profiles.LoadIssues.Concat(connected ? issues : Enumerable.Empty<string>()).Distinct())
                list.Label(issue.Translate());
        }
    }
    public sealed class ControllerRuntime : MonoBehaviour
    {
        private NativeBridge? backend;
        private ControllerSettings? settings;
        private readonly InputRouter router = new();
        private bool focused = true;
        public ControllerSnapshot Snapshot { get; private set; } = ControllerSnapshot.Disconnected;
        public RoutedFrame LastFrame { get; private set; } = new();
        public string Status => backend?.Diagnostic ?? "RC_BridgeMissing";
        public string ErrorDetail => backend?.ErrorDetail ?? "";
        public Bindings Bindings => router.Bindings;
        public IReadOnlyList<string> BindingIssues => router.BindingIssues;
        public void Initialize(string root, ControllerSettings config)
        {
            settings = config;
            backend = new NativeBridge(root, Application.platform == RuntimePlatform.OSXPlayer);
            focused = Application.isFocused;
        }
        private void Update()
        {
            if (backend == null || settings == null) return;
            Snapshot = backend.Poll();
            router.Profiles = settings.Profiles;
            router.Deadzone = settings.Deadzone;
            router.Sensitivity = settings.Sensitivity;
            router.InvertY = settings.InvertY;
            // Stage 1 owns input only while the diagnostic window is the topmost window.
            DiagnosticsWindow? window = null;
            var windows = Find.WindowStack.Windows;
            if (windows.Count > 0) window = windows[windows.Count - 1] as DiagnosticsWindow;
            LastFrame = router.Update(Time.frameCount, Time.realtimeSinceStartup, Snapshot,
                window != null, focused && Application.isFocused, InputContext.Modal, window);
            if (LastFrame.SafetyPause && Current.ProgramState == ProgramState.Playing) Find.TickManager.Pause();
            window?.Handle(LastFrame);
        }
        private void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            if (!hasFocus)
            {
                router.Reset();
                if (Find.WindowStack.IsOpen<DiagnosticsWindow>() && Current.ProgramState == ProgramState.Playing)
                    Find.TickManager.Pause();
            }
        }
        private void OnDestroy() { backend?.Dispose(); }
    }
    public sealed class DiagnosticsWindow : Window
    {
        private readonly ControllerRuntime runtime;
        private readonly FocusList focus = new();
        private int confirmations;
        private bool showBindings;
        private Vector2 scroll;
        public override Vector2 InitialSize => new(760, 640);
        public DiagnosticsWindow(ControllerRuntime runtime)
        {
            this.runtime = runtime;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnAccept = false;
            doCloseX = true;
            draggable = true;
            focus.SetItems(new[] { new FocusItem("test"), new FocusItem("bindings"), new FocusItem("close") });
        }
        public void Handle(RoutedFrame frame)
        {
            // Cancellation takes precedence if both buttons are pressed in a single sample.
            if (frame.Actions.Contains(ControllerAction.Cancel)) { Close(); return; }
            if (frame.Actions.Contains(ControllerAction.Up)) focus.Move(-1);
            else if (frame.Actions.Contains(ControllerAction.Down)) focus.Move(1);
            if (frame.Actions.Contains(ControllerAction.Confirm) && focus.CanConfirm) Activate(focus.Current!.Value.Id);
            scroll.y = Mathf.Max(0, scroll.y - frame.Cursor.Y * 450 * Time.unscaledDeltaTime);
        }
        private void Activate(string id)
        {
            if (id == "test") confirmations++;
            else if (id == "bindings") showBindings = !showBindings;
            else if (id == "close") Close();
        }
        public override void DoWindowContents(Rect rect)
        {
            GameFont previousFont = Text.Font;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, rect.width, 36), "RC_Diagnostics".Translate());
            Text.Font = GameFont.Small;
            var view = new Rect(0, 0, rect.width - 20, showBindings ? 1080 : 760);
            var viewport = new Rect(0, 44, rect.width, Mathf.Max(40, rect.height - 174));
            Widgets.BeginScrollView(viewport, ref scroll, view);
            var list = new Listing_Standard();
            list.Begin(view);
            list.Label("RC_Prototype".Translate());
            list.Label("RC_Runtime".Translate() + $": {VersionControl.CurrentVersionString} / {Application.unityVersion} / {IntPtr.Size * 8}-bit / {SystemInfo.operatingSystem}");
            list.Label(runtime.Status.Translate());
            foreach (var issue in runtime.BindingIssues.Distinct()) list.Label(issue.Translate());
            if (runtime.ErrorDetail.Length > 0) list.Label(runtime.ErrorDetail);
            var pad = runtime.Snapshot;
            list.Label("RC_Device".Translate() + $": {pad.Model} / {pad.Name} / #{pad.DeviceId}");
            list.Label("RC_Buttons".Translate() + $": {pad.Buttons}");
            list.Label($"L: {pad.LeftStick.X:F3}, {pad.LeftStick.Y:F3}    R: {pad.RightStick.X:F3}, {pad.RightStick.Y:F3}");
            list.Label($"L2: {pad.LeftTrigger:F3}    R2: {pad.RightTrigger:F3}");
            list.Label("RC_PauseBinding".Translate() + ": " + PlayStationGlyphs.Label(runtime.Bindings.ButtonFor(InputContext.Gameplay, ControllerAction.Pause)));
            list.Label((runtime.LastFrame.Armed ? "RC_Ready" : "RC_ReleaseControls").Translate());
            list.Label("RC_Confirmations".Translate() + $": {confirmations}");
            list.Label("RC_SteamInput".Translate());
            if (showBindings)
            {
                list.Label("RC_Bindings".Translate());
                foreach (var pair in runtime.Bindings.For(InputContext.Gameplay))
                    list.Label(PlayStationGlyphs.Label(pair.Key) + "  " + ("RC_Action_" + pair.Value).Translate());
            }
            list.End();
            Widgets.EndScrollView();
            string[] ids = { "test", "bindings", "close" };
            string[] labels = { "RC_TestConfirm", "RC_Bindings", "RC_Close" };
            float width = (rect.width - 16) / 3;
            for (int i = 0; i < ids.Length; i++)
            {
                var button = new Rect(i * (width + 8), rect.height - 114, width, 48);
                if (focus.Index == i) Widgets.DrawBoxSolid(button.ExpandedBy(3), new Color(0.25f, 0.72f, 0.92f));
                if (Widgets.ButtonText(button, labels[i].Translate())) Activate(ids[i]);
            }
            Widgets.Label(new Rect(0, rect.height - 54, rect.width, 48), "RC_DiagnosticHelp".Translate(
                PlayStationGlyphs.Label(runtime.Bindings.ButtonFor(InputContext.Modal, ControllerAction.Up)),
                PlayStationGlyphs.Label(runtime.Bindings.ButtonFor(InputContext.Modal, ControllerAction.Down)),
                PlayStationGlyphs.Label(runtime.Bindings.ButtonFor(InputContext.Modal, ControllerAction.Confirm)),
                PlayStationGlyphs.Label(runtime.Bindings.ButtonFor(InputContext.Modal, ControllerAction.Cancel))));
            Text.Font = previousFont;
        }
    }
}
