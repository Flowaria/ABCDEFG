using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace ABCDEFG.Reroutables;

internal record AssetContext(
    AssetsFileInstance assetFileInstance,
    AssetFileInfo assetFileInfo,
    AssetTypeValueField baseField
    );