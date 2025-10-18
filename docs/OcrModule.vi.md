# Mô-đun OCR

WindowTranslator cho phép bạn chọn từ nhiều mô-đun OCR để nhận dạng văn bản trên màn hình.  
Mỗi mô-đun có những đặc điểm riêng và việc chọn mô-đun phù hợp với trường hợp sử dụng của bạn sẽ cho phép nhận dạng văn bản chính xác hơn.

## Nhận dạng ký tự Windows mới (Beta) ![Mặc định](https://img.shields.io/badge/Mặc%20định-brightgreen)

Mô-đun OCR cục bộ do Microsoft cung cấp.

### Ưu điểm
- **Độ chính xác nhận dạng**: Có độ chính xác nhận dạng cao nhất
- **Nhanh**: Tốc độ xử lý rất nhanh

### Nhược điểm
- **Sử dụng bộ nhớ**: Có thể sử dụng hơn 1GB bộ nhớ chỉ cho xử lý nhận dạng
- **Môi trường hoạt động**: Có thể không hoạt động trong một số môi trường (khuyến nghị Windows 10 trở lên)

---

### Nhận dạng ký tự chuẩn Windows

Công cụ OCR được tích hợp sẵn trong Windows 10 trở lên.

### Ưu điểm
- **Sử dụng bộ nhớ**: Nhẹ và sử dụng bộ nhớ thấp
- **Môi trường hoạt động**: Có sẵn rộng rãi trên Windows 10 trở lên

### Nhược điểm
- **Độ chính xác nhận dạng**: Có thể yếu với phông chữ phức tạp hoặc chữ viết tay
- **Thiết lập**: Có thể cần cài đặt thủ công dữ liệu ngôn ngữ

---

## Tesseract OCR

Công cụ OCR nguồn mở.

### Ưu điểm
- **Hỗ trợ đa ngôn ngữ**: Hỗ trợ hơn 100 ngôn ngữ
- **Ổn định**: Công cụ đáng tin cậy với lịch sử lâu dài

### Nhược điểm
- **Độ chính xác nhận dạng**: Có thể kém hơn so với các OCR khác

---

## Cách chọn mô-đun

Vui lòng chọn mô-đun hoạt động theo thứ tự độ chính xác nhận dạng cao sau:

1. OneOcr
2. Nhận dạng ký tự chuẩn Windows
3. Tesseract OCR
- Khi cần nhận dạng chất lượng cao nhất
- Văn bản phức tạp mà OCR thông thường không thể nhận dạng
- Khi cần nhận dạng quan tâm đến ngữ cảnh

---

## Chọn mô-đun

| Mục đích | Mô-đun được đề xuất |
|----------|---------------------|
| Bắt đầu sử dụng ngay | **OneOcr** |
| Cần nhận dạng chất lượng cao nhất | **Google AI OCR** hoặc **LLM OCR** |
| Muốn giảm chi phí | **OneOcr** hoặc **Tesseract** |
| Ưu tiên bảo mật | **OneOcr** hoặc **Tesseract** |
| Cần hỗ trợ đa ngôn ngữ | **Tesseract** hoặc **Google AI OCR** |
| Nhận dạng chữ viết tay | **Google AI OCR** hoặc **LLM OCR** |
| Môi trường ngoại tuyến | **OneOcr** hoặc **Tesseract** |

---

## Mẹo cải thiện độ chính xác OCR

Bất kể bạn sử dụng mô-đun OCR nào, bạn có thể cải thiện độ chính xác nhận dạng bằng cách chú ý đến những điều sau:

1. **Độ phân giải màn hình**: Hiển thị độ phân giải cao cải thiện độ chính xác nhận dạng
2. **Kích thước phông chữ**: Phông chữ quá nhỏ khó nhận dạng, vui lòng điều chỉnh đến kích thước phù hợp
3. **Độ tương phản**: Độ tương phản cao hơn giữa văn bản và nền cải thiện độ chính xác nhận dạng
4. **Hiển thị rõ ràng**: Nhắm đến hiển thị không có mờ hoặc biến dạng
5. **Cài đặt ngôn ngữ**: Đặt ngôn ngữ mục tiêu nhận dạng một cách chính xác
