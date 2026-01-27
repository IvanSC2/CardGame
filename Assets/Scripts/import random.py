import random

def girar_ruleta():
    # Definimos los números rojos tradicionales de la ruleta
    numeros_rojos = {1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36}
    
    # Generamos un número aleatorio entre 0 y 36 (37 posibilidades)
    resultado = random.randint(0, 36)
    
    # Lógica de asignación de color
    if resultado == 0:
        return "Verde", resultado
    elif resultado in numeros_rojos:
        return "Rojo", resultado
    else:
        return "Negro", resultado

# Ejemplo de uso
color, numero = girar_ruleta()
print(f"La bola ha caído en el número {numero} ({color})")