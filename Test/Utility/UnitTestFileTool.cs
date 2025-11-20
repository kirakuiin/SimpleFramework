using System;
using System.IO;
using NUnit.Framework;
using SimpleFramework.Utility;

namespace Test.Utility;

/// <summary>
/// FileTool 工具类的单元测试
/// </summary>
[TestFixture]
public class TestFileTool
{
    private string _testDirPath;
    private TestData _testData;

    [SetUp]
    public void Setup()
    {
        _testDirPath = Path.Combine(Path.GetTempPath(), "TestFileTool", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirPath);
        _testData = new TestData
        {
            Id = 1,
            Name = "TestObject",
            IsActive = true,
            Score = 99.5,
            CreatedDate = DateTime.Now
        };
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testDirPath))
        {
            try
            {
                Directory.Delete(_testDirPath, true);
            }
            catch
            {
                // 忽略清理错误
            }
        }
    }

    [Test]
    public void TestSaveAsJsonAndLoadFromJsonSavesAndLoadsCorrectly()
    {
        // Arrange
        string testFile = Path.Combine(_testDirPath, "test.json");

        // Act
        FileTool.SaveAsJson(_testData, testFile);
        bool loadResult = FileTool.LoadFromJson(testFile, out TestData loadedData);

        // Assert
        Assert.IsTrue(loadResult);
        Assert.IsNotNull(loadedData);
        Assert.AreEqual(_testData.Id, loadedData.Id);
        Assert.AreEqual(_testData.Name, loadedData.Name);
        Assert.AreEqual(_testData.IsActive, loadedData.IsActive);
        Assert.AreEqual(_testData.Score, loadedData.Score);
        Assert.IsTrue(File.Exists(testFile));
    }

    [Test]
    public void TestSaveAsBinaryAndLoadFromBinarySavesAndLoadsCorrectly()
    {
        // Arrange
        string testFile = Path.Combine(_testDirPath, "test.bin");

        // Act
        FileTool.SaveAsBinary(_testData, testFile);
        bool loadResult = FileTool.LoadFromBinary(testFile, out TestData loadedData);

        // Assert
        Assert.IsTrue(loadResult);
        Assert.IsNotNull(loadedData);
        Assert.AreEqual(_testData.Id, loadedData.Id);
        Assert.AreEqual(_testData.Name, loadedData.Name);
        Assert.AreEqual(_testData.IsActive, loadedData.IsActive);
        Assert.AreEqual(_testData.Score, loadedData.Score);
        Assert.IsTrue(File.Exists(testFile));
    }

    [Test]
    public void TestLoadFromNonExistentFileReturnsFalseAndOutputsWarning()
    {
        // Arrange
        string nonExistentFile = Path.Combine(_testDirPath, "nonexistent.json");

        // Act
        bool jsonResult = FileTool.LoadFromJson(nonExistentFile, out TestData jsonData);
        bool binaryResult = FileTool.LoadFromBinary(nonExistentFile, out TestData binaryData);

        // Assert
        Assert.IsFalse(jsonResult);
        Assert.AreEqual(default(TestData), jsonData);
        Assert.IsFalse(binaryResult);
        Assert.AreEqual(default(TestData), binaryData);
    }

    [Test]
    public void TestSaveAndLoadComplexObjectWithNestedTypesWorksCorrectly()
    {
        // Arrange
        var complexData = new ComplexTestData
        {
            Id = 100,
            Settings = new TestSettings
            {
                Theme = "Dark",
                Language = "zh-CN",
                MaxConnections = 50
            },
            Tags = new[] { "tag1", "tag2", "tag3" },
            Metadata = new TestMetadata
            {
                Version = "1.0.0",
                Author = "TestUser",
                CreatedDate = DateTime.Now.AddDays(-1)
            }
        };
        string jsonFile = Path.Combine(_testDirPath, "complex.json");
        string binaryFile = Path.Combine(_testDirPath, "complex.bin");

        // Act
        FileTool.SaveAsJson(complexData, jsonFile);
        bool jsonLoaded = FileTool.LoadFromJson(jsonFile, out ComplexTestData loadedFromJson);

        FileTool.SaveAsBinary(complexData, binaryFile);
        bool binaryLoaded = FileTool.LoadFromBinary(binaryFile, out ComplexTestData loadedFromBinary);

        // Assert
        Assert.IsTrue(jsonLoaded);
        Assert.IsNotNull(loadedFromJson);
        Assert.AreEqual(complexData.Id, loadedFromJson.Id);
        Assert.AreEqual(complexData.Settings.Theme, loadedFromJson.Settings.Theme);
        Assert.AreEqual(complexData.Settings.Language, loadedFromJson.Settings.Language);
        Assert.AreEqual(complexData.Settings.MaxConnections, loadedFromJson.Settings.MaxConnections);
        CollectionAssert.AreEqual(complexData.Tags, loadedFromJson.Tags);
        Assert.AreEqual(complexData.Metadata.Author, loadedFromJson.Metadata.Author);

        Assert.IsTrue(binaryLoaded);
        Assert.IsNotNull(loadedFromBinary);
        Assert.AreEqual(complexData.Id, loadedFromBinary.Id);
        Assert.AreEqual(complexData.Settings.Theme, loadedFromBinary.Settings.Theme);
    }

    [Test]
    public void TestFileOverwriteBehaviorReplacesExistingContentCorrectly()
    {
        // Arrange
        string testFile = Path.Combine(_testDirPath, "overwrite.json");
        var originalData = new TestData { Id = 1, Name = "Original", IsActive = false, Score = 0.0 };
        var newData = new TestData { Id = 999, Name = "NewData", IsActive = true, Score = 100.0 };

        // Act
        FileTool.SaveAsJson(originalData, testFile);
        FileTool.SaveAsJson(newData, testFile); // 覆盖写入
        bool loadResult = FileTool.LoadFromJson(testFile, out TestData loadedData);

        // Assert
        Assert.IsTrue(loadResult);
        Assert.AreEqual(newData.Id, loadedData.Id);
        Assert.AreEqual(newData.Name, loadedData.Name);
        Assert.AreEqual(newData.IsActive, loadedData.IsActive);
        Assert.AreEqual(newData.Score, loadedData.Score);
        Assert.AreNotEqual(originalData.Id, loadedData.Id);
    }
}

/// <summary>
/// 测试数据类
/// </summary>
public class TestData
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public double Score { get; set; }
    public DateTime CreatedDate { get; set; }
}

/// <summary>
/// 复杂测试数据类，包含嵌套对象
/// </summary>
public class ComplexTestData
{
    public int Id { get; set; }
    public TestSettings Settings { get; set; } = new();
    public string[] Tags { get; set; } = [];
    public TestMetadata Metadata { get; set; } = new();
}

/// <summary>
/// 测试设置类
/// </summary>
public class TestSettings
{
    public string Theme { get; set; } = "Light";
    public string Language { get; set; } = "en-US";
    public int MaxConnections { get; set; } = 10;
}

/// <summary>
/// 测试元数据类
/// </summary>
public class TestMetadata
{
    public string Version { get; set; } = "1.0.0";
    public string Author { get; init; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}