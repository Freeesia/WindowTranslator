# Çeviri Modülleri

WindowTranslator, birden fazla çeviri modülü arasından seçim yapmanıza olanak tanır.  
Her modülün kendine özgü özellikleri vardır ve kullanım durumunuz için uygun modülü seçerek çeviriyi daha rahat kullanabilirsiniz.

## Bergamot ![Varsayılan](https://img.shields.io/badge/Varsayılan-brightgreen)

Çevrimdışı çalışan bir makine çevirisi modülü.

### Avantajlar
- **Tamamen Ücretsiz**: Hiçbir ücret yok
- **Çeviri Sınırı Yok**: İstediğiniz kadar çeviri yapabilirsiniz
- **Hızlı**: Yerel olarak işlendiği için çeviri hızlıdır
- **Gizlilik**: İnternet bağlantısı gerekmez, veriler dışarıya gönderilmez
- **Kararlılık**: Ağ koşullarından etkilenmez

### Dezavantajlar
- **Çeviri Doğruluğu**: Bulut tabanlı hizmetlere kıyasla daha düşük çeviri doğruluğu
- **Bellek Kullanımı**: Çeviri işleme için belirli miktarda bellek kullanır
- **Dil Desteği**: Yalnızca bazı dil çiftleri desteklenir

### Önerilen Kullanım Durumları
- Ücretsiz kullanmak istediğinizde
- Çevrimdışı ortamlarda kullanım
- Gizlilik önemli olduğunda
- Sık sık çeviri yaparken

---

## Google Translate

Google'ın çeviri hizmetini kullanan bir çeviri modülü.

### Avantajlar
- **Tamamen Ücretsiz**: API anahtarı olmadan kullanılabilir
- **Çok Dilli Destek**: Birçok dil çiftini destekler
- **Kolay**: Özel yapılandırma gerektirmez

### Dezavantajlar
- **Çeviri Sınırları**: Günde çevrilebilecek karakter sayısı sınırlıdır
- **Çeviri Doğruluğu**: Diğer ücretli hizmetlere kıyasla daha az doğru olabilir
- **Hız**: Ağ koşullarından etkilenir
- **Kararlılık**: Kullanım kısıtlamaları nedeniyle aniden kullanılamaz hale gelebilir

### Önerilen Kullanım Durumları
- Nadir kullanım
- Hemen kullanmaya başlamak istediğinizde
- Çeşitli dil çiftleri çevirmek istediğinizde

---

## DeepL

Yüksek kaliteli çevirilerle tanınan DeepL'in çeviri hizmetini kullanan bir modül.

### Avantajlar
- **Yüksek Doğruluk**: Doğal, yüksek kaliteli çeviriler sağlar
- **Cömert Ücretsiz Katman**: Ayda 500.000 karaktere kadar ücretsiz (Free API)
- **Hızlı**: Hızlı çeviri işleme
- **Sözlük Desteği**: Sözlükler kullanarak çeviri tutarlılığını koruyabilir

### Dezavantajlar
- **API Kaydı Gerekli**: DeepL API kaydı ve API anahtarı kurulumu gerektirir
- **Ücretsiz Katman Sınırları**: Ücretsiz katmanı aşarken ücretli plana geçiş gereklidir
- **Dil Desteği**: Google ve diğerlerine kıyasla sınırlı dil desteği

### Önerilen Kullanım Durumları
- Yüksek kaliteli çeviri gerektiğinde
- Orta düzeyde kullanım sıklığı

---

## Google AI (Gemini)

Google'ın en son yapay zeka teknolojisinden yararlanan bir çeviri modülü.

### Avantajlar
- **En Yüksek Doğruluk**: Bağlamsal anlayışla çok yüksek kaliteli çeviri yapabilir
- **Esneklik**: Çeviri stilini ayarlamak için istemler özelleştirebilir
- **Sözlük Desteği**: Sözlükler kullanarak çeviri tutarlılığını koruyabilir

### Dezavantajlar
- **API Anahtarı Gerekli**: Google AI Studio'dan API anahtarı edinme ve kurulum gerektirir
- **Kullanıma Göre Ödeme**: Kullanıma dayalı ücretlendirme (minimal olsa da)
- **Hız**: LLM tabanı nedeniyle diğer modüllerden daha uzun işleme süresi alır

### Önerilen Kullanım Durumları
- En yüksek kaliteli çeviri gerektiğinde
- Özelleştirilmiş çeviri stili gerektiğinde
- Bağlam farkında çeviri önemli olduğunda

---

## ChatGPT API (veya Yerel LLM)

ChatGPT API veya yerel LLM kullanan bir çeviri modülü.

### Avantajlar
- **En Yüksek Doğruluk**: Büyük dil modelleri tarafından yüksek kaliteli çeviri
- **Esneklik**: Çeviri stilini ayarlamak için istemler özelleştirebilir
- **Sözlük Desteği**: Sözlükler kullanarak çeviri tutarlılığını koruyabilir
- **Yerel LLM Desteği**: Kendi LLM sunucunuzu da kullanabilirsiniz

### Dezavantajlar
- **API Anahtarı Gerekli**: Her hizmet için API anahtarı kurulumu gerektirir (yerel LLM hariç)
- **Kullanıma Göre Ödeme**: Kullanıma dayalı ücretlendirme (yerel LLM hariç)
- **Hız**: Daha uzun işleme süresi
- **Yerel LLM Gereksinimleri**: Kendi LLM'inizi çalıştırırken yüksek özellikli PC gereklidir

### Önerilen Kullanım Durumları
- En yüksek kaliteli çeviri gerektiğinde
- Özelleştirilmiş çeviri stili gerektiğinde
- Gizlilik önemliyken yüksek kaliteli çeviri istendiğinde (yerel LLM)

---

## PLaMo

Japonca için özelleştirilmiş yerel LLM kullanan bir çeviri modülü.

### Avantajlar
- **Japonca Uzmanlaşması**: Japonca çeviri için optimize edilmiş
- **Tamamen Ücretsiz**: Açık kaynak modeli, ücret yok
- **Gizlilik**: Yerel olarak çalışır, veriler dışarıya gönderilmez
- **Çevrimdışı**: İnternet bağlantısı gerekmez

### Dezavantajlar
- **Yüksek Özellik Gereksinimleri**: GPU dahil yüksek performanslı PC gerektirir
- **Bellek Kullanımı**: Büyük miktarda bellek gerektirir (8GB veya daha fazlası önerilir)
- **Hız**: GPU olmadan işleme zaman alır

### Önerilen Kullanım Durumları
- Yüksek performanslı bir PC sahibi olduğunuzda
- Gizlilik en üst öncelik olduğunda
- Japonca çeviri kalitesi önemli olduğunda

---

## Modül Nasıl Seçilir

| Amaç                              | Önerilen Modül                               |
| --------------------------------- | -------------------------------------------- |
| Hemen kullanmaya başlama          | **Bergamot** veya **Google Translate**      |
| En yüksek kaliteli çeviri         | **Google AI** veya **ChatGPT API**          |
| Maliyetleri düşük tutma           | **Bergamot** veya **DeepL (ücretsiz katman içinde)** |
| Gizlilik odaklı                   | **Bergamot** veya **PLaMo**                 |
| Yüksek sıklıkta kullanım          | **Bergamot** veya **DeepL**                 |
