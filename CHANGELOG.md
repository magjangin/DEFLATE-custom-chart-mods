# 변경 이력

버전 기준은 `DEFLATE custom chart/Core/DeflateMod.cs`의 `DeflateMod.Version`입니다. 릴리즈는 `tools/release.ps1`이 이 값과 이 파일 최상단 `## [버전]`, `Properties/AssemblyInfo.cs`가 같은지 확인한 뒤 만듭니다.

## [1.1.1] - 2026-11-23

기준 게임 빌드는 1.1.0과 같습니다: DEFLATE **1.2** (Steam Build `26402105`), MelonLoader **0.7.3**.

### 수정
- BMS `#RANDOM`/`#IF` 분기가 있으면 모든 분기의 노트를 한꺼번에 읽어 노트가 겹쳐 나오던 문제: 1번 분기(`#IF 1`, `#SETRANDOM`이면 그 값)만 읽고 경고를 남깁니다
- 커버로 정렬상 첫 PNG가 잡혀 BMS 배경 이미지 등이 자켓으로 뜨던 문제: 이름에 `cover`/`jacket`/`thumbnail`/`thumb`가 들어간 PNG·JPG를 먼저 고릅니다
- 48 kHz 음악 안내(`[BMS 타이밍]`)가 실행당 한 번만 나와 두 번째 곡부터 빠지던 문제

### 개선
- STOP(`#STOPxx`/09 채널)과 LN 채널(5x/6x)을 만나면 조용히 무시하지 않고 경고를 남깁니다(지원하지 않는 기능)
- info.txt에 아티스트가 없으면 기본값 대신 BMS `#ARTIST`를 씁니다
- 옛 버전이 만든 `config.txt`에 없는 설정 키(`LogLevel`, `SmokeTest` 등)를 기본값으로 파일 끝에 덧붙입니다(기존 값·주석은 그대로). `config.txt`는 임시 파일에 쓴 뒤 바꿔 넣어 반쯤 쓴 파일이 남지 않습니다

### 성능
- `NoteSway`/`NoteSpeedChaos`의 노트마다 불리는 경로에서 Unity `Mathf`(IL2CPP 호출) 대신 관리 코드 `MathF`/`Math`를 씁니다
- 인게임 HUD 로그 훅(`HUDControl.RefreshSongMetaUI`)을 `LogLevel=0`에서는 아예 걸지 않습니다
- 커버 텍스처를 밉맵 없이 만들어 메모리·디코딩 시간을 줄였습니다

### 확인 상태
- 클라우드 환경에 게임 DLL이 없어 **빌드·실기 확인 전**입니다. 게임 PC에서 `dotnet build -c Release`(경고 0) 후 스모크 테스트(`SmokeTest=1`)로 확인하세요.

## [1.1.0] - 2026-11-23

첫 공개 릴리즈입니다. 기준 게임 빌드: DEFLATE **1.2** (Steam Build `26402105`), Unity `2022.3.62f2`, MelonLoader **0.7.3**.

### 추가
- `hwa/` 폴더의 커스텀 곡을 원곡 블록 복제로 곡 목록에 주입 (앨범/단독 곡/루트 배치 자동 판별)
- 커스텀 BMS 차트(노트·홀드·드롭·라인바), BGM, BGA(mp4), 자켓 주입. 곡 목록 프리뷰에도 적용
- 플레이 옵션 `savecustomkey/config.txt`: `AutoPlay`, `BlockSave`, `NoteSway`, `NoteSpeedChaos`, `LogLevel`(0/1/2), `SmokeTest`
- 무인 스모크 테스트(`SmokeTest=1`): 첫 커스텀 곡을 자동 선택·오토플레이하고 `[SmokeTest] PASS/FAIL`을 남김
- 업데이트 내성: `SafePatcher`(훅 클래스 단위 격리 + 사전 JIT 검사), `GameCompat`(빌드 지문), 대상 존재 가드, 내성 리플렉션, 열거형 이름 기반 레인 판정, 핫패스 서킷 브레이커
- `GameCompat`가 Steam 매니페스트의 업데이트 대기(`TargetBuildID`/`StateFlags`)를 경고하고, `LogLevel=1`에서 자동 업데이트 설정을 안내
- BMS·info.txt·config.txt 인코딩 자동 판별(`TextDecoder`): BOM → UTF-8 → Shift-JIS/CP949/GB18030. Shift-JIS BMS의 제목·키음 파일명이 깨지던 문제 해결

### 수정
- 게임 1.2 대응: `LaneType` 중첩 이동, `DiscID` 형식, `DiffStarsCtrl` NRE 스팸
- 숙주 곡 BGM과 커스텀 BGM이 겹쳐 나오던 문제
- 곡 선택/인게임 컨텍스트가 로딩 씬을 건너뛰는 경로에서 꼬이던 문제
- BMS 노트가 두 레인에 복제되던 문제(노트 하나 = 레인 하나), 48 kHz 음악에서 노트가 점점 일찍 나오던 문제

### 알려진 한계
- 결과 화면·롱노트·드롭 레인·킥/스네어 키음 노트는 실기 미확인
- `BlockSave=1`은 키 설정·볼륨 등 게임 설정 저장도 막습니다
