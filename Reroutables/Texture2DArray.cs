using AssetsTools.NET;

namespace ABCDEFG.Reroutables;

internal class Texture2DArray : ReroutableAsset
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int Depth { get; set; }

    public override bool IsSameAsset(ReroutableAsset other)
    {
        if (!other.Name.Equals(Name, StringComparison.Ordinal))
            return false;

        if (other is not Texture2DArray otherTex)
            return false;

        if (otherTex.Width != Width)
            return false;

        if (otherTex.Height != Height)
            return false;

        if (otherTex.Depth != Depth)
            return false;

        return true;
    }

    public override void ReadExtraFromBaseField(AssetTypeValueField baseField)
    {
        Width = baseField["m_Width"].AsInt;
        Height = baseField["m_Height"].AsInt;
        Depth = baseField["m_Depth"].AsInt;
    }

    public override void ReadExtra(BinaryReader reader)
    {
        Width = reader.Read7BitEncodedInt();
        Height = reader.Read7BitEncodedInt();
        Depth = reader.Read7BitEncodedInt();
    }

    public override void WriteExtra(BinaryWriter writer)
    {
        writer.Write7BitEncodedInt(Width);
        writer.Write7BitEncodedInt(Height);
        writer.Write7BitEncodedInt(Depth);
    }

    public override string ToString()
    {
        return $"{Name} ({Width} x {Height} x {Depth})";
    }
}
