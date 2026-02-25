# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![Crowdin](https://badges.crowdin.net/windowtranslator/localized.svg)](https://crowdin.com/project/windowtranslator)
[![Microsoft Store](https://get.microsoft.com/images/es-es%20dark.svg)](https://apps.microsoft.com/detail/9pjd2fdzqxm3?referrer=appbadge&mode=direct)

WindowTranslator es una herramienta para traducir ventanas de aplicaciones en Windows.

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md) | [VI](./README.vi.md) | [HI](./README.hi.md) | [MS](./README.ms.md) | [ID](./README.id.md) | [PT-BR](./README.pt-BR.md) | [FR](./README.fr.md) | [ES](./README.es.md) | [AR](./README.ar.md) | [TR](./README.tr.md) | [TH](./README.th.md) | [RU](./README.ru.md) | [FIL](./README.fil.md) | [PL](./README.pl.md) | [FA](./README.fa.md)

## Tabla de contenidos
- [ WindowTranslator](#-windowtranslator)
  - [Tabla de contenidos](#tabla-de-contenidos)
  - [Descargar](#descargar)
    - [Versión Microsoft Store ](#versión-microsoft-store-)
    - [Versión instalable](#versión-instalable)
    - [Versión portátil](#versión-portátil)
  - [Cómo usar](#cómo-usar)
    - [Bergamot ](#bergamot-)
  - [Otras funciones](#otras-funciones)

## Descargar
### Versión Microsoft Store ![Recomendada](https://img.shields.io/badge/Recomendada-brightgreen)

Instale desde [Microsoft Store](https://apps.microsoft.com/detail/9pjd2fdzqxm3?referrer=appbadge&mode=direct).
Funciona incluso en entornos donde .NET no está instalado.

### Versión instalable

Descargue `WindowTranslator-(versión).msi` desde la [página de releases de GitHub](https://github.com/Freeesia/WindowTranslator/releases/latest) y ejecútelo para instalar.  
Video tutorial de instalación aquí⬇️  
[![Video tutorial de instalación](https://github.com/user-attachments/assets/b5babc02-715b-43bc-ba97-f23078ffd39b)](https://youtu.be/wvcbCLA9chQ?t=7)

### Versión portátil

Descargue el archivo zip desde la [página de releases de GitHub](https://github.com/Freeesia/WindowTranslator/releases/latest) y extráigalo en cualquier carpeta.  
- `WindowTranslator-(versión).zip` : Requiere entorno .NET  
- `WindowTranslator-full-(versión).zip` : Independiente de .NET

## Cómo usar

### Bergamot ![Predeterminado](https://img.shields.io/badge/Predeterminado-brightgreen)

1. Inicie `WindowTranslator.exe` y haga clic en el botón de traducción.  
   ![Botón de traducción](images/translate.png)
2. Seleccione la ventana de la aplicación que desea traducir y haga clic en el botón "OK".  
   ![Selección de ventana](images/select.png)
3. Desde la pestaña "Configuración general", seleccione los idiomas de origen y destino en "Configuración de idioma".  
   ![Configuración de idioma](images/language.png)
4. Después de completar la configuración, haga clic en el botón "OK" para cerrar la pantalla de configuración.  
   > Puede ser necesario instalar la función OCR.
   > Por favor siga las instrucciones para instalarla.
5. Después de un momento, los resultados de traducción se mostrarán como superposición.  
   ![Resultados de traducción](images/result.png)

> [!NOTE]
> Varios módulos de traducción están disponibles en WindowTranslator.  
> Google Translate tiene un límite bajo en la cantidad de texto que se puede traducir. Si lo usa con frecuencia, considere usar otros módulos.  
> Puede consultar la lista de módulos de traducción disponibles en los videos a continuación o en la [Documentación](https://wt.studiofreesia.com/TranslateModule.es).
> 
> |                |                                                           Video de uso                                                            | Ventajas                    | Desventajas                        |
> | :------------: | :-----------------------------------------------------------------------------------------------------------------------------------: | :---------------------------- | :----------------------------------- |
> |   Bergamot     | | Completamente gratis<br/>Sin límite de traducción<br/>Traducción rápida | Menor precisión de traducción<br/>Requiere más de 1GB de memoria libre |
> |   Google Translate   | [![Video de configuración de Google Translate](https://github.com/user-attachments/assets/bbf45370-0387-47e1-b690-3183f37e06d2)](https://youtu.be/83A8T890N5M)  | Completamente gratis | Límite de traducción bajo<br/>Menor precisión de traducción |
> |     DeepL      |   [![Video de configuración de DeepL](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0)   | Gran nivel gratuito<br/>Traducción rápida | |
> |     Gemini     | [![Video de configuración de Google AI](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I) | Alta precisión de traducción | Se requiere pequeña tarifa |
> |    ChatGPT     | TBD | Alta precisión de traducción | Se requiere pequeña tarifa |
> | LLM local | TBD | El servicio es gratuito | Se requiere PC de alto rendimiento |

## Otras funciones

Además de los módulos de traducción, WindowTranslator tiene varias funciones.  
Si desea saber más, consulte el [Wiki](https://github.com/Freeesia/WindowTranslator/wiki).

---
[Política de privacidad](PrivacyPolicy.md)

Este documento fue traducido del japonés utilizando traducción automática.
