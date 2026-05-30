using System;
using System.IO;
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
}
