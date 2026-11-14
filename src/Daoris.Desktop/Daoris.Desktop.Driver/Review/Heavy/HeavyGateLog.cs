namespace Daoris.Driver;

internal static class HeavyGateLog
{
    internal static void CopyTo(string? path, TextWriter output)
    {
        if (path is null || !File.Exists(path)) return;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        var buffer = new char[8192];
        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0) output.Write(buffer, 0, read);
        output.Flush();
    }
}
