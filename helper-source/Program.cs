using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Vortice.Direct3D9;
using static Vortice.Direct3D9.D3D9;

static class Native
{
    [DllImport("user32.dll")] public static extern IntPtr GetDesktopWindow();
}

sealed record ShaderBlob(bool Vertex, byte[] Data, string Source);

static class RageFxc
{
    static byte U8(BinaryReader r) => r.ReadByte();
    static ushort U16(BinaryReader r) => r.ReadUInt16();
    static void SkipString(BinaryReader r) { var n = U8(r); r.BaseStream.Seek(n, SeekOrigin.Current); }

    static byte[] ReadProgram(BinaryReader r)
    {
        var parameters = U8(r);
        for (var i = 0; i < parameters; i++)
        {
            r.BaseStream.Seek(4, SeekOrigin.Current);
            SkipString(r);
        }
        var size = U16(r);
        _ = U16(r);
        return r.ReadBytes(size);
    }

    public static IEnumerable<ShaderBlob> Read(string path)
    {
        using var s = File.OpenRead(path);
        using var r = new BinaryReader(s);
        if (r.ReadUInt32() != 0x61786772) yield break;

        var vs = U8(r);
        for (var i = 0; i < vs; i++)
        {
            var b = ReadProgram(r);
            if (b.Length > 0) yield return new(true, b, path);
        }

        var ps = U8(r);
        for (var i = 0; i < ps; i++)
        {
            var b = ReadProgram(r);
            if (b.Length > 0) yield return new(false, b, path);
        }
    }
}

static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length != 1 || !Directory.Exists(args[0]))
        {
            Console.Error.WriteLine("usage: GTAIV.exe <GTAIV folder>");
            return 2;
        }

        var root = Path.GetFullPath(args[0]);
        Console.WriteLine($"root={root}");
        var unique = new Dictionary<string, ShaderBlob>(StringComparer.Ordinal);
        var fxcCount = 0;
        var bad = 0;
        foreach (var f in Directory.EnumerateFiles(root, "*.fxc", SearchOption.AllDirectories))
        {
            fxcCount++;
            try
            {
                foreach (var shader in RageFxc.Read(f))
                {
                    var h = Convert.ToHexStringLower(SHA1.HashData(shader.Data));
                    unique.TryAdd(h, shader);
                }
            }
            catch (Exception e)
            {
                bad++;
                Console.Error.WriteLine($"fxc-error {f}: {e.Message}");
            }
        }

        var vsCount = unique.Values.Count(x => x.Vertex);
        var psCount = unique.Count - vsCount;
        Console.WriteLine($"fxc={fxcCount} bad={bad} unique={unique.Count} vs={vsCount} ps={psCount}");

        var invalid = unique.Values.Count(x => x.Data.Length < 4 || BinaryPrimitives.ReadUInt32LittleEndian(x.Data) != (x.Vertex ? 0xFFFE0300u : 0xFFFF0300u));
        if (invalid != 0)
        {
            Console.Error.WriteLine($"invalid shader tokens={invalid}");
            return 3;
        }

        var dxvk = Path.Combine(root, "vulkan.dll");
        if (!File.Exists(dxvk))
        {
            Console.Error.WriteLine($"DXVK not found: {dxvk}");
            return 5;
        }
        NativeLibrary.SetDllImportResolver(typeof(D3D9).Assembly, (library, assembly, searchPath) =>
            string.Equals(library, "d3d9.dll", StringComparison.OrdinalIgnoreCase)
                ? NativeLibrary.Load(dxvk)
                : IntPtr.Zero);
        Console.WriteLine($"dxvk={dxvk}");
        using var d3d = Direct3DCreate9();
        var hwnd = Native.GetDesktopWindow();
        var pp = new PresentParameters
        {
            Windowed = true,
            SwapEffect = SwapEffect.Discard,
            DeviceWindowHandle = hwnd,
            BackBufferWidth = 1,
            BackBufferHeight = 1,
            PresentationInterval = PresentInterval.Immediate
        };

        using var device = d3d.CreateDevice(0, DeviceType.Hardware, hwnd,
            CreateFlags.HardwareVertexProcessing | CreateFlags.Multithreaded | CreateFlags.FpuPreserve,
            pp);

        Console.WriteLine("device=ok");
        foreach (System.Diagnostics.ProcessModule m in System.Diagnostics.Process.GetCurrentProcess().Modules)
            if (m.ModuleName.Contains("d3d9", StringComparison.OrdinalIgnoreCase) || m.ModuleName.Contains("vulkan", StringComparison.OrdinalIgnoreCase)) Console.WriteLine($"module={m.ModuleName} => {m.FileName}");
        var done = 0;
        var failed = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var shader in unique.Values)
        {
            try
            {
                if (shader.Vertex)
                {
                    using var obj = device.CreateVertexShader<byte>(shader.Data);
                }
                else
                {
                    using var obj = device.CreatePixelShader<byte>(shader.Data);
                }
            }
            catch (Exception e)
            {
                failed++;
                if (failed <= 20) Console.Error.WriteLine($"create-error {(shader.Vertex ? "VS" : "PS")} {Path.GetFileName(shader.Source)}: {e.Message}");
            }
            done++;
            if (done % 100 == 0 || done == unique.Count)
                Console.WriteLine($"progress={done}/{unique.Count} failed={failed} rate={done / Math.Max(sw.Elapsed.TotalSeconds, 0.001):F0}/s");
        }

        Console.WriteLine($"done={done} failed={failed} seconds={sw.Elapsed.TotalSeconds:F2}");
        return failed == 0 ? 0 : 4;
    }
}


