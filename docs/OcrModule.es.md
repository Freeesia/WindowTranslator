# Módulo OCR

En WindowTranslator, puede elegir entre varios módulos OCR para reconocer el texto en la pantalla.  
Cada módulo tiene sus características, y al seleccionar el módulo apropiado según el uso, es posible un reconocimiento de texto más preciso.

## Nuevo reconocimiento Windows (beta) ![Predeterminado](https://img.shields.io/badge/Predeterminado-brightgreen)

Un módulo OCR local proporcionado por Microsoft.

### Ventajas
- **Precisión de reconocimiento**: La más alta precisión de reconocimiento
- **Rápido**: Velocidad de procesamiento muy rápida

### Desventajas
- **Uso de memoria**: El procesamiento de reconocimiento puede usar más de 1 GB de memoria
- **Entorno de operación**: Puede no funcionar en algunos entornos (Windows 10 o superior recomendado)

---

## Reconocimiento de caracteres estándar de Windows

Un motor OCR integrado en Windows 10 y versiones posteriores.

### Ventajas
- **Uso de memoria**: Ligero con bajo uso de memoria
- **Entorno de operación**: Ampliamente disponible en Windows 10 y versiones posteriores

### Desventajas
- **Precisión de reconocimiento**: Puede ser débil para fuentes complejas o escritura a mano
- **Configuración**: Puede ser necesaria la instalación manual de datos de idioma

---

## Tesseract OCR

Un motor OCR de código abierto.

### Ventajas
- **Soporte multilingüe**: Admite más de 100 idiomas
- **Estabilidad**: Un motor confiable con una larga historia

### Desventajas
- **Precisión de reconocimiento**: La precisión puede ser inferior en comparación con otros OCR

---

## Cómo elegir un módulo

Por favor seleccione un módulo funcional en el siguiente orden de alta precisión de reconocimiento.

1. Nuevo reconocimiento Windows (beta)
2. Reconocimiento de caracteres estándar de Windows
3. Tesseract OCR
