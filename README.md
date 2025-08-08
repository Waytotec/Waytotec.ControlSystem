
# 🚀 Waytotec.ControlSystem

WPF 기반의 디바이스 통합 제어 및 모니터링 시스템입니다.  
카메라, 비상벨, 전광판 등 다양한 장비를 실시간으로 제어하고 상태를 시각적으로 확인할 수 있도록 설계되었습니다.

---

## 🏗️ 프로젝트 구조

```
Waytotec.ControlSystem/
├── Waytotec.ControlSystem.App/              # WPF UI 진입점 (MVVM 구조)
│   ├── Views/                               # 사용자 화면 XAML (예: DashboardView, SettingsView 등)
│   ├── ViewModels/                          # 각 View에 대응되는 ViewModel
│   ├── Resources/                           # 스타일, 템플릿, 브러시 등
│   ├── Assets/                              # 이미지, 아이콘 등
│   ├── Themes/                              # 다크모드/라이트모드 지원 등
│   └── App.xaml.cs                          # DI 및 MainWindow 실행
│
├── Waytotec.ControlSystem.Core/             # 비즈니스 로직, 도메인 모델 정의 (MVVM의 Model + Services)
│   ├── Models/                              # 데이터 모델 (DeviceStatus, CameraInfo 등)
│   └── Interfaces/                          # 서비스 인터페이스 (예: ICameraService, IAlertService)
│   └── Enums/                               # 장비 타입, 상태 코드 등
│   └── Helpers/                             # 유틸리티 클래스
│
├── Waytotec.ControlSystem.Infrastructure/   # 외부 리소스 연동 (네트워크, DB, 파일 IO 등)
│   └── Services/                            # MockDeviceService 등
│   └── Network/                             # TCP, UDP 통신 관련 클래스
│   └── Storage/                             # 설정 저장/불러오기 (JSON, XML 등)
│   └── Logging/                             # 로그 처리 시스템
│   └── Security/                            # 암호화, 인증 처리 등
│
├── Waytotec.ControlSystem.IoC/              # DI 구성 (Microsoft.Extensions.DependencyInjection)
│   └── ContainerConfig.cs
│
└── Waytotec.ControlSystem.Tests/            # 단위 테스트 프로젝트
```

---

## ⚙️ 기술 스택

- .NET 8 (WPF)
- MVVM 아키텍처
- Dependency Injection (Microsoft.Extensions.DependencyInjection)
- Reactive UI 지원 (비동기 Task 기반)
- 향후 [UNO Platform](https://platform.uno/)으로 크로스 플랫폼 확장 예정

---

## 🔧 실행 방법

```bash
git clone https://github.com/your-org/Waytotec.ControlSystem.git
cd Waytotec.ControlSystem
dotnet restore
dotnet build
```

Visual Studio에서 `Waytotec.ControlSystem.sln` 열기

---

## 📝 요구 사항 정리

|---|---|

### 1. 분석 및 설계
```text
 - 각 프로그램(DCS, LCS) 전체 기능 실행 후 기능 분석 및 코드 분석 기간 필요
 - 전체 Device 모델별 타입 분류 (카메라, 비상벨, 인터폰...)
 - 전체 Device 모델별 기능 실행 및 테스트
 - 단계별 기능 소스 코드 구현
 - 단계별 화면 디자인 (UI/UX)
 - 프로그램 배포 프로젝트 생성
```

### 2. 협의사항
```text
 - DB서버 또는 File서버 필요한가? (프로그램 업데이트, 히스토리 기록)
   - 폐쇄망 현장은 이력 관리 불편...
 - Client 실행 환경은 오로지 Windows OS 뿐인가? (향후 Mobile 또는 Pad, Web 지원 여부)
 - 실행했던 기능 History 관리 기능 
 - 현장별 폴더 관리 기능 필요?
 - 폐쇄망 주차 서버 PC를 이용한 로컬 챗봇 기능 필요?
```

---

## 📦 향후 기능 확장

- 디바이스 실시간 상태 갱신
- 네트워크 통신(TCP/UDP)
- 디바이스 제어 명령 전송
- 다중 뷰 및 사용자 권한 제어
- UNO Platform 기반 모바일/웹/데스크탑 확장

---

## 🏢 About Waytotec

이 프로젝트는 **Waytotec**에서 개발하는 산업용 통합 디바이스 제어 시스템입니다.
