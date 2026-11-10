using AssetsTools.NET;
using System.IO.Hashing;

namespace ABCDEFG.Reroutables;

internal class Mesh : ReroutableAsset
{
    public byte IndexFormat { get; set; }
    public int IndexBufferSize { get; set; }
    public int VertexCount { get; set; }
    public ulong DataHash { get; set; }

    public override bool IsSameAsset(ReroutableAsset other)
    {
        if (!NameEqualsLoosen(other.Name, Name))
            return false;

        if (other is not Mesh otherMesh)
            return false;

        if (otherMesh.IndexFormat != IndexFormat)
            return false;

        if (otherMesh.IndexBufferSize != IndexBufferSize)
            return false;

        if (otherMesh.VertexCount != VertexCount)
            return false;

        if (otherMesh.DataHash != DataHash)
            return false;

        return true;
    }

    public override void ReadExtraFromBaseField(AssetTypeValueField baseField)
    {
        var indexBuffer = baseField["m_IndexBuffer"];
        var vertexData = baseField["m_VertexData"];

        IndexFormat = baseField["m_IndexFormat"].AsByte;
        IndexBufferSize = indexBuffer["Array"].AsByteArray.Length;
        VertexCount = vertexData["m_VertexCount"].AsInt;

        var hash = new XxHash64();
        hash.Append(indexBuffer["Array"].AsByteArray);
        hash.Append(vertexData["m_DataSize"].AsByteArray);

        Span<byte> buffer = stackalloc byte[24];
        foreach (var channel in vertexData["m_Channels"]["Array"])
        {
            BitConverter.TryWriteBytes(buffer.Slice(0, 4), channel["stream"].AsInt);
            BitConverter.TryWriteBytes(buffer.Slice(4, 4), channel["offset"].AsInt);
            BitConverter.TryWriteBytes(buffer.Slice(8, 4), channel["format"].AsInt);
            BitConverter.TryWriteBytes(buffer.Slice(12, 4), channel["dimension"].AsInt);
            hash.Append(buffer.Slice(0, 16));
        }

        foreach (var subMesh in baseField["m_SubMeshes"]["Array"])
        {
            BitConverter.TryWriteBytes(buffer.Slice(0, 4), subMesh["firstByte"].AsUInt);
            BitConverter.TryWriteBytes(buffer.Slice(4, 4), subMesh["indexCount"].AsUInt);
            BitConverter.TryWriteBytes(buffer.Slice(8, 4), subMesh["topology"].AsInt);
            BitConverter.TryWriteBytes(buffer.Slice(12, 4), subMesh["baseVertex"].AsUInt);
            BitConverter.TryWriteBytes(buffer.Slice(16, 4), subMesh["firstVertex"].AsUInt);
            BitConverter.TryWriteBytes(buffer.Slice(20, 4), subMesh["vertexCount"].AsUInt);
            hash.Append(buffer);
        }

        DataHash = hash.GetCurrentHashAsUInt64();
    }

    public override void ReadExtra(BinaryReader reader)
    {
        IndexFormat = reader.ReadByte();
        IndexBufferSize = reader.Read7BitEncodedInt();
        VertexCount = reader.Read7BitEncodedInt();
        DataHash = reader.ReadUInt64();
    }

    public override void WriteExtra(BinaryWriter writer)
    {
        writer.Write(IndexFormat);
        writer.Write7BitEncodedInt(IndexBufferSize);
        writer.Write7BitEncodedInt(VertexCount);
        writer.Write(DataHash);
    }

    public override string ToString()
    {
        return $"{Name} (format: {IndexFormat} indices: {IndexBufferSize} vertex: {VertexCount} / hash: {DataHash})";
    }
}
