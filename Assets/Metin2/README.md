# Metin2 map importer

Run **Tools > Metin2 > Build All Maps**. Put extracted packs in `Metin2,5/Extracted` (outside Assets) and keep their folder structure. Convert static `.gr2` models to `.fbx`, `.obj`, or `.dae`; converted `.png` textures are preferred over legacy `.dds` files.

The importer scans `Setting.txt`, `MapProperty.txt`, `AreaData*`, `.prb` CRC references, 16-bit `height.raw`, `tile.raw`, binary `water.wtr`, texture sets, and converted models. Used assets are copied to `Assets/Metin2/Raw`; scenes, terrain layers/data, and water meshes go to `Assets/Metin2/Generated`.

Missing or zero-byte source references are written to `Assets/Metin2/Generated/ImportReport.txt` without stopping other maps. Both Raw and Generated are rebuildable and intentionally ignored by Git.
