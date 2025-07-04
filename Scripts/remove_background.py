import os
import sys
from pathlib import Path
from rembg import remove
from PIL import Image
import argparse

def remove_background_from_image(input_path, output_path):
    """
    Elimina el fons d'una imatge i la guarda amb fons transparent.
    
    Args:
        input_path (str): Ruta de la imatge d'entrada
        output_path (str): Ruta on es guardarà la imatge processada
    """
    try:
        # Obrir la imatge
        with open(input_path, 'rb') as input_file:
            input_data = input_file.read()
        
        # Eliminar el fons
        output_data = remove(input_data)
        
        # Guardar la imatge amb fons transparent
        with open(output_path, 'wb') as output_file:
            output_file.write(output_data)
            
        print(f"✓ Processat: {os.path.basename(input_path)}")
        return True
        
    except Exception as e:
        print(f"✗ Error processant {os.path.basename(input_path)}: {str(e)}")
        return False

def process_folder(input_folder, output_folder):
    """
    Processa totes les imatges d'una carpeta.
    
    Args:
        input_folder (str): Carpeta amb les imatges originals
        output_folder (str): Carpeta on es guardaran les imatges processades
    """
    # Formats d'imatge suportats
    supported_formats = ['.jpg', '.jpeg', '.png', '.bmp', '.tiff', '.webp']
    
    # Crear la carpeta de sortida si no existeix
    Path(output_folder).mkdir(parents=True, exist_ok=True)
    
    # Obtenir totes les imatges de la carpeta
    image_files = []
    for format in supported_formats:
        image_files.extend(Path(input_folder).glob(f'*{format}'))
        image_files.extend(Path(input_folder).glob(f'*{format.upper()}'))
    
    if not image_files:
        print(f"No s'han trobat imatges a la carpeta: {input_folder}")
        return
    
    print(f"S'han trobat {len(image_files)} imatges per processar...")
    print("-" * 50)
    
    processed = 0
    errors = 0
    
    for image_path in image_files:
        # Crear el nom del fitxer de sortida (sempre en PNG per mantenir transparència)
        output_filename = image_path.stem + '_no_background.png'
        output_path = Path(output_folder) / output_filename
        
        # Processar la imatge
        if remove_background_from_image(str(image_path), str(output_path)):
            processed += 1
        else:
            errors += 1
    
    print("-" * 50)
    print(f"\nResum:")
    print(f"  - Imatges processades correctament: {processed}")
    print(f"  - Errors: {errors}")
    print(f"  - Les imatges processades s'han guardat a: {output_folder}")

def main():
    parser = argparse.ArgumentParser(
        description='Elimina el fons de les imatges d\'una carpeta i el fa transparent'
    )
    parser.add_argument(
        'input_folder',
        help='Carpeta amb les imatges originals'
    )
    parser.add_argument(
        '-o', '--output',
        default='output_no_background',
        help='Carpeta de sortida (per defecte: output_no_background)'
    )
    
    args = parser.parse_args()
    
    # Verificar que la carpeta d'entrada existeix
    if not os.path.exists(args.input_folder):
        print(f"Error: La carpeta '{args.input_folder}' no existeix.")
        sys.exit(1)
    
    print("Script per eliminar fons d'imatges")
    print("==================================\n")
    
    # Processar les imatges
    process_folder(args.input_folder, args.output)

if __name__ == "__main__":
    main()