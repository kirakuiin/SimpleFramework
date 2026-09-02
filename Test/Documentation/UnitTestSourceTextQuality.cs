using NUnit.Framework;

namespace SimpleFramework.Test.Documentation;

public class TestSourceTextQuality
{
    private static readonly string[] CorruptedTextMarkers =
    [
        "鑾峰", "瀵硅", "鍒涘", "澶ч", "鍔熻", "涓€", "涓嶅", "鍦ㄥ", "鐖跺", "瀛愬",
        "鐢熷", "懡鍛", "鍛ㄦ", "湡濂", "戠害", "琛ㄧ", "鎵€", "夋潈", "銆", "锛",
        "鐨勫", "鏌ヨ", "娉ㄥ", "瀹炰", "鍙", "€?", "�"
    ];

    [Test]
    public void TestRepositoryTextDoesNotContainCorruptedChineseText()
    {
        var root = GetRepositoryRoot();
        var sourceFiles = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsUnder(path, root, ".git"))
            .Where(path => !IsUnder(path, root, ".superpowers"))
            .Where(path => !IsUnder(path, root, "bin"))
            .Where(path => !IsUnder(path, root, "obj"))
            .Where(path => !IsUnder(path, root, "Test"));
        var publicDocs = new[]
        {
            Path.Combine(root, "README.md"),
            Path.Combine(root, "docs", "domain-lifecycle.md"),
            Path.Combine(root, "docs", "bindable-property.md")
        };
        var files = sourceFiles.Concat(publicDocs).ToArray();

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
