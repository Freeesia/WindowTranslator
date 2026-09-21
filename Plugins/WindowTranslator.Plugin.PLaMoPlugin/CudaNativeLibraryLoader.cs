using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace WindowTranslator.Plugin.PLaMoPlugin;

internal static class CudaNativeLibraryLoader
{
    private static readonly Lock Sync = new();
    private static bool loaded;

    internal static void Load(string runtimeDirectory, string pluginDirectory)
    {
        lock (Sync)
        {
            if (loaded)
            {
                return;
            }

            var nativeDirectory = Path.Combine(pluginDirectory, "runtimes", "win-x64", "native");
            var cudaDirectory = Path.Combine(nativeDirectory, "cuda12");
            var avxDirectory = Avx512F.IsSupported && Avx512BW.IsSupported && Avx512DQ.IsSupported
                ? "avx512"
                : Avx2.IsSupported ? "avx2" : Avx.IsSupported ? "avx" : "noavx";

            // Windows の DLL ローダーが依存 DLL を名前で探す前に、解決済みの絶対パスから読み込む。
            foreach (var fileName in new[] { "cudart64_12.dll", "cublasLt64_12.dll", "cublas64_12.dll" })
            {
                NativeLibrary.Load(Path.Combine(runtimeDirectory, fileName));
            }
            NativeLibrary.Load(Path.Combine(cudaDirectory, "ggml-base.dll"));
            NativeLibrary.Load(Path.Combine(nativeDirectory, avxDirectory, "ggml-cpu.dll"));
            NativeLibrary.Load(Path.Combine(cudaDirectory, "ggml-cuda.dll"));
            NativeLibrary.Load(Path.Combine(cudaDirectory, "ggml.dll"));
            loaded = true;
        }
    }
}
