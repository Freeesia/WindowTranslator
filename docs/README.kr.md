# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)

WindowTranslator는 Windows 애플리케이션의 창을 번역하는 도구입니다.

[JA](README.md) | [EN](README.en.md) | [DE](README.de.md) | [KR](README.kr.md) | [ZH-CN](README.zh-cn.md) | [ZH-TW](README.zh-tw.md)

## 목차
- [ WindowTranslator](#-windowtranslator)
  - [목차](#목차)
  - [다운로드](#다운로드)
    - [설치판 ](#설치판-)
    - [포터블 버전](#포터블-버전)
  - [사용법](#사용법)
    - [Google 번역 ](#google-번역-)
  - [기타 기능](#기타-기능)

## 다운로드
### 설치판 ![추천](https://img.shields.io/badge/%E3%82%AA%E3%82%B9%E3%82%B9%E3%83%A1-brightgreen)
[GitHub Releases 페이지](https://github.com/Freeesia/WindowTranslator/releases/latest)에서 `WindowTranslator-(버전).msi` 파일을 다운로드하여 실행하면 설치할 수 있습니다.  
설치 동영상:  
[![설치 동영상](https://github.com/user-attachments/assets/b5babc02-715b-43bc-ba97-f23078ffd39b)](https://youtu.be/wvcbCLA9chQ?t=7)

### 포터블 버전
[GitHub Releases 페이지](https://github.com/Freeesia/WindowTranslator/releases/latest)에서 zip 파일을 다운로드하여 원하는 폴더에 압축 해제하십시오.  
- `WindowTranslator-(버전).zip` : .NET 환경 필요  
- `WindowTranslator-full-(버전).zip` : .NET 비종속

## 사용법

### Google 번역 ![기본](https://img.shields.io/badge/Default-brightgreen)

1. `WindowTranslator.exe`를 실행하고 번역 버튼을 클릭합니다.  
   ![번역 버튼](images/translate.png)
2. 번역할 애플리케이션의 창을 선택한 후, "OK" 버튼을 클릭합니다.  
   ![창 선택](images/select.png)
3. "전체 설정" 탭의 "언어 설정"에서 원본 언어와 번역할 언어를 선택합니다.  
   ![언어 설정](images/language.png)
4. 설정 후 "OK" 버튼을 클릭하여 설정 창을 닫습니다.  
   > OCR 기능이 필요한 경우, 안내에 따라 설치해 주십시오.
5. 브라우저가 열리며 Google 로그인 화면이 표시됩니다.  
   ![로그인 화면](images/login.png)
6. 로그인 후, 권한 요청 화면에서 "전체 선택" 후 "계속" 버튼을 클릭합니다.  
   ![권한 화면](images/auth.png)
7. 잠시 후 번역 결과가 오버레이로 표시됩니다.  
   ![번역 결과](images/result.png)

> [!NOTE]
> WindowTranslator는 다양한 번역 모듈을 지원합니다. 여기서는 기본값인 Google 번역 사용법을 안내합니다.  
> Google 번역은 번역 가능한 텍스트 양이 적으므로, 사용량이 많은 경우 다른 모듈을 고려해 보세요.  
> 사용 가능한 번역 모듈의 전체 목록은 아래 동영상 또는 [Wiki](https://github.com/Freeesia/WindowTranslator/wiki#translation)를 참조하십시오.
> 
> |               |                                사용법 동영상                                 | 장점                          | 단점                                 |
> | ------------- | :------------------------------------------------------------------------: | ----------------------------- | ------------------------------------ |
> | Google 번역   |                                  TBD                                     | 설정이 간편함<br/>무료          | 번역 한도가 낮음<br/>정확도가 낮음      |
> | DeepL         | [![DeepL 설정 동영상](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0) | 무료 할당량 많음<br/>번역 속도 빠름 | 정확도가 낮음                        |
> | GoogleAI      | [![Google AI 설정 동영상](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I) | 높은 정확도                  | 소액 결제가 필요함                     |
> | LLM (클라우드)  |                                  TBD                                     | 높은 정확도                  | 소액 결제가 필요함                     |
> | LLM (로컬)     |                                  TBD                                     | 무료 서비스                  | 고성능 PC 필요                         |

## 기타 기능

자세한 기능은 [Wiki](https://github.com/Freeesia/WindowTranslator/wiki)를 참조하십시오.

---  
개인정보 처리방침: [개인정보 처리방침](PrivacyPolicy.kr.md)

> ※ 이 문서는 기계 번역되었습니다.