using AssetsTools.NET;

namespace ABCDEFG.Reroutables;

internal class Shader : ReroutableAsset
{
    public override bool IsSameAsset(ReroutableAsset other)
    {
        if (!other.Name.Equals(Name, StringComparison.Ordinal))
            return false;

        if (other is not Shader)
            return false;

        return true;
    }

    public override void ReadNameFromBaseField(AssetTypeValueField baseField)
    {
        Name = baseField["m_ParsedForm"]["m_Name"].AsString;
    }

    public override string ToString()
    {
        return Name;
    }
}
