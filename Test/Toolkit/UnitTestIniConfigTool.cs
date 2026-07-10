using System;
using System.IO;
using System.Globalization;
using NUnit.Framework;
using SimpleFramework.Toolkit;

namespace Test.Toolkit;

/// <summary>
/// IniConfigTool 工具类的单元测试
/// </summary>
[TestFixture]
public class TestIniConfigTool
{
    private string _testDirPath = default!;
    private IniConfigTool _configTool = default!;
    private IniConfigTool _autoFlushConfigTool = default!;

    [SetUp]
    public void Setup()
    {
        _testDirPath = Path.Combine(Path.GetTempPath(), "TestIniConfigTool", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirPath);
        _configTool = new IniConfigTool(false);
        _autoFlushConfigTool = new IniConfigTool(true);
    }

    [TearDown]
    public void TearDown()
    {
        _configTool?.Dispose();
        _autoFlushConfigTool?.Dispose();

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
    public void TestBasicConfigReadWriteLoadAndSaveCorrectly()
    {
        // Arrange
        string testFile = Path.Combine(_testDirPath, "basic.ini");
        File.WriteAllText(testFile, """
            [Database]
            Host=localhost
            Port=3306
            Timeout=30
            EnableSSL=true
            [App]
            Name=MyApp
            Version=1.2.3
            """, System.Text.Encoding.UTF8);

        // Act
        _configTool.LoadConfig(testFile);

        // Assert
        Assert.AreEqual("localhost", _configTool.Get<string>("Database", "Host"));
        Assert.AreEqual(3306, _configTool.Get<int>("Database", "Port"));
        Assert.AreEqual(30, _configTool.Get<int>("Database", "Timeout"));
        Assert.AreEqual(true, _configTool.Get<bool>("Database", "EnableSSL"));
        Assert.AreEqual("MyApp", _configTool.Get<string>("App", "Name"));
        Assert.AreEqual("1.2.3", _configTool.Get<string>("App", "Version"));
    }

    [Test]
    public void TestSetAndGetVariousTypesHandlesAllBasicTypes()
    {
        // Arrange
        string testFile = Path.Combine(_testDirPath, "types.ini");
        _configTool.LoadConfig(testFile);

        // Act
        _configTool.Set("Types", "StringValue", "Hello World");
        _configTool.Set("Types", "IntValue", 42);
        _configTool.Set("Types", "DoubleValue", 3.14159);
        _configTool.Set("Types", "BoolValue", true);

        // Assert
        Assert.AreEqual("Hello World", _configTool.Get<string>("Types", "StringValue"));
        Assert.AreEqual(42, _configTool.Get<int>("Types", "IntValue"));
        Assert.AreEqual(3.14159, _configTool.Get<double>("Types", "DoubleValue"));
        Assert.AreEqual(true, _configTool.Get<bool>("Types", "BoolValue"));
        Assert.AreEqual(null, _configTool.Get<string>("Types", "NullValue"));
    }

    [Test]
    public void TestGetDefaultValueWhenKeyNotExistsReturnsDefault()
    {
        // Arrange
        string testFile = Path.Combine(_testDirPath, "default.ini");
        File.WriteAllText(testFile, "[Section]\nExistingKey=ExistingValue", System.Text.Encoding.UTF8);
        _configTool.LoadConfig(testFile);

        // Act & Assert
        Assert.AreEqual("DefaultValue", _configTool.Get<string>("Section", "NonExistent", "DefaultValue"));
        Assert.AreEqual(999, _configTool.Get<int>("Section", "NonExistent", 999));
        Assert.AreEqual(true, _configTool.Get<bool>("Section", "NonExistent", true));
        Assert.AreEqual(3.14, _configTool.Get<double>("Section", "NonExistent", 3.14));
        Assert.AreEqual("ExistingValue", _configTool.Get<string>("Section", "ExistingKey", "DefaultValue"));
    }

    [Test]
    public void TestSaveAndReloadConfigurationPersistsCorrectly()
    {
        // Arrange
        string testFile = Path.Combine(_testDirPath, "persist.ini");
        _configTool.LoadConfig(testFile);
        _configTool.Set("Database", "Host", "localhost");
        _configTool.Set("Database", "Port", 5432);
        _configTool.Set("App", "Debug", true);

        // Act
        _configTool.SaveConfig();

        // 创建新的配置工具实例来验证持久化
        using var newConfigTool = new IniConfigTool();
        newConfigTool.LoadConfig(testFile);

        // Assert
        Assert.AreEqual("localhost", newConfigTool.Get<string>("Database", "Host"));
        Assert.AreEqual(5432, newConfigTool.Get<int>("Database", "Port"));
        Assert.AreEqual(true, newConfigTool.Get<bool>("App", "Debug"));
    }

    [Test]
    public void TestAutoFlushEnabledSavesImmediately()
    {
        // Arrange
        string testFile = Path.Combine(_testDirPath, "autoflush.ini");
        _autoFlushConfigTool.LoadConfig(testFile);

        // Act
        _autoFlushConfigTool.Set("Auto", "Key", "Value");

        // Assert
        Assert.IsTrue(File.Exists(testFile));
        string content = File.ReadAllText(testFile, System.Text.Encoding.UTF8);
        Assert.IsTrue(content.Contains("Key=Value"));
    }

    [Test]
    public void TestFileWithCommentsIgnoresCommentsCorrectly()
    {
        // Arrange
        string testFile = Path.Combine(_testDirPath, "comments.ini");
        File.WriteAllText(testFile, """
            ; 这是一个注释
            # 这是另一个注释

            [Section1]
            ; 注释在section内
            Key1=Value1
            # 另一个注释
            Key2=Value2

            ; Section前的注释
            [Section2]
            Key3=Value3
            """, System.Text.Encoding.UTF8);

        // Act
        _configTool.LoadConfig(testFile);

        // Assert
        Assert.AreEqual("Value1", _configTool.Get<string>("Section1", "Key1"));
        Assert.AreEqual("Value2", _configTool.Get<string>("Section1", "Key2"));
        Assert.AreEqual("Value3", _configTool.Get<string>("Section2", "Key3"));
    }

    [Test]
    public void TestInvalidInputsDoesNotCrash()
    {
        // Arrange
        string testFile = Path.Combine(_testDirPath, "invalid.ini");
        _configTool.LoadConfig(testFile);

        // Act & Assert - 这些操作都不应该抛出异常
        Assert.DoesNotThrow(() => _configTool.LoadConfig(null!));
        Assert.DoesNotThrow(() => _configTool.LoadConfig(""));
        Assert.DoesNotThrow(() => _configTool.LoadConfig("   "));
        Assert.DoesNotThrow(() => _configTool.Set("", "Key", "Value"));
        Assert.DoesNotThrow(() => _configTool.Set("Section", "", "Value"));
        Assert.DoesNotThrow(() => _configTool.Set("   ", "Key", "Value"));
        Assert.DoesNotThrow(() => _configTool.Set("Section", "   ", "Value"));
        Assert.AreEqual("default", _configTool.Get<string>("", "Key", "default"));
        Assert.AreEqual("default", _configTool.Get<string>("Section", "", "default"));
    }

    [Test]
    public void TestTypeConversionInvalidValuesReturnsDefaults()
    {
        // Arrange
        string testFile = Path.Combine(_testDirPath, "conversion.ini");
        File.WriteAllText(testFile, "[Section]\nInvalidInt=abc\nInvalidBool=xyz\nInvalidDate=not-a-date", System.Text.Encoding.UTF8);
        _configTool.LoadConfig(testFile);

        // Act & Assert
        Assert.AreEqual(999, _configTool.Get<int>("Section", "InvalidInt", 999));
        Assert.AreEqual(0, _configTool.Get<int>("Section", "InvalidInt"));
        Assert.AreEqual(false, _configTool.Get<bool>("Section", "InvalidBool", false));
        Assert.AreEqual(false, _configTool.Get<bool>("Section", "InvalidBool"));
        Assert.AreEqual(default(DateTime), _configTool.Get<DateTime>("Section", "InvalidDate"));
        Assert.AreEqual(new DateTime(2000, 1, 1), _configTool.Get<DateTime>("Section", "InvalidDate", new DateTime(2000, 1, 1)));
    }

    [Test]
    public void TestMultipleSectionsHandlesCorrectly()
    {
        // Arrange
        string testFile = Path.Combine(_testDirPath, "multi.ini");
        File.WriteAllText(testFile, """
            [Database]
            Host=localhost
            Port=3306

            [App]
            Name=MyApp
            Version=1.0

            [Logging]
            Level=Debug
            File=app.log
            """, System.Text.Encoding.UTF8);

        // Act
        _configTool.LoadConfig(testFile);

        // Assert
        Assert.AreEqual("localhost", _configTool.Get<string>("Database", "Host"));
        Assert.AreEqual(3306, _configTool.Get<int>("Database", "Port"));
        Assert.AreEqual("MyApp", _configTool.Get<string>("App", "Name"));
        Assert.AreEqual("1.0", _configTool.Get<string>("App", "Version"));
        Assert.AreEqual("Debug", _configTool.Get<string>("Logging", "Level"));
        Assert.AreEqual("app.log", _configTool.Get<string>("Logging", "File"));
    }

    [Test]
    public void TestDisposeAutoSavesUnsavedData()
    {
        // Arrange
        string testFile = Path.Combine(_testDirPath, "dispose.ini");
        _configTool.LoadConfig(testFile);
        _configTool.Set("Section", "Key", "Value");

        // Act
        _configTool.Dispose();

        // Assert
        Assert.IsTrue(File.Exists(testFile));
        string content = File.ReadAllText(testFile, System.Text.Encoding.UTF8);
        Assert.IsTrue(content.Contains("Key=Value"));
    }

    [Test]
    public void TestDisposedToolDoesNotLoadOrSave()
    {
        string loadFile = Path.Combine(_testDirPath, "disposed-load.ini");
        string saveFile = Path.Combine(_testDirPath, "disposed-save.ini");
        File.WriteAllText(loadFile, "[Section]\nKey=Loaded", System.Text.Encoding.UTF8);
        _configTool.Set("Section", "Key", "Original");
        _configTool.Dispose();

        _configTool.LoadConfig(loadFile);
        _configTool.SaveConfig(saveFile);

        Assert.AreEqual(null, _configTool.Get<string>("Section", "Key"));
        Assert.IsFalse(File.Exists(saveFile));
    }
    
    [Test]
    public void TestReload()
    {
        // Arrange
        string testFile = Path.Combine(_testDirPath, "dispose.ini");
        _configTool.LoadConfig(testFile);
        _configTool.Set("Section", "Key", "Value");

        // Act
        _configTool.Reload();

        // Assert
        Assert.AreEqual(null, _configTool.Get<string>("Section", "Key"));
        
        _configTool.Set("Section", "Key", "Value");
        _configTool.Flush();
        _configTool.Reload();
        
        Assert.AreEqual("Value", _configTool.Get<string>("Section", "Key"));
    }

    [Test]
    public void TestTypedValuesRoundTripAcrossCultures()
    {
        var testFile = Path.Combine(_testDirPath, "culture.ini");
        var value = 1234.5;
        var date = new DateTime(2026, 7, 10, 13, 14, 15, 123, DateTimeKind.Utc).AddTicks(4567);
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            _configTool.LoadConfig(testFile);
            _configTool.Set("Values", "Number", value);
            _configTool.Set("Values", "Date", date);
            _configTool.Set("Values", "Raw", "1,5");
            _configTool.SaveConfig();

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            using var reloaded = new IniConfigTool();
            reloaded.LoadConfig(testFile);

            Assert.Multiple(() =>
            {
                Assert.AreEqual(value, reloaded.Get<double>("Values", "Number"));
                Assert.AreEqual(date, reloaded.Get<DateTime>("Values", "Date"));
                Assert.AreEqual("1,5", reloaded.Get<string>("Values", "Raw"));
                Assert.That(File.ReadAllText(testFile), Does.Contain("Number=1234.5"));
            });
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
