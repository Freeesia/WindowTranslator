# Module OCR

Dans WindowTranslator, vous pouvez choisir parmi plusieurs modules OCR pour reconnaître le texte à l'écran.  
Chaque module a ses caractéristiques, et en sélectionnant le module approprié selon l'utilisation, une reconnaissance de texte plus précise est possible.

## Nouvelle reconnaissance Windows (bêta) ![Défaut](https://img.shields.io/badge/Défaut-brightgreen)

Un module OCR local fourni par Microsoft.

### Avantages
- **Précision de reconnaissance**: La plus haute précision de reconnaissance
- **Rapide**: Vitesse de traitement très rapide

### Inconvénients
- **Utilisation mémoire**: Le traitement de reconnaissance peut utiliser plus de 1 Go de mémoire
- **Environnement d'exploitation**: Peut ne pas fonctionner dans certains environnements (Windows 10 ou supérieur recommandé)

---

## Reconnaissance de caractères Windows standard

Un moteur OCR intégré à Windows 10 et versions ultérieures.

### Avantages
- **Utilisation mémoire**: Léger avec une faible utilisation de mémoire
- **Environnement d'exploitation**: Largement disponible sur Windows 10 et versions ultérieures

### Inconvénients
- **Précision de reconnaissance**: Peut être faible pour les polices complexes ou l'écriture manuscrite
- **Configuration**: L'installation manuelle des données linguistiques peut être nécessaire

---

## Tesseract OCR

Un moteur OCR open source.

### Avantages
- **Support multilingue**: Prend en charge plus de 100 langues
- **Stabilité**: Un moteur fiable avec une longue histoire

### Inconvénients
- **Précision de reconnaissance**: La précision peut être inférieure par rapport aux autres OCR

---

## Comment choisir un module

Veuillez sélectionner un module fonctionnel dans l'ordre suivant de haute précision de reconnaissance.

1. Nouvelle reconnaissance Windows (bêta)
2. Reconnaissance de caractères Windows standard
3. Tesseract OCR
