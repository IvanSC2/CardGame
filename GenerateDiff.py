import subprocess
import os
from datetime import datetime
import re

def generate_git_diff():
    try:
        current_dir = os.getcwd()
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        output_filename = f"REPORTE_COMPLETO_{timestamp}.txt"

        print(f"📄 Capturando cambios existentes y archivos nuevos en: {current_dir}")

        # 1. Capturar cambios en archivos que ya existían
        diff_proc = subprocess.run(
            ['git', 'diff', '--binary=off'], 
            capture_output=True, text=True, encoding='utf-8', errors='ignore'
        )
        diff_content = diff_proc.stdout

        # 2. Capturar contenido de ARCHIVOS NUEVOS (Untracked)
        # Buscamos la lista de archivos que no están en Git
        untracked_files_proc = subprocess.run(
            ['git', 'ls-files', '--others', '--exclude-standard'], 
            capture_output=True, text=True, encoding='utf-8'
        )
        untracked_files = untracked_files_proc.stdout.splitlines()

        new_files_content = ""
        for file_path in untracked_files:
            # Solo leemos archivos de texto/código para evitar errores
            if file_path.endswith(('.cs', '.py', '.txt', '.json', '.html', '.css')):
                try:
                    with open(file_path, 'r', encoding='utf-8') as f:
                        new_files_content += f"\n\n--- ARCHIVO NUEVO: {file_path} ---\n"
                        # Añadimos el '+' a cada línea para que parezca un diff
                        new_files_content += "".join([f"+{line}" for line in f.readlines()])
                except Exception:
                    continue

        total_content = diff_content + new_files_content

        if not total_content.strip():
            print("⚠️ No hay cambios ni archivos nuevos.")
            return

        # 3. Limpieza de ceros/binarios (tu filtro de seguridad)
        lines = total_content.splitlines()
        clean_lines = []
        for line in lines:
            if re.search(r'0{50,}', line) or "image data" in line.lower():
                if line.startswith('-') or line.startswith('+'):
                    clean_lines.append(line[:100] + " ... [DATOS BINARIOS OMITIDOS] ...")
                continue
            clean_lines.append(line)

        # 4. Guardar resultado final
        with open(output_filename, "w", encoding="utf-8") as f:
            f.write("\n".join(clean_lines))

        print(f"✅ ¡Todo listo! Se han incluido cambios y archivos nuevos en: {output_filename}")

    except Exception as e:
        print(f"❌ Error: {e}")

if __name__ == "__main__":
    generate_git_diff()