using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Service.SelfTestServ;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Engine.Tests.Service.SelfTestServ;

public sealed class SelfTestServiceTests
{
    [Fact]
    public void Run_OnThisPlatform_PassesEveryCheck()
    {
        using var provider = new ServiceCollection().AddLogging().AddCubiomesEngine().BuildServiceProvider();

        var report = provider.GetRequiredService<ISelfTestService>().Run();

        Assert.True(report.Passed, string.Join(Environment.NewLine, report.Checks.Where(check => !check.Passed)));
        Assert.NotEmpty(report.Checks);
    }
}
