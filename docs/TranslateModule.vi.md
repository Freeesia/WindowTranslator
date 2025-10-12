# Mô-đun Dịch thuật

WindowTranslator cho phép bạn chọn từ nhiều mô-đun dịch thuật.  
Mỗi mô-đun có những đặc điểm riêng và việc chọn mô-đun phù hợp với trường hợp sử dụng của bạn sẽ giúp bạn có trải nghiệm dịch thuật thoải mái hơn.

## Bergamot ![Mặc định](https://img.shields.io/badge/Mặc%20định-brightgreen)

Mô-đun dịch máy hoạt động ngoại tuyến.

### Ưu điểm
- **Hoàn toàn miễn phí**: Không có bất kỳ phí nào
- **Không giới hạn dịch**: Dịch bao nhiêu lần tùy thích
- **Nhanh**: Dịch nhanh vì xử lý cục bộ
- **Bảo mật**: Không cần kết nối internet, dữ liệu không được gửi ra bên ngoài
- **Ổn định**: Không bị ảnh hưởng bởi điều kiện mạng

### Nhược điểm
- **Độ chính xác dịch**: Độ chính xác thấp hơn so với các dịch vụ dựa trên đám mây
- **Sử dụng bộ nhớ**: Cần 1GB hoặc nhiều hơn bộ nhớ trống
- **Hỗ trợ ngôn ngữ**: Chỉ hỗ trợ một số cặp ngôn ngữ nhất định

### Trường hợp sử dụng được đề xuất
- Môi trường có kết nối internet không ổn định
- Khi ưu tiên bảo mật
- Sử dụng dịch với tần suất cao

---

## Google Dịch

Mô-đun dịch thuật sử dụng dịch vụ dịch của Google.

### Ưu điểm
- **Hoàn toàn miễn phí**: Có thể sử dụng mà không cần API key
- **Hỗ trợ đa ngôn ngữ**: Hỗ trợ nhiều cặp ngôn ngữ
- **Đơn giản**: Không cần cấu hình đặc biệt

### Nhược điểm
- **Giới hạn dịch**: Số ký tự có thể dịch mỗi ngày bị giới hạn
- **Độ chính xác dịch**: Độ chính xác thấp hơn so với các dịch vụ trả phí khác trong một số trường hợp
- **Tốc độ**: Bị ảnh hưởng bởi điều kiện mạng
- **Ổn định**: Có thể đột ngột không khả dụng do giới hạn sử dụng

### Trường hợp sử dụng được đề xuất
- Sử dụng với tần suất thấp
- Khi muốn bắt đầu sử dụng ngay lập tức
- Khi cần dịch các cặp ngôn ngữ đa dạng

---

## DeepL

Mô-đun sử dụng dịch vụ dịch thuật DeepL được biết đến với chất lượng cao.

### Ưu điểm
- **Độ chính xác cao**: Nhận được bản dịch tự nhiên và chất lượng cao
- **Hạn mức miễn phí hào phóng**: Tối đa 500.000 ký tự mỗi tháng miễn phí (API miễn phí)
- **Nhanh**: Xử lý dịch nhanh
- **Sử dụng kinh doanh**: Bản dịch chất lượng cao ngay cả cho tài liệu chuyên môn

### Nhược điểm
- **Cần đăng ký API**: Cần đăng ký DeepL API và thiết lập API key
- **Giới hạn hạn mức miễn phí**: Cần chuyển sang gói trả phí khi vượt quá hạn mức miễn phí
- **Hỗ trợ ngôn ngữ**: Hỗ trợ ngôn ngữ hạn chế hơn so với Google

### Trường hợp sử dụng được đề xuất
- Khi cần bản dịch chất lượng cao
- Dịch tài liệu kinh doanh
- Sử dụng với tần suất trung bình

---

## Google AI (Gemini)

Mô-đun dịch thuật tận dụng công nghệ AI mới nhất của Google.

### Ưu điểm
- **Độ chính xác cao nhất**: Bản dịch chất lượng rất cao với hiểu biết ngữ cảnh
- **Biểu đạt linh hoạt**: Được dịch với cách diễn đạt tự nhiên
- **Hỗ trợ thuật ngữ chuyên môn**: Hỗ trợ tài liệu kỹ thuật, game và nội dung chuyên môn
- **Hạn mức miễn phí**: Có thể sử dụng miễn phí đến một lượng nhất định

### Nhược điểm
- **Cần API key**: Cần đăng ký với Google Cloud Platform và thiết lập API key
- **Thanh toán theo mức sử dụng**: Phí phát sinh sau khi vượt quá hạn mức miễn phí (nhưng tối thiểu)
- **Tốc độ**: Mất nhiều thời gian xử lý hơn các mô-đun khác do dựa trên LLM

### Trường hợp sử dụng được đề xuất
- Khi cần bản dịch chất lượng cao nhất
- Dịch nội dung chuyên môn như game hoặc tài liệu kỹ thuật
- Khi cần dịch quan tâm đến ngữ cảnh

---

## Plugin LLM (ChatGPT/Claude/LLM cục bộ)

Mô-đun dịch thuật sử dụng OpenAI, Anthropic hoặc LLM cục bộ.

### Ưu điểm
- **Độ chính xác cao nhất**: Bản dịch chất lượng cao bằng các mô hình ngôn ngữ lớn
- **Linh hoạt**: Tùy chỉnh prompt để điều chỉnh phong cách dịch
- **Hiểu ngữ cảnh**: Bản dịch xem xét ngữ cảnh dài hơn
- **Hỗ trợ LLM cục bộ**: Cũng có thể sử dụng máy chủ LLM của riêng bạn

### Nhược điểm
- **Cần API key**: Cần thiết lập API key cho mỗi dịch vụ (trừ LLM cục bộ)
- **Thanh toán theo mức sử dụng**: Phí dựa trên mức sử dụng (trừ LLM cục bộ)
- **Tốc độ**: Thời gian xử lý dài hơn
- **Yêu cầu LLM cục bộ**: Cần PC hiệu năng cao để chạy LLM của riêng bạn

### Trường hợp sử dụng được đề xuất
- Khi cần bản dịch chất lượng cao nhất
- Khi cần phong cách dịch tùy chỉnh
- Khi ưu tiên bảo mật trong khi muốn bản dịch chất lượng cao (LLM cục bộ)

---

## PLaMo

Mô-đun dịch thuật sử dụng LLM cục bộ chuyên về tiếng Nhật.

### Ưu điểm
- **Chuyên về tiếng Nhật**: Được tối ưu hóa cho dịch tiếng Nhật
- **Hoàn toàn miễn phí**: Không có phí với mô hình nguồn mở
- **Bảo mật**: Chạy cục bộ, dữ liệu không được gửi ra bên ngoài
- **Ngoại tuyến**: Không cần kết nối internet

### Nhược điểm
- **Yêu cầu cấu hình cao**: Cần PC hiệu năng cao bao gồm GPU
- **Thiết lập**: Cấu hình ban đầu phức tạp
- **Sử dụng bộ nhớ**: Cần lượng lớn bộ nhớ (khuyến nghị 8GB trở lên)
- **Tốc độ**: Mất thời gian xử lý nếu không có GPU

### Trường hợp sử dụng được đề xuất
- Khi bạn sở hữu PC hiệu năng cao
- Khi bảo mật là ưu tiên hàng đầu
- Khi ưu tiên chất lượng dịch tiếng Nhật

---

## Chọn mô-đun

| Mục đích | Mô-đun được đề xuất |
|----------|---------------------|
| Bắt đầu sử dụng ngay | **Bergamot** hoặc **Google Dịch** |
| Cần bản dịch chất lượng cao nhất | **Google AI** hoặc **Plugin LLM** |
| Muốn giảm chi phí | **Bergamot** hoặc **DeepL (trong hạn mức miễn phí)** |
| Ưu tiên bảo mật | **Bergamot** hoặc **PLaMo** |
| Sử dụng tần suất cao | **Bergamot** hoặc **DeepL** |
| Sử dụng kinh doanh | **DeepL** hoặc **Google AI** |
