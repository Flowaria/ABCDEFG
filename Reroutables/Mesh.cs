using AssetsTools.NET;

namespace ABCDEFG.Reroutables;

internal class Mesh : ReroutableAsset
{
    public int IndicesCount { get; set; }

    public override bool IsSameAsset(ReroutableAsset other)
    {
        if (!other.Name.Equals(Name, StringComparison.Ordinal))
            return false;

        if (other is not Mesh otherMesh)
            return false;

        if (otherMesh.IndicesCount != IndicesCount)
            return false;

        return true;
    }

    public override void ReadExtraFromBaseField(AssetTypeValueField baseField)
    {
        var indices = baseField["m_IndexBuffer"]["Array"].AsByteArray;
        IndicesCount = indices.Length;
    }

    public override void ReadExtra(BinaryReader reader)
    {
        IndicesCount = reader.Read7BitEncodedInt();
    }

    public override void WriteExtra(BinaryWriter writer)
    {
        writer.Write7BitEncodedInt(IndicesCount);
    }

    public override string ToString()
    {
        return $"{Name} ({IndicesCount} indices)";
    }
}
