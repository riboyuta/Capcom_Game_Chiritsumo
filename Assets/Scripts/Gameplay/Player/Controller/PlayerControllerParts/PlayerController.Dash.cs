public sealed partial class PlayerController
{
    public enum DashRefillReason
    {
        Grounded,
        Gimmick,
        Scripted
    }

    public bool CanUseDashNow => locomotionSystem != null && locomotionSystem.CanUseDashNowInternal();

    public bool TryRefillDash(DashRefillReason reason)
    {
        return locomotionSystem != null && locomotionSystem.TryRefillDash(reason);
    }
}