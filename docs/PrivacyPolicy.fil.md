---
title: Patakaran sa Privacy
description: Patakaran sa Privacy ng WindowTranslator
---

Ang Patakaran sa Privacy na ito ay nangangasiwa sa paggamit ng "WindowTranslator" (dito ay tatawaging "ang Aplikasyon").

## Panimula
Ang Aplikasyon ay open source at available sa GitHub ( https://github.com/Freeesia/WindowTranslator ).  
Ang Aplikasyon ay karaniwang hindi nangongolekta ng personal na impormasyon o datos ng paggamit mula sa mga user. Gayunpaman, kapag ang mga user ay tahasang pumili na magpadala ng mga error report, ang minimal na impormasyon na kinakailangan para sa teknikal na solusyon ng problema ay kinokolekta.

## Pagkolekta ng Personal na Impormasyon

### Impormasyon na Kinokolekta ng Aplikasyon  
Ang Aplikasyon ay hindi direktang nangongolekta ng personal na datos tulad ng user input, kasaysayan ng pakikipag-ugnayan, o impormasyon ng device sa panahon ng normal na paggamit.
Gayunpaman, kapag ang mga user ay tahasang pumili na magpadala ng mga error report, ang kinakailangang impormasyon para sa teknikal na solusyon ng problema ay kinokolekta.

### Mga Log ng Error at Crash Report  
Ang Aplikasyon ay nangongolekta ng sumusunod na impormasyon lamang kapag ang mga user ay tahasang nagsagawa ng pagsusumite ng error report:

- Impormasyon ng operasyon ng aplikasyon
- Impormasyon ng hardware specification ng PC
- Impormasyon ng OS environment
- Mga kalagayan ng pagkakaroon ng error

Ang impormasyong ito ay ginagamit lamang para sa teknikal na solusyon ng problema at pagpapabuti ng kalidad ng aplikasyon, at hindi naglalaman ng personal na nakakakilanlan na impormasyon.
Ang pagsusumite ng error report ay ganap na kusang-loob at hindi kailanman ipapadala nang awtomatiko nang walang pahintulot ng user.

## Paghawak ng Google User Data  
Kapag nag-access sa mga serbisyo ng Google Apps Script, ang Aplikasyon ay kumukuha lamang ng minimum na kinakailangang mga authentication token.  
Walang personal na impormasyon ng user na ginagamit lampas sa authentication na ito.

### Paggamit ng Nakolektang Data  
Ang nakuhang impormasyon ng authentication ay ginagamit lamang upang magsagawa ng Google Apps Script sa loob ng Aplikasyon.  
Ang authentication na ito ay mahigpit na limitado sa layunin ng pagpapagana ng paggamit ng mga serbisyo ng Google.

### Pagbabahagi o Paglipat ng Data  
Ang provider ng Aplikasyon ay hindi nangongolekta, nag-iimbak, nagbabahagi, o naglilipat ng anumang impormasyon ng authentication o iba pang Google user data.  
Lahat ng datos ng authentication ay nakaimbak sa PC ng user at ginagamit lamang para sa pag-access sa mga serbisyo ng Google.

### Proteksyon ng Data  
Ang datos ng authentication ay ligtas na nakaimbak sa PC ng user, at ang provider ng Aplikasyon ay walang anumang karapatan sa pag-access sa datos na ito.  
Ang mga karagdagang hakbang sa seguridad ay depende sa pamamahala ng seguridad ng PC ng mismong user.

### Pagpapanatili at Pagtanggal ng Data  
Ang mga authentication token ay nakaimbak lamang sa PC ng user.  
Ang pagtanggal ng mga token na ito ay nakukumpleto sa pamamagitan ng pag-alis ng user sa folder na matatagpuan sa `%APPDATA%\StudioFreesia\WindowTranslator\GoogleAppsScriptPlugin`.

## Paggamit ng mga Translation Engine at Third-Party Libraries  
Ang Aplikasyon ay dinisenyo upang suportahan ang maraming translation engine, kabilang ang Google Translate at DeepL.  
Ang mga translation engine at nauugnay na library na ito ay maaaring mangolekta ng user data o impormasyon ng paggamit alinsunod sa kanilang mga kani-kaniyang patakaran sa privacy.  
Mahigpit na inirerekomenda sa mga user na suriin ang mga patakaran sa privacy ng mga serbisyo ng pagsasalin na ginagamit nila.

## Impormasyon sa Pakikipag-ugnayan  
Para sa anumang mga katanungan o feedback tungkol sa Patakarang Privacy na ito, mangyaring makipag-ugnayan sa:  
GitHub Account: [Freeesia](https://github.com/Freeesia)

## Mga Pagbabago sa Patakarang Privacy na Ito  
Ang Patakarang Privacy na ito ay maaaring baguhin nang walang paunang abiso dahil sa mga pagbabago sa batas o functionality ng aplikasyon.  
Ang anumang mga pagbabago ay agad na ipaaabot sa pamamagitan ng Aplikasyon o ng GitHub repository.

> [!WARNING]
> Tandaan: Ang file na ito ay machine translated mula sa orihinal na Japanese version, na siyang master version.
