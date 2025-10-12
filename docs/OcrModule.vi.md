# Mô-đun OCR

WindowTranslator cho phép bạn chọn từ nhiều mô-đun OCR để nhận dạng văn bản trên màn hình.  
Mỗi mô-đun có những đặc điểm riêng và việc chọn mô-đun phù hợp với trường hợp sử dụng của bạn sẽ cho phép nhận dạng văn bản chính xác hơn.

## OneOcr ![Mặc định](https://img.shields.io/badge/Mặc%20định-brightgreen)

Mô-đun OCR cục bộ do Microsoft cung cấp.

### Ưu điểm
- **Hoàn toàn miễn phí**: Không có bất kỳ phí nào
- **Nhanh**: Nhận dạng nhanh vì xử lý cục bộ
- **Bảo mật**: Dữ liệu không được gửi ra bên ngoài
- **Ngoại tuyến**: Không cần kết nối internet
- **Ổn định**: Không bị ảnh hưởng bởi điều kiện mạng
- **Nhẹ**: Sử dụng bộ nhớ thấp

### Nhược điểm
- **Độ chính xác nhận dạng**: Độ chính xác thấp hơn cho phông chữ phức tạp hoặc chữ viết tay
- **Hỗ trợ ngôn ngữ**: Chỉ hỗ trợ ngôn ngữ hạn chế
- **Ký tự đặc biệt**: Có thể yếu với ký tự trang trí hoặc bố cục đặc biệt

### Trường hợp sử dụng được đề xuất
- Nhận dạng văn bản phông chữ tiêu chuẩn
- Khi ưu tiên bảo mật
- Sử dụng môi trường ngoại tuyến
- Sử dụng PC cấu hình thấp

---

## Tesseract OCR

Công cụ OCR nguồn mở.

### Ưu điểm
- **Hoàn toàn miễn phí**: Nguồn mở và miễn phí sử dụng
- **Hỗ trợ đa ngôn ngữ**: Hỗ trợ hơn 100 ngôn ngữ
- **Khả năng tùy chỉnh**: Có thể tùy chỉnh bằng cách thêm dữ liệu đào tạo
- **Ngoại tuyến**: Không cần kết nối internet
- **Ổn định**: Công cụ đáng tin cậy với lịch sử lâu dài

### Nhược điểm
- **Độ chính xác nhận dạng**: Độ chính xác thấp hơn so với OCR dựa trên AI mới nhất
- **Thiết lập**: Cần cài đặt dữ liệu ngôn ngữ
- **Tốc độ**: Tốc độ xử lý tương đối chậm
- **Hình ảnh chất lượng thấp**: Yếu với hình ảnh mờ hoặc nhiều nhiễu

### Trường hợp sử dụng được đề xuất
- Nhận dạng văn bản bằng nhiều ngôn ngữ
- Khi cần tùy chỉnh
- Nhận dạng chuyên biệt cho ngôn ngữ cụ thể

---

## Google AI OCR (Gemini Vision)

Mô-đun OCR tận dụng công nghệ AI của Google.

### Ưu điểm
- **Độ chính xác cao nhất**: Độ chính xác nhận dạng rất cao với công nghệ AI
- **Hỗ trợ chữ viết tay**: Có thể nhận dạng chữ viết tay
- **Bố cục phức tạp**: Nhận dạng chính xác bố cục phức tạp
- **Hỗ trợ đa ngôn ngữ**: Hỗ trợ phạm vi rộng các ngôn ngữ
- **Khả năng chịu đựng chất lượng hình ảnh**: Độ chính xác nhận dạng cao ngay cả với hình ảnh chất lượng thấp
- **Hiểu ngữ cảnh**: Nhận dạng xem xét ngữ cảnh

### Nhược điểm
- **Cần API key**: Cần đăng ký với Google Cloud Platform và thiết lập API key
- **Thanh toán theo mức sử dụng**: Phí dựa trên mức sử dụng (có hạn mức miễn phí)
- **Tốc độ**: Mất thời gian xử lý qua mạng
- **Bảo mật**: Dữ liệu hình ảnh được gửi đến máy chủ Google
- **Chỉ trực tuyến**: Cần kết nối internet

### Trường hợp sử dụng được đề xuất
- Nhận dạng chữ viết tay hoặc văn bản trang trí
- Khi cần độ chính xác nhận dạng cao
- Nhận dạng văn bản với bố cục phức tạp
- Khi chất lượng hình ảnh thấp

---

## LLM OCR

Mô-đun OCR sử dụng khả năng thị giác của các mô hình ngôn ngữ lớn (LLM).

### Ưu điểm
- **Độ chính xác cao nhất**: Độ chính xác nhận dạng rất cao với công nghệ AI mới nhất
- **Hiểu ngữ cảnh**: Nhận dạng xem xét toàn bộ ngữ cảnh hình ảnh
- **Linh hoạt**: Hỗ trợ bố cục phức tạp và phông chữ đặc biệt
- **Hỗ trợ đa ngôn ngữ**: Hỗ trợ phạm vi rộng các ngôn ngữ
- **Khả năng suy luận**: Không chỉ nhận dạng ký tự mà còn nhận dạng dựa trên hiểu biết

### Nhược điểm
- **Cần API key**: Cần API key từ OpenAI, Anthropic, v.v.
- **Thanh toán theo mức sử dụng**: Phí dựa trên mức sử dụng
- **Tốc độ**: Thời gian xử lý dài hơn
- **Chi phí**: Chi phí cao hơn do tiêu thụ token cao cho xử lý hình ảnh
- **Bảo mật**: Dữ liệu hình ảnh được gửi đến dịch vụ bên ngoài

### Trường hợp sử dụng được đề xuất
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
