import subprocess
import os
from datetime import datetime
import re

def generate_git_diff():
    try:
        current_dir = os.getcwd()
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        output_filename = f"REPORTE_TFG_{timestamp}.txt"

        print(f"📄 Analizando estructura de CardGame en: {current_dir}")

        # Comandos de Git diseñados específicamente para tu árbol de directorios:
        # 1. 'Assets/Scripts/*.cs' -> Captura todos tus scripts de lógica.
        # 2. 'Assets/Scenes/MainGame.unity' -> Captura los cambios en la mesa.
        # 3. 'Assets/Scenes/MainMenu.unity' -> Captura los cambios en el menú.
        command = [
            'git', 'diff', '--cached', '--text',
            '--', 
            'Assets/Scripts/*.cs', 
            'Assets/Scenes/MainGame.unity', 
            'Assets/Scenes/MainMenu.unity'
        ]

        result = subprocess.run(
            command, 
            capture_output=True, 
            text=True, 
            encoding='utf-8',
            errors='ignore'
        )

        raw_content = result.stdout
        if not raw_content.strip():
            print("⚠️ No se detectaron cambios en scripts o escenas principales. Verifica el 'git add .'")
            return

        # LÓGICA DE FILTRADO PARA EVITAR LOS BLOQUES DE CEROS (Binarios serializados de Unity)
        lines = raw_content.splitlines()
        clean_lines = []
        saltar_bloque_binario = False
        
        for line in lines:
            # Si entramos en una sección de datos de imagen o typeless, activamos el salto
            if "image data:" in line or "_typelessdata:" in line:
                saltar_bloque_binario = True
                clean_lines.append(line + " ... [DATOS BINARIOS OMITIDOS] ...")
                continue
            
            # Si detectamos una línea con una propiedad normal o un nuevo objeto de Unity, dejamos de saltar
            if saltar_bloque_binario:
                if re.match(r'^\s*[a-zA-Z_]+:', line) or line.startswith('---'):
                    saltar_bloque_binario = False
                else:
                    continue

            # Filtro de seguridad adicional para líneas de ceros infinitas
            if re.search(r'0{60,}', line):
                continue
            
            clean_lines.append(line)

        # Escritura del reporte final
        with open(output_filename, "w", encoding="utf-8") as f:
            f.write("\n".join(clean_lines))

        print(f"✅ ¡Todo listo! Reporte guardado en: {output_filename}")

    except Exception as e:
        print(f"❌ Error crítico: {e}")

if __name__ == "__main__":
    generate_git_diff()