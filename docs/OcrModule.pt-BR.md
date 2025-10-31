# Módulo OCR

O WindowTranslator permite que você escolha entre vários módulos de OCR para reconhecer texto na tela.  
Cada módulo tem suas próprias características, e escolher o módulo adequado para o seu caso de uso permitirá um reconhecimento de texto mais preciso.

## Pengenalan Karakter Windows Baru (Beta) ![Padrão](https://img.shields.io/badge/Padrão-brightgreen)

Módulo OCR lokal yang disediakan oleh Microsoft.

### Keuntungan
- **Akurasi Pengenalan**: Memiliki akurasi pengenalan tertinggi
- **Cepat**: Kecepatan pemrosesan sangat cepat

### Kerugian
- **Penggunaan Memori**: Mungkin menggunakan lebih de 1GB memori hanya untuk pemrosesan pengenalan
- **Lingkungan Operasi**: Mungkin tidak berfungsi dalam beberapa lingkungan (Windows 10 atau lebih baru direkomendasikan)

---

## Pengenalan Karakter Standar Windows

Mesin OCR yang disertakan secara standar dengan Windows 10 dan lebih baru.

### Keuntungan
- **Penggunaan Memori**: Ringan dengan penggunaan memori rendah
- **Lingkungan Operasi**: Tersedia secara luas di Windows 10 dan lebih baru

### Kerugian
- **Akurasi Pengenalan**: Mungkin lemah dengan font kompleks atau teks tulisan tangan
- **Setup**: Instalasi manual data bahasa mungkin diperlukan

---

## Tesseract OCR

Mesin OCR sumber terbuka.

### Keuntungan
- **Dukungan Multibahasa**: Mendukung lebih de 100 bahasa
- **Stabilitas**: Mesin yang dapat diandalkan dengan sejarah panjang

### Kerugian
- **Akurasi Pengenalan**: Mungkin lebih rendah dibandingkan OCR lain

---

## Memilih Modul

Silakan pilih modul yang berfungsi dalam urutan akurasi pengenalan tinggi berikut:

1. Pengenalan Karakter Windows Baru (Beta)
2. Pengenalan Karakter Standar Windows
3. Tesseract OCR
