using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

internal static class LegacySpeedTreeExtractor
{
    const string SpeedTreeDll = "SpeedTreeRT.dll";
    const int GeometryBytes = 288;
    const int Billboard0Offset = 240;

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool SetDllDirectory(string path);

    [DllImport(SpeedTreeDll, EntryPoint = "??0CSpeedTreeRT@@QAE@XZ", CallingConvention = CallingConvention.ThisCall)]
    static extern IntPtr ConstructTree(IntPtr self);
    [DllImport(SpeedTreeDll, EntryPoint = "??1CSpeedTreeRT@@QAE@XZ", CallingConvention = CallingConvention.ThisCall)]
    static extern void DestroyTree(IntPtr self);
    [DllImport(SpeedTreeDll, EntryPoint = "?LoadTree@CSpeedTreeRT@@QAE_NPBD@Z", CallingConvention = CallingConvention.ThisCall, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)] static extern bool LoadTree(IntPtr self, string path);
    [DllImport(SpeedTreeDll, EntryPoint = "?Compute@CSpeedTreeRT@@QAE_NPBMI_N@Z", CallingConvention = CallingConvention.ThisCall)]
    [return: MarshalAs(UnmanagedType.I1)] static extern bool Compute(IntPtr self, IntPtr transform, uint seed, [MarshalAs(UnmanagedType.I1)] bool compositeStrips);
    [DllImport(SpeedTreeDll, EntryPoint = "?GetBoundingBox@CSpeedTreeRT@@QBEXPAM@Z", CallingConvention = CallingConvention.ThisCall)]
    static extern void GetBoundingBox(IntPtr self, [Out] float[] bounds);
    [DllImport(SpeedTreeDll, EntryPoint = "?GetGeometry@CSpeedTreeRT@@QAEXAAUSGeometry@1@KFFF@Z", CallingConvention = CallingConvention.ThisCall)]
    static extern void GetGeometry(IntPtr self, IntPtr geometry, uint flags, short branchLod, short frondLod, short leafLod);
    [DllImport(SpeedTreeDll, EntryPoint = "?SetCamera@CSpeedTreeRT@@SAXPBM0@Z", CallingConvention = CallingConvention.Cdecl)]
    static extern void SetCamera(float[] position, float[] direction);
    [DllImport(SpeedTreeDll, EntryPoint = "?SetDropToBillboard@CSpeedTreeRT@@SAX_N@Z", CallingConvention = CallingConvention.Cdecl)]
    static extern void SetDropToBillboard([MarshalAs(UnmanagedType.I1)] bool enabled);
    [DllImport(SpeedTreeDll, EntryPoint = "?SetLodLevel@CSpeedTreeRT@@QAEXM@Z", CallingConvention = CallingConvention.ThisCall)]
    static extern void SetLodLevel(IntPtr self, float level);

    [DllImport(SpeedTreeDll, EntryPoint = "??0SGeometry@CSpeedTreeRT@@QAE@XZ", CallingConvention = CallingConvention.ThisCall)]
    static extern IntPtr ConstructGeometry(IntPtr self);
    [DllImport(SpeedTreeDll, EntryPoint = "??1SGeometry@CSpeedTreeRT@@QAE@XZ", CallingConvention = CallingConvention.ThisCall)]
    static extern void DestroyGeometry(IntPtr self);
    [DllImport(SpeedTreeDll, EntryPoint = "??0STextures@CSpeedTreeRT@@QAE@XZ", CallingConvention = CallingConvention.ThisCall)]
    static extern IntPtr ConstructTextures(IntPtr self);
    [DllImport(SpeedTreeDll, EntryPoint = "??1STextures@CSpeedTreeRT@@QAE@XZ", CallingConvention = CallingConvention.ThisCall)]
    static extern void DestroyTextures(IntPtr self);
    [DllImport(SpeedTreeDll, EntryPoint = "?GetTextures@CSpeedTreeRT@@QBEXAAUSTextures@1@@Z", CallingConvention = CallingConvention.ThisCall)]
    static extern void GetTextures(IntPtr self, IntPtr textures);

    static int Main(string[] args)
    {
        if (args.Length != 2 || !File.Exists(args[0]) || !File.Exists(args[1])) return Fail("Usage: extractor SpeedTreeRT.dll tree.spt");
        SetDllDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0])));
        IntPtr tree = Marshal.AllocHGlobal(1024);
        IntPtr geometry = Marshal.AllocHGlobal(GeometryBytes);
        IntPtr textures = Marshal.AllocHGlobal(28);
        Zero(tree, 1024); Zero(geometry, GeometryBytes); Zero(textures, 28);
        bool treeConstructed = false, geometryConstructed = false, texturesConstructed = false;
        try
        {
            ConstructTree(tree); treeConstructed = true;
            if (!LoadTree(tree, Path.GetFullPath(args[1]))) return Fail("LoadTree failed");
            if (!Compute(tree, IntPtr.Zero, 1, true)) return Fail("Compute failed");
            ConstructGeometry(geometry); geometryConstructed = true;
            ConstructTextures(textures); texturesConstructed = true;
            SetCamera(new[] { 0f, -1000f, 100f }, new[] { 0f, 1f, -0.1f });
            SetDropToBillboard(true);
            SetLodLevel(tree, 0f);
            GetGeometry(tree, geometry, 8u, -1, -1, -1);
            GetTextures(tree, textures);

            int billboard = Billboard0Offset;
            bool active = Marshal.ReadByte(geometry, billboard) != 0;
            IntPtr uvPointer = Marshal.ReadIntPtr(geometry, billboard + 4);
            IntPtr coordinatePointer = Marshal.ReadIntPtr(geometry, billboard + 8);
            if (!active || uvPointer == IntPtr.Zero || coordinatePointer == IntPtr.Zero) return Fail("Billboard geometry unavailable");
            float[] bounds = new float[6]; GetBoundingBox(tree, bounds);
            float[] coordinates = ReadFloats(coordinatePointer, 12);
            float[] uv = ReadFloats(uvPointer, 8);
            string branch = ReadAnsiPointer(textures, 0);
            string composite = ReadAnsiPointer(textures, 20);
            Console.WriteLine("OK|" + Values(bounds) + "|" + Values(coordinates) + "|" + Values(uv) + "|" + Escape(branch) + "|" + Escape(composite));
            return 0;
        }
        catch (Exception exception) { return Fail(exception.GetType().Name + ": " + exception.Message); }
        finally
        {
            if (texturesConstructed) DestroyTextures(textures);
            if (geometryConstructed) DestroyGeometry(geometry);
            if (treeConstructed) DestroyTree(tree);
            Marshal.FreeHGlobal(textures); Marshal.FreeHGlobal(geometry); Marshal.FreeHGlobal(tree);
        }
    }

    static void Zero(IntPtr pointer, int count) { for (int index = 0; index < count; index++) Marshal.WriteByte(pointer, index, 0); }
    static float[] ReadFloats(IntPtr pointer, int count) { float[] values = new float[count]; Marshal.Copy(pointer, values, 0, count); return values; }
    static string ReadAnsiPointer(IntPtr structure, int offset) { IntPtr value = Marshal.ReadIntPtr(structure, offset); return value == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(value) ?? string.Empty; }
    static string Values(float[] values) { return string.Join(",", values.Select(value => value.ToString("R", CultureInfo.InvariantCulture))); }
    static string Escape(string value) { return (value ?? string.Empty).Replace("|", string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty); }
    static int Fail(string message) { Console.Error.WriteLine(message); return 2; }
}
