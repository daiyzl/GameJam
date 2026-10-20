"""Read-only image inspection. Does not edit or resample generated assets."""
from pathlib import Path
from PIL import Image
import json

root = Path(__file__).resolve().parent
grids = {"character": (3, 4), "ground_elements": (3, 2), "water": (3, 3), "rain": (3, 2), "fire": (2, 2)}
checks = []
for path in sorted((root / "Sprites").glob("*.png")):
    with Image.open(path) as image:
        image.verify()
    with Image.open(path) as image:
        cols, rows = grids.get(path.stem, (1, 1))
        assert image.width % cols == 0 and image.height % rows == 0, f"Uneven atlas: {path.name}"
        record = {"name": path.name, "width": image.width, "height": image.height, "mode": image.mode,
                  "columns": cols, "rows": rows, "frameWidth": image.width // cols, "frameHeight": image.height // rows}
        if path.stem != "ground_elements":
            assert image.mode == "RGBA", f"Missing transparency: {path.name}"
            alpha = image.getchannel("A")
            assert alpha.getextrema()[0] == 0 and alpha.getextrema()[1] >= 250, f"Missing transparent or solid pixels: {path.name}"
            # Allow <= 2/255 alpha from generated-image quantization at empty corners.
            assert image.getpixel((0, 0))[3] <= 2, f"Opaque background corner: {path.name}"
            for row in range(rows):
                for col in range(cols):
                    box = (col * image.width // cols, row * image.height // rows,
                           (col + 1) * image.width // cols, (row + 1) * image.height // rows)
                    assert alpha.crop(box).getextrema()[1] >= 200, f"Empty atlas frame: {path.name} {col},{row}"
            record["transparentBackground"] = True
        checks.append(record)
assert len(checks) == 9, "Expected nine generated PNG assets"
report = {"status": "passed", "checks": checks}
(root / "Verification" / "image-inspection.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print("PASS: 9 valid PNGs; transparent object/effect backgrounds; all atlas cells contain artwork; exact equal cell sizes.")
