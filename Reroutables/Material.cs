namespace ABCDEFG.Reroutables;

internal class Material : ReroutableAsset
{
    public override bool IsSameAsset(ReroutableAsset other)
    {
        if (!other.Name.Equals(Name, StringComparison.Ordinal))
            return false;

        if (other is not Material)
            return false;

        return true;
    }

    public override string ToString()
    {
        return $"{Name}";
    }
}
