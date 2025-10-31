# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![Crowdin](https://badges.crowdin.net/windowtranslator/localized.svg)](https://crowdin.com/project/windowtranslator)
[![Microsoft Store](https://get.microsoft.com/images/en-us%20dark.svg)](https://apps.microsoft.com/detail/9pjd2fdzqxm3?referrer=appbadge&mode=direct)

WindowTranslator adalah alat untuk menterjemah tetingkap aplikasi pada Windows.

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md) | [VI](./README.vi.md) | [HI](./README.hi.md) | [MS](./README.ms.md) | [ID](./README.id.md)

## Kandungan
- [ WindowTranslator](#-windowtranslator)
  - [Kandungan](#kandungan)
  - [Muat Turun](#muat-turun)
    - [Versi Microsoft Store ](#versi-microsoft-store-)
    - [Versi Pemasang](#versi-pemasang)
    - [Versi Mudah Alih](#versi-mudah-alih)
  - [Cara Penggunaan](#cara-penggunaan)
    - [Bergamot ](#bergamot-)
  - [Ciri-ciri Lain](#ciri-ciri-lain)

## Muat Turun
### Versi Microsoft Store ![Disyorkan](https://img.shields.io/badge/Disyorkan-brightgreen)

Pasang dari [Microsoft Store](https://apps.microsoft.com/detail/9pjd2fdzqxm3?referrer=appbadge&mode=direct).
Berfungsi walaupun dalam persekitaran di mana .NET tidak dipasang.

### Versi Pemasang

Muat turun `WindowTranslator-(versi).msi` dari [halaman keluaran GitHub](https://github.com/Freeesia/WindowTranslator/releases/latest) dan jalankan untuk memasang.  
Video tutorial pemasangan ada di sini⬇️  
[![Video tutorial pemasangan](https://github.com/user-attachments/assets/b5babc02-715b-43bc-ba97-f23078ffd39b)](https://youtu.be/wvcbCLA9chQ?t=7)

### Versi Mudah Alih

Muat turun fail zip dari [halaman keluaran GitHub](https://github.com/Freeesia/WindowTranslator/releases/latest) dan ekstrak ke mana-mana folder.  
- `WindowTranslator-(versi).zip` : Memerlukan persekitaran .NET  
- `WindowTranslator-full-(versi).zip` : Bebas daripada .NET

## Cara Penggunaan

### Bergamot ![Lalai](https://img.shields.io/badge/Lalai-brightgreen)

1. Lancarkan `WindowTranslator.exe` dan klik butang terjemah.  
   ![Butang Terjemah](images/translate.png)
2. Pilih tetingkap aplikasi yang anda mahu terjemahkan dan klik butang "OK".  
   ![Pemilihan Tetingkap](images/select.png)
3. Dari tab "Tetapan Umum", pilih bahasa sumber dan sasaran dalam "Tetapan Bahasa".  
   ![Tetapan Bahasa](images/language.png)
4. Selepas menyelesaikan tetapan, klik butang "OK" untuk menutup skrin tetapan.  
   > Pemasangan fungsi OCR mungkin diperlukan.
   > Sila ikuti arahan untuk memasang.
5. Selepas beberapa ketika, hasil terjemahan akan dipaparkan sebagai hamparan.  
   ![Hasil Terjemahan](images/result.png)

> [!NOTE]
> Pelbagai modul terjemahan tersedia dalam WindowTranslator.  
> Google Terjemahan mempunyai had rendah pada jumlah teks yang boleh diterjemahkan. Jika anda menggunakannya dengan kerap, pertimbangkan untuk menggunakan modul lain.  
> Anda boleh menyemak senarai modul terjemahan yang tersedia dalam video di bawah atau di [Dokumentasi](https://wt.studiofreesia.com/TranslateModule.en).
> 
> |                |                                                           Video Cara Penggunaan                                                            | Kelebihan                    | Kekurangan                        |
> | :------------: | :-----------------------------------------------------------------------------------------------------------------------------------: | :---------------------------- | :----------------------------------- |
> |   Bergamot     | | Percuma sepenuhnya<br/>Tiada had terjemahan<br/>Terjemahan pantas | Ketepatan terjemahan lebih rendah<br/>Memerlukan lebih daripada 1GB memori bebas |
> |   Google Terjemahan   | [![Video Persediaan Google Terjemahan](https://github.com/user-attachments/assets/bbf45370-0387-47e1-b690-3183f37e06d2)](https://youtu.be/83A8T890N5M)  | Percuma sepenuhnya | Had terjemahan rendah<br/>Ketepatan terjemahan lebih rendah |
> |     DeepL      |   [![Video Persediaan DeepL](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0)   | Peringkat percuma yang besar<br/>Terjemahan pantas | |
> |     Gemini     | [![Video Persediaan Google AI](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I) | Ketepatan terjemahan tinggi | Bayaran kecil diperlukan |
> |    ChatGPT     | TBD | Ketepatan terjemahan tinggi | Bayaran kecil diperlukan |
> | LLM Tempatan | TBD | Perkhidmatan itu sendiri percuma | PC spesifikasi tinggi diperlukan |

## Ciri-ciri Lain

Selain modul terjemahan, WindowTranslator mempunyai pelbagai ciri.  
Jika anda ingin mengetahui lebih lanjut, sila semak [Wiki](https://github.com/Freeesia/WindowTranslator/wiki).

---
[Dasar Privasi](PrivacyPolicy.md)

Dokumen ini diterjemahkan dari Bahasa Jepun menggunakan terjemahan mesin.
