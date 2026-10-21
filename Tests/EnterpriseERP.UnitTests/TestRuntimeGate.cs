using TrustedInfrastructure;
namespace EnterpriseERP.UnitTests;
internal sealed class TestRuntimeGate : ITrustedRuntimeGate
{
    public static TestRuntimeGate Available => new();
    public bool IsAvailable { get; set; } = true;
    public void ThrowIfUnavailable() { if (!IsAvailable) throw new TrustedVaultUnavailableException(); }
}
