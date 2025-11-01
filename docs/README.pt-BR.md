# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![Crowdin](https://badges.crowdin.net/windowtranslator/localized.svg)](https://crowdin.com/project/windowtranslator)
[![Microsoft Store](https://get.microsoft.com/images/pt-br%20dark.svg)](https://apps.microsoft.com/detail/9pjd2fdzqxm3?referrer=appbadge&mode=direct)

WindowTranslator é uma ferramenta para traduzir as janelas de aplicativos do Windows.

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md) | [VI](./README.vi.md) | [HI](./README.hi.md) | [MS](./README.ms.md) | [ID](./README.id.md) | [PT-BR](./README.pt-BR.md)

## Índice
- [ WindowTranslator](#-windowtranslator)
  - [Índice](#Índice)
  - [Download](#Download)
    - [Versão Microsoft Store ](#microsoft-store-)
    - [Versão para instalação](#Versão para instalação)
    - [Versão portátil](#Versão portátil)
  - [Como usar](#Como usar)
    - [Bergamot ](#bergamot-)
  - [Outras funções](#Outras funções)

## Download
### Versão Microsoft Store ![Recomendado](https://img.shields.io/badge/%E3%82%AA%E3%82%B9%E3%82%B9%E3%83%A1-brightgreen)

[Microsoft Store](https://apps.microsoft.com/detail/9pjd2fdzqxm3?referrer=appbadge&mode=direct)para instalar.
Funciona mesmo em ambientes onde o .NET não está instalado.

### Versão para instalação

[página de releases do GitHub](https://github.com/Freeesia/WindowTranslator/releases/latest)de `WindowTranslator-(versão).msi`e execute para instalar.  
Vídeo tutorial de instalação aqui⬇️  
[![Vídeo de instalação](https://github.com/user-attachments/assets/b5babc02-715b-43bc-ba97-f23078ffd39b)](https://youtu.be/wvcbCLA9chQ?t=7)

### Versão portátil

[página de releases do GitHub](https://github.com/Freeesia/WindowTranslator/releases/latest)de arquivo zip e extraia para qualquer pasta.  
- `WindowTranslator-(versão).zip` : Requer ambiente .NET  
- `WindowTranslator-full-(versão).zip` : Independente do .NET

## Como usar

### Bergamot ![Padrão](https://img.shields.io/badge/Padrão-brightgreen)

1. `WindowTranslator.exe`e clique no botão de tradução.  
   ![Botão de Tradução](images/translate.png)
2. Selecione a janela do aplicativo que deseja traduzir e clique no botão "OK".  
   ![Seleção de Janela](images/select.png)
3. Na aba "Configurações Gerais", em "Configuração de Idioma", selecione o idioma de origem e destino.  
   ![Configuração de Idioma](images/language.png)
4. Após concluir as configurações, clique no botão "OK" para fechar a tela de configurações.  
   > A instalação da função OCR pode ser necessária.
   > Siga as instruções para instalar.
5. Após alguns instantes, o resultado da tradução será exibido como sobreposição.  
   ![Resultado da Tradução](images/result.png)

> [!NOTE]
> Vários módulos de tradução estão disponíveis no WindowTranslator.  
> O Google Tradutor tem um limite baixo de texto traduzível. Se você usa com frequência, considere usar outros módulos.  
> A lista de módulos de tradução disponíveis pode ser vista no vídeo abaixo ou na [documentação](https://wt.studiofreesia.com/TranslateModule).
> 
> |                |                                                              Vídeo de uso                                                               | Vantagens                    | Desvantagens                        |
> | :------------: | :-----------------------------------------------------------------------------------------------------------------------------------: | :-------------------------- | :-------------------------------- |
> |   Bergamot     | | Totalmente gratuito<br/>Sem limite de tradução<br/>Tradução rápida | Menor precisão de tradução<br/>Requer mais de 1GB de memória livre |
> |   Google Tradutor   | [![Vídeo de configuração do Google Tradutor](https://github.com/user-attachments/assets/bbf45370-0387-47e1-b690-3183f37e06d2)](https://youtu.be/83A8T890N5M)  | Totalmente gratuito | Limite de tradução baixo<br/>Menor precisão de tradução |
> |     DeepL      |   [![Vídeo de configuração do DeepL](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0)   | Grande cota gratuita<br/>Tradução rápida | |
> |     Gemini     | [![Vídeo de configuração do Google AI](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I) | Alta precisão de tradução | Requer pequeno pagamento |
> |    ChatGPT     | TBD | Alta precisão de tradução | Requer pequeno pagamento |
> |  LLM Local   | TBD | O serviço em si é gratuito | Requer PC de alto desempenho |

## Outras funções

Além dos módulos de tradução, o WindowTranslator possui várias outras funções.  
Para aqueles que desejam usar mais recursos, consulte a[Wiki](https://github.com/Freeesia/WindowTranslator/wiki) .

---
[Política de Privacidade](PrivacyPolicy.md)
