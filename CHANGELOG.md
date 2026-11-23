# 변경 이력

버전 기준은 `DEFLATE custom chart/Core/DeflateMod.cs`의 `DeflateMod.Version`입니다. 릴리즈는 `tools/release.ps1`이 이 값과 이 파일 최상단 `## [버전]`, `Properties/AssemblyInfo.cs`가 같은지 확인한 뒤 만듭니다.

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
