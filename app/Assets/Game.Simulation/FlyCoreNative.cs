// P/Invoke binding to FlyCore (C ABI, see flycore/include/flycore.h). No per-neuron objects; buffers per tick.
using System;
using System.Runtime.InteropServices;

namespace ThatDamnFly.Simulation
{
    public static class FlyCoreNative
    {
#if UNITY_IOS || UNITY_WEBGL
        const string Lib = "__Internal";
#else
        const string Lib = "flycore";
#endif
        public const uint AbiVersion = 1;
        public const int Sectors = 8, MotorChannels = 8;

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct SensoryFrame
        {
            public uint struct_size, version;
            public fixed float expansion[8];
            public fixed float angular_size[8];
            public fixed float motion_h[8];
            public fixed float motion_v[8];
            public fixed float surface_proximity[8];
            public float background_drive;
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct MotorFrame
        {
            public uint struct_size, version;
            public fixed float channel[8];
            public uint spikes_this_step, flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct ModelInfo
        {
            public uint struct_size, format_version, neuron_count, edge_count, input_channel_count, output_channel_count, max_delay_steps, reserved;
            public fixed byte payload_sha256[32];
            public fixed byte model_id[64];
        }

        public enum Status { Ok = 0, InvalidArg = 1, BadModel = 2, HashMismatch = 3, Limit = 4, Numeric = 5, Oom = 6, Abi = 7 }

        [DllImport(Lib)] public static extern uint fc_abi_version();
        [DllImport(Lib)] public static extern int fc_inspect(byte[] model, UIntPtr len, ref ModelInfo info);
        [DllImport(Lib)] public static extern int fc_create(byte[] model, UIntPtr len, ulong seed, out IntPtr handle);
        [DllImport(Lib)] public static extern int fc_step(IntPtr h, ref SensoryFrame input, uint delta_ms, ref MotorFrame output);
        [DllImport(Lib)] public static extern int fc_reset(IntPtr h, ulong seed);
        [DllImport(Lib)] public static extern int fc_get_info(IntPtr h, ref ModelInfo info);
        [DllImport(Lib)] public static extern void fc_destroy(IntPtr h);
    }

    /// <summary>Instance of the neural simulator. One per round; Dispose is mandatory.</summary>
    public sealed class FlyCoreInstance : IDisposable
    {
        IntPtr _h;
        FlyCoreNative.SensoryFrame _in;
        FlyCoreNative.MotorFrame _out;
        public readonly uint NeuronCount, EdgeCount;
        public readonly string ModelId;
        public readonly float[] Motor = new float[FlyCoreNative.MotorChannels];
        public uint LastSpikes { get; private set; }

        public static bool TryInspect(byte[] model, out string modelId, out uint neurons, out FlyCoreNative.Status status)
        {
            var info = new FlyCoreNative.ModelInfo { struct_size = (uint)Marshal.SizeOf<FlyCoreNative.ModelInfo>() };
            status = (FlyCoreNative.Status)FlyCoreNative.fc_inspect(model, (UIntPtr)model.Length, ref info);
            modelId = status == FlyCoreNative.Status.Ok ? IdOf(ref info) : null; neurons = info.neuron_count;
            return status == FlyCoreNative.Status.Ok;
        }

        public FlyCoreInstance(byte[] model, ulong seed)
        {
            if (FlyCoreNative.fc_abi_version() != FlyCoreNative.AbiVersion) throw new InvalidOperationException("FlyCore ABI mismatch");
            var st = (FlyCoreNative.Status)FlyCoreNative.fc_create(model, (UIntPtr)model.Length, seed, out _h);
            if (st != FlyCoreNative.Status.Ok) throw new InvalidOperationException("fc_create: " + st);
            var info = new FlyCoreNative.ModelInfo { struct_size = (uint)Marshal.SizeOf<FlyCoreNative.ModelInfo>() };
            FlyCoreNative.fc_get_info(_h, ref info);
            NeuronCount = info.neuron_count; EdgeCount = info.edge_count; ModelId = IdOf(ref info);
            _in = new FlyCoreNative.SensoryFrame { struct_size = (uint)Marshal.SizeOf<FlyCoreNative.SensoryFrame>(), version = FlyCoreNative.AbiVersion };
            _out = new FlyCoreNative.MotorFrame { struct_size = (uint)Marshal.SizeOf<FlyCoreNative.MotorFrame>(), version = FlyCoreNative.AbiVersion };
        }

        static unsafe string IdOf(ref FlyCoreNative.ModelInfo info)
        {
            fixed (byte* p = info.model_id) { int n = 0; while (n < 64 && p[n] != 0) n++; return System.Text.Encoding.UTF8.GetString(p, n); }
        }

        /// <summary>Copies the 41 channels (kind×8+sector; 40 = background) and advances delta_ms. Returns false on a numeric error.</summary>
        public unsafe bool Step(float[] channels, uint deltaMs)
        {
            fixed (float* e = _in.expansion) for (int s = 0; s < 8; s++) e[s] = channels[s];
            fixed (float* a = _in.angular_size) for (int s = 0; s < 8; s++) a[s] = channels[8 + s];
            fixed (float* m = _in.motion_h) for (int s = 0; s < 8; s++) m[s] = channels[16 + s];
            fixed (float* m = _in.motion_v) for (int s = 0; s < 8; s++) m[s] = channels[24 + s];
            fixed (float* sp = _in.surface_proximity) for (int s = 0; s < 8; s++) sp[s] = channels[32 + s];
            _in.background_drive = channels[40];
            var st = (FlyCoreNative.Status)FlyCoreNative.fc_step(_h, ref _in, deltaMs, ref _out);
            if (st != FlyCoreNative.Status.Ok) return false;
            fixed (float* c = _out.channel) for (int i = 0; i < 8; i++) Motor[i] = c[i];
            LastSpikes = _out.spikes_this_step;
            return true;
        }

        public void Reset(ulong seed) { FlyCoreNative.fc_reset(_h, seed); Array.Clear(Motor, 0, Motor.Length); }
        public void Dispose() { if (_h != IntPtr.Zero) { FlyCoreNative.fc_destroy(_h); _h = IntPtr.Zero; } }
    }
}
