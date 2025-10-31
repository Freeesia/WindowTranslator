# OCRMódulo

No WindowTranslator, você pode selecionar entre vários módulos de OCR para reconhecer texto na tela.  
Cada módulo possui características próprias, e selecionar o módulo apropriado para o seu caso de uso permitirá um reconhecimento de texto mais preciso.

## Novo reconhecimento de caracteres do Windows (Beta) ![Padrão](https://img.shields.io/badge/Padrão-brightgreen)

Módulo OCR local fornecido pela Microsoft.

### Vantagens
- **Precisão de reconhecimento**: Possui a maior precisão de reconhecimento
- **Rápido**: Velocidade de processamento muito rápida

### Desvantagens
- **Uso de memória**: Pode usar mais de 1GB de memória apenas para processamento de reconhecimento
- **Ambiente de operação**: Pode não funcionar em alguns ambientes (Windows 10 ou posterior recomendado)

---

## Reconhecimento de caracteres padrão do Windows

Motor OCR padrão incluído no Windows 10 e posterior.

### Vantagens
- **Uso de memória**: Leve e com baixo uso de memória
- **Ambiente de operação**: Amplamente disponível no Windows 10 e posterior

### Desvantagens
- **Precisão de reconhecimento**: Pode ter dificuldade com fontes complexas ou texto manuscrito
- **Configuração**: Pode ser necessária a instalação manual de dados de idioma

---

## Tesseract OCR

Motor OCR de código aberto.

### Vantagens
- **Suporte multilíngue**: Suporta mais de 100 idiomas
- **Estabilidade**: Motor confiável com longa história

### Desvantagens
- **Precisão de reconhecimento**: A precisão pode ser inferior comparada a outros OCRs

---

## Como escolher um módulo

Selecione os módulos na seguinte ordem de maior precisão de reconhecimento.

1. Novo reconhecimento de caracteres do Windows (Beta)
2. Reconhecimento de caracteres padrão do Windows
3. Tesseract OCR
