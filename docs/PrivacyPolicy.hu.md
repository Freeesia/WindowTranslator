---
title: Adatvédelmi irányelvek
description: WindowTranslator adatvédelmi irányelvei
---

Ezek az adatvédelmi irányelvek a „WindowTranslator" alkalmazás (a továbbiakban: „Alkalmazás") használatát szabályozzák.

## Bevezetés
Az Alkalmazás nyílt forráskódú szoftver, és elérhető a GitHubon (https://github.com/Freeesia/WindowTranslator).  
Az Alkalmazás alapvetően nem gyűjt személyes adatokat vagy használati adatokat felhasználóitól. Ha azonban a felhasználók kifejezetten úgy döntenek, hogy hibajelentéseket küldenek, minimális információ kerül összegyűjtésre a technikai problémák megoldásához.

## Személyes adatok gyűjtése

### Az Alkalmazás által gyűjtött információk  
Az Alkalmazás normál használat során nem gyűjt közvetlenül személyes adatokat, például felhasználói beviteli adatokat, interakciós előzményeket vagy eszközadatokat.
Ha azonban a felhasználók kifejezetten úgy döntenek, hogy hibajelentéseket küldenek, összegyűjtésre kerülnek a technikai problémák megoldásához szükséges információk.

### Hibanaplók és összeomlási jelentések  
Az Alkalmazás csak akkor gyűjti az alábbi információkat, ha a felhasználók kifejezetten elvégzik a hibajelentés küldését:

- Az alkalmazás működésével kapcsolatos információk
- A számítógép hardver specifikációjára vonatkozó információk
- Az operációs rendszer környezetére vonatkozó információk
- A hiba bekövetkezésének körülményei

Ezeket az információkat kizárólag technikai problémák megoldására és az alkalmazás minőségének javítására használják, és nem tartalmaznak személyazonosításra alkalmas információkat.
A hibajelentés küldése teljesen önkéntes, és soha nem kerül sor automatikusan a felhasználó hozzájárulása nélkül.

## A Google felhasználói adatok kezelése  
A Google Apps Script szolgáltatások elérésekor az Alkalmazás csak a minimálisan szükséges hitelesítési tokeneket szerzi be.  
A felhasználó személyes adatait nem használják fel ezen hitelesítésen túl.

### Az összegyűjtött adatok felhasználása  
A megszerzett hitelesítési információkat kizárólag a Google Apps Script Alkalmazáson belüli futtatásához használják.  
Ez a hitelesítés szigorúan a Google-szolgáltatások használatának lehetővé tételére korlátozódik.

### Adatok megosztása vagy átvitele  
Az Alkalmazás szolgáltatója nem gyűjt, tárol, oszt meg vagy ad át semmilyen hitelesítési információt vagy egyéb Google-felhasználói adatot.  
Az összes hitelesítési adat a felhasználó számítógépén tárolódik, és kizárólag a Google-szolgáltatásokhoz való hozzáféréshez használják.

### Adatvédelem  
A hitelesítési adatok biztonságosan tárolódnak a felhasználó számítógépén, és az Alkalmazás szolgáltatójának nincs hozzáférési joga hozzájuk.  
A további biztonsági intézkedések a felhasználó számítógépének biztonsági kezelésétől függnek.

### Adatmegőrzés és -törlés  
A hitelesítési tokenek csak a felhasználó számítógépén tárolódnak.  
Ezeket a tokeneket a felhasználó törli az `%APPDATA%\StudioFreesia\WindowTranslator\GoogleAppsScriptPlugin` mappában lévő mappa eltávolításával.

## Harmadik féltől származó fordítómotor és könyvtárak használata  
Az Alkalmazás úgy lett tervezve, hogy több fordítómotort támogasson, beleértve a Google Translate-et és a DeepL-t.  
Ezek a fordítómotorok és a kapcsolódó könyvtárak a saját adatvédelmi irányelveiknek megfelelően gyűjthetnek felhasználói adatokat vagy használati információkat.  
A felhasználókat erősen ajánlott tanulmányozni az általuk használt fordítási szolgáltatások adatvédelmi irányelveit.

## Kapcsolatfelvételi adatok  
Az adatvédelmi irányelvekkel kapcsolatos kérdések vagy megjegyzések esetén kérjük, lépjen kapcsolatba velünk:  
GitHub fiók: [Freeesia](https://github.com/Freeesia)

## Az adatvédelmi irányelvek változásai  
Ezek az adatvédelmi irányelvek előzetes értesítés nélkül módosíthatók a jogszabályok vagy az alkalmazás funkcióinak változása miatt.  
Minden változásról azonnal értesítjük az Alkalmazáson vagy a GitHub tárhelyen keresztül.

> [!WARNING]
> Figyelmeztetés: Ezt a fájlt gépi fordítással fordítottuk az eredeti japán verzióból, amely a főverziót képezi.
