# WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub version](https://badge.fury.io/gh/Freeesia%2FWindowTranslator.svg)](https://badge.fury.io/gh/Freeesia%2FWindowTranslator)
[![NuGet version](https://badge.fury.io/nu/WindowTranslator.Abstractions.svg)](https://badge.fury.io/nu/WindowTranslator.Abstractions )

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

### 기타 설정

#### 번역 결과를 다른 창에 표시하기

번역 결과를 다른 창에 표시할 수 있습니다.  
설정 화면의 '전체 설정' 탭의 '번역 결과 표시 모드'에서 '캡처 창'을 선택하고 '확인' 버튼을 눌러 설정 화면을 닫습니다.
![표시 모드 설정](images/settings_window.png)

번역할 앱을 선택하면 번역 결과가 별도의 창에 표시됩니다.
![창 모드](images/window_mode.png)

#### 특정 애플리케이션의 창을 항상 번역하기

특정 애플리케이션이 실행될 때 WindowTranslator가 애플리케이션을 감지하여 번역을 시작하도록 설정할 수 있다.

1. `WindowTranslator.exe`를 실행하여 설정 화면을 엽니다.  
  ![설정](images/settings.png)
1. `SettingsViewModel` 탭에서 `Register to startup command`의 `실행` 버튼을 눌러 로그온 시 자동 실행되도록 설정합니다.
  ![시작 등록](images/startup.png)
1. '전체 설정' 탭의 '자동 번역 대상'에 번역할 애플리케이션의 프로세스 이름을 입력합니다.  
  ![자동 번역 대상](images/always_translate.png)
  * '한번 번역 대상으로 선택한 프로세스가 실행되면 자동으로 번역하기'에 체크하면 자동으로 번역 대상으로 등록됩니다.
1. 설정이 완료되면 'OK' 버튼을 눌러 설정 화면을 닫습니다.
2. 이후 대상 프로세스가 시작될 때 번역을 시작할지 여부를 알려주는 알림이 표시됩니다.  
  ![알림](images/notify.png)

##### 알림이 표시되지 않는 경우

알림이 표시되지 않는다면 '응답 불가'가 활성화되어 있을 수 있습니다.  
다음과 같은 방법으로 알림을 활성화하세요.

1. Windows의 '설정'에서 '시스템'의 '알림' 설정을 엽니다.  
 ![설정](images/win_settings.png)
1. '응답하지 않음을 자동으로 켜기'를 선택하고 '전체 화면 모드에서 앱을 사용할 때'의 체크를 해제합니다.
  ![응답 불가](images/full.png)
1. '우선 알림 설정하기'의 '앱 추가'를 누릅니다.  
 ![알림 설정](images/notification.png)
 ![우선순위 알림](images/priority.png)
1. 'WindowTranslator'를 선택합니다.
  ![앱 선택](images/select_app.png)


> Translated with www.DeepL.com/Translator (free version)