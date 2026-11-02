using AssetsTools.NET;

namespace ABCDEFG.Reroutables;

internal class Texture2D : ReroutableAsset
{
    public int Width { get; set; }
    public int Height { get; set; }

    public override bool IsSameAsset(ReroutableAsset other)
    {
        if (!other.Name.Equals(Name, StringComparison.Ordinal))
            return false;

        if (other is not Texture2D otherTex)
            return false;

        if (otherTex.Width != Width)
            return false;

        if (otherTex.Height != Height)
            return false;

        return true;
    }

    public override void ReadExtraFromBaseField(AssetTypeValueField baseField)
    {
        Width = baseField["m_Width"].AsInt;
        Height = baseField["m_Height"].AsInt;
    }

    public override void ReadExtra(BinaryReader reader)
    {
        Width = reader.Read7BitEncodedInt();
        Height = reader.Read7BitEncodedInt();
    }

    public override void WriteExtra(BinaryWriter writer)
    {
        writer.Write7BitEncodedInt(Width);
        writer.Write7BitEncodedInt(Height);
    }

    public override string ToString()
    {
        return $"{Name} ({Width} x {Height})";
    }
}
