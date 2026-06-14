import argparse
import os
import sys
from pathlib import Path

from rembg import remove


SUPPORTED_FORMATS = ('.jpg', '.jpeg', '.png', '.bmp', '.tiff', '.webp')


def remove_background_from_image(input_path, output_path):
    try:
        output_path = Path(output_path)
        output_path.parent.mkdir(parents=True, exist_ok=True)

        with open(input_path, 'rb') as input_file:
            input_data = input_file.read()

        output_data = remove(input_data)

        with open(output_path, 'wb') as output_file:
            output_file.write(output_data)

        print(f"✓ Processed: {Path(input_path).name} -> {output_path}")
        return True
    except Exception as e:
        print(f"✗ Error processing {Path(input_path).name}: {str(e)}")
        return False


def process_folder(input_folder, output_folder):
    image_files = []
    for extension in SUPPORTED_FORMATS:
        image_files.extend(Path(input_folder).glob(f'*{extension}'))
        image_files.extend(Path(input_folder).glob(f'*{extension.upper()}'))

    if not image_files:
        print(f"No images found in the folder: {input_folder}")
        return False

    output_folder = Path(output_folder)
    output_folder.mkdir(parents=True, exist_ok=True)

    print(f"Found {len(image_files)} images to process...")
    print("-" * 50)

    processed = 0
    errors = 0

    for image_path in image_files:
        output_path = output_folder / f'{image_path.stem}_no_background.png'
        if remove_background_from_image(str(image_path), str(output_path)):
            processed += 1
        else:
            errors += 1

    print("-" * 50)
    print("Summary:")
    print(f"  - Images processed successfully: {processed}")
    print(f"  - Errors: {errors}")
    print(f"  - Saved to: {output_folder}")
    return errors == 0


def resolve_single_output(input_path, explicit_output):
    if explicit_output:
        explicit_output = Path(explicit_output)
        if explicit_output.exists() and explicit_output.is_dir():
            return explicit_output / f'{input_path.stem}_no_background.png'

        if explicit_output.suffix:
            return explicit_output

        return explicit_output / f'{input_path.stem}_no_background.png'

    return Path.cwd() / 'output_no_background' / f'{input_path.stem}_no_background.png'


def main():
    parser = argparse.ArgumentParser(
        description='Removes the background from a single image or all images in a folder'
    )
    parser.add_argument('input_path', help='Input image or folder')
    parser.add_argument('output_path', nargs='?', help='Optional output file/folder path')
    parser.add_argument('-o', '--output', dest='output_option', help='Optional output file/folder path')

    args = parser.parse_args()

    input_path = Path(args.input_path)
    output_value = args.output_option or args.output_path

    if not input_path.exists():
        print(f"Error: '{input_path}' does not exist.")
        sys.exit(1)

    if input_path.is_file():
        if input_path.suffix.lower() not in SUPPORTED_FORMATS:
            print(f"Error: '{input_path}' is not a supported image.")
            sys.exit(1)

        output_path = resolve_single_output(input_path, output_value)
        sys.exit(0 if remove_background_from_image(str(input_path), str(output_path)) else 1)

    output_folder = output_value or 'output_no_background'
    sys.exit(0 if process_folder(str(input_path), output_folder) else 1)


if __name__ == "__main__":
    main()
