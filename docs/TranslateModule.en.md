# Translation Modules

WindowTranslator allows you to choose from multiple translation modules.  
Each module has its own characteristics, and selecting the appropriate module for your use case will help you enjoy a more comfortable translation experience.

## Bergamot ![Default](https://img.shields.io/badge/Default-brightgreen)

An offline machine translation module.

### Advantages
- **Completely Free**: No fees whatsoever
- **No Translation Limit**: Translate as many times as you want
- **Fast**: Quick translations as it processes locally
- **Privacy**: No internet connection required, data is not sent externally
- **Stability**: Not affected by network conditions

### Disadvantages
- **Translation Accuracy**: Lower accuracy compared to cloud-based services
- **Memory Usage**: Requires 1GB or more of free memory
- **Language Support**: Only certain language pairs are supported

### Recommended Use Cases
- Unstable internet connection environments
- When privacy is a priority
- High-frequency translation usage

---

## Google Translate

A translation module using Google's translation service.

### Advantages
- **Completely Free**: Can be used without an API key
- **Multilingual Support**: Supports many language pairs
- **Easy**: No special configuration required

### Disadvantages
- **Translation Limit**: Limited number of characters that can be translated per day
- **Translation Accuracy**: Lower accuracy compared to other paid services in some cases
- **Speed**: Affected by network conditions
- **Stability**: May suddenly become unavailable due to usage limits

### Recommended Use Cases
- Low-frequency usage
- When you want to start using immediately
- When you need to translate various language pairs

---

## DeepL

A module using DeepL's translation service, known for high-quality translations.

### Advantages
- **High Accuracy**: Natural and high-quality translations
- **Generous Free Tier**: Up to 500,000 characters per month for free (Free API)
- **Fast**: Quick translation processing
- **Business Use**: High-quality translations even for specialized documents

### Disadvantages
- **API Registration Required**: DeepL API registration and API key setup necessary
- **Free Tier Limit**: Migration to paid plan required when free tier is exceeded
- **Language Support**: More limited language support compared to Google

### Recommended Use Cases
- When high-quality translations are required
- Business document translation
- Moderate frequency usage

---

## Google AI (Gemini)

A translation module leveraging Google's latest AI technology.

### Advantages
- **Highest Accuracy**: Very high-quality translations with contextual understanding
- **Flexible Expressions**: Translated with natural phrasing
- **Technical Terminology**: Supports technical documents, games, and specialized content
- **Free Tier**: Can be used for free up to a certain amount

### Disadvantages
- **API Key Required**: Registration with Google Cloud Platform and API key setup necessary
- **Pay-as-you-go**: Charges apply after exceeding free tier (though minimal)
- **Speed**: Takes longer to process than other modules due to LLM base

### Recommended Use Cases
- When the highest quality translations are required
- Translation of specialized content like games or technical documents
- When context-aware translation is necessary

---

## LLM Plugin (ChatGPT/Claude/Local LLM)

A translation module using OpenAI, Anthropic, or local LLMs.

### Advantages
- **Highest Accuracy**: High-quality translations using large language models
- **Flexibility**: Customize prompts to adjust translation style
- **Context Understanding**: Translations considering longer context
- **Local LLM Support**: Can use your own LLM server

### Disadvantages
- **API Key Required**: API key setup for each service necessary (except local LLM)
- **Pay-as-you-go**: Charges based on usage (except local LLM)
- **Speed**: Longer processing time
- **Local LLM Requirements**: High-spec PC required for running your own LLM

### Recommended Use Cases
- When the highest quality translations are required
- When customized translation style is needed
- When prioritizing privacy while wanting high-quality translations (local LLM)

---

## PLaMo

A translation module using local LLM specialized for Japanese.

### Advantages
- **Japanese-Specialized**: Optimized for Japanese translation
- **Completely Free**: No fees with open-source model
- **Privacy**: Runs locally, data is not sent externally
- **Offline**: No internet connection required

### Disadvantages
- **High-Spec Requirements**: Requires high-performance PC including GPU
- **Setup**: Complex initial configuration
- **Memory Usage**: Requires large amount of memory (8GB+ recommended)
- **Speed**: Takes time to process without GPU

### Recommended Use Cases
- When you own a high-performance PC
- When privacy is the top priority
- When prioritizing Japanese translation quality

---

## Choosing a Module

| Purpose | Recommended Module |
|---------|-------------------|
| Start using immediately | **Bergamot** or **Google Translate** |
| Highest quality translation needed | **Google AI** or **LLM Plugin** |
| Want to keep costs down | **Bergamot** or **DeepL (within free tier)** |
| Privacy priority | **Bergamot** or **PLaMo** |
| High-frequency usage | **Bergamot** or **DeepL** |
| Business use | **DeepL** or **Google AI** |
