# OCR Modülleri

WindowTranslator, ekrandaki metni tanımak için birden fazla OCR modülü arasından seçim yapmanıza olanak tanır.  
Her modülün kendine özgü özellikleri vardır ve kullanım durumunuz için uygun modülü seçmek daha doğru metin tanıma sağlar.

## Yeni Windows Karakter Tanıma (Beta) ![Varsayılan](https://img.shields.io/badge/Varsayılan-brightgreen)

Microsoft tarafından sağlanan yerel bir OCR modülü.

### Avantajlar
- **Tanıma Doğruluğu**: En yüksek tanıma doğruluğuna sahiptir
- **Hızlı**: Çok hızlı işleme hızı

### Dezavantajlar
- **Bellek Kullanımı**: Sadece tanıma işlemi için 1GB'den fazla bellek kullanabilir
- **Çalışma Ortamı**: Bazı ortamlarda çalışmayabilir (Windows 10 veya üzeri önerilir)

---

## Windows Standart Karakter Tanıma

Windows 10 ve sonraki sürümlerde standart olarak gelen OCR motoru.

### Avantajlar
- **Bellek Kullanımı**: Hafif ve düşük bellek kullanımı
- **Çalışma Ortamı**: Windows 10 ve sonraki sürümlerde yaygın olarak kullanılabilir

### Dezavantajlar
- **Tanıma Doğruluğu**: Karmaşık yazı tipleri veya el yazısı metinlerde zayıf olabilir
- **Kurulum**: Dil verilerinin manuel kurulumu gerekebilir

---

## Tesseract OCR

Açık kaynaklı bir OCR motoru.

### Avantajlar
- **Çok Dilli Destek**: 100'den fazla dili destekler
- **Kararlılık**: Uzun geçmişe sahip güvenilir motor

### Dezavantajlar
- **Tanıma Doğruluğu**: Diğer OCR'lere kıyasla daha düşük olabilir

---

## Modül Seçimi

Lütfen yüksek tanıma doğruluğu sırasına göre çalışan modülü seçin:

1. Yeni Windows Karakter Tanıma (Beta)
2. Windows Standart Karakter Tanıma
3. Tesseract OCR
