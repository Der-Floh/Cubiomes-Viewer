using Cubiomes_Viewer.Engine.Data.SelfTests;

namespace Cubiomes_Viewer.Engine.Service.SelfTestServ;

/// <summary>
/// Verifies on the running platform that the native generator produces the reference results.
/// </summary>
public interface ISelfTestService
{
    SelfTestReport Run();
}
