using System;
using System.IO;
using System.Runtime.InteropServices;
using RimController.Core;

namespace RimController.Mod
{
    [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Ansi)]
    internal struct NativeSnapshot
    {
        public uint Size, Abi;
        public ulong DeviceId, Buttons;
        public uint Capabilities, Model, Connected;
        public float LeftX, LeftY, RightX, RightY, LeftTrigger, RightTrigger;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Name;
    }
    public sealed class NativeBridge : IControllerBackend
    {
        private const string LibSystem = "/usr/lib/libSystem.B.dylib";
        [DllImport(LibSystem)] private static extern IntPtr dlopen(string path, int mode);
        [DllImport(LibSystem)] private static extern IntPtr dlsym(IntPtr handle, string symbol);
        [DllImport(LibSystem)] private static extern IntPtr dlerror();
        // Keep library loaded for the lifetime of the process: ObjC classes/frameworks must not be unloaded.
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int PollDelegate(ref NativeSnapshot state, uint size, uint abi);
        private PollDelegate? poll;
        private bool disposed;
        public string Diagnostic { get; private set; } = "";
        public bool Available => poll != null && !disposed;
        public NativeBridge(string modRoot, bool isMac)
        {
            if (!isMac) { Diagnostic = "RC_MacRequired"; return; }
            string path = Path.GetFullPath(Path.Combine(modRoot, "Native", "macOS", "librimcontroller.dylib"));
            if (!File.Exists(path)) { Diagnostic = "RC_BridgeMissing"; return; }
            try
            {
                IntPtr handle = dlopen(path, 2); // RTLD_NOW | RTLD_LOCAL, absolute path only
                if (handle == IntPtr.Zero) throw new InvalidOperationException(ReadError());
                IntPtr symbol = dlsym(handle, "rc_poll");
                if (symbol == IntPtr.Zero) throw new InvalidOperationException(ReadError());
                poll = (PollDelegate)Marshal.GetDelegateForFunctionPointer(symbol, typeof(PollDelegate));
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException ||
                ex is BadImageFormatException || ex is InvalidOperationException || ex is ArgumentException)
            {
                Diagnostic = "RC_BridgeError";
                ErrorDetail = ex.Message;
            }
        }
        public string ErrorDetail { get; private set; } = "";
        private static string ReadError() => Marshal.PtrToStringAnsi(dlerror()) ?? "dlopen/dlsym failed";
        public ControllerSnapshot Poll()
        {
            if (!Available) return ControllerSnapshot.Disconnected;
            var raw = new NativeSnapshot { Name = "" };
            uint size = (uint)Marshal.SizeOf(typeof(NativeSnapshot));
            int status = poll!(ref raw, size, 1);
            if (status != 0 || raw.Abi != 1 || raw.Size != size)
            {
                Diagnostic = "RC_BridgeError";
                ErrorDetail = $"Native status {status}; ABI {raw.Abi}, size {raw.Size}, expected 1/{size}";
                poll = null;
                return ControllerSnapshot.Disconnected;
            }
            Diagnostic = raw.Connected == 0 ? "RC_NoController" : "RC_Connected";
            return new ControllerSnapshot
            {
                Connected = raw.Connected != 0, DeviceId = raw.DeviceId,
                Name = raw.Name, Model = (PadModel)raw.Model, Capabilities = (PadCapabilities)raw.Capabilities,
                Buttons = (PadButton)raw.Buttons, LeftStick = new Stick(raw.LeftX, raw.LeftY),
                RightStick = new Stick(raw.RightX, raw.RightY), LeftTrigger = raw.LeftTrigger, RightTrigger = raw.RightTrigger
            };
        }
        public void Dispose() { disposed = true; poll = null; }
    }
}
