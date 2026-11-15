
import json
from pathlib import Path
from PIL import Image

BASE_DIR = Path(__file__).resolve().parent
MAPPING_FILE = BASE_DIR / "mapping.json"


def convert_png_to_svg(png_path, svg_path):
    with Image.open(png_path) as img:
        img = img.convert("RGBA")
        width, height = img.size

        pixels = [
            [img.getpixel((x, y))[3] > 0 for x in range(width)]
            for y in range(height)
        ]

    rectangles = []
    active = {}

    for y in range(height):
        runs = []
        x = 0

        while x < width:
            if not pixels[y][x]:
                x += 1
                continue

            start = x
            while x < width and pixels[y][x]:
                x += 1

            runs.append((start, x - start))

        next_active = {}

        for x, w in runs:
            key = (x, w)

            if key in active:
                start_y, h = active[key]
                next_active[key] = (start_y, h + 1)
            else:
                next_active[key] = (y, 1)

        for (x, w), (start_y, h) in active.items():
            if (x, w) not in next_active:
                rectangles.append((x, start_y, w, h))

        active = next_active

    for (x, w), (start_y, h) in active.items():
        rectangles.append((x, start_y, w, h))

    svg = [
        f'<svg xmlns="http://www.w3.org/2000/svg" '
        f'width="{width}" height="{height}" '
        f'viewBox="0 0 {width} {height}" '
        f'shape-rendering="crispEdges" fill="currentColor">'
    ]

    for x, y, w, h in rectangles:
        svg.append(
            f'<rect x="{x}" y="{y}" '
            f'width="{w}" height="{h}"/>'
        )

    svg.append("</svg>")

    # Create category folder if necessary
    svg_path.parent.mkdir(parents=True, exist_ok=True)

    # Save SVG before deleting the original PNG
    svg_path.write_text("\n".join(svg), encoding="utf-8")
    png_path.unlink()

    print(
        f"{png_path.name} -> "
        f"{svg_path.relative_to(BASE_DIR)} "
        f"({len(rectangles)} rectangles)"
    )


def main():
    with MAPPING_FILE.open("r", encoding="utf-8") as f:
        mapping = json.load(f)

    for category, names in mapping.items():
        output_dir = BASE_DIR / category
        output_dir.mkdir(parents=True, exist_ok=True)

        print(f"\nProcessing {category}...")

        for index, svg_name in enumerate(names, start=1):
            png_path = BASE_DIR / f"{category}-{index}.png"
            svg_path = output_dir / f"{svg_name}.svg"

            if not png_path.is_file():
                print(f"Skipping missing file: {png_path.name}")
                continue

            try:
                convert_png_to_svg(png_path, svg_path)
            except Exception as e:
                print(f"Error converting {png_path.name}: {e}")

    print("\nDone!")


if __name__ == "__main__":
    main()
