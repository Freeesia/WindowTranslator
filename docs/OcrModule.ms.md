# Modul OCR

WindowTranslator membenarkan anda memilih daripada pelbagai modul OCR untuk mengecam teks pada skrin.  
Setiap modul mempunyai ciri-cirinya sendiri, dan memilih modul yang sesuai untuk kes penggunaan anda akan membolehkan pengecaman teks yang lebih tepat.

## Pengecaman Aksara Windows Baharu (Beta) ![Lalai](https://img.shields.io/badge/Lalai-brightgreen)

Modul OCR tempatan yang disediakan oleh Microsoft.

### Kelebihan
- **Ketepatan Pengecaman**: Mempunyai ketepatan pengecaman tertinggi
- **Pantas**: Kelajuan pemprosesan sangat pantas

### Kekurangan
- **Penggunaan Memori**: Mungkin menggunakan lebih 1GB memori hanya untuk pemprosesan pengecaman
- **Persekitaran Operasi**: Mungkin tidak berfungsi dalam sesetengah persekitaran (Windows 10 atau lebih baharu disyorkan)

---

## Pengecaman Aksara Standard Windows

Enjin OCR yang disertakan secara standard dengan Windows 10 dan lebih baharu.

### Kelebihan
- **Penggunaan Memori**: Ringan dengan penggunaan memori rendah
- **Persekitaran Operasi**: Tersedia secara meluas pada Windows 10 dan lebih baharu

### Kekurangan
- **Ketepatan Pengecaman**: Mungkin lemah dengan fon kompleks atau teks tulisan tangan
- **Persediaan**: Pemasangan manual data bahasa mungkin diperlukan

---

## Tesseract OCR

Enjin OCR sumber terbuka.

### Kelebihan
- **Sokongan Berbilang Bahasa**: Menyokong lebih 100 bahasa
- **Kestabilan**: Enjin yang boleh dipercayai dengan sejarah panjang

### Kekurangan
- **Ketepatan Pengecaman**: Mungkin lebih rendah berbanding OCR lain

---

## Memilih Modul

Sila pilih modul yang berfungsi mengikut urutan ketepatan pengecaman tinggi berikut:

1. Pengecaman Aksara Windows Baharu (Beta)
2. Pengecaman Aksara Standard Windows
3. Tesseract OCR
