using System;
using System.Collections.Generic;
using System.Linq;
using RimController.Core;
using Xunit;

namespace RimController.Tests;

public class ProfileTests
{
    private const PadCapabilities Caps = PadCapabilities.R3 | PadCapabilities.TouchpadClick;
    private static ControllerSnapshot Pad(PadModel model = PadModel.DualSense, PadButton buttons = PadButton.None,
        PadCapabilities caps = Caps) => new() { Connected = true, DeviceId = (ulong)model, Model = model, Buttons = buttons, Capabilities = caps };

    [Fact] public void BothModelsRoundTripWithSeparateMappings()
    {
        var profiles = new ControllerProfiles();
        Assert.True(profiles.DualSense.TryRebind(Caps, InputContext.Menu, ControllerAction.Confirm, PadButton.L3, out _));
        Assert.True(profiles.DualShock4.TryRebind(Caps, InputContext.Menu, ControllerAction.Cancel, PadButton.R3, out _));
        var restored = ProfileCodec.Decode(ProfileCodec.Encode(profiles));
        Assert.Empty(restored.LoadIssues);
        Assert.Equal(PadButton.L3, restored.DualSense.Resolve(Caps, new List<string>()).ButtonFor(InputContext.Menu, ControllerAction.Confirm));
        Assert.Equal(PadButton.Cross, restored.DualShock4.Resolve(Caps, new List<string>()).ButtonFor(InputContext.Menu, ControllerAction.Confirm));
        Assert.Equal(PadButton.R3, restored.DualShock4.Resolve(Caps, new List<string>()).ButtonFor(InputContext.Menu, ControllerAction.Cancel));
    }
    [Fact] public void EmptyLegacyProfileRetainsDefaults()
    {
        var profiles = ProfileCodec.Decode(null);
        Assert.True(profiles.TutorialEnabled); Assert.Empty(profiles.LoadIssues);
        Assert.Empty(profiles.DualSense.Export()); Assert.Empty(profiles.Lessons);
        Assert.Equal(PadButton.Touchpad, profiles.DualSense.Resolve(Caps, new List<string>()).ButtonFor(InputContext.Gameplay, ControllerAction.Pause));
    }
    [Theory]
    [InlineData("<broken")]
    [InlineData("<!DOCTYPE profiles [<!ENTITY x 'value'>]><profiles version='1'>&x;</profiles>")]
    [InlineData("<profiles version='2'/>")]
    public void InvalidOrFutureProfilesFailToDefaultsWithNotice(string xml)
    {
        var profiles = ProfileCodec.Decode(xml);
        Assert.NotEmpty(profiles.LoadIssues); Assert.True(profiles.TutorialEnabled);
        Assert.Empty(profiles.DualSense.Export());
    }
    [Fact] public void UnknownEnumAndInvalidLessonAreExplained()
    {
        var profiles = ProfileCodec.Decode("<profiles version='1'><controller model='DualSense'><binding context='Menu' action='Future' button='Cross'/></controller><lessons><lesson id='x' status='999'/></lessons></profiles>");
        Assert.Contains("RC_ProfileUnknown", profiles.LoadIssues);
        Assert.Contains("RC_ProfileInvalid", profiles.LoadIssues);
        Assert.Empty(profiles.Lessons);
    }
    [Fact] public void ConflictsDoNotChangeExistingBindingsAndResetAffectsOnlyOneModel()
    {
        var profiles = new ControllerProfiles();
        profiles.DualShock4.TryRebind(Caps, InputContext.Menu, ControllerAction.Confirm, PadButton.L3, out _);
        Assert.False(profiles.DualSense.TryRebind(Caps, InputContext.Menu, ControllerAction.Confirm, PadButton.Circle, out var issue));
        Assert.Equal("RC_ProfileConflict", issue);
        profiles.DualSense.TryRebind(Caps, InputContext.Menu, ControllerAction.Confirm, PadButton.L3, out _);
        profiles.DualSense.Reset();
        Assert.Empty(profiles.DualSense.Export()); Assert.Single(profiles.DualShock4.Export());
    }
    [Fact] public void RestoreAcceptsPermutationButRejectsCollisionChains()
    {
        var profiles = new ControllerProfiles(); var p = profiles.DualSense;
        p.TryRebind(Caps, InputContext.Menu, ControllerAction.Confirm, PadButton.L3, out _);
        p.TryRebind(Caps, InputContext.Menu, ControllerAction.Cancel, PadButton.Cross, out _);
        p.TryRebind(Caps, InputContext.Menu, ControllerAction.Confirm, PadButton.Circle, out _);
        var issues = new List<string>();
        var restored = ProfileCodec.Decode(ProfileCodec.Encode(profiles)).DualSense.Resolve(Caps, issues);
        Assert.Empty(issues);
        Assert.Equal(PadButton.Circle, restored.ButtonFor(InputContext.Menu, ControllerAction.Confirm));
        Assert.Equal(PadButton.Cross, restored.ButtonFor(InputContext.Menu, ControllerAction.Cancel));
        restored = Bindings.Restore(Caps, new[] {
            new BindingOverride(InputContext.Menu, ControllerAction.Confirm, PadButton.Circle),
            new BindingOverride(InputContext.Menu, ControllerAction.Cancel, PadButton.Square) }, issues);
        Assert.NotEmpty(issues);
        Assert.Equal(PadButton.Cross, restored.ButtonFor(InputContext.Menu, ControllerAction.Confirm));
        Assert.Equal(PadButton.Circle, restored.ButtonFor(InputContext.Menu, ControllerAction.Cancel));
    }
    [Fact] public void CapabilityFallbackPreservesUnavailableCustomBindingForReconnect()
    {
        var profile = new ControllerProfile(PadModel.DualSense);
        profile.TryRebind(Caps, InputContext.Gameplay, ControllerAction.Pause, PadButton.Touchpad, out _);
        var issues = new List<string>();
        Assert.Equal(PadButton.R3, profile.Resolve(PadCapabilities.R3, issues).ButtonFor(InputContext.Gameplay, ControllerAction.Pause));
        Assert.Contains("RC_ProfileUnavailable", issues);
        Assert.Equal(PadButton.Touchpad, profile.Resolve(Caps, new List<string>()).ButtonFor(InputContext.Gameplay, ControllerAction.Pause));
        Assert.False(profile.TryRebind(PadCapabilities.None, InputContext.Menu, ControllerAction.Confirm, PadButton.Touchpad, out _));
    }
    [Fact] public void InvalidOverridesCannotIntroduceModalPauseOrDuplicateActions()
    {
        var issues = new List<string>();
        var bindings = Bindings.Restore(Caps, new[] {
            new BindingOverride(InputContext.Modal, ControllerAction.Pause, PadButton.L3),
            new BindingOverride(InputContext.Menu, ControllerAction.Confirm, PadButton.L3),
            new BindingOverride(InputContext.Menu, ControllerAction.Confirm, PadButton.R3),
            new BindingOverride((InputContext)99, ControllerAction.Cancel, PadButton.Circle) }, issues);
        Assert.Equal(PadButton.None, bindings.ButtonFor(InputContext.Modal, ControllerAction.Pause));
        Assert.Equal(PadButton.Cross, bindings.ButtonFor(InputContext.Menu, ControllerAction.Confirm));
        Assert.Contains("RC_ProfileUnknown", issues); Assert.Contains("RC_ProfileConflict", issues);
    }
    [Fact] public void RouterAppliesProfilesPerModelAndRequiresNeutralAfterEdit()
    {
        var profiles = new ControllerProfiles(); var router = new InputRouter { Profiles = profiles };
        router.Update(0, 0, Pad(), true, true, InputContext.Menu);
        profiles.DualSense.TryRebind(Caps, InputContext.Menu, ControllerAction.Confirm, PadButton.L3, out _);
        Assert.Empty(router.Update(1, .1, Pad(buttons: PadButton.L3), true, true, InputContext.Menu).Actions);
        router.Update(2, .2, Pad(), true, true, InputContext.Menu);
        Assert.Contains(ControllerAction.Confirm, router.Update(3, .3, Pad(buttons: PadButton.L3), true, true, InputContext.Menu).Actions);
        router.Update(4, .4, Pad(PadModel.DualShock4), true, true, InputContext.Menu);
        Assert.Equal(PadButton.Cross, router.Bindings.ButtonFor(InputContext.Menu, ControllerAction.Confirm));
        router.Update(5, .5, Pad(buttons: PadButton.L3), true, true, InputContext.Menu);
        Assert.Equal(PadButton.L3, router.Bindings.ButtonFor(InputContext.Menu, ControllerAction.Confirm));
        Assert.Empty(router.Update(6, .6, Pad(buttons: PadButton.L3), true, true, InputContext.Menu).Actions);
        router.Update(7, .7, Pad(), true, true, InputContext.Menu);
        Assert.Contains(ControllerAction.Confirm, router.Update(8, .8, Pad(buttons: PadButton.L3), true, true, InputContext.Menu).Actions);
    }
    [Fact] public void AnalogNavigationUsesActionAfterDirectionIsRebound()
    {
        var profiles = new ControllerProfiles();
        profiles.DualSense.TryRebind(Caps, InputContext.Menu, ControllerAction.Up, PadButton.L3, out _);
        var router = new InputRouter { Profiles = profiles };
        router.Update(0, 0, Pad(), true, true, InputContext.Menu);
        var pad = Pad(); pad.LeftStick = new Stick(0, 1);
        Assert.Equal(new[] { ControllerAction.Up }, router.Update(1, .1, pad, true, true, InputContext.Menu).Actions);
    }
    [Fact] public void DirectRebindingAlsoRequiresNeutral()
    {
        var router = new InputRouter(); router.Update(0, 0, Pad(), true, true, InputContext.Menu);
        router.Bindings.TryRebind(InputContext.Menu, ControllerAction.Confirm, PadButton.L3);
        Assert.Empty(router.Update(1, .1, Pad(buttons: PadButton.L3), true, true, InputContext.Menu).Actions);
    }
    [Fact] public void TutorialProgressAndEnablementSurviveProfileStorage()
    {
        Tutorial<int> Course() => new(new[] {
            new TutorialLesson<int>("a", "A", ControllerAction.Confirm, InputContext.Menu, n => n > 0),
            new TutorialLesson<int>("b", "B", ControllerAction.Cancel, InputContext.Menu, n => n > 1),
            new TutorialLesson<int>("c", "C", ControllerAction.Context, InputContext.Menu, n => n > 2) });
        var profiles = new ControllerProfiles(); var course = Course(); course.ControllerMode = true;
        course.Observe(1); course.Skip(); course.Enabled = false; profiles.SaveTutorial(course);
        var restored = ProfileCodec.Decode(ProfileCodec.Encode(profiles)); var loaded = Course(); restored.LoadTutorial(loaded);
        Assert.False(loaded.Enabled); Assert.Equal(LessonStatus.Completed, loaded.Status("a"));
        Assert.Equal(LessonStatus.Skipped, loaded.Status("b")); Assert.Equal(LessonStatus.Pending, loaded.Status("c"));
        loaded.Replay("a"); restored.SaveTutorial(loaded);
        profiles = ProfileCodec.Decode(ProfileCodec.Encode(restored)); profiles.LoadTutorial(course);
        Assert.Equal(LessonStatus.Pending, course.Status("a"));
    }
    [Fact] public void ExportIsDetachedAndCompleteBindingsRoundTrip()
    {
        var bindings = new Bindings(Caps); bindings.TryRebind(InputContext.Menu, ControllerAction.Confirm, PadButton.L3);
        var issues = new List<string>(); var restored = Bindings.Restore(Caps, bindings.Export(), issues);
        Assert.Empty(issues);
        Assert.Equal(PadButton.L3, restored.ButtonFor(InputContext.Menu, ControllerAction.Confirm));
        var profile = new ControllerProfile(PadModel.DualSense); profile.Restore(bindings.Export());
        var snapshot = profile.Export(); snapshot[0] = new BindingOverride(InputContext.Modal, ControllerAction.Pause, PadButton.Touchpad);
        Assert.DoesNotContain(profile.Export(), x => x.Context == InputContext.Modal && x.Action == ControllerAction.Pause);
    }
}
