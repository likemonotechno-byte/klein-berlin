#import <Foundation/Foundation.h>
#import <GameController/GameController.h>
#include <stddef.h>
#include <stdio.h>
#include <string.h>
#include "rimcontroller.h"

_Static_assert(sizeof(rc_snapshot) == 188, "C# snapshot layout must match");
_Static_assert(offsetof(rc_snapshot, name) == 60, "C# name offset must match");
static GCController *selected;
static uint64_t generation;

static void set_button(rc_snapshot *s, GCControllerButtonInput *input, unsigned bit) {
    if (input != nil && input.isPressed) s->buttons |= UINT64_C(1) << bit;
}

__attribute__((visibility("default")))
int32_t rc_poll(rc_snapshot *s, uint32_t size, uint32_t abi) {
    if (s == NULL || size != sizeof(rc_snapshot)) return -1;
    if (abi != RC_ABI_VERSION) return -2;
    memset(s, 0, sizeof(*s));
    s->size = sizeof(*s);
    s->abi = RC_ABI_VERSION;
    if (![NSThread isMainThread]) return -3;
    @autoreleasepool {
        NSArray<GCController *> *controllers = GCController.controllers;
        if (selected == nil || ![controllers containsObject:selected]) {
            selected = nil;
            // Keep one owner until it disconnects. Never merge inputs from two pads.
            for (GCController *candidate in controllers) {
                GCExtendedGamepad *pad = candidate.extendedGamepad;
                if ([pad isKindOfClass:GCDualSenseGamepad.class] ||
                    [pad isKindOfClass:GCDualShockGamepad.class]) {
                    selected = candidate;
                    generation++;
                    break;
                }
            }
        }
        if (selected == nil) return 0;
        GCExtendedGamepad *pad = selected.extendedGamepad;
        if (pad == nil) { selected = nil; return 0; }
        s->connected = 1;
        s->device_id = generation;
        s->model = [pad isKindOfClass:GCDualSenseGamepad.class] ? 2 : 1;
        snprintf(s->name, sizeof(s->name), "%s", (selected.vendorName ?: @"PlayStation Controller").UTF8String);
        s->left_x = pad.leftThumbstick.xAxis.value;
        s->left_y = pad.leftThumbstick.yAxis.value;
        s->right_x = pad.rightThumbstick.xAxis.value;
        s->right_y = pad.rightThumbstick.yAxis.value;
        s->left_trigger = pad.leftTrigger.value;
        s->right_trigger = pad.rightTrigger.value;
        set_button(s, pad.buttonA, 0);  // Cross
        set_button(s, pad.buttonB, 1);  // Circle
        set_button(s, pad.buttonX, 2);  // Square
        set_button(s, pad.buttonY, 3);  // Triangle
        set_button(s, pad.leftShoulder, 4);
        set_button(s, pad.rightShoulder, 5);
        set_button(s, pad.leftTrigger, 6);
        set_button(s, pad.rightTrigger, 7);
        set_button(s, pad.leftThumbstickButton, 8);
        set_button(s, pad.rightThumbstickButton, 9);
        set_button(s, pad.buttonMenu, 10); // Options; Share/Create intentionally unbound
        GCControllerButtonInput *touchpad = nil;
        if ([pad isKindOfClass:GCDualSenseGamepad.class]) touchpad = ((GCDualSenseGamepad *)pad).touchpadButton;
        else if ([pad isKindOfClass:GCDualShockGamepad.class]) touchpad = ((GCDualShockGamepad *)pad).touchpadButton;
        if (touchpad != nil) { s->capabilities |= 1; set_button(s, touchpad, 11); }
        if (pad.rightThumbstickButton != nil) s->capabilities |= 2;
        set_button(s, pad.dpad.up, 12);
        set_button(s, pad.dpad.down, 13);
        set_button(s, pad.dpad.left, 14);
        set_button(s, pad.dpad.right, 15);
        return 0;
    }
}
