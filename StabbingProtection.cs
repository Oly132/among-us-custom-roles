namespace Forger;
internal static class StabbingProtection
{
    internal static bool TryConsume(ref bool spent,bool eligible)
    {
        if(spent || !eligible)return false;
        spent=true;return true;
    }
}
