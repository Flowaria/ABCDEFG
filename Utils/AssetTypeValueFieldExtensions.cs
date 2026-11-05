using AssetsTools.NET;

namespace ABCDEFG.Utils;

internal static class AssetTypeValueFieldExtensions
{
    public static bool IsNullOrDummy(this AssetTypeValueField field)
    {
        if (field == null)
            return true;

        if (field.IsDummy)
            return true;

        if (field == AssetTypeValueField.DUMMY_FIELD)
            return true;

        return false;
    }
}
