# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)

WindowTranslator는 윈도우 애플리케이션의 창을 번역하는 도구입니다.

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md)

## 다운로드

[GitHub 릴리즈 페이지](https://github.com/Freeesia/WindowTranslator/releases/latest)에서 zip을 다운로드하여 원하는 폴더에 압축을 풀고 압축을 해제합니다.

* `WindowTranslator-(버전).zip`은 .NET이 설치된 환경에서 동작합니다.
* `WindowTranslator-full-(버전).zip`은 .NET이 설치되지 않은 환경에서도 작동합니다.

## 사용방법

### 사전 준비

#### 언어 설정

Windows의 언어 설정에 번역할 언어와 번역할 언어를 추가해 주세요.   
[Windows 언어 추가 방법](https://support.microsoft.com/ja-jp/windows/windows-%E7%94%A8%E3%81%AE%E8%A8%80%E8%AA%9E%E3%83%91%E3%83%83%E3%82 %AF-a5094319-a92d-18de-5b53-1cfc697cfca8)   

#### DeepL API 키 받기

[DeepL 사이트](https://www.deepl.com/ja/pro-api)에서 사용자 등록을 하고 API 키를 발급받는다.  
(필자는 무료 플랜의 API 키로 동작을 확인했지만, 유료 플랜의 API 키로도 동작할 것으로 예상합니다)

### 실행

#### 최초 설정

1. `WindowTranslator.exe`를 실행하여 설정 화면을 엽니다.  
  ![설정](images/settings.png)
2. '전체 설정' 탭의 '언어 설정'에서 번역할 원/번역 대상 언어를 선택합니다.  
  ![언어 설정](images/language.png)
3. 'DeepLOptions' 탭의 API Key: DeepL 의 API Key를 입력합니다.
  ![DeepL 설정](images/deepl.png)
1. 설정이 완료되면 'OK' 버튼을 눌러 설정 화면을 닫는다.

#### 번역 시작

1. `WindowTranslator.exe`를 실행하고 번역 버튼을 누릅니다.  
  ![번역 버튼](images/translate.png)
2. 번역하고자 하는 앱의 창을 선택하고 'OK' 버튼을 누른다.
  ![창 선택](images/select.png)
3. 번역 결과가 오버레이로 표시됩니다.  
  ![번역 결과](images/result.png)


> Translated with www.DeepL.com/Translator (free version)