using System;
using System.Linq;
using System.Runtime.InteropServices;
using RimController.Core;
using RimController.Mod;
using Xunit;

namespace RimController.Tests;

public class InputTests
{
    private static ControllerSnapshot Pad(PadButton buttons = PadButton.None, ulong id = 1,
        PadCapabilities caps = PadCapabilities.TouchpadClick | PadCapabilities.R3) => new()
        { Connected = true, DeviceId = id, Buttons = buttons, Model = PadModel.DualSense, Capabilities = caps };
    private static RoutedFrame Step(InputRouter router, long frame, double time, ControllerSnapshot? pad = null,
        bool active = true, bool focused = true, InputContext context = InputContext.Menu) =>
        router.Update(frame, time, pad ?? Pad(), active, focused, context);

    [Fact] public void InitialHeldConfirmMustBeReleasedBeforeActivation()
    {
        var router = new InputRouter();
        Assert.Empty(Step(router, 0, 0, Pad(PadButton.Cross)).Actions);
        Assert.Empty(Step(router, 1, .01, Pad(PadButton.Cross)).Actions);
        Assert.True(Step(router, 2, .02).Armed);
        Assert.Contains(ControllerAction.Confirm, Step(router, 3, .03, Pad(PadButton.Cross)).Actions);
        Assert.Empty(Step(router, 4, 5, Pad(PadButton.Cross)).Actions);
    }
    [Fact] public void DuplicateAndOldFramesNeverDispatch()
    {
        var router = new InputRouter();
        Step(router, 0, 0);
        Assert.Single(Step(router, 1, .1, Pad(PadButton.Cross)).Actions);
        Assert.Empty(Step(router, 1, .1, Pad(PadButton.Cross)).Actions);
        Assert.Empty(Step(router, 0, .2, Pad(PadButton.Circle)).Actions);
    }
    [Fact] public void NavigationRepeatsButConfirmationDoesNot()
    {
        var router = new InputRouter();
        Step(router, 0, 0);
        var held = Pad(PadButton.Down | PadButton.Cross);
        Assert.Equal(2, Step(router, 1, .1, held).Actions.Count);
        Assert.Empty(Step(router, 2, .3, held).Actions);
        Assert.Equal(new[] { ControllerAction.Down }, Step(router, 3, .46, held).Actions);
        Assert.Empty(Step(router, 4, .5, held).Actions);
        Assert.Equal(new[] { ControllerAction.Down }, Step(router, 5, .56, held).Actions);
    }
    [Fact] public void LongStallDoesNotReplayAccumulatedRepeats()
    {
        var router = new InputRouter();
        Step(router, 0, 0);
        Step(router, 1, .1, Pad(PadButton.Down));
        Assert.Single(Step(router, 2, 100, Pad(PadButton.Down)).Actions);
    }
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void LostFocusOrModePausesOnceAndRequiresNeutral(bool active, bool focused)
    {
        var router = new InputRouter();
        Step(router, 0, 0);
        Step(router, 1, .1, Pad(PadButton.Cross));
        Assert.True(Step(router, 2, .2, active: active, focused: focused).SafetyPause);
        Assert.False(Step(router, 3, .3, active: active, focused: focused).SafetyPause);
        Assert.Empty(Step(router, 4, .4, Pad(PadButton.Cross)).Actions);
        Step(router, 5, .5);
        Assert.Single(Step(router, 6, .6, Pad(PadButton.Cross)).Actions);
    }
    [Fact] public void DisconnectAndDeviceReplacementCannotReplayHeldInput()
    {
        var router = new InputRouter();
        Step(router, 0, 0);
        Assert.True(Step(router, 1, .1, ControllerSnapshot.Disconnected).SafetyPause);
        Assert.Empty(Step(router, 2, .2, Pad(PadButton.Cross, 2)).Actions);
        Step(router, 3, .3, Pad(id: 2));
        var swap = Step(router, 4, .4, Pad(PadButton.Cross, 3));
        Assert.True(swap.SafetyPause);
        Assert.Empty(swap.Actions);
    }
    [Fact] public void ContextChangeCannotConfirmUnderlyingScreen()
    {
        var router = new InputRouter();
        Step(router, 0, 0, context: InputContext.Modal);
        Assert.Single(Step(router, 1, .1, Pad(PadButton.Cross), context: InputContext.Modal).Actions);
        Assert.Empty(Step(router, 2, .2, Pad(PadButton.Cross), context: InputContext.Gameplay).Actions);
        Step(router, 3, .3, context: InputContext.Gameplay);
        Assert.Single(Step(router, 4, .4, Pad(PadButton.Cross), context: InputContext.Gameplay).Actions);
    }
    [Fact] public void ModalDoesNotDispatchPauseMenuOrCamera()
    {
        var router = new InputRouter();
        Step(router, 0, 0, context: InputContext.Modal);
        var pad = Pad(PadButton.Options | PadButton.Touchpad);
        pad.LeftStick = new Stick(.9f, 0);
        pad.RightTrigger = 1;
        var result = Step(router, 1, .1, pad, context: InputContext.Modal);
        Assert.DoesNotContain(ControllerAction.Pause, result.Actions);
        Assert.DoesNotContain(ControllerAction.GameMenu, result.Actions);
        Assert.Equal(0, result.Camera.Magnitude);
        Assert.Equal(0, result.Zoom);
    }
    [Fact] public void BuildShouldersRotateInsteadOfSelectingColonists()
    {
        var router = new InputRouter();
        Step(router, 0, 0, context: InputContext.Building);
        var result = Step(router, 1, .1, Pad(PadButton.L1), context: InputContext.Building);
        Assert.Equal(new[] { ControllerAction.RotateLeft }, result.Actions);
    }
    [Theory]
    [InlineData(PadCapabilities.TouchpadClick | PadCapabilities.R3, PadButton.Touchpad)]
    [InlineData(PadCapabilities.R3, PadButton.R3)]
    [InlineData(PadCapabilities.None, PadButton.None)]
    public void PauseBindingAndHintReflectCapabilities(PadCapabilities caps, PadButton expected)
    {
        var bindings = new Bindings(caps);
        Assert.Equal(expected, bindings.ButtonFor(InputContext.Gameplay, ControllerAction.Pause));
        Assert.Equal(PadButton.None, bindings.ButtonFor(InputContext.Modal, ControllerAction.Pause));
    }
    [Fact] public void RebindingRejectsConflictsAndKeepsExistingMapping()
    {
        var bindings = new Bindings(PadCapabilities.R3);
        Assert.False(bindings.TryRebind(InputContext.Menu, ControllerAction.Confirm, PadButton.Circle));
        Assert.False(bindings.TryRebind(InputContext.Menu, ControllerAction.Confirm, PadButton.Cross | PadButton.Circle));
        Assert.False(bindings.TryRebind(InputContext.Menu, ControllerAction.Confirm, (PadButton)(1UL << 40)));
        Assert.False(bindings.TryRebind(InputContext.Modal, ControllerAction.Pause, PadButton.R3));
        Assert.Equal(PadButton.Cross, bindings.ButtonFor(InputContext.Menu, ControllerAction.Confirm));
        Assert.True(bindings.TryRebind(InputContext.Menu, ControllerAction.Confirm, PadButton.L3));
        Assert.Equal(PadButton.L3, bindings.ButtonFor(InputContext.Menu, ControllerAction.Confirm));
        Assert.Equal(PadButton.Cross, bindings.ButtonFor(InputContext.Gameplay, ControllerAction.Confirm));
    }
    [Fact] public void StickDriftIsSuppressedAndDiagonalMagnitudeCapped()
    {
        Assert.Equal(0, new Stick(.05f, .05f).Filter(.18f).Magnitude);
        Assert.InRange(new Stick(1, 1).Filter(.18f, 2).Magnitude, .999f, 1.001f);
        Assert.True(new Stick(0, .5f).Filter(.18f, 1, true).Y < 0);
        Assert.Equal(0, new Stick(float.NaN, 0).Filter(.18f).Magnitude);
        Assert.Equal(0, new Stick(float.PositiveInfinity, 0).Filter(.18f).Magnitude);
    }
    [Fact] public void HeldStickOrTriggerBlocksArming()
    {
        var router = new InputRouter();
        var pad = Pad();
        pad.RightTrigger = 1;
        Assert.False(Step(router, 0, 0, pad).Armed);
        pad.RightTrigger = 0;
        pad.LeftStick = new Stick(.8f, 0);
        Assert.False(Step(router, 1, .1, pad).Armed);
        Assert.True(Step(router, 2, .2).Armed);
    }
    [Fact] public void StickAndDpadDoNotDoubleDispatchSameDirection()
    {
        var router = new InputRouter();
        Step(router, 0, 0);
        var pad = Pad(PadButton.Up);
        pad.LeftStick = new Stick(0, 1);
        Assert.Equal(new[] { ControllerAction.Up }, Step(router, 1, .1, pad).Actions);
    }
    [Theory]
    [InlineData(InputContext.Menu, true, 1f)]
    [InlineData(InputContext.Menu, false, .3f)]
    [InlineData(InputContext.Modal, true, .3f)]
    public void MenuStickNavigationIgnoresCameraPreferences(InputContext context, bool invertY, float sensitivity)
    {
        var router = new InputRouter { InvertY = invertY, Sensitivity = sensitivity };
        Step(router, 0, 0, context: context);
        var pad = Pad(); pad.LeftStick = new Stick(0, 1);
        Assert.Equal(new[] { ControllerAction.Up }, Step(router, 1, .1, pad, context: context).Actions);
    }
    [Theory]
    [InlineData(InputContext.Gameplay)]
    [InlineData(InputContext.Building)]
    public void CameraAndCursorRetainSensitivityAndInversion(InputContext context)
    {
        var router = new InputRouter { InvertY = true, Sensitivity = .3f };
        Step(router, 0, 0, context: context);
        var pad = Pad(); pad.LeftStick = pad.RightStick = new Stick(0, 1);
        var result = Step(router, 1, .1, pad, context: context);
        Assert.Equal(-.3f, result.Camera.Y, 5);
        Assert.Equal(-.3f, result.Cursor.Y, 5);
        Assert.Empty(result.Actions);
    }
    [Fact] public void CapabilityChangeRequiresNeutralAndUpdatesHint()
    {
        var router = new InputRouter();
        Step(router, 0, 0);
        Assert.Empty(Step(router, 1, .1, Pad(PadButton.R3, caps: PadCapabilities.R3)).Actions);
        Assert.Equal(PadButton.R3, router.Bindings.ButtonFor(InputContext.Gameplay, ControllerAction.Pause));
    }
    [Fact] public void InvalidTimeDoesNotLeaveHeldInputArmed()
    {
        var router = new InputRouter();
        Step(router, 0, 1);
        Assert.Empty(Step(router, 1, double.NaN, Pad(PadButton.Cross)).Actions);
        Assert.Empty(Step(router, 2, 2, Pad(PadButton.Cross)).Actions);
        Step(router, 3, 3);
        Assert.Single(Step(router, 4, 4, Pad(PadButton.Cross)).Actions);
    }
    [Fact] public void GameplayAxesAreFilteredAndTriggersClamped()
    {
        var router = new InputRouter();
        Step(router, 0, 0, context: InputContext.Gameplay);
        var pad = Pad();
        pad.LeftStick = new Stick(1, 0); pad.RightStick = new Stick(0, 1);
        pad.LeftTrigger = float.NaN; pad.RightTrigger = 9;
        var frame = Step(router, 1, .1, pad, context: InputContext.Gameplay);
        Assert.Equal(1, frame.Camera.X); Assert.Equal(1, frame.Cursor.Y); Assert.Equal(1, frame.Zoom);
    }
    [Fact] public void NativeAbiMatchesCHeaderAndRoundTrips()
    {
        Assert.Equal(188, Marshal.SizeOf<NativeSnapshot>());
        Assert.Equal(60, Marshal.OffsetOf<NativeSnapshot>(nameof(NativeSnapshot.Name)).ToInt32());
        var input = new NativeSnapshot { Size = 188, Abi = 1, DeviceId = 99, Buttons = (ulong)PadButton.Touchpad,
            Capabilities = 3, Connected = 1, Model = 2, LeftX = -.75f, RightTrigger = 1, Name = "DualSense" };
        IntPtr memory = Marshal.AllocHGlobal(188);
        try
        {
            Marshal.StructureToPtr(input, memory, false);
            var output = Marshal.PtrToStructure<NativeSnapshot>(memory);
            Assert.Equal(input.Name, output.Name); Assert.Equal(input.Buttons, output.Buttons);
            Assert.Equal(input.DeviceId, output.DeviceId); Assert.Equal(input.LeftX, output.LeftX);
            Assert.Equal(input.RightTrigger, output.RightTrigger);
        }
        finally { Marshal.FreeHGlobal(memory); }
    }
    [Fact] public void NonMacBackendFailsClosedWithoutLoadingNativeLibrary()
    {
        using var bridge = new NativeBridge("/does-not-exist", false);
        Assert.False(bridge.Available); Assert.False(bridge.Poll().Connected);
        Assert.Equal("RC_MacRequired", bridge.Diagnostic);
    }
    [Fact] public void MissingLibraryFailsClosedAndDisposeIsIdempotent()
    {
        var bridge = new NativeBridge("/does-not-exist", true);
        Assert.False(bridge.Available); Assert.Equal("RC_BridgeMissing", bridge.Diagnostic);
        bridge.Dispose(); bridge.Dispose(); Assert.False(bridge.Poll().Connected);
    }
}

public class FocusTests
{
    [Fact] public void FocusSurvivesReorderingAndFallsBackWhenRemoved()
    {
        var focus = new FocusList();
        focus.SetItems(new[] { new FocusItem("a"), new FocusItem("b"), new FocusItem("c") });
        focus.Move(1);
        focus.SetItems(new[] { new FocusItem("b"), new FocusItem("a") });
        Assert.Equal("b", focus.Current?.Id);
        focus.SetItems(new[] { new FocusItem("a") });
        Assert.Equal("a", focus.Current?.Id);
    }
    [Fact] public void EmptyListsAndDisabledActionsAreSafe()
    {
        var focus = new FocusList();
        focus.Move(1); Assert.Null(focus.Current); Assert.False(focus.CanConfirm);
        focus.SetItems(new[] { new FocusItem("a", false, "Missing wood") });
        Assert.Equal("Missing wood", focus.Current?.DisabledReason); Assert.False(focus.CanConfirm);
        focus.SetItems(Array.Empty<FocusItem>()); Assert.Null(focus.Current);
    }
    [Fact] public void DuplicateIdsAndUnexplainedDisabledItemsAreRejected()
    {
        var focus = new FocusList();
        Assert.Throws<ArgumentException>(() => focus.SetItems(new[] { new FocusItem("a"), new FocusItem("a") }));
        Assert.Throws<ArgumentException>(() => new FocusItem("a", false));
    }
    [Fact] public void NavigationClampsAtEdgesEvenWithLargeDeltas()
    {
        var focus = new FocusList();
        focus.SetItems(new[] { new FocusItem("a"), new FocusItem("b") });
        focus.Move(int.MaxValue); Assert.Equal(1, focus.Index);
        focus.Move(int.MaxValue); Assert.Equal(1, focus.Index);
        focus.Move(int.MinValue); Assert.Equal(0, focus.Index);
    }
}
