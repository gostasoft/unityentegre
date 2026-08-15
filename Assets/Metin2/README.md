# Metin2 map importer

Run **Tools > Metin2 > Build All Maps**. Put extracted packs in `Metin2,5/Extracted` (outside Assets) and keep their folder structure. Convert static `.gr2` models to `.fbx`, `.obj`, or `.dae`; converted `.png` textures are preferred over legacy `.dds` files.

The importer scans `Setting.txt`, `MapProperty.txt`, `AreaData*`, `.prb` CRC references, 16-bit `height.raw`, `tile.raw`, binary `water.wtr`, texture sets, and converted models. Used assets are copied to `Assets/Metin2/Raw`; scenes, terrain layers/data, and water meshes go to `Assets/Metin2/Generated`.

Missing or zero-byte source references are written to `Assets/Metin2/Generated/ImportReport.txt` without stopping other maps. Both Raw and Generated are rebuildable and intentionally ignored by Git.

## Login and character flow

Run **Tools > Metin2 > Build Login Flow** to build the original-client-inspired frontend. The generated `Metin2_Intro` scene implements:

`Login > Empire > Character Select > Character Create > Loading > Empire Start Map`

- Four local character slots are available for frontend testing.
- Warrior, Assassin, Sura, and Shaman have male and female FBX previews.
- This first version uses a local session. The real account server can be connected later without replacing the screens.
- **Tools > Metin2 > Reset Local Login Data** clears the test account, empire, and character slots.
- Missing frontend assets are reported in `Assets/Metin2/Generated/FrontendBuildReport.txt` without stopping the build.

## Player gameplay data

Run `Tools/Metin2/Build Player Gameplay` after converting the PC and PC2 GR2 motion packs with `Tools/Metin2Player/Convert-Metin2PlayerAnimations.bat`.

- The builder uses the original `playersettingmodule.py`, `.msa`, `.mse` and `.mde` source data.
- It creates eight source-matched class/gender motion sets, animator controllers, combo input timings, movement accumulation, skill effect events and hit spheres.
- Generated FBX/animation/effect assets remain local and are reproducible; only the builder and converter are committed.
- The supplied PC/PC2 source is missing `shaman/skill/2_me.gr2` and `2_target.gr2` for both gender packs. These two source references remain reported rather than substituted.
