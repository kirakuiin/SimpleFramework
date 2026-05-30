using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using NUnit.Framework;
using SimpleFramework.Net;

namespace Test.Net;

[TestFixture]
public class NetCoreTests
{
    [Test]
    public void GameNetOptions_WithEmptyApplicationId_IsRejected()
    {
        var options = new GameNetOptions
        {
            Application = new NetApplicationInfo { ApplicationId = Guid.Empty }
        };

        var ex = Assert.Throws<ArgumentException>(() => GameNet.ValidateOptions(options));
        Assert.That(ex!.Message, Does.Contain("ApplicationId"));
    }

    [Test]
    public void GameNetOptions_WithInvalidProtocolVersion_IsRejected()
    {
        var options = new GameNetOptions
        {
            Application = new NetApplicationInfo
            {
                ApplicationId = Guid.NewGuid(),
                ProtocolVersion = 0
            }
        };

        var ex = Assert.Throws<ArgumentException>(() => GameNet.ValidateOptions(options));
        Assert.That(ex!.Message, Does.Contain("ProtocolVersion"));
    }

    [Test]
    public void DiagnosticsSnapshot_IsReadOnlyCopy()
    {
        var diagnostics = new NetDiagnostics();
        diagnostics.AddPacketSent(10);
        diagnostics.AddPacketReceived(5);
        diagnostics.AddError();

        var snapshot = diagnostics.GetSnapshot();

        Assert.That(snapshot.PacketsSent, Is.EqualTo(1));
        Assert.That(snapshot.BytesSent, Is.EqualTo(10));
        Assert.That(snapshot.PacketsReceived, Is.EqualTo(1));
        Assert.That(snapshot.BytesReceived, Is.EqualTo(5));
        Assert.That(snapshot.ErrorCount, Is.EqualTo(1));
    }

    [Test]
    public void NetProject_TargetFramework_RemainsNet8()
    {
        var projectPath = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "Net", "Net.csproj"));
        var document = XDocument.Load(projectPath);

        Assert.That(document.Root!.Element("PropertyGroup")!.Element("TargetFramework")!.Value, Is.EqualTo("net8.0"));
    }

    [Test]
    public async Task GameNet_Dispose_IsIdempotent_AndApisFailAfterDispose()
    {
        var network = new MemoryNetNetwork();
        var net = new GameNet(network.CreateTransport("server"), new GameNetOptions
        {
            Application = new NetApplicationInfo { ApplicationId = Guid.NewGuid() }
        });

        await net.DisposeAsync();
        await net.DisposeAsync();

        var result = await net.HostAsync(new HostOptions { Port = 7777 });
        Assert.That(result.Status, Is.EqualTo(NetSessionStatus.ObjectDisposed));
    }

    [Test]
    public async Task GameNet_ConcurrentHostCalls_OnlyOneStarts()
    {
        var network = new MemoryNetNetwork();
        await using var net = new GameNet(network.CreateTransport("server"), new GameNetOptions
        {
            Application = new NetApplicationInfo { ApplicationId = Guid.NewGuid() }
        });

        var first = net.HostAsync(new HostOptions { Port = 7777 });
        var second = net.HostAsync(new HostOptions { Port = 7778 });

        var results = await Task.WhenAll(first, second);
        Assert.That(results.Count(r => r.Status == NetSessionStatus.Ok), Is.EqualTo(1));
        Assert.That(results.Count(r => r.Status == NetSessionStatus.InvalidState), Is.EqualTo(1));
    }
}
