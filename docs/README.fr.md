# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![Crowdin](https://badges.crowdin.net/windowtranslator/localized.svg)](https://crowdin.com/project/windowtranslator)
[![Microsoft Store](https://get.microsoft.com/images/fr-fr%20dark.svg)](https://apps.microsoft.com/detail/9pjd2fdzqxm3?referrer=appbadge&mode=direct)

WindowTranslator est un outil de traduction de fenêtres d'applications sur Windows.

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md) | [VI](./README.vi.md) | [HI](./README.hi.md) | [MS](./README.ms.md) | [ID](./README.id.md) | [PT-BR](./README.pt-BR.md) | [FR](./README.fr.md) | [ES](./README.es.md) | [AR](./README.ar.md)

## Table des matières
- [ WindowTranslator](#-windowtranslator)
  - [Table des matières](#table-des-matières)
  - [Téléchargement](#téléchargement)
    - [Version Microsoft Store ](#version-microsoft-store-)
    - [Version installable](#version-installable)
    - [Version portable](#version-portable)
  - [Comment utiliser](#comment-utiliser)
    - [Bergamot ](#bergamot-)
  - [Autres fonctionnalités](#autres-fonctionnalités)

## Téléchargement
### Version Microsoft Store ![Recommandée](https://img.shields.io/badge/Recommandée-brightgreen)

Installez depuis le [Microsoft Store](https://apps.microsoft.com/detail/9pjd2fdzqxm3?referrer=appbadge&mode=direct).
Fonctionne même dans les environnements où .NET n'est pas installé.

### Version installable

Téléchargez `WindowTranslator-(version).msi` depuis la [page des releases GitHub](https://github.com/Freeesia/WindowTranslator/releases/latest) et exécutez-le pour installer.  
Vidéo tutoriel d'installation ici⬇️  
[![Vidéo tutoriel d'installation](https://github.com/user-attachments/assets/b5babc02-715b-43bc-ba97-f23078ffd39b)](https://youtu.be/wvcbCLA9chQ?t=7)

### Version portable

Téléchargez le fichier zip depuis la [page des releases GitHub](https://github.com/Freeesia/WindowTranslator/releases/latest) et extrayez-le dans n'importe quel dossier.  
- `WindowTranslator-(version).zip` : Nécessite l'environnement .NET  
- `WindowTranslator-full-(version).zip` : Indépendant de .NET

## Comment utiliser

### Bergamot ![Défaut](https://img.shields.io/badge/Défaut-brightgreen)

1. Lancez `WindowTranslator.exe` et cliquez sur le bouton de traduction.  
   ![Bouton de traduction](images/translate.png)
2. Sélectionnez la fenêtre de l'application que vous souhaitez traduire et cliquez sur le bouton "OK".  
   ![Sélection de fenêtre](images/select.png)
3. Depuis l'onglet "Paramètres généraux", sélectionnez les langues source et cible dans "Paramètres de langue".  
   ![Paramètres de langue](images/language.png)
4. Après avoir terminé les paramètres, cliquez sur le bouton "OK" pour fermer l'écran de paramètres.  
   > L'installation de la fonction OCR peut être nécessaire.
   > Veuillez suivre les instructions pour l'installer.
5. Après un moment, les résultats de traduction seront affichés en superposition.  
   ![Résultats de traduction](images/result.png)

> [!NOTE]
> Divers modules de traduction sont disponibles dans WindowTranslator.  
> Google Traduction a une limite basse sur la quantité de texte pouvant être traduite. Si vous l'utilisez fréquemment, envisagez d'utiliser d'autres modules.  
> Vous pouvez consulter la liste des modules de traduction disponibles dans les vidéos ci-dessous ou sur la [Documentation](https://wt.studiofreesia.com/TranslateModule.fr).
> 
> |                |                                                           Vidéo d'utilisation                                                            | Avantages                    | Inconvénients                        |
> | :------------: | :-----------------------------------------------------------------------------------------------------------------------------------: | :---------------------------- | :----------------------------------- |
> |   Bergamot     | | Complètement gratuit<br/>Pas de limite de traduction<br/>Traduction rapide | Précision de traduction inférieure<br/>Nécessite plus de 1Go de mémoire libre |
> |   Google Traduction   | [![Vidéo de configuration Google Traduction](https://github.com/user-attachments/assets/bbf45370-0387-47e1-b690-3183f37e06d2)](https://youtu.be/83A8T890N5M)  | Complètement gratuit | Limite de traduction basse<br/>Précision de traduction inférieure |
> |     DeepL      |   [![Vidéo de configuration DeepL](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0)   | Grande offre gratuite<br/>Traduction rapide | |
> |     Gemini     | [![Vidéo de configuration Google AI](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I) | Haute précision de traduction | Petit frais requis |
> |    ChatGPT     | TBD | Haute précision de traduction | Petit frais requis |
> | LLM local | TBD | Service lui-même gratuit | PC haute performance requis |

## Autres fonctionnalités

En plus des modules de traduction, WindowTranslator dispose de diverses fonctionnalités.  
Si vous souhaitez en savoir plus, veuillez consulter le [Wiki](https://github.com/Freeesia/WindowTranslator/wiki).

---
[Politique de confidentialité](PrivacyPolicy.md)

Ce document a été traduit du japonais en utilisant la traduction automatique.
