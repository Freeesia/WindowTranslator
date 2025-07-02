# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![Crowdin](https://badges.crowdin.net/windowtranslator/localized.svg)](https://crowdin.com/project/windowtranslator)

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

Tải xuống `WindowTranslator-(phiên bản).msi` từ [trang Releases trên GitHub](https://github.com/Freeesia/WindowTranslator/releases/latest) và chạy để cài đặt.  
Video hướng dẫn cài đặt ở đây⬇️  
[![Video hướng dẫn cài đặt](https://github.com/user-attachments/assets/b5babc02-715b-43bc-ba97-f23078ffd39b)](https://youtu.be/wvcbCLA9chQ?t=7)

### Phiên bản di động

Tải xuống tệp zip từ [trang Releases trên GitHub](https://github.com/Freeesia/WindowTranslator/releases/latest) và giải nén vào thư mục bạn muốn.  
- `WindowTranslator-(phiên bản).zip` : Cần môi trường .NET  
- `WindowTranslator-full-(phiên bản).zip` : Không phụ thuộc .NET

## Cách sử dụng

### Bergamot ![Mặc định](https://img.shields.io/badge/デフォルト-brightgreen)

1. Khởi chạy `WindowTranslator.exe` và nhấn nút dịch.  
   ![Nút dịch](images/translate.png)
2. Chọn cửa sổ ứng dụng bạn muốn dịch và nhấn nút "OK".  
   ![Chọn cửa sổ](images/select.png)
3. Chọn ngôn ngữ nguồn và ngôn ngữ đích từ "Cài đặt ngôn ngữ" trong tab "Cài đặt chung".  
   ![Cài đặt ngôn ngữ](images/language.png)
4. Sau khi cài đặt hoàn tất, nhấn nút "OK" để đóng màn hình cài đặt.  
   > Có thể cần cài đặt chức năng OCR.
   > Vui lòng làm theo hướng dẫn để cài đặt.
5. Sau một lúc, kết quả dịch sẽ được hiển thị dưới dạng lớp phủ.  
   ![Kết quả dịch](images/result.png)

> [!NOTE]
> WindowTranslator có nhiều mô-đun dịch khác nhau có sẵn.  
> Google Dịch có giới hạn lượng văn bản có thể dịch thấp, nếu bạn sử dụng thường xuyên, hãy cân nhắc sử dụng các mô-đun khác.  
> Bạn có thể xem danh sách các mô-đun dịch có sẵn trong video bên dưới hoặc trên [Wiki](https://github.com/Freeesia/WindowTranslator/wiki#翻訳).
> 
> |                |                                                              Video hướng dẫn                                                           | Ưu điểm                    | Nhược điểm                        |
> | :------------: | :-----------------------------------------------------------------------------------------------------------------------------------: | :-------------------------- | :-------------------------------- |
> |   Bergamot     | | Hoàn toàn miễn phí<br/>Không giới hạn dịch<br/>Dịch nhanh | Độ chính xác dịch thấp hơn<br/>Cần ít nhất 1GB RAM trống |
> |   Google Dịch   | [![Video cài đặt Google Dịch](https://github.com/user-attachments/assets/bbf45370-0387-47e1-b690-3183f37e06d2)](https://youtu.be/83A8T890N5M)  | Hoàn toàn miễn phí | Giới hạn dịch thấp<br/>Độ chính xác dịch thấp hơn |
> |     DeepL      |   [![Video cài đặt DeepL](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0)   | Nhiều mức miễn phí<br/>Dịch nhanh | |
> |     Gemini     | [![Video cài đặt Google AI](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I) | Độ chính xác dịch cao | Cần thanh toán một khoản nhỏ |
> |    ChatGPT     | TBD | Độ chính xác dịch cao | Cần thanh toán một khoản nhỏ |
> | LLM cục bộ | TBD | Dịch vụ miễn phí | Cần PC cấu hình cao |

## Các tính năng khác

Ngoài các mô-đun dịch, WindowTranslator còn có nhiều tính năng khác.  
Để tìm hiểu thêm về cách sử dụng nâng cao, vui lòng xem [Wiki](https://github.com/Freeesia/WindowTranslator/wiki).

---
[Chính sách riêng tư](PrivacyPolicy.vi.md)

Tài liệu này được dịch từ tiếng Nhật bằng công cụ dịch máy.
