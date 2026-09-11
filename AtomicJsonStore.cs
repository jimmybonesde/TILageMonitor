using System.Text.Json;

namespace TILageMonitor;

public static class AtomicJsonStore
{
    public static void Write<T>(string path, T value, JsonSerializerOptions options)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Der Speicherpfad enthält kein Verzeichnis.");
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(value, options));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
