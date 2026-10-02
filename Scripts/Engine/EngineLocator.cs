namespace SuperChess.Engine;

/// <summary>
/// Pikafish 引擎文件定位：Android 上从 nativeLibraryDir 解析引擎/NNUE 路径，
/// 并按 CPU dotprod 特性选择引擎变体（增强版失败可回退普通版）；
/// 桌面平台从项目 engines/windows/ 解析通用构建（需手动放置，不入库）。
/// </summary>
public static class EngineLocator
{
    private const string DotProdEngine = "libpikafish-armv8-dotprod.so";
    private const string PlainEngine = "libpikafish-armv8.so";
    private const string NnueFile = "libpikafish.nnue.so";
    private const string GodotLibName = "libgodot_android.so";

    private const string WindowsEngine = "Pikafish-Windows-x86-64-universal.exe";
    private const string WindowsNnue = "pikafish.nnue";

    /// <summary>
    /// 解析 Android 安装后的原生库目录（nativeLibraryDir）。
    /// 实现方式：从 /proc/self/maps 中已加载的 libgodot_android.so 路径提取所在目录，
    /// 避免 Java interop 依赖。非 Android 平台返回 null。
    /// </summary>
    public static string? GetNativeLibraryDir()
    {
        if (!OperatingSystem.IsAndroid())
        {
            return null;
        }

        try
        {
            foreach (var line in File.ReadLines("/proc/self/maps"))
            {
                if (!line.Contains(GodotLibName, StringComparison.Ordinal))
                {
                    continue;
                }

                // maps 行格式：address perms offset dev inode path
                var lastSpace = line.LastIndexOf(' ');
                if (lastSpace < 0 || lastSpace + 1 >= line.Length)
                {
                    continue;
                }

                var path = line[(lastSpace + 1)..];
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    return dir;
                }
            }
        }
        catch (Exception)
        {
            // /proc 读取失败（异常沙箱环境）视为不可用。
        }

        return null;
    }

    /// <summary>
    /// 检测 CPU 是否支持 dotprod 指令（ARM v8.2），决定启用增强版引擎。
    /// </summary>
    public static bool HasDotProd()
    {
        try
        {
            foreach (var line in File.ReadLines("/proc/cpuinfo"))
            {
                if (line.StartsWith("Features", StringComparison.Ordinal) &&
                    line.Contains("dotprod", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        catch (Exception)
        {
            // 读取失败按不支持处理，走普通版。
        }

        return false;
    }

    /// <summary>
    /// 解析应使用的 Pikafish 引擎可执行文件绝对路径。
    /// Android：优先 dotprod 增强版，不存在时回退普通版；
    /// 桌面：projectDir 下 engines/windows/ 的通用构建。均不可用返回 null。
    /// </summary>
    public static string? ResolveEnginePath(string? projectDir = null)
    {
        var libDir = GetNativeLibraryDir();
        if (libDir is null)
        {
            return ResolveWindowsPath(projectDir, WindowsEngine);
        }

        string[] candidates = HasDotProd()
            ? [Path.Combine(libDir, DotProdEngine), Path.Combine(libDir, PlainEngine)]
            : [Path.Combine(libDir, PlainEngine)];
        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// 解析 NNUE 权重文件绝对路径（供 setoption EvalFile 使用）；
    /// Android 取 nativeLibraryDir，桌面取 engines/windows/；不可用返回 null。
    /// </summary>
    public static string? ResolveNnuePath(string? projectDir = null)
    {
        var libDir = GetNativeLibraryDir();
        if (libDir is null)
        {
            return ResolveWindowsPath(projectDir, WindowsNnue);
        }

        var path = Path.Combine(libDir, NnueFile);
        return File.Exists(path) ? path : null;
    }

    /// <summary>桌面分支：在 projectDir/engines/windows/ 下定位文件，缺失或未传目录返回 null。</summary>
    private static string? ResolveWindowsPath(string? projectDir, string fileName)
    {
        if (string.IsNullOrEmpty(projectDir))
        {
            return null;
        }

        var path = Path.Combine(projectDir, "engines", "windows", fileName);
        return File.Exists(path) ? path : null;
    }
}
