# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![Crowdin](https://badges.crowdin.net/windowtranslator/localized.svg)](https://crowdin.com/project/windowtranslator)
[![Microsoft Store](https://get.microsoft.com/images/pt-br%20dark.svg)](https://apps.microsoft.com/detail/9pjd2fdzqxm3?referrer=appbadge&mode=direct)

O WindowTranslator é uma ferramenta para traduzir as janelas dos aplicativos do Windows.

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md) | [VI](./README.vi.md) | [HI](./README.hi.md) | [MS](./README.ms.md) | [PT-BR](./README.pt-BR.md)

## Índice
- [ WindowTranslator](#-windowtranslator)
  - [Índice](#daftar-isi)
  - [Baixe](#unduh)
    - [Versão da Microsoft Store ](#versi-microsoft-store-)
    - [Versão de Instalação](#versi-installer)
    - [Versão Portátil](#versi-portabel)
  - [Como usar](#cara-penggunaan)
    - [Bergamot ](#bergamot-)
  - [Outras funcionalidades](#fitur-lainnya)

## Baixe
### Versão da Microsoft Store ![Recomendado](https://img.shields.io/badge/Recomendado-brightgreen)

Instalar da [Microsoft Store](https://apps.microsoft.com/detail/9pjd2fdzqxm3?referrer=appbadge&mode=direct).
Funciona mesmo em ambientes onde o .NET não está instalado.

### Versão de Instalação

Baixe `WindowTranslator-(versi).msi` de [halaman rilis GitHub](https://github.com/Freeesia/WindowTranslator/releases/latest) e execute para instalar.  
Vídeo tutorial de instalação aqui⬇️  
[![Video tutorial instalasi](https://github.com/user-attachments/assets/b5babc02-715b-43bc-ba97-f23078ffd39b)](https://youtu.be/wvcbCLA9chQ?t=7)

### Versão Portátil

Baixe file zip de [halaman rilis GitHub](https://github.com/Freeesia/WindowTranslator/releases/latest) e extraia para qualquer pasta.  
- `WindowTranslator-(versi).zip` : Requer ambiente .NET  
- `WindowTranslator-full-(versi).zip` : Independen de .NET

## Como usar

### Bergamot ![Padrão](https://img.shields.io/badge/Padrão-brightgreen)

1. Execute `WindowTranslator.exe` e clique no botão de tradução.  
   ![Botão de Tradução](images/translate.png)
2. Selecione a janela do aplicativo que deseja traduzir e clique no botão "OK".  
   ![Seleção de Janela](images/select.png)
3. Dari tab "Pengaturan Umum", pilih bahasa sumber dan target dalam "Pengaturan Bahasa".  
   ![Pengaturan Bahasa](images/language.png)
4. Setelah menyelesaikan pengaturan, klik tombol "OK" untuk menutup layar pengaturan.  
   > Instalasi fungsi OCR mungkin diperlukan.
   > Silakan ikuti instruksi untuk menginstal.
5. Setelah beberapa saat, hasil terjemahan akan ditampilkan sebagai hamparan.  
   ![Hasil Tradução](images/result.png)

> [!NOTE]
> Berbagai modul terjemahan tersedia di WindowTranslator.  
> Google Tradução memiliki batas rendah pada jumlah teks yang dapat diterjemahkan. Jika Anda sering menggunakannya, pertimbangkan untuk menggunakan modul lain.  
> Anda dapat memeriksa daftar modul terjemahan yang tersedia dalam video di bawah atau di [Dokumentasi](https://wt.studiofreesia.com/TranslateModule.en).
> 
> |                |                                                           Video Como usar                                                            | Keuntungan                    | Kerugian                        |
> | :------------: | :-----------------------------------------------------------------------------------------------------------------------------------: | :---------------------------- | :----------------------------------- |
> |   Bergamot     | | Sepenuhnya gratis<br/>Tidak ada batas terjemahan<br/>Tradução cepat | Akurasi terjemahan lebih rendah<br/>Memerlukan lebih de 1GB memori bebas |
> |   Google Tradução   | [![Video Setup Google Tradução](https://github.com/user-attachments/assets/bbf45370-0387-47e1-b690-3183f37e06d2)](https://youtu.be/83A8T890N5M)  | Sepenuhnya gratis | Batas terjemahan rendah<br/>Akurasi terjemahan lebih rendah |
> |     DeepL      |   [![Video Setup DeepL](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0)   | Tingkat gratis yang besar<br/>Tradução cepat | |
> |     Gemini     | [![Video Setup Google AI](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I) | Akurasi terjemahan tinggi | Biaya kecil diperlukan |
> |    ChatGPT     | TBD | Akurasi terjemahan tinggi | Biaya kecil diperlukan |
> | LLM Lokal | TBD | Layanan itu sendiri gratis | PC spesifikasi tinggi diperlukan |

## Outras funcionalidades

Selain modul terjemahan, WindowTranslator memiliki berbagai fitur.  
Jika Anda ingin mempelajari lebih lanjut, silakan periksa [Wiki](https://github.com/Freeesia/WindowTranslator/wiki).

---
[Política de Privacidade](PrivacyPolicy.md)

Dokumen ini diterjemahkan de Bahasa Jepang menggunakan terjemahan mesin.
