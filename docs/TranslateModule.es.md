# Módulo de traducción

En WindowTranslator, puede elegir y utilizar varios módulos de traducción.  
Cada módulo tiene sus características, y al seleccionar el módulo apropiado según el uso, puede utilizar la traducción de manera más cómoda.

## Bergamot ![Predeterminado](https://img.shields.io/badge/Predeterminado-brightgreen)

Un módulo de traducción automática que funciona sin conexión.

### Ventajas
- **Completamente gratis**: Sin cargos
- **Sin límite de traducción**: Traduzca cuanto quiera
- **Rápido**: Procesamiento local para traducción rápida
- **Privacidad**: No se necesita conexión a Internet, los datos no se envían al exterior
- **Estabilidad**: No afectado por la red

### Desventajas
- **Precisión de traducción**: La precisión de traducción es inferior en comparación con los servicios en la nube
- **Uso de memoria**: Utiliza cierta cantidad de memoria para el procesamiento de traducción
- **Idiomas soportados**: Solo se admiten algunos pares de idiomas

### Escenarios de uso recomendados
- Cuando desea usar gratis
- Uso en entorno sin conexión
- Cuando la privacidad es importante
- Cuando realiza traducciones frecuentes

---

## Google Translate

Un módulo de traducción que utiliza el servicio de traducción de Google.

### Ventajas
- **Completamente gratis**: Se puede usar sin clave API
- **Soporte multilingüe**: Admite muchos pares de idiomas
- **Fácil**: No se requiere configuración especial

### Desventajas
- **Límite de traducción**: Límite en el número de caracteres que se pueden traducir por día
- **Precisión de traducción**: La precisión puede ser inferior en comparación con otros servicios de pago
- **Velocidad**: Afectado por la red
- **Estabilidad**: Puede volverse repentinamente no disponible debido a restricciones de uso

### Escenarios de uso recomendados
- Uso de baja frecuencia
- Cuando desea comenzar inmediatamente
- Cuando desea traducir varios pares de idiomas

---

## DeepL

Un módulo que utiliza el servicio de traducción DeepL, conocido por traducciones de alta calidad.

### Ventajas
- **Alta precisión**: Traducciones naturales y de alta calidad
- **Nivel gratuito sustancial**: Hasta 500,000 caracteres gratis por mes (API gratuita)
- **Rápido**: Procesamiento de traducción rápido
- **Soporte de glosario**: Mantenga la consistencia de traducción con glosarios

### Desventajas
- **Registro API requerido**: Se necesita registro en la API de DeepL y configuración de clave API
- **Límite gratuito**: Si se supera el nivel gratuito, se necesita actualizar a un plan de pago
- **Idiomas soportados**: Los idiomas soportados son más limitados que Google

### Escenarios de uso recomendados
- Cuando busca traducciones de alta calidad
- Uso de frecuencia media

---

## Google AI (Gemini)

Un módulo de traducción que utiliza la última tecnología de IA de Google.

### Ventajas
- **Mayor precisión**: Traducciones de muy alta calidad que entienden el contexto
- **Flexibilidad**: Personalice prompts para ajustar el estilo de traducción
- **Soporte de glosario**: Mantenga la consistencia de traducción con glosarios

### Desventajas
- **Clave API requerida**: La clave API debe obtenerse y configurarse desde Google AI Studio
- **Pago por uso**: Cargos basados en el uso (pero pequeña cantidad)
- **Velocidad**: Tiempo de procesamiento más largo porque está basado en LLM

### Escenarios de uso recomendados
- Cuando busca traducciones de la más alta calidad
- Cuando se necesita un estilo de traducción personalizado
- Cuando se necesitan traducciones sensibles al contexto

---

## API ChatGPT (O LLM local)

Un módulo de traducción que utiliza la API de ChatGPT o un LLM local.

### Ventajas
- **Mayor precisión**: Traducciones de alta calidad por grandes modelos de lenguaje
- **Flexibilidad**: Personalice prompts para ajustar el estilo de traducción
- **Soporte de glosario**: Mantenga la consistencia de traducción con glosarios
- **Soporte LLM local**: Posibilidad de usar su propio servidor LLM

### Desventajas
- **Clave API requerida**: Se necesita configuración de clave API de cada servicio (excepto LLM local)
- **Pago por uso**: Cargos basados en el uso (excepto LLM local)
- **Velocidad**: Tiempo de procesamiento más largo
- **Requisitos LLM local**: Se necesita PC de alto rendimiento para ejecutar su propio LLM

### Escenarios de uso recomendados
- Cuando busca traducciones de la más alta calidad
- Cuando se necesita un estilo de traducción personalizado
- Cuando desea traducciones de alta calidad mientras valora la privacidad (LLM local)

---

## PLaMo

Un módulo de traducción que utiliza un LLM local especializado para japonés.

### Ventajas
- **Especializado en japonés**: Optimizado para traducciones al japonés
- **Completamente gratis**: Modelo de código abierto sin cargos
- **Privacidad**: Funciona localmente, los datos no se envían al exterior
- **Sin conexión**: No se necesita conexión a Internet

### Desventajas
- **Requisitos de alto rendimiento**: Se necesita PC de alto rendimiento con GPU
- **Uso de memoria**: Se necesita gran cantidad de memoria (8 GB o más recomendado)
- **Velocidad**: Tiempo de procesamiento más largo sin GPU

### Escenarios de uso recomendados
- Cuando posee un PC de alto rendimiento
- Cuando la privacidad es lo más importante
- Cuando valora la calidad de traducción al japonés

---

## Cómo elegir un módulo

| Objetivo                 | Módulo recomendado                            |
| -------------------- | ----------------------------------------- |
| Comenzar inmediatamente   | **Bergamot** o **Google Translate**        |
| Traducciones de la más alta calidad | **Google AI** o **API ChatGPT**      |
| Reducir costos     | **Bergamot** o **DeepL (dentro del nivel gratuito)** |
| Prioridad a la privacidad     | **Bergamot** o **PLaMo**             |
| Uso de alta frecuencia         | **Bergamot** o **DeepL**             |
