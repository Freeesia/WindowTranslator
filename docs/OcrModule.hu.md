# OCR modulok

A WindowTranslator több OCR modul közül választhat a képernyőn lévő szöveg felismeréséhez.  
Minden modulnak megvannak a saját jellemzői, és a felhasználási esetnek megfelelő modul kiválasztásával pontosabb szövegfelismerés érhető el.

## Új Windows karakterfelismerés (Beta) ![Alapértelmezett](https://img.shields.io/badge/Alapértelmezett-brightgreen)

A Microsoft által biztosított helyi OCR modul.

### Előnyök
- **Felismerési pontosság**: A legjobb felismerési pontossággal rendelkezik
- **Sebesség**: Nagyon gyors feldolgozási sebesség

### Hátrányok
- **Memóriahasználat**: Csak a felismerési feldolgozáshoz több mint 1 GB memóriát használhat
- **Működési környezet**: Egyes környezetekben nem működik (Windows 10 vagy újabb ajánlott)

---

## Windows szabványos karakterfelismerés

Az OCR motor, amely a Windows 10 és újabb verziókban alapértelmezett.

### Előnyök
- **Memóriahasználat**: Könnyű, alacsony memóriahasználattal
- **Működési környezet**: Széles körben elérhető Windows 10 és újabb verziókban

### Hátrányok
- **Felismerési pontosság**: Összetett betűtípusoknál vagy kézírásnál gyengébb lehet
- **Beállítás**: A nyelvi adatok manuális telepítése szükséges lehet

---

## Tesseract OCR

Nyílt forráskódú OCR motor.

### Előnyök
- **Többnyelvű támogatás**: Több mint 100 nyelvet támogat
- **Stabilitás**: Hosszú múltra visszatekintő megbízható motor

### Hátrányok
- **Felismerési pontosság**: Más OCR motorokhoz képest gyengébb lehet

---

## Modul kiválasztása

Kérjük, válassza ki a modult a következő sorrendben, a magas felismerési pontosság alapján:

1. Új Windows karakterfelismerés (Beta)
2. Windows szabványos karakterfelismerés
3. Tesseract OCR
