using System;

namespace RimController.Core
{
    [Flags]
    public enum PadButton : ulong
    {
        None = 0, Cross = 1UL << 0, Circle = 1UL << 1, Square = 1UL << 2,
        Triangle = 1UL << 3, L1 = 1UL << 4, R1 = 1UL << 5,
        L2 = 1UL << 6, R2 = 1UL << 7, L3 = 1UL << 8, R3 = 1UL << 9,
        Options = 1UL << 10, Touchpad = 1UL << 11,
        Up = 1UL << 12, Down = 1UL << 13, Left = 1UL << 14, Right = 1UL << 15
    }
    public enum PadModel : uint { Unknown, DualShock4, DualSense }
    [Flags]
    public enum PadCapabilities : uint { None = 0, TouchpadClick = 1, R3 = 2 }
    public enum InputContext { Gameplay, Menu, Modal, Building }
    public enum ControllerAction
    {
        Confirm, Cancel, Context, Overview, Previous, Next, Pause, GameMenu,
        SpeedDown, SpeedUp, Alerts, Build, Up, Down, Left, Right,
        PagePrevious, PageNext, RotateLeft, RotateRight
    }
    public readonly struct Stick
    {
        public readonly float X, Y;
        public Stick(float x, float y) { X = x; Y = y; }
        public float Magnitude => (float)Math.Sqrt(X * X + Y * Y);
        public Stick Filter(float deadzone, float sensitivity = 1, bool invertY = false)
        {
            if (!Finite(X) || !Finite(Y) || !Finite(deadzone) || !Finite(sensitivity)) return default;
            deadzone = Math.Max(0, Math.Min(0.9f, deadzone));
            float length = Magnitude;
            if (length <= deadzone || length == 0) return default;
            float scale = Math.Min(1, (Math.Min(1, length) - deadzone) / (1 - deadzone) * Math.Max(0, sensitivity));
            return new Stick(X / length * scale, Y / length * scale * (invertY ? -1 : 1));
        }
        internal static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
    }
    public sealed class ControllerSnapshot
    {
        public static ControllerSnapshot Disconnected => new ControllerSnapshot();
        public bool Connected { get; set; }
        public ulong DeviceId { get; set; }
        public string Name { get; set; } = "";
        public PadModel Model { get; set; }
        public PadCapabilities Capabilities { get; set; }
        public PadButton Buttons { get; set; }
        public Stick LeftStick { get; set; }
        public Stick RightStick { get; set; }
        public float LeftTrigger { get; set; }
        public float RightTrigger { get; set; }
        public bool IsNeutral(float deadzone) => Buttons == PadButton.None &&
            Stick.Finite(LeftStick.Magnitude) && Stick.Finite(RightStick.Magnitude) &&
            LeftStick.Magnitude <= deadzone && RightStick.Magnitude <= deadzone &&
            Stick.Finite(LeftTrigger) && Stick.Finite(RightTrigger) &&
            LeftTrigger >= 0 && LeftTrigger < 0.1f && RightTrigger >= 0 && RightTrigger < 0.1f;
    }
    public interface IControllerBackend : IDisposable
    {
        ControllerSnapshot Poll();
        string Diagnostic { get; }
    }
    public static class PlayStationGlyphs
    {
        // Text accompanies symbols because the game's fonts may not contain every glyph.
        public static string Label(PadButton button) => button switch
        {
            PadButton.Cross => "×", PadButton.Circle => "○", PadButton.Square => "□",
            PadButton.Triangle => "△", PadButton.Touchpad => "Touchpad",
            PadButton.Up => "↑", PadButton.Down => "↓", PadButton.Left => "←", PadButton.Right => "→",
            _ => button.ToString()
        };
    }
}
