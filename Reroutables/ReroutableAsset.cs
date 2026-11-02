using AssetsTools.NET;

namespace ABCDEFG.Reroutables;

internal abstract class ReroutableAsset
{
    public int FileIndex { get; set; }
    public long PathID { get; set; }
    public string Name { get; set; }
    public abstract bool IsSameAsset(ReroutableAsset other);
    public virtual void ReadExtraFromBaseField(AssetTypeValueField baseField) { }
    public virtual void ReadExtra(BinaryReader reader) { }
    public virtual void WriteExtra(BinaryWriter writer) { }

    public virtual void ReadNameFromBaseField(AssetTypeValueField baseField)
    {
        Name = baseField["m_Name"].AsString;
    }

    public static T FromBaseField<T>(int fileIndex, long pathID, AssetTypeValueField baseField) where T : ReroutableAsset, new()
    {
        var asset = new T();
        asset.FileIndex = fileIndex;
        asset.PathID = pathID;
        asset.ReadNameFromBaseField(baseField);
        asset.ReadExtraFromBaseField(baseField);
        return asset;
    }

    public static T Read<T>(BinaryReader reader) where T : ReroutableAsset, new()
    {
        var asset = new T();
        asset.FileIndex = reader.Read7BitEncodedInt();
        asset.PathID = reader.ReadInt64();
        asset.Name = reader.ReadString();
        asset.ReadExtra(reader);
        return asset;
    }

    public static void Write<T>(T asset, BinaryWriter writer) where T : ReroutableAsset, new()
    {
        writer.Write7BitEncodedInt(asset.FileIndex);
        writer.Write(asset.PathID);
        writer.Write(asset.Name);
        asset.WriteExtra(writer);
    }
}
