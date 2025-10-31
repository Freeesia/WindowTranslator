# Módulo de Tradução

WindowTranslator memungkinkan Anda memilih de beberapa modul terjemahan.  
Setiap modul memiliki karakteristiknya sendiri, dan dengan memilih modul yang sesuai untuk kasus penggunaan Anda, Anda dapat menggunakan terjemahan dengan lebih nyaman.

## Bergamot ![Padrão](https://img.shields.io/badge/Padrão-brightgreen)

Modul terjemahan mesin yang berfungsi offline.

### Keuntungan
- **Sepenuhnya Gratis**: Tidak ada biaya sama sekali
- **Tidak Ada Batas Tradução**: Anda dapat menerjemahkan sebanyak yang Anda inginkan
- **Cepat**: Tradução cepat karena diproses secara lokal
- **Privasi**: Tidak ada koneksi internet yang diperlukan, data tidak dikirim ke luar
- **Stabilitas**: Tidak terpengaruh oleh kondisi jaringan

### Kerugian
- **Akurasi Tradução**: Akurasi terjemahan lebih rendah dibandingkan layanan berbasis cloud
- **Penggunaan Memori**: Menggunakan sejumlah memori untuk pemrosesan terjemahan
- **Dukungan Bahasa**: Hanya beberapa pasangan bahasa yang didukung

### Kasus Penggunaan yang Recomendado
- Ketika Anda ingin menggunakannya secara gratis
- Penggunaan dalam lingkungan offline
- Ketika privasi penting
- Ketika menerjemahkan secara sering

---

## Google Tradução

Modul terjemahan menggunakan layanan terjemahan Google.

### Keuntungan
- **Sepenuhnya Gratis**: Dapat digunakan tanpa kunci API
- **Dukungan Multibahasa**: Mendukung banyak pasangan bahasa
- **Mudah**: Tidak ada konfigurasi khusus yang diperlukan

### Kerugian
- **Batas Tradução**: Jumlah karakter terbatas yang dapat diterjemahkan per hari
- **Akurasi Tradução**: Mungkin kurang akurat dibandingkan layanan berbayar lainnya
- **Kecepatan**: Terpengaruh oleh kondisi jaringan
- **Stabilitas**: Mungkin tiba-tiba tidak tersedia karena pembatasan penggunaan

### Kasus Penggunaan yang Recomendado
- Penggunaan yang jarang
- Ketika Anda ingin mulai menggunakan segera
- Ketika Anda ingin menerjemahkan berbagai pasangan bahasa

---

## DeepL

Modul menggunakan layanan terjemahan DeepL, terkenal dengan terjemahan berkualitas tinggi.

### Keuntungan
- **Akurasi Tinggi**: Menyediakan terjemahan alami berkualitas tinggi
- **Tingkat Gratis yang Murah Hati**: Hingga 500.000 karakter per bulan secara gratis (API Gratis)
- **Cepat**: Pemrosesan terjemahan cepat
- **Dukungan Glosarium**: Dapat mempertahankan konsistensi terjemahan menggunakan glosarium

### Kerugian
- **Pendaftaran API Diperlukan**: Memerlukan pendaftaran API DeepL dan setup kunci API
- **Batas Tingkat Gratis**: Migrasi ke paket berbayar diperlukan saat melebihi tingkat gratis
- **Dukungan Bahasa**: Dukungan bahasa terbatas dibandingkan Google dan lainnya

### Kasus Penggunaan yang Recomendado
- Ketika terjemahan berkualitas tinggi diperlukan
- Frekuensi penggunaan sedang

---

## Google AI (Gemini)

Modul terjemahan yang memanfaatkan teknologi AI terbaru Google.

### Keuntungan
- **Akurasi Tertinggi**: Mampu melakukan terjemahan berkualitas sangat tinggi dengan pemahaman kontekstual
- **Fleksibilitas**: Dapat menyesuaikan prompt untuk menyesuaikan gaya terjemahan
- **Dukungan Glosarium**: Dapat mempertahankan konsistensi terjemahan menggunakan glosarium

### Kerugian
- **Kunci API Diperlukan**: Memerlukan perolehan kunci API dan setup de Google AI Studio
- **Bayar per penggunaan**: Biaya berdasarkan penggunaan (walaupun minimal)
- **Kecepatan**: Membutuhkan waktu pemrosesan lebih lama depada modul lain karena berbasis LLM

### Kasus Penggunaan yang Recomendado
- Ketika terjemahan berkualitas tertinggi diperlukan
- Ketika gaya terjemahan kustom diperlukan
- Ketika terjemahan sadar konteks penting

---

## API ChatGPT (ATAU LLM Lokal)

Modul terjemahan menggunakan API ChatGPT atau LLM lokal.

### Keuntungan
- **Akurasi Tertinggi**: Tradução berkualitas tinggi oleh model bahasa besar
- **Fleksibilitas**: Dapat menyesuaikan prompt untuk menyesuaikan gaya terjemahan
- **Dukungan Glosarium**: Dapat mempertahankan konsistensi terjemahan menggunakan glosarium
- **Dukungan LLM Lokal**: Juga dapat menggunakan server LLM Anda sendiri

### Kerugian
- **Kunci API Diperlukan**: Memerlukan setup kunci API untuk setiap layanan (kecuali LLM lokal)
- **Bayar per penggunaan**: Biaya berdasarkan penggunaan (kecuali LLM lokal)
- **Kecepatan**: Waktu pemrosesan lebih lama
- **Persyaratan LLM Lokal**: PC spesifikasi tinggi diperlukan saat menjalankan LLM sendiri

### Kasus Penggunaan yang Recomendado
- Ketika terjemahan berkualitas tertinggi diperlukan
- Ketika gaya terjemahan kustom diperlukan
- Ketika privasi penting sambil menginginkan terjemahan berkualitas tinggi (LLM lokal)

---

## PLaMo

Modul terjemahan menggunakan LLM lokal khusus untuk Bahasa Jepang.

### Keuntungan
- **Khusus Jepang**: Dioptimalkan untuk terjemahan Jepang
- **Sepenuhnya Gratis**: Model sumber terbuka tanpa biaya
- **Privasi**: Berjalan secara lokal, data tidak dikirim ke luar
- **Offline**: Tidak ada koneksi internet yang diperlukan

### Kerugian
- **Persyaratan Spesifikasi Tinggi**: Memerlukan PC berkinerja tinggi termasuk GPU
- **Penggunaan Memori**: Memerlukan jumlah memori yang besar (8GB atau lebih direkomendasikan)
- **Kecepatan**: Pemrosesan membutuhkan waktu tanpa GPU

### Kasus Penggunaan yang Recomendado
- Ketika Anda memiliki PC berkinerja tinggi
- Ketika privasi adalah prioritas utama
- Ketika kualitas terjemahan Jepang penting

---

## Cara Memilih Modul

| Tujuan                              | Modul yang Recomendado                           |
| ----------------------------------- | ----------------------------------------------------- |
| Mulai menggunakan segera            | **Bergamot** atau **Google Tradução**               |
| Tradução berkualitas tertinggi    | **Google AI** atau **API ChatGPT**                    |
| Menjaga biaya rendah                | **Bergamot** atau **DeepL (dalam tingkat gratis)**    |
| Fokus privasi                       | **Bergamot** atau **PLaMo**                           |
| Penggunaan frekuensi tinggi         | **Bergamot** atau **DeepL**                           |
