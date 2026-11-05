using AssetsTools.NET;
using System.IO.Hashing;

namespace ABCDEFG.Reroutables;

internal class Mesh : ReroutableAsset
{
    public int IndicesCount { get; set; }
    public uint IndicesHash { get; set; }

    public override bool IsSameAsset(ReroutableAsset other)
    {
        if (!NameEqualsLoosen(other.Name, Name))
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
        IndicesHash = XxHash32.HashToUInt32(indices);
    }

    public override void ReadExtra(BinaryReader reader)
    {
        IndicesCount = reader.Read7BitEncodedInt();
        IndicesHash = reader.ReadUInt32();
    }

    public override void WriteExtra(BinaryWriter writer)
    {
        writer.Write7BitEncodedInt(IndicesCount);
        writer.Write(IndicesHash);
    }

    public override string ToString()
    {
        return $"{Name} ({IndicesCount} indices - hash: {IndicesHash})";
    }
}
