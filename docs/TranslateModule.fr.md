# Module de traduction

Dans WindowTranslator, vous pouvez choisir et utiliser plusieurs modules de traduction.  
Chaque module a ses caractéristiques, et en sélectionnant le module approprié selon l'utilisation, vous pouvez utiliser la traduction plus confortablement.

## Bergamot ![Défaut](https://img.shields.io/badge/Défaut-brightgreen)

Un module de traduction automatique qui fonctionne hors ligne.

### Avantages
- **Complètement gratuit**: Aucun frais
- **Pas de limite de traduction**: Traduisez autant que vous voulez
- **Rapide**: Traitement local pour une traduction rapide
- **Confidentialité**: Pas de connexion Internet nécessaire, les données ne sont pas envoyées à l'extérieur
- **Stabilité**: Non affecté par le réseau

### Inconvénients
- **Précision de traduction**: La précision de traduction est inférieure par rapport aux services cloud
- **Utilisation mémoire**: Utilise une certaine quantité de mémoire pour le traitement de traduction
- **Langues prises en charge**: Seules certaines paires de langues sont prises en charge

### Scénarios d'utilisation recommandés
- Lorsque vous voulez utiliser gratuitement
- Utilisation dans un environnement hors ligne
- Lorsque la confidentialité est importante
- Lorsque vous effectuez des traductions fréquentes

---

## Google Traduction

Un module de traduction utilisant le service de traduction de Google.

### Avantages
- **Complètement gratuit**: Peut être utilisé sans clé API
- **Support multilingue**: Prend en charge de nombreuses paires de langues
- **Facile**: Aucune configuration spéciale requise

### Inconvénients
- **Limite de traduction**: Limite sur le nombre de caractères pouvant être traduits par jour
- **Précision de traduction**: La précision peut être inférieure par rapport aux autres services payants
- **Vitesse**: Affecté par le réseau
- **Stabilité**: Peut soudainement devenir indisponible en raison des restrictions d'utilisation

### Scénarios d'utilisation recommandés
- Utilisation à basse fréquence
- Lorsque vous voulez commencer immédiatement
- Lorsque vous voulez traduire diverses paires de langues

---

## DeepL

Un module utilisant le service de traduction DeepL, connu pour des traductions de haute qualité.

### Avantages
- **Haute précision**: Traductions naturelles et de haute qualité
- **Offre gratuite substantielle**: Jusqu'à 500 000 caractères gratuits par mois (API gratuite)
- **Rapide**: Traitement de traduction rapide
- **Support de glossaire**: Maintenez la cohérence de traduction avec des glossaires

### Inconvénients
- **Inscription API requise**: L'inscription à l'API DeepL et la configuration de la clé API sont nécessaires
- **Limite gratuite**: Si l'offre gratuite est dépassée, une mise à niveau vers un plan payant est nécessaire
- **Langues prises en charge**: Les langues prises en charge sont plus limitées que Google

### Scénarios d'utilisation recommandés
- Lorsque vous recherchez des traductions de haute qualité
- Utilisation à fréquence moyenne

---

## Google AI (Gemini)

Un module de traduction utilisant la dernière technologie IA de Google.

### Avantages
- **Plus haute précision**: Traductions de très haute qualité qui comprennent le contexte
- **Flexibilité**: Personnalisez les prompts pour ajuster le style de traduction
- **Support de glossaire**: Maintenez la cohérence de traduction avec des glossaires

### Inconvénients
- **Clé API requise**: La clé API doit être obtenue et configurée depuis Google AI Studio
- **Paiement à l'usage**: Frais basés sur l'utilisation (mais petit montant)
- **Vitesse**: Temps de traitement plus long car basé sur LLM

### Scénarios d'utilisation recommandés
- Lorsque vous recherchez des traductions de la plus haute qualité
- Lorsqu'un style de traduction personnalisé est nécessaire
- Lorsque des traductions sensibles au contexte sont nécessaires

---

## API ChatGPT (OU LLM local)

Un module de traduction utilisant l'API ChatGPT ou un LLM local.

### Avantages
- **Plus haute précision**: Traductions de haute qualité par grands modèles de langage
- **Flexibilité**: Personnalisez les prompts pour ajuster le style de traduction
- **Support de glossaire**: Maintenez la cohérence de traduction avec des glossaires
- **Support LLM local**: Possibilité d'utiliser votre propre serveur LLM

### Inconvénients
- **Clé API requise**: La configuration de la clé API de chaque service est nécessaire (sauf LLM local)
- **Paiement à l'usage**: Frais basés sur l'utilisation (sauf LLM local)
- **Vitesse**: Temps de traitement plus long
- **Exigences LLM local**: PC haute performance nécessaire pour exécuter votre propre LLM

### Scénarios d'utilisation recommandés
- Lorsque vous recherchez des traductions de la plus haute qualité
- Lorsqu'un style de traduction personnalisé est nécessaire
- Lorsque vous voulez des traductions de haute qualité tout en valorisant la confidentialité (LLM local)

---

## PLaMo

Un module de traduction utilisant un LLM local spécialisé pour le japonais.

### Avantages
- **Spécialisé japonais**: Optimisé pour les traductions japonaises
- **Complètement gratuit**: Modèle open source sans frais
- **Confidentialité**: Fonctionne localement, les données ne sont pas envoyées à l'extérieur
- **Hors ligne**: Pas de connexion Internet nécessaire

### Inconvénients
- **Exigences haute performance**: PC haute performance avec GPU nécessaire
- **Utilisation mémoire**: Grande quantité de mémoire nécessaire (8 Go ou plus recommandé)
- **Vitesse**: Temps de traitement plus long sans GPU

### Scénarios d'utilisation recommandés
- Lorsque vous possédez un PC haute performance
- Lorsque la confidentialité est la plus importante
- Lorsque vous valorisez la qualité de traduction japonaise

---

## Comment choisir un module

| Objectif                 | Module recommandé                            |
| -------------------- | ----------------------------------------- |
| Commencer immédiatement   | **Bergamot** ou **Google Traduction**        |
| Traductions de la plus haute qualité | **Google AI** ou **API ChatGPT**      |
| Réduire les coûts     | **Bergamot** ou **DeepL (dans l'offre gratuite)** |
| Priorité à la confidentialité     | **Bergamot** ou **PLaMo**             |
| Utilisation haute fréquence         | **Bergamot** ou **DeepL**             |
