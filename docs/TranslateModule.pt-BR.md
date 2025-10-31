# TraduçãoMódulo

No WindowTranslator, você pode selecionar e usar vários módulos de tradução.  
Cada módulo possui características próprias, e ao selecionar o módulo apropriado para o seu caso de uso, você pode utilizar as traduções de forma mais conveniente.

## Bergamot ![Padrão](https://img.shields.io/badge/Padrão-brightgreen)

Módulo de tradução automática que funciona offline.

### Vantagens
- **Totalmente gratuito**: Sem custos
- **Sem limite de tradução**: Traduza quantas vezes quiser
- **Rápido**: Tradução rápida pois é processado localmente
- **Privacidade**: Não requer conexão à internet, dados não são enviados externamente
- **Estabilidade**: Não afetado pela rede

### Desvantagens
- **Precisão da tradução**: A precisão da tradução é inferior comparada aos serviços baseados em nuvem
- **Uso de memória**: Usa uma certa quantidade de memória para processamento de tradução
- **Idiomas suportados**: Apenas alguns pares de idiomas são suportados

### Cenários de uso recomendados
- Para uso gratuito
- Para uso em ambiente offline
- Para quem prioriza privacidade
- Para tradução frequente

---

## Google Tradutor

Módulo de tradução que utiliza o serviço de tradução do Google.

### Vantagens
- **Totalmente gratuito**: Pode ser usado sem chave de API
- **Suporte multilíngue**: Suporta muitos pares de idiomas
- **Fácil**: Não requer configuração especial

### Desvantagens
- **Limite de tradução**: Há um limite no número de caracteres que podem ser traduzidos por dia
- **Precisão da tradução**: A precisão pode ser inferior comparada a outros serviços pagos
- **Velocidade**: Afetado pela rede
- **Estabilidade**: Pode ficar indisponível repentinamente devido a limites de uso

### Cenários de uso recomendados
- Para uso de baixa frequência
- Para começar a usar imediatamente
- Para traduzir com diversos pares de idiomas

---

## DeepL

Módulo que utiliza o serviço de tradução DeepL, conhecido por traduções de alta qualidade.

### Vantagens
- **Alta precisão**: Obtenha traduções naturais e de alta qualidade
- **Ampla cota gratuita**: Use gratuitamente até 500.000 caracteres por mês (API gratuita)
- **Rápido**: Processamento de tradução rápido
- **Suporte a glossário**: Use glossários para manter consistência na tradução

### Desvantagens
- **Registro de API necessário**: É necessário registrar-se na API DeepL e configurar a chave de API
- **Limite de cota gratuita**: Se exceder a cota gratuita, é necessário migrar para um plano pago
- **Idiomas suportados**: Os idiomas suportados são limitados comparados ao Google e outros

### Cenários de uso recomendados
- Para quem busca traduções de alta qualidade
- Para uso de frequência moderada

---

## Google AI (Gemini)

Módulo de tradução que utiliza a mais recente tecnologia de IA do Google.

### Vantagens
- **Máxima precisão**: Permite traduções de altíssima qualidade com compreensão de contexto
- **Flexibilidade**: Customize prompts para ajustar o estilo de tradução
- **Suporte a glossário**: Use glossários para manter consistência na tradução

### Desvantagens
- **Chave de API necessária**: É necessário obter e configurar uma chave de API no Google AI Studio
- **Cobrança por uso**: Haverá cobrança de acordo com o uso (mas pequena)
- **Velocidade**: Como é baseado em LLM, leva mais tempo de processamento que outros módulos

### Cenários de uso recomendados
- Para quem busca traduções da mais alta qualidade
- Quando é necessário um estilo de tradução customizado
- Quando é necessária tradução que valoriza o contexto

---

## ChatGPT API (OR LLM Local)

Módulo de tradução que utiliza ChatGPT API ou LLM local.

### Vantagens
- **Máxima precisão**: Traduções de alta qualidade por modelos de linguagem em grande escala
- **Flexibilidade**: Customize prompts para ajustar o estilo de tradução
- **Suporte a glossário**: Use glossários para manter consistência na tradução
- **Suporte a LLM local**: Também pode usar seu próprio servidor LLM

### Desvantagens
- **Chave de API necessária**: É necessário configurar a chave de API de cada serviço (exceto LLM local)
- **Cobrança por uso**: Haverá cobrança de acordo com o uso (exceto LLM local)
- **Velocidade**: Tempo de processamento mais longo
- **Requisitos do LLM local**: Para executar seu próprio LLM, é necessário um PC de alto desempenho

### Cenários de uso recomendados
- Para quem busca traduções da mais alta qualidade
- Quando é necessário um estilo de tradução customizado
- Para quem deseja traduções de alta qualidade priorizando privacidade (LLM local)

---

## PLaMo

Módulo de tradução que utiliza LLM local especializado em japonês.

### Vantagens
- **Especializado em japonês**: Otimizado para tradução de japonês
- **Totalmente gratuito**: Modelo de código aberto sem custos
- **Privacidade**: Como opera localmente, dados não são enviados externamente
- **Offline**: Não requer conexão à internet

### Desvantagens
- **Requisitos de alto desempenho**: É necessário um PC de alto desempenho incluindo GPU
- **Uso de memória**: Requer grande quantidade de memória (8GB ou mais recomendado)
- **Velocidade**: Sem GPU, o processamento leva mais tempo

### Cenários de uso recomendados
- Para quem possui um PC de alto desempenho
- Para quem prioriza ao máximo a privacidade
- Para quem prioriza a qualidade da tradução de japonês

---

## Como escolher um módulo

| Objetivo                 | Módulo recomendado                            |
| -------------------- | ----------------------------------------- |
| Para começar a usar imediatamente   | **Bergamot** ou **Google Tradutor**        |
| Quando é necessária tradução da mais alta qualidade | **Google AI** ou **ChatGPT API**      |
| Para manter os custos baixos     | **Bergamot** ou **DeepL (cota gratuita)** |
| Priorizar privacidade     | **Bergamot** ou **PLaMo**             |
| Para uso frequente         | **Bergamot** ou **DeepL**             |
