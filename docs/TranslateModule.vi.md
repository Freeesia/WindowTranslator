# Mô-đun Dịch thuật

WindowTranslator có thể chọn và sử dụng từ nhiều mô-đun dịch thuật.  
Mỗi mô-đun có các đặc điểm riêng, bằng cách chọn mô-đun phù hợp theo mục đích sử dụng, bạn có thể sử dụng dịch thuật một cách thoải mái hơn.

## Bergamot ![Mặc định](https://img.shields.io/badge/Mặc%20định-brightgreen)

Mô-đun dịch máy hoạt động ngoại tuyến.

### Ưu điểm
- **Hoàn toàn miễn phí**: Hoàn toàn không tốn phí
- **Không giới hạn dịch**: Có thể dịch không giới hạn lần
- **Nhanh**: Dịch nhanh vì được xử lý cục bộ
- **Quyền riêng tư**: Không cần kết nối internet, dữ liệu không được gửi ra bên ngoài
- **Ổn định**: Không bị ảnh hưởng bởi mạng

### Nhược điểm
- **Độ chính xác dịch**: So với các dịch vụ dựa trên đám mây, độ chính xác dịch thuật thấp hơn
- **Sử dụng bộ nhớ**: Xử lý dịch thuật sử dụng một lượng bộ nhớ nhất định
- **Ngôn ngữ hỗ trợ**: Chỉ hỗ trợ một số cặp ngôn ngữ

### Các tình huống sử dụng được khuyến nghị
- Khi muốn sử dụng miễn phí
- Sử dụng trong môi trường ngoại tuyến
- Khi coi trọng quyền riêng tư
- Khi dịch với tần suất cao

---

## Google Dịch

Mô-đun dịch thuật sử dụng dịch vụ dịch của Google.

### Ưu điểm
- **Hoàn toàn miễn phí**: Có thể sử dụng mà không cần API key
- **Hỗ trợ đa ngôn ngữ**: Hỗ trợ nhiều cặp ngôn ngữ
- **Đơn giản**: Không cần cấu hình đặc biệt

### Nhược điểm
- **Giới hạn dịch**: Số ký tự có thể dịch mỗi ngày bị giới hạn
- **Độ chính xác dịch**: So với các dịch vụ trả phí khác có thể kém chính xác hơn
- **Tốc độ**: Bị ảnh hưởng bởi mạng
- **Ổn định**: Có thể đột ngột không khả dụng do giới hạn sử dụng

### Các tình huống sử dụng được khuyến nghị
- Sử dụng không thường xuyên
- Khi muốn bắt đầu sử dụng ngay lập tức
- Khi muốn dịch các cặp ngôn ngữ đa dạng

---

## DeepL

Mô-đun sử dụng dịch vụ dịch thuật DeepL được biết đến với chất lượng cao.

### Ưu điểm
- **Độ chính xác cao**: Cung cấp bản dịch tự nhiên và chất lượng cao
- **Hạn mức miễn phí hào phóng**: Tối đa 500.000 ký tự mỗi tháng miễn phí (API miễn phí)
- **Nhanh**: Xử lý dịch nhanh
- **Hỗ trợ thuật ngữ**: Có thể duy trì tính nhất quán trong dịch thuật bằng cách sử dụng thuật ngữ

### Nhược điểm
- **Cần đăng ký API**: Cần đăng ký DeepL API và thiết lập API key
- **Giới hạn hạn mức miễn phí**: Khi vượt quá hạn mức miễn phí cần chuyển sang gói trả phí
- **Hỗ trợ ngôn ngữ**: Hỗ trợ ngôn ngữ hạn chế so với Google và các dịch vụ khác

### Các tình huống sử dụng được khuyến nghị
- Khi cần bản dịch chất lượng cao
- Sử dụng với tần suất trung bình

---

## Google AI (Gemini)

Mô-đun dịch thuật tận dụng công nghệ AI mới nhất của Google.

### Ưu điểm
- **Độ chính xác cao nhất**: Có khả năng dịch chất lượng rất cao với hiểu biết ngữ cảnh
- **Linh hoạt**: Có thể tùy chỉnh prompt để điều chỉnh phong cách dịch
- **Hỗ trợ thuật ngữ**: Có thể duy trì tính nhất quán trong dịch thuật bằng cách sử dụng thuật ngữ

### Nhược điểm
- **Cần API key**: Cần lấy và thiết lập API key từ Google AI Studio
- **Thanh toán theo mức sử dụng**: Phí dựa trên mức sử dụng (nhưng tối thiểu)
- **Tốc độ**: Mất nhiều thời gian xử lý hơn các mô-đun khác do dựa trên LLM

### Các tình huống sử dụng được khuyến nghị
- Khi cần bản dịch chất lượng cao nhất
- Khi cần phong cách dịch tùy chỉnh
- Khi cần dịch quan tâm đến ngữ cảnh

---

## ChatGPT API (hoặc LLM cục bộ)

Mô-đun dịch thuật sử dụng ChatGPT API hoặc LLM cục bộ.

### Ưu điểm
- **Độ chính xác cao nhất**: Bản dịch chất lượng cao bằng các mô hình ngôn ngữ lớn
- **Linh hoạt**: Có thể tùy chỉnh prompt để điều chỉnh phong cách dịch
- **Hỗ trợ thuật ngữ**: Có thể duy trì tính nhất quán trong dịch thuật bằng cách sử dụng thuật ngữ
- **Hỗ trợ LLM cục bộ**: Cũng có thể sử dụng máy chủ LLM của riêng bạn

### Nhược điểm
- **Cần API key**: Cần thiết lập API key cho mỗi dịch vụ (trừ LLM cục bộ)
- **Thanh toán theo mức sử dụng**: Phí dựa trên mức sử dụng (trừ LLM cục bộ)
- **Tốc độ**: Thời gian xử lý dài hơn
- **Yêu cầu LLM cục bộ**: Khi vận hành LLM riêng cần PC hiệu năng cao

### Các tình huống sử dụng được khuyến nghị
- Khi cần bản dịch chất lượng cao nhất
- Khi cần phong cách dịch tùy chỉnh
- Khi coi trọng quyền riêng tư mà vẫn muốn bản dịch chất lượng cao (LLM cục bộ)

---

## PLaMo

Mô-đun dịch thuật sử dụng LLM cục bộ chuyên về tiếng Nhật.

### Ưu điểm
- **Chuyên về tiếng Nhật**: Được tối ưu hóa cho dịch tiếng Nhật
- **Hoàn toàn miễn phí**: Mô hình nguồn mở không tốn phí
- **Quyền riêng tư**: Chạy cục bộ, dữ liệu không được gửi ra bên ngoài
- **Ngoại tuyến**: Không cần kết nối internet

### Nhược điểm
- **Yêu cầu cấu hình cao**: Cần PC hiệu năng cao bao gồm GPU
- **Sử dụng bộ nhớ**: Cần lượng lớn bộ nhớ (khuyến nghị 8GB trở lên)
- **Tốc độ**: Xử lý mất thời gian nếu không có GPU

### Các tình huống sử dụng được khuyến nghị
- Khi bạn sở hữu PC hiệu năng cao
- Khi quyền riêng tư là ưu tiên hàng đầu
- Khi coi trọng chất lượng dịch tiếng Nhật

---

## Cách chọn mô-đun

| Mục đích                        | Mô-đun được đề xuất                         |
| ------------------------------- | -------------------------------------------- |
| Bắt đầu sử dụng ngay           | **Bergamot** hoặc **Google Dịch**            |
| Bản dịch chất lượng cao nhất   | **Google AI** hoặc **ChatGPT API**          |
| Muốn giảm chi phí              | **Bergamot** hoặc **DeepL (trong hạn mức miễn phí)** |
| Tập trung vào quyền riêng tư   | **Bergamot** hoặc **PLaMo**                 |
| Sử dụng tần suất cao           | **Bergamot** hoặc **DeepL**                 |
