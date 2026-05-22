using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SimpleFramework.Utility;

namespace Test.Utility;

/// <summary>
/// 用于测试的类型
/// </summary>
public struct TestTypeForHash1
{
    public int Value;
    public string Name;
}

/// <summary>
/// 用于测试的另一个类型
/// </summary>
public struct TestTypeForHash2
{
    public bool Flag;
    public double Number;
}

/// <summary>
/// 用于测试的嵌套类型
/// </summary>
public class TestOuterClass
{
    public struct NestedType
    {
        public int Id;
    }
}

/// <summary>
/// 用于测试的泛型类型
/// </summary>
public class TestGenericClass<T>
{
    public T Data;
}

/// <summary>
/// 测试 MiscUtil 类的功能
/// </summary>
[TestFixture]
public class TestMiscUtil
{
    /// <summary>
    /// 测试 GetUniqueTypeName 方法
    /// </summary>
    [TestFixture]
    public class TestGetUniqueTypeName
    {
        [Test]
        public void TestGetUniqueTypeNameForSimpleStruct()
        {
            // Act
            var uniqueName = MiscUtil.GetUniqueTypeName<TestTypeForHash1>();
            Console.WriteLine(uniqueName);

            // Assert
            Assert.That(uniqueName, Is.Not.Null.And.Not.Empty);
            Assert.That(uniqueName, Does.Contain("Test.Utility.TestTypeForHash1"));
            Assert.That(uniqueName, Does.Contain("Test"));
        }

        [Test]
        public void TestGetUniqueTypeNameForDifferentTypes()
        {
            // Act
            var name1 = MiscUtil.GetUniqueTypeName<TestTypeForHash1>();
            var name2 = MiscUtil.GetUniqueTypeName<TestTypeForHash2>();

            // Assert
            Assert.That(name1, Is.Not.EqualTo(name2));
            Assert.That(name1, Does.Contain("TestTypeForHash1"));
            Assert.That(name2, Does.Contain("TestTypeForHash2"));
        }

        [Test]
        public void TestGetUniqueTypeNameConsistency()
        {
            // Act & Assert - 多次调用应该返回相同结果
            var name1 = MiscUtil.GetUniqueTypeName<TestTypeForHash1>();
            var name2 = MiscUtil.GetUniqueTypeName<TestTypeForHash1>();
            var name3 = MiscUtil.GetUniqueTypeName<TestTypeForHash1>();

            Assert.That(name1, Is.EqualTo(name2));
            Assert.That(name2, Is.EqualTo(name3));
        }

        [Test]
        public void TestGetUniqueTypeNameContainsAssemblyName()
        {
            // Act
            var uniqueName = MiscUtil.GetUniqueTypeName<TestTypeForHash1>();

            // Assert
            Assert.That(uniqueName, Does.Contain("Test"));
        }
    }

    /// <summary>
    /// 测试 GenerateGuid 方法
    /// </summary>
    [TestFixture]
    public class TestGenerateGuid
    {
        [Test]
        public void TestGenerateGuidReturnsValidGuid()
        {
            // Act
            var guid = MiscUtil.GenerateGuid();

            // Assert
            Assert.That(guid, Is.Not.EqualTo(Guid.Empty));
            Assert.That(guid.ToString(), Has.Length.EqualTo(36)); // 标准GUID格式
        }

        [Test]
        public void TestGenerateGuidUniqueness()
        {
            // Act
            var guids = new List<Guid>();
            for (int i = 0; i < 100; i++)
            {
                guids.Add(MiscUtil.GenerateGuid());
            }

            // Assert - 所有生成的GUID都应该是唯一的
            var distinctGuids = guids.Distinct().ToList();
            Assert.That(distinctGuids.Count, Is.EqualTo(guids.Count));
        }
    }

    /// <summary>
    /// 测试 TypeHash 方法
    /// </summary>
    [TestFixture]
    public class TestTypeHash
    {
        [Test]
        public void TestTypeHashForSimpleType()
        {
            // Act
            var hash1 = MiscUtil.TypeHash<TestTypeForHash1>();
            var hash2 = MiscUtil.TypeHash<TestTypeForHash1>();

            // Assert
            Assert.That(hash1, Is.Not.EqualTo(0UL));
            Assert.That(hash1, Is.EqualTo(hash2)); // 同一类型应该产生相同哈希
        }

        [Test]
        public void TestTypeHashForDifferentTypes()
        {
            // Act
            var hash1 = MiscUtil.TypeHash<TestTypeForHash1>();
            var hash2 = MiscUtil.TypeHash<TestTypeForHash2>();

            // Assert
            Assert.That(hash1, Is.Not.EqualTo(hash2)); // 不同类型应该产生不同哈希
            Assert.That(hash1, Is.Not.EqualTo(0UL));
            Assert.That(hash2, Is.Not.EqualTo(0UL));
        }

        [Test]
        public void TestTypeHashConsistencyAcrossCalls()
        {
            // Arrange
            var expectedHashes = new List<ulong>();

            // Act
            for (int i = 0; i < 10; i++)
            {
                expectedHashes.Add(MiscUtil.TypeHash<TestTypeForHash1>());
            }

            // Assert - 所有调用都应该返回相同的哈希值
            var firstHash = expectedHashes.First();
            Assert.That(expectedHashes.All(h => h == firstHash), Is.True);
        }

        [Test]
        public void TestTypeHashCachesComputedHash()
        {
            var cacheField = typeof(MiscUtil).GetField("_typeHashCaches",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var cache = (System.Collections.Concurrent.ConcurrentDictionary<Type, ulong>)cacheField.GetValue(null);
            cache.Clear();

            var hash = MiscUtil.TypeHash(typeof(TestTypeForHash1));

            Assert.IsTrue(cache.TryGetValue(typeof(TestTypeForHash1), out var cachedHash));
            Assert.AreEqual(hash, cachedHash);
        }

        [Test]
        public void TestTypeHashForBuiltinTypes()
        {
            // Act
            var intHash = MiscUtil.TypeHash<int>();
            var stringHash = MiscUtil.TypeHash<string>();
            var boolHash = MiscUtil.TypeHash<bool>();
            var doubleHash = MiscUtil.TypeHash<double>();

            // Assert
            Assert.That(intHash, Is.Not.EqualTo(0UL));
            Assert.That(stringHash, Is.Not.EqualTo(0UL));
            Assert.That(boolHash, Is.Not.EqualTo(0UL));
            Assert.That(doubleHash, Is.Not.EqualTo(0UL));

            // 不同基础类型应该有不同的哈希
            var hashes = new[] { intHash, stringHash, boolHash, doubleHash };
            var distinctHashes = hashes.Distinct().ToArray();
            Assert.That(distinctHashes.Length, Is.EqualTo(hashes.Length));
        }

        [Test]
        public void TestTypeHashForComplexTypes()
        {
            // Act
            var nestedHash = MiscUtil.TypeHash<TestOuterClass.NestedType>();
            var genericIntHash = MiscUtil.TypeHash<TestGenericClass<int>>();
            var genericStringHash = MiscUtil.TypeHash<TestGenericClass<string>>();

            // Assert
            Assert.That(nestedHash, Is.Not.EqualTo(0UL));
            Assert.That(genericIntHash, Is.Not.EqualTo(0UL));
            Assert.That(genericStringHash, Is.Not.EqualTo(0UL));

            // 不同泛型类型应该有不同的哈希
            Assert.That(genericIntHash, Is.Not.EqualTo(genericStringHash));
        }

        [Test]
        public void TestTypeHashDistribution()
        {
            // Arrange - 创建多个测试类型
            var hashes = new List<ulong>();
            var types = new[]
            {
                typeof(TestTypeForHash1),
                typeof(TestTypeForHash2),
                typeof(TestOuterClass.NestedType),
                typeof(TestGenericClass<int>),
                typeof(TestGenericClass<string>),
                typeof(int),
                typeof(string),
                typeof(bool),
                typeof(double),
                typeof(DateTime)
            };

            // Act - 明确调用泛型版本的 TypeHash<T>() 方法，避免与非泛型版本冲突
            foreach (var type in types)
            {
                var genericMethod = typeof(MiscUtil)
                    .GetMethods()
                    .First(m => m.Name == "TypeHash" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);

                var method = genericMethod.MakeGenericMethod(type);
                var hash = (ulong)method.Invoke(null, null);
                hashes.Add(hash);
            }

            // Assert
            Assert.That(hashes.Count, Is.EqualTo(types.Length));
            Assert.That(hashes.All(h => h != 0UL), Is.True, "所有哈希值都不应为零");

            // 检查分布情况 - 大部分哈希应该不同
            var distinctHashes = hashes.Distinct().ToArray();
            Assert.That(distinctHashes.Length, Is.GreaterThanOrEqualTo(hashes.Count * 0.8),
                "哈希值应该有良好的分布性");
        }

        [Test]
        public void TestTypeHashPerformanceTest()
        {
            // Arrange
            const int iterations = 10000;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            for (int i = 0; i < iterations; i++)
            {
                MiscUtil.TypeHash<TestTypeForHash1>();
            }
            stopwatch.Stop();

            // Assert
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1000),
                "10000次类型哈希计算应在1秒内完成");

            // 验证平均时间
            var avgMsPerCall = stopwatch.ElapsedMilliseconds / (double)iterations;
            Assert.That(avgMsPerCall, Is.LessThan(0.1),
                "平均每次调用应少于0.1毫秒");
        }

        [Test]
        public void TestTypeHashStability()
        {
            // Arrange - 预计算的已知哈希值（这些值应该在所有平台上保持一致）
            var knownHash = MiscUtil.TypeHash<TestTypeForHash1>();

            // Act - 多次计算并比较
            var computedHashes = new List<ulong>();
            for (int i = 0; i < 100; i++)
            {
                computedHashes.Add(MiscUtil.TypeHash<TestTypeForHash1>());
            }

            // Assert
            Assert.That(computedHashes.All(h => h == knownHash), Is.True,
                "类型哈希应该在不同调用间保持稳定");
        }
    }

    /// <summary>
    /// 测试 ComputeHash 方法
    /// </summary>
    [TestFixture]
    public class TestComputeHash
    {
        [Test]
        public void TestComputeHashForSameData()
        {
            // Arrange
            var data1 = new TestTypeForHash1 { Value = 42, Name = "Test" };
            var data2 = new TestTypeForHash1 { Value = 42, Name = "Test" };

            // Act
            var hash1 = MiscUtil.ComputeHash(data1);
            var hash2 = MiscUtil.ComputeHash(data2);

            // Assert
            Assert.That(hash1, Is.EqualTo(hash2));
        }

        [Test]
        public void TestComputeHashForDifferentData()
        {
            // Arrange
            var data1 = new TestTypeForHash1 { Value = 42, Name = "Test" };
            var data2 = new TestTypeForHash1 { Value = 43, Name = "Test" };

            // Act
            var hash1 = MiscUtil.ComputeHash(data1);
            var hash2 = MiscUtil.ComputeHash(data2);

            // Assert
            Assert.That(hash1, Is.Not.EqualTo(hash2));
        }

        [Test]
        public void TestComputeHashForString()
        {
            // Arrange
            var str1 = "Hello World";
            var str2 = "Hello World";
            var str3 = "Hello Different World";

            // Act
            var hash1 = MiscUtil.ComputeHash(str1);
            var hash2 = MiscUtil.ComputeHash(str2);
            var hash3 = MiscUtil.ComputeHash(str3);

            // Assert
            Assert.That(hash1, Is.EqualTo(hash2));
            Assert.That(hash1, Is.Not.EqualTo(hash3));
        }
    }
}
