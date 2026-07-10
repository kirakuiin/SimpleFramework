using NUnit.Framework;

namespace SimpleFramework.Test.Documentation;

public class TestSourceTextQuality
{
    private static readonly string[] CorruptedTextMarkers =
    [
        "閼?", "鐎?", "濮?", "鏉?", "閸?", "娑?", "瑜?", "妫?", "缂?", "鐟?",
        "缁?", "閻?", "鐏?", "閸?", "娴?", "鐞?", "閻?", "姒?", "鐎?", "閺?",
        "閸?", "閸?", "濞?", "浼?", "鐠?", "缍?", "閵?", "閿?", "鈧?", "锟?"
    ];

    [Test]
    public void TestProductionSourceDoesNotContainCorruptedChineseText()
    {
        var root = GetRepositoryRoot();
        var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsUnder(path, root, "bin"))
            .Where(path => !IsUnder(path, root, "obj"))
            .Where(path => !IsUnder(path, root, "Test"))
            .ToArray();

        var failures = files
            .SelectMany(path => FindMarkers(path).Select(marker => $"{Path.GetRelativePath(root, path)} contains {marker}"))
            .ToArray();

        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
    }

    private static IEnumerable<string> FindMarkers(string path)
    {
        var text = File.ReadAllText(path);
        return CorruptedTextMarkers.Where(text.Contains);
    }

    private static bool IsUnder(string path, string root, string directory)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SimpleFramework.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
