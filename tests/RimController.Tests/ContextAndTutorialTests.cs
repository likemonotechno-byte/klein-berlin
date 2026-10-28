using System;
using System.Collections.Generic;
using RimController.Core;
using Xunit;

namespace RimController.Tests;

public class ContextStackTests
{
    private static ControllerSnapshot Pad(PadButton buttons = PadButton.None) => new()
        { Connected = true, DeviceId = 1, Buttons = buttons };

    [Fact] public void NestedModalsRequireReleaseAndRestoreParentFocus()
    {
        var stack = new InputContextStack();
        var router = new InputRouter();
        var parent = new InputScope(InputContext.Modal);
        parent.Focus.SetItems(new[] { new FocusItem("a"), new FocusItem("b") });
        parent.Focus.Move(1);
        stack.Push(parent);
        stack.Route(router, 0, 0, Pad(), true, true);
        Assert.Contains(ControllerAction.Confirm, stack.Route(router, 1, .1, Pad(PadButton.Cross), true, true).Actions);
        var child = new InputScope(InputContext.Modal);
        stack.Push(child);
        Assert.Empty(stack.Route(router, 2, .2, Pad(PadButton.Cross), true, true).Actions);
        stack.Route(router, 3, .3, Pad(), true, true);
        Assert.Contains(ControllerAction.Confirm, stack.Route(router, 4, .4, Pad(PadButton.Cross), true, true).Actions);
        stack.Remove(child);
        Assert.Empty(stack.Route(router, 5, .5, Pad(PadButton.Cross), true, true).Actions);
        Assert.Same(parent, stack.Current);
        Assert.Equal("b", stack.Current!.Focus.Current?.Id);
        stack.Route(router, 6, .6, Pad(), true, true);
        Assert.Single(stack.Route(router, 7, .7, Pad(PadButton.Cross), true, true).Actions);
    }
    [Fact] public void RemovingUnderlyingWindowDoesNotDismissModal()
    {
        var stack = new InputContextStack();
        var parent = new InputScope(InputContext.Menu);
        var child = new InputScope(InputContext.Modal);
        stack.Push(parent); stack.Push(child);
        Assert.True(stack.Remove(parent));
        Assert.Same(child, stack.Current);
        Assert.False(stack.Remove(parent));
        Assert.Throws<ArgumentException>(() => stack.Push(child));
        stack.Clear(); Assert.Null(stack.Current);
        Assert.Empty(stack.Route(new InputRouter(), 0, 0, Pad(PadButton.Cross), true, true).Actions);
    }
    [Fact] public void OwnerChangeClearsNavigationRepeatEvenForSameContextType()
    {
        var router = new InputRouter();
        var first = new object(); var second = new object();
        router.Update(0, 0, Pad(), true, true, InputContext.Modal, first);
        router.Update(1, .1, Pad(PadButton.Down), true, true, InputContext.Modal, first);
        Assert.Empty(router.Update(2, 1, Pad(PadButton.Down), true, true, InputContext.Modal, second).Actions);
        Assert.Empty(router.Update(3, 2, Pad(PadButton.Down), true, true, InputContext.Modal, second).Actions);
    }
}

public class TutorialTests
{
    private static Tutorial<int> Course() => new(new[] {
        new TutorialLesson<int>("shelter", "ShelterText", ControllerAction.Build, InputContext.Gameplay, state => state >= 1),
        new TutorialLesson<int>("research", "ResearchText", ControllerAction.Confirm, InputContext.Menu, state => state >= 2)
    });

    [Fact] public void OnlyObservedConditionsCompleteOneLessonAtATime()
    {
        var tutorial = Course(); tutorial.ControllerMode = true;
        Assert.False(tutorial.Observe(0));
        Assert.Equal("shelter", tutorial.Current?.Id);
        Assert.True(tutorial.Observe(2));
        Assert.Equal("research", tutorial.Current?.Id);
        Assert.Equal(LessonStatus.Pending, tutorial.Status("research"));
        Assert.True(tutorial.Observe(2)); Assert.Null(tutorial.Current);
        Assert.False(tutorial.Observe(2));
    }
    [Fact] public void ClassicModeAndDisabledTutorialHideHintsAndFreezeProgress()
    {
        var tutorial = Course();
        Assert.Null(tutorial.Current); Assert.False(tutorial.Observe(2)); Assert.False(tutorial.Skip());
        tutorial.ControllerMode = true; tutorial.Enabled = false;
        Assert.Null(tutorial.Current); Assert.False(tutorial.Observe(2));
        tutorial.Enabled = true;
        Assert.Equal("shelter", tutorial.Current?.Id);
    }
    [Fact] public void SkippingAndReplayAreDistinctFromCompletion()
    {
        var tutorial = Course(); tutorial.ControllerMode = true;
        Assert.True(tutorial.Skip());
        Assert.Equal(LessonStatus.Skipped, tutorial.Status("shelter"));
        Assert.True(tutorial.Observe(2));
        tutorial.Replay("shelter"); Assert.Equal("shelter", tutorial.Current?.Id);
        Assert.True(tutorial.Observe(1)); Assert.Null(tutorial.Current);
        Assert.Throws<ArgumentException>(() => tutorial.Replay("unknown"));
    }
    [Fact] public void ProfileRestoreIgnoresUnknownAndInvalidEntriesAndExportIsDetached()
    {
        var tutorial = Course(); tutorial.ControllerMode = true;
        tutorial.Skip(); var saved = tutorial.Export();
        saved["research"] = (LessonStatus)999;
        saved["future"] = LessonStatus.Completed;
        var restored = Course(); restored.ControllerMode = true; restored.Restore(saved);
        Assert.Equal(LessonStatus.Skipped, restored.Status("shelter"));
        Assert.Equal("research", restored.Current?.Id);
        saved["shelter"] = LessonStatus.Pending;
        Assert.Equal(LessonStatus.Skipped, tutorial.Status("shelter"));
        restored.Restore(new Dictionary<string, LessonStatus>());
        Assert.Equal("shelter", restored.Current?.Id);
    }
    [Fact] public void HintsResolveCurrentRebindingAndTouchpadFallback()
    {
        var lesson = new TutorialLesson<int>("pause", "PauseText", ControllerAction.Pause, InputContext.Gameplay, state => state == 1);
        var bindings = new Bindings(PadCapabilities.R3);
        Assert.Equal("R3", lesson.ButtonLabel(bindings));
        Assert.True(bindings.TryRebind(InputContext.Gameplay, ControllerAction.Pause, PadButton.L3));
        Assert.Equal("L3", lesson.ButtonLabel(bindings));
        Assert.Equal("Touchpad", lesson.ButtonLabel(new Bindings(PadCapabilities.TouchpadClick)));
    }
    [Fact] public void EmptyCoursesAreSafeAndDuplicateIdsAreRejected()
    {
        var tutorial = new Tutorial<int>(Array.Empty<TutorialLesson<int>>()) { ControllerMode = true };
        Assert.Null(tutorial.Current); Assert.False(tutorial.Observe(2)); Assert.False(tutorial.Skip());
        var lesson = new TutorialLesson<int>("id", "key", ControllerAction.Confirm, InputContext.Menu, _ => true);
        Assert.Throws<ArgumentException>(() => new Tutorial<int>(new[] { lesson, lesson }));
    }
}
