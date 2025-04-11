# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)

WindowTranslator là công cụ để dịch cửa sổ của các ứng dụng Windows.

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md) | [VI](./README.vi.md)

## Mục lục

- [ WindowTranslator](#-windowtranslator)
  - [Mục lục](#mục-lục)
  - [Tải xuống](#tải-xuống)
    - [Phiên bản cài đặt ](#phiên-bản-cài-đặt-)
    - [Phiên bản di động](#phiên-bản-di-động)
  - [Cách sử dụng](#cách-sử-dụng)
    - [Bergamot ](#bergamot-)
  - [Các tính năng khác](#các-tính-năng-khác)

## Tải xuống

### Phiên bản cài đặt ![Đề xuất](https://img.shields.io/badge/%E3%82%AA%E3%82%B9%E3%82%B9%E3%83%A1-brightgreen)

[GitHubのリリースページ](https://github.com/Freeesia/WindowTranslator/releases/latest)から`WindowTranslator-(バージョン).msi`をダウンロードして実行しインストールします。\
インストール手順動画はこちら⬇️\
[![インストール手順動画](https://github.com/user-attachments/assets/b5babc02-715b-43bc-ba97-f23078ffd39b)](https://youtu.be/wvcbCLA9chQ?t=7)

### Phiên bản di động

Tải xuống tệp zip từ [trang Releases trên GitHub](https://github.com/Freeesia/WindowTranslator/releases/latest) và giải nén vào thư mục bạn muốn.

- `WindowTranslator-(phiên bản).zip` : Cần môi trường .NET
- `WindowTranslator-full-(phiên bản).zip` : Không phụ thuộc .NET

## Cách sử dụng

### Bergamot ![Mặc định](https://img.shields.io/badge/デフォルト-brightgreen)

1. `WindowTranslator.exe`を起動し、翻訳ボタンを押下します。\
  ![翻訳ボタン](images/translate.png)
2. 翻訳したいアプリのウィンドウを選択し、「OK」ボタンを押下します。\
  ![ウィンドウ選択](images/select.png)
3. 「全体設定」タブの「言語設定」から翻訳元・翻訳先を選択します。\
  ![言語設定](images/language.png)
4. Sau khi cài đặt hoàn tất, nhấn nút "OK" để đóng màn hình cài đặt.
  > OCR機能のインストールが必要な場合があります。> 指示に従いインストールしてください。
5. しばらくすると翻訳結果がオーバーレイで表示されます。\
  ![翻訳結果](images/result.png)

> [!NOTE]
> WindowTranslatorでは各種翻訳モジュールが利用可能です。\
> Google翻訳は翻訳可能なテキスト量が少なく、利用頻度が高い場合は他のモジュールの利用を検討してください。\
> 利用可能な翻訳モジュール一覧は下記の動画もしくは[Wiki](https://github.com/Freeesia/WindowTranslator/wiki#翻訳)でご確認いただけます。
>
> |                                  |                                                                Video hướng dẫn                                                                | Ưu điểm                                                   | Nhược điểm                                               |
> | :------------------------------: | :-------------------------------------------------------------------------------------------------------------------------------------------: | :-------------------------------------------------------- | :------------------------------------------------------- |
> |             Bergamot             |                                                                                                                                               | Hoàn toàn miễn phí<br/>Không giới hạn dịch<br/>Dịch nhanh | Độ chính xác dịch thấp hơn<br/>Cần ít nhất 1GB RAM trống |
> |            Google Dịch           | [![Video cài đặt Google Dịch](https://github.com/user-attachments/assets/bbf45370-0387-47e1-b690-3183f37e06d2)](https://youtu.be/83A8T890N5M) | Hoàn toàn miễn phí                                        | Giới hạn dịch thấp<br/>Độ chính xác dịch thấp hơn        |
> |               DeepL              |    [![Video cài đặt DeepL](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0)    | Nhiều mức miễn phí<br/>Dịch nhanh                         |                                                          |
> |             GoogleAI             |  [![Video cài đặt Google AI](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I)  | Độ chính xác dịch cao                                     | Cần thanh toán một khoản nhỏ                             |
> | LLM (đám mây) |                                                                      TBD                                                                      | Độ chính xác dịch cao                                     | Cần thanh toán một khoản nhỏ                             |
> |  LLM (cục bộ) |                                                                      TBD                                                                      | Dịch vụ miễn phí                                          | Cần PC cấu hình cao                                      |

## Các tính năng khác

翻訳モジュール以外にも、WindowTranslatorには様々な機能が搭載されています。\
もっと使いこなしたい方は、[Wiki](https://github.com/Freeesia/WindowTranslator/wiki) をご確認ください。

---

[Chính sách riêng tư](PrivacyPolicy.vi.md)
