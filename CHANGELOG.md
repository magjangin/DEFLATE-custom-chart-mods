# 변경 이력

버전 기준은 `DEFLATE custom chart/Core/DeflateMod.cs`의 `DeflateMod.Version`입니다. 릴리즈는 `tools/release.ps1`이 이 값과 이 파일 최상단 `## [버전]`, `Properties/AssemblyInfo.cs`가 같은지 확인한 뒤 만듭니다.

## [1.1.3] - 2026-11-24

기준 게임 빌드는 그대로입니다: DEFLATE **1.2** (Steam Build `26402105`), Unity `2022.3.62f2`, MelonLoader **0.7.3**.

### 수정
- **CP949(한국어 ANSI)로 저장한 BMS**의 한글이 반각 가타카나로 깨지던 문제: 한글 완성형의 약 2/3가 Shift-JIS로도 오류 없이 읽혀, 한글이 적은 파일(예: 제목 `화영왕`, 키음 `하이햇.wav`)은 Shift-JIS로 잘못 판별됐습니다. 그러면 `홀드 시작.wav` 같은 키음 이름이 깨져 홀드·킥/스네어 판정이 조용히 빠질 수 있었습니다. 이제 읽은 결과가 반각 가타카나투성이면 CP949로 다시 읽습니다
- 3자리 확장 ID(`#WAV001`) BMS에서 `03` 채널(16진 BPM 변경)을 통째로 버리던 문제: `03` 값은 ID가 아니라 BPM이라 항상 2자리입니다
- **원곡과 제목이 같은 커스텀 곡**(공식 곡의 커스텀 채보 등)이 있으면, 원곡을 골랐을 때 제목 폴백으로 커스텀 BGM·차트·커버가 붙을 수 있던 문제: 원곡과 겹치는 제목은 ID로만 판정하고 시작 시 경고를 남깁니다

### 개선
- 키음 이름 `킥`·`스네어`(한글)도 킥·스네어 레인으로 배치합니다(홀드 키음은 이미 한글 이름을 받았음)

### 개발·업데이트 대응
- 게임 없이 도는 단위 테스트 37개(`tests/`): BMS 시간·BPM 변경·마디 길이·분기·홀드·라인바, 레인 매핑, 인코딩 판별. GitHub Actions가 PR마다 실행하고, `tools/release.ps1`도 빌드 전에 실행합니다
- `tools/check_hook_targets.py`: 게임 업데이트 후 새 시그니처 덤프와 훅을 대조해 대상 삭제(MISSING)·오버로드(OVERLOADED)·파라미터 이름 변경(PARAM)을 게임 실행 전에 알려줍니다(AGENTS.md §6의 bash 스니펫을 대체)

### 확인 상태
- 게임 PC에서 `dotnet build -c Release -warnaserror` 경고 0 / 오류 0, 단위 테스트 37/37 통과.
- 인게임 스모크 PASS는 1.1.2에 이어 **아직 미확인**입니다(게임 창을 앞에 두고 `SmokeTest=1`로 확인 필요).

## [1.1.2] - 2026-11-23

기준 게임 빌드는 그대로입니다: DEFLATE **1.2** (Steam Build `26402105`), Unity `2022.3.62f2`, MelonLoader **0.7.3**.

### 업데이트 내성
- BMS 노트·라인바 주입 훅 6개와 BGM/BGA 교체 훅(`TrackAssetManager.LoadAudioClip`/`LoadVideoURL`, 인게임 `LoadVideo`)이 게임 메서드의 **파라미터 이름**(`trackID`, `lane`, `key` …) 대신 **타입**으로 인자를 받습니다. 게임 업데이트로 파라미터 이름만 바뀌어도 훅이 빠지지 않습니다(예전에는 패치 단계에서 실패해 조용히 원곡 차트로 플레이됨)
- 시작 로그에 MelonLoader 버전 지문을 추가했습니다. 기준(0.7.3)과 다르면 `[GameCompat] MelonLoader … 불일치!` 경고를 남깁니다

### 성능
- `LogLevel=0`에서 상세/디버그 로그의 문자열을 만들지 않습니다. 예전에는 출력하지 않는 로그도 매번 문자열을 조립했고, 그 안의 `VideoPlayer.url`·`name` 같은 IL2CPP 프로퍼티까지 읽었습니다(영상 재생·레인 재로드·곡 시작마다)
- 인게임에서 커스텀 BGM이 아직 로딩 중일 때 여러 훅이 각자 대기 코루틴을 띄우던 것을 곡당 하나로 줄였습니다

### 수정
- 스트리밍으로 연 커스텀 BGM의 요청을 클립을 받자마자 해제하던 것을, 클립과 함께 해제하도록 바꿨습니다(긴 곡 도중 소리가 끊길 위험 제거)
- 스모크 테스트: 곡 목록 커서가 움직이지 않으면 200회 헛이동 대신 새 목록을 기다렸다가, 끝내 안 오면 "게임 창이 백그라운드/인트로에 머묾" 힌트와 함께 FAIL합니다. BGM이 실제로 흘러가는지(1초 간격 재생 위치)도 판정에 넣었습니다

### 릴리즈 도구
- `tools/release.ps1 -Publish`가 Windows PowerShell 5.1에서 `git fetch`의 정상 stderr 출력을 오류로 처리해 게시 단계에서 멈추던 문제를 고쳤습니다(종료 코드로 판정)

### 확인 상태
- 게임 PC에서 `dotnet build -c Release` 경고 0 / 오류 0. v1.1.1 코드도 이 PC에서 경고 0으로 빌드되는 것을 확인했습니다.
- **인게임 스모크 테스트는 아직 PASS를 확인하지 못했습니다.** 게임 창이 백그라운드인 상태로 실행해 인트로에서 멈췄습니다(모드 오류 아님, 위 스모크 테스트 개선의 계기). 게임 창을 앞에 둔 채 `SmokeTest=1`로 한 번 확인하세요.

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
