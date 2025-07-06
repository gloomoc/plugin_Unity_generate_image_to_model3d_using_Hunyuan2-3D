import os
import sys
from pathlib import Path
from rembg import remove
from PIL import Image
import argparse

def remove_background_from_image(input_path, output_path):
    """
    Removes the background from an image and saves it with a transparent background.
    
    Args:
        input_path (str): Path to the input image
        output_path (str): Path where the processed image will be saved
    """
    try:
        # Open the image
        with open(input_path, 'rb') as input_file:
            input_data = input_file.read()
        
        # Remove the background
        output_data = remove(input_data)
        
        # Save the image with a transparent background
        with open(output_path, 'wb') as output_file:
            output_file.write(output_data)
            
        print(f"✓ Processed: {os.path.basename(input_path)}")
        return True
        
    except Exception as e:
        print(f"✗ Error processing {os.path.basename(input_path)}: {str(e)}")
        return False

def process_folder(input_folder, output_folder):
    """
    Processes all images in a folder.
    
    Args:
        input_folder (str): Folder with the original images
        output_folder (str): Folder where the processed images will be saved
    """
    # Supported image formats
    supported_formats = ['.jpg', '.jpeg', '.png', '.bmp', '.tiff', '.webp']
    
    # Create the output folder if it doesn't exist
    Path(output_folder).mkdir(parents=True, exist_ok=True)
    
    # Get all images from the folder
    image_files = []
    for format in supported_formats:
        image_files.extend(Path(input_folder).glob(f'*{format}'))
        image_files.extend(Path(input_folder).glob(f'*{format.upper()}'))
    
    if not image_files:
        print(f"No images found in the folder: {input_folder}")
        return
    
    print(f"{len(image_files)} images found to process...")
    print("-" * 50)
    
    processed = 0
    errors = 0
    
    for image_path in image_files:
        # Create the output filename (always PNG to maintain transparency)
        output_filename = image_path.stem + '_no_background.png'
        output_path = Path(output_folder) / output_filename
        
        # Process the image
        if remove_background_from_image(str(image_path), str(output_path)):
            processed += 1
        else:
            errors += 1
    
    print("-" * 50)
    print(f"\nSummary:")
    print(f"  - Images processed successfully: {processed}")
    print(f"  - Errors: {errors}")
    print(f"  - Processed images have been saved to: {output_folder}")

def main():
    parser = argparse.ArgumentParser(
        description='Removes the background from images in a folder and makes it transparent'
    )
    parser.add_argument(
        'input_folder',
        help='Folder with the original images'
    )
    parser.add_argument(
        '-o', '--output',
        default='output_no_background',
        help='Output folder (default: output_no_background)'
    )
    
    args = parser.parse_args()
    
    # Verify that the input folder exists
    if not os.path.exists(args.input_folder):
        print(f"Error: The folder '{args.input_folder}' does not exist.")
        sys.exit(1)
    
    print("Script to remove image backgrounds")
    print("==================================\n")
    
    # Process the images
    process_folder(args.input_folder, args.output)

if __name__ == "__main__":
    main()