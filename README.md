# AssetBundle Compressor Deluxe Edition for GTFO (ABCDEFG)
Tool for compressing AssetBundle built for GTFO.

## What's different from `AssetBundleCompressor`?
- Cache all base-game asset before compressing. Constant compression time is guaranteed in any assetbundle
- Supports all compression format: `LZMA/LZ4/LZ4HC/NOCOMP`
- Multi-threading has implemented for compressing multiple file. Significantly boosts speed when compressing multiple bundles
- Proper log has added `output.log`, `error.log`
- Supports better `Mesh` and `Material` rerouting. could reduce both disk space and VRAM usage.

## Setup
- To start compressing you should generate asset cache first.
- Run ABCDEFG.exe once and required `config.json` file will be generated.
  - to run on linux/unix: `dotnet ABCDEFG.dll`

## Config
#### GTFODataPath
```json
"GTFODataPath": ""
```
- Absolute Path to the `GTFO_Data` folder
- Example: `"GTFODataPath": "C:/Program Files (x86)/Steam/steamapps/common/GTFO/GTFO_Data"`

#### MaxThread
```json
"MaxThread": 1
```
- Number of threads to use when compressing multiple assetbundles.
- This is clamped between 1 and your systems max thread count

#### WriteReroutedOnDisk
```json
"WriteReroutedOnDisk": false
```
- Write Intermediate Rerouted AssetBundle to disk?
- Setting to `true` will write intermediate assetbundle result to your drive instead of ram.
- Recommended to be `false` unless you having out of memory issue.

#### LogVerbose
```json
"LogVerbose": false
```
- Should log verbose messages?
- Setting to `true` will give you much more detailed logs while compressing, In cost of logging overhead.
- Useful when you need to debug if something went wrong.

## Caching
- Once `config.json` is configured you need to cache base game asset lookup.
- Running `ABCDEFG` will give you prompt:
`No arguments given. Do you want to generate the cache? (Press 'Y' to confirm)`
- Press Y to continue.
  - Alternative way: `ABCDEFG --cache` or `dotnet ABCDEFG.dll --cache` will build cache without prompt.

## Compressing
- If cache is ready. you are now able to compress AssetBundles.

#### Drag and Drop Method
- Drag and Drop files to `ABCDEFG.exe` will compress given assetbundle files as old `AssetBundleCompressor` does.
- There are also few drag and drop scripts ready for use.
  - `compress_lz4hc` will compress given file into LZ4HC format (with -compressed suffix)
  - `compress_lzma` will compress given file into LZMA format (with -compressed suffix)
  - `compress_lz4hc_to_subdir` will compress given file into LZ4HC format (under new sub-directory `compressed/` with same file name)
  - `compress_lzma_to_subdir` will compress given file into LZMA format (under new sub-directory `compressed/` with same file name)

#### Command-Line Method
- This gives you more control over tooling/automating compress job.
- Every arguments works as set parameter. Means every trailing file will have same setting unless it explicitly set again.
  - Example: `ABCDEFG --lzma "file1" "file2" --lz4 "file3"` sets `file1` and `file2` as LZMA, `file3` as LZ4
- `--lzma` set compressing method to LZMA
- `--lz4` set compressing method to LZ4
- `--lz4hc` set compressing method to LZ4HC
- `--nocomp` disable compression (asset rerouting still works)
- `--exclude <RegEX string>` exclude matching trailing files from compressing
- `--output-format <format string>` format output file name `{FileName}` can be used as variable
  - Example: `--output-format "{FileName}-compressed"`: Suffix as `-compressed`
  - `--output-format "compressed/{FileName}"`: Sub-pathing as `compressed/`
  - `--output-format "/home/flowaria/compressedbundles/{FileName}"`: Absolute pathing works!
- `--write-metadata <true/false>` toggle `.metadata.json` writing
- Quick Example:
  - `ABCDEFG --output-format "{filename}-compressed" --exclude "-compressed$" -lz4hc "flow_weaponparts" -lzma "flowgeo_tech" "flowgeo_mining" "flowgeo_service"`