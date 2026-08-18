"""Convert the original warrior effect DDS textures to alpha-preserving PNG assets."""
from pathlib import Path
from PIL import Image


def fnv1a(value: str) -> str:
    result = 2166136261
    for character in value.lower():
        result ^= ord(character)
        result = (result * 16777619) & 0xFFFFFFFF
    return f"{result:08x}"


project = Path(__file__).resolve().parents[1]
source_root = project / "Metin2,5" / "Extracted" / "PC" / "ymir work" / "pc" / "warrior" / "effect"
output_root = project / "Assets" / "Metin2" / "Raw" / "Effects" / "ConvertedDDS"
output_root.mkdir(parents=True, exist_ok=True)

converted = 0
for source in source_root.glob("*.dds"):
    relative = str(source.relative_to(project)).replace("\\", "/")
    destination = output_root / f"{source.stem}_{fnv1a(str(source))}.png"
    with Image.open(source) as image:
        image.convert("RGBA").save(destination, "PNG")
    converted += 1

print(f"Converted {converted} original warrior DDS textures.")
