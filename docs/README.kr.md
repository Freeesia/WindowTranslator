# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![Crowdin](https://badges.crowdin.net/windowtranslator/localized.svg)](https://crowdin.com/project/windowtranslator)

WindowTranslator는 Windows 애플리케이션 창을 번역하기 위한 도구입니다.

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md) | [VI](./README.vi.md)

## 목차
- [ WindowTranslator](#-windowtranslator)
  - [목차](#목차)
  - [다운로드](#다운로드)
    - [설치 버전 ](#설치-버전-)
    - [포터블 버전](#포터블-버전)
  - [사용법](#사용법)
    - [Bergamot ](#bergamot-)
  - [기타 기능](#기타-기능)

## 다운로드
### 설치 버전 ![추천](https://img.shields.io/badge/추천-brightgreen)

[GitHub 릴리스 페이지](https://github.com/Freeesia/WindowTranslator/releases/latest)에서 `WindowTranslator-(버전).msi`를 다운로드하여 실행하고 설치합니다.  
설치 안내 비디오⬇️  
[![설치 안내 비디오](https://github.com/user-attachments/assets/b5babc02-715b-43bc-ba97-f23078ffd39b)](https://youtu.be/wvcbCLA9chQ?t=7)

### 포터블 버전

[GitHub 릴리스 페이지](https://github.com/Freeesia/WindowTranslator/releases/latest)에서 zip 파일을 다운로드하여 원하는 폴더에 압축을 풉니다.  
- `WindowTranslator-(버전).zip` : .NET 환경 필요  
- `WindowTranslator-full-(버전).zip` : .NET 독립

## 사용법

### Bergamot ![기본값](https://img.shields.io/badge/기본값-brightgreen)

1. `WindowTranslator.exe`를 실행하고 번역 버튼을 클릭합니다.  
   ![번역 버튼](images/translate.png)
2. 번역하려는 애플리케이션 창을 선택하고 "확인" 버튼을 클릭합니다.  
   ![창 선택](images/select.png)
3. "일반 설정" 탭의 "언어 설정"에서 원본 언어와 대상 언어를 선택합니다.  
   ![언어 설정](images/language.png)
4. 설정을 완료한 후 "확인" 버튼을 클릭하여 설정 화면을 닫습니다.  
   > OCR 기능 설치가 필요할 수 있습니다.
   > 지시에 따라 설치하십시오.
5. 잠시 후 번역 결과가 오버레이로 표시됩니다.  
   ![번역 결과](images/result.png)

> [!NOTE]
> WindowTranslator에서는 다양한 번역 모듈이 사용 가능합니다.  
> Google 번역은 번역할 수 있는 텍스트 양이 적으며, 자주 사용하는 경우 다른 모듈 사용을 고려해보세요.  
> 사용 가능한 번역 모듈 목록은 아래 동영상이나 [위키](https://github.com/Freeesia/WindowTranslator/wiki#翻訳)에서 확인할 수 있습니다.
> 
> |                |                                                          사용법 동영상                                                           | 장점                    | 단점                        |
> | :------------: | :-----------------------------------------------------------------------------------------------------------------------------------: | :---------------------------- | :----------------------------------- |
> |   Bergamot     | | 완전 무료<br/>번역 제한 없음<br/>번역 속도 빠름 | 번역 정확도가 낮음<br/>1GB 이상의 여유 메모리 필요 |
> |   Google 번역   | [![Google 번역 설정 동영상](https://github.com/user-attachments/assets/bbf45370-0387-47e1-b690-3183f37e06d2)](https://youtu.be/83A8T890N5M)  | 완전 무료 | 낮은 번역 제한<br/>번역 정확도가 낮음 |
> |     DeepL      |   [![DeepL 설정 동영상](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0)   | 무료 사용량 많음<br/>번역 속도 빠름 | |
> |     Gemini     | [![Google AI 설정 동영상](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I) | 번역 정확도 높음 | 소액 결제 필요 |
> | ChatGPT (클라우드) | TBD | 번역 정확도 높음 | 소액 결제 필요 |
> | ChatGPT (로컬) | TBD | 서비스 자체는 무료 | 고사양 PC 필요 |

## 기타 기능

번역 모듈 외에도 WindowTranslator에는 다양한 기능이 있습니다.  
더 많은 정보를 원하시면 [위키](https://github.com/Freeesia/WindowTranslator/wiki)를 확인하세요.

---
[개인정보 처리방침](PrivacyPolicy.md)

이 문서는 일본어에서 기계 번역을 사용하여 번역되었습니다.