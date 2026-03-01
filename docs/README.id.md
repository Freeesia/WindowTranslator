# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![Crowdin](https://badges.crowdin.net/windowtranslator/localized.svg)](https://crowdin.com/project/windowtranslator)
[![Microsoft Store](https://get.microsoft.com/images/en-us%20dark.svg)](https://apps.microsoft.com/detail/9pjd2fdzqxm3?referrer=appbadge&mode=direct)

WindowTranslator adalah alat untuk menerjemahkan jendela aplikasi di Windows.

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md) | [VI](./README.vi.md) | [HI](./README.hi.md) | [MS](./README.ms.md) | [ID](./README.id.md) | [PT-BR](./README.pt-BR.md) | [FR](./README.fr.md) | [ES](./README.es.md) | [AR](./README.ar.md) | [TR](./README.tr.md) | [TH](./README.th.md) | [RU](./README.ru.md) | [FIL](./README.fil.md) | [PL](./README.pl.md) | [FA](./README.fa.md)

## Daftar Isi
- [ WindowTranslator](#-windowtranslator)
  - [Daftar Isi](#daftar-isi)
  - [Unduh](#unduh)
    - [Versi Microsoft Store ](#versi-microsoft-store-)
    - [Versi Installer](#versi-installer)
    - [Versi Portabel](#versi-portabel)
  - [Cara Penggunaan](#cara-penggunaan)
    - [Bergamot ](#bergamot-)
  - [Fitur Lainnya](#fitur-lainnya)

## Unduh
### Versi Microsoft Store ![Direkomendasikan](https://img.shields.io/badge/Direkomendasikan-brightgreen)

Instal dari [Microsoft Store](https://apps.microsoft.com/detail/9pjd2fdzqxm3?referrer=appbadge&mode=direct).
Berfungsi bahkan dalam lingkungan di mana .NET tidak terinstal.

### Versi Installer

Unduh `WindowTranslator-(versi).msi` dari [halaman rilis GitHub](https://github.com/Freeesia/WindowTranslator/releases/latest) dan jalankan untuk menginstal.  
Video tutorial instalasi ada di sini⬇️  
[![Video tutorial instalasi](https://github.com/user-attachments/assets/b5babc02-715b-43bc-ba97-f23078ffd39b)](https://youtu.be/wvcbCLA9chQ?t=7)

### Versi Portabel

Unduh file zip dari [halaman rilis GitHub](https://github.com/Freeesia/WindowTranslator/releases/latest) dan ekstrak ke folder mana pun.  
- `WindowTranslator-(versi).zip` : Memerlukan lingkungan .NET  
- `WindowTranslator-full-(versi).zip` : Independen dari .NET

## Cara Penggunaan

### Bergamot ![Default](https://img.shields.io/badge/Default-brightgreen)

1. Jalankan `WindowTranslator.exe` dan klik tombol terjemah.  
   ![Tombol Terjemah](images/translate.png)
2. Pilih jendela aplikasi yang ingin Anda terjemahkan dan klik tombol "OK".  
   ![Pemilihan Jendela](images/select.png)
3. Dari tab "Pengaturan Umum", pilih bahasa sumber dan target dalam "Pengaturan Bahasa".  
   ![Pengaturan Bahasa](images/language.png)
4. Setelah menyelesaikan pengaturan, klik tombol "OK" untuk menutup layar pengaturan.  
   > Instalasi fungsi OCR mungkin diperlukan.
   > Silakan ikuti instruksi untuk menginstal.
5. Setelah beberapa saat, hasil terjemahan akan ditampilkan sebagai hamparan.  
   ![Hasil Terjemahan](images/result.png)

> [!NOTE]
> Berbagai modul terjemahan tersedia di WindowTranslator.  
> Google Terjemahan memiliki batas rendah pada jumlah teks yang dapat diterjemahkan. Jika Anda sering menggunakannya, pertimbangkan untuk menggunakan modul lain.  
> Anda dapat memeriksa daftar modul terjemahan yang tersedia dalam video di bawah atau di [Dokumentasi](https://wt.studiofreesia.com/TranslateModule.en).
> 
> |                |                                                           Video Cara Penggunaan                                                            | Keuntungan                    | Kerugian                        |
> | :------------: | :-----------------------------------------------------------------------------------------------------------------------------------: | :---------------------------- | :----------------------------------- |
> |   Bergamot     | | Sepenuhnya gratis<br/>Tidak ada batas terjemahan<br/>Terjemahan cepat | Akurasi terjemahan lebih rendah<br/>Memerlukan lebih dari 1GB memori bebas |
> |   Google Terjemahan   | [![Video Setup Google Terjemahan](https://github.com/user-attachments/assets/bbf45370-0387-47e1-b690-3183f37e06d2)](https://youtu.be/83A8T890N5M)  | Sepenuhnya gratis | Batas terjemahan rendah<br/>Akurasi terjemahan lebih rendah |
> |     DeepL      |   [![Video Setup DeepL](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0)   | Tingkat gratis yang besar<br/>Terjemahan cepat | |
> |     Gemini     | [![Video Setup Google AI](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I) | Akurasi terjemahan tinggi | Biaya kecil diperlukan |
> |    ChatGPT     | TBD | Akurasi terjemahan tinggi | Biaya kecil diperlukan |
> | LLM Lokal | TBD | Layanan itu sendiri gratis | PC spesifikasi tinggi diperlukan |

## Fitur Lainnya

Selain modul terjemahan, WindowTranslator memiliki berbagai fitur.  
Jika Anda ingin mempelajari lebih lanjut, silakan periksa [Wiki](https://github.com/Freeesia/WindowTranslator/wiki).

---
[Kebijakan Privasi](PrivacyPolicy.md)

Dokumen ini diterjemahkan dari Bahasa Jepang menggunakan terjemahan mesin.
