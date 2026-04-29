using System;
using System.IO;
using System.Text;

namespace UniversalSensRandomizer.Services;

public sealed class LiveOutputWriter
{
    private readonly string filePath;

    public LiveOutputWriter()
    {
        filePath = Path.Combine(AppContext.BaseDirectory, "current_sensitivity.txt");
    }

    public string FilePath => filePath;

    public void Write(string text)
    {
        string tempPath = filePath + ".tmp";
        File.WriteAllText(tempPath, text, Encoding.UTF8);
        File.Move(tempPath, filePath, overwrite: true);
    }
}
