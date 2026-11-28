# 변경 이력

버전 기준은 `DEFLATE custom chart/Core/DeflateMod.cs`의 `DeflateMod.Version`입니다. 릴리즈는 `tools/release.ps1`이 이 값과 이 파일 최상단 `## [버전]`, `Properties/AssemblyInfo.cs`가 같은지 확인한 뒤 만듭니다.

## [1.1.10] - 2026-11-28

기준 게임 빌드는 그대로입니다: DEFLATE **1.2** (Steam Build `26402105`), Unity `2022.3.62f2`, MelonLoader **0.7.3**.

### 핵심 결함 수정 및 업데이트 내성 강화
- **인게임 라인바(LineBar) 난이도별 차트 완벽 연동:**
  - 기존 `BuildBmsLineBarEvents`에서 인게임 컨트롤러 미전달로 인해 Easy/Hard 플레이 시에도 라인바(마디선/박자선)가 Normal 차트 기준으로 고정 주입되던 버그 수정.
  - `LoadLineBarEvents`, `ReplaceLineBarEventsFromTrack`, `ReplaceLineBarEventsFromTrackAtSample`에 컨트롤러 인스턴스를 전달하여 현재 선택된 난이도(`gameData.nowDifficulty`)의 차트 마디선/박자선과 정확히 동기화.
- **세이브 데이터 차단(BlockSave) 조건부 Prepare 결함 수정:**
  - `GameData_SaveGameData_Patch`, `SaveGamePlayDataToFile_Patch`, `UpdateGamePlayData_Patch`의 `Prepare()`가 `ModConfig.Instance.BlockSave`를 검사하여 `BlockSave=0`일 때 훅 자체가 빠지던 문제 수정.
  - `BlockSave=0`(순정 곡 점수 저장 허용) 상태에서도 커스텀 곡 플레이(`HwaAssetManager.IsTargetTrackActive`) 시에는 100% 원천 차단되도록 패치를 상시 활성화.
- **BMS 정지(#STOP) 이벤트와 동일 틱 노트 타이밍 정밀화:**
  - STOP 이벤트와 동일한 틱(Tick)에 존재하는 노트가 정지 시간(stopSec) 가산 후로 밀리지 않고, 정지 직전의 올바른 시점에 정확히 판정되도록 타임라인 `PreStopTimeSeconds` 역추적 보정.
- **스모크 테스트 난이도별 차트 판정 연동:**
  - `SmokeTest.ProbeInGame`에서 현재 플레이 중인 난이도의 차트 노트 수를 기준으로 일치 여부를 검증하도록 개선.
- **CustomSongEntry 난이도별 차트 참조 일관성 확보:**
  - `EasyBmsFilePath` 등이 기본 차트와 동일할 때 `EasyChart` 속성이 `null`로 남지 않고 `Chart` 인스턴스로 일관되게 바인딩되도록 개선.
- **MemberFallbacks 후보군 보강:**
  - `GameData.nowDifficulty`, `GameData.targetKorePath`에 대한 백업 리플렉션 후보군 추가.

### 테스트 & 품질
- 오프라인 단위 테스트 1개 추가 (총 66개 전체 통과 확인): STOP과 동일 틱 노트 타이밍 검증, GameData 폴백 후보군 검증 등.
- 빌드 경고 0 / 오류 0 (`-warnaserror`), 훅 대상 대조 0건.

## [1.1.9] - 2026-11-28

기준 게임 빌드는 그대로입니다: DEFLATE **1.2** (Steam Build `26402105`), Unity `2022.3.62f2`, MelonLoader **0.7.3**.

### 신규 기능 및 세이브 보호 강화
- **BGA 비디오 지원 포맷 확장 및 가중치 랭킹 시스템:**
  - 기존 `.mp4`에 더해 Unity `VideoPlayer`가 지원하는 `.webm`, `.mov`, `.m4v` 동영상 포맷 지원.
  - 영상 파일명 키워드 가중치 판별 시스템(`BgaRank`)을 도입하여, 폴더 내 여러 영상이 존재할 경우 `pv`/`bga`/`video`/`movie`/`영상`이 포함된 영상을 우선 채택.
- **BMS 헤더 따옴표 자동 정제 및 `#PLAYER` 파싱 지원:**
  - `#TITLE`, `#ARTIST`, `#SUBTITLE`, `#GENRE`, `#LNOBJ`, `#STOP`, `#WAVxx`, `#BPMxx` 등 헤더 값에 둘러싸인 큰따옴표(`"`) 및 작은따옴표(`'`)를 자동 정제. 에디터 출력 포맷 차이로 인한 키음 누락 및 문자열 깨짐 방지.
  - `#PLAYER` 헤더 파싱을 지원하여 1P/2P 모드 메타데이터 안전 보존.
- **세이브 데이터 원천 차단(BlockSave) 범위 대폭 강화:**
  - 기존 `GameData.SaveGameData` 차단에 더해, `GameData.SaveGamePlayDataToFile` 및 `GameData.UpdateGamePlayData` 훅을 신설.
  - 커스텀 곡 플레이 시 원본 세이브 파일 및 게임 랭킹 데이터가 영구 세이브에 섞이거나 덮어씌워지는 위험을 100% 원천 차단.
- **결과 화면(Panel_Result) 메타데이터 재동기화:**
  - `Panel_Result.Awake` 시점에 `gameData`의 메타데이터(`Title`, `Author`, `Cover`, 난이도 별점)를 다시 한 번 커스텀 곡 정보로 재동기화하여 연출 일관성 확보.

### 테스트 & 품질
- 오프라인 단위 테스트 4개 추가 (총 65개 전체 통과 확인): BMS 헤더 따옴표/작은따옴표 자동 정제, `#PLAYER` 지시어 파싱 등.
- 빌드 경고 0 / 오류 0 (`-warnaserror`), 훅 대상 대조 0건.

## [1.1.8] - 2026-11-27

기준 게임 빌드는 그대로입니다: DEFLATE **1.2** (Steam Build `26402105`), Unity `2022.3.62f2`, MelonLoader **0.7.3**.

### 신규 기능 및 차트 호환성 강화
- **난이도별 차트 지원 (Easy / Normal / Hard):**
  - `info.txt`에서 `chart_easy`, `chart_normal`, `chart_hard` (및 `ez`, `nm`, `hd`, 한글 키워드 `이지 차트`, `노멀 차트`, `하드 차트` 등) 파싱 지원.
  - 곡 폴더 내 BMS 파일명 패턴(`*easy*.bms`, `*normal*.bms`, `*hard*.bms` 등) 자동 감지.
  - 특정 난이도 미지정 시 기본 BMS 차트로 자동 폴백(기존 단일 차트 완벽 호환).
  - 인게임 난이도 선택(`DiffCtrl`, `gameData.nowDifficulty`)과 연동하여 해당 난이도에 맞는 차트 동적 주입.
- **표준 BMS 롱노트 채널 (5x / 6x 채널) 완벽 지원:**
  - BMS 표준 롱노트 채널(51, 52, 53, 54, 56 및 2P 6x)을 단타로 버리거나 무시하지 않고 파싱.
  - Head-Tail 페어링(`PairLongNotesFromLnChannels`)으로 롱노트 변환 및 고아 Head 노트 단타 강등 안전 처리.
- **BMS 정지 명령 (#STOPxx 및 09 채널) 완벽 지원:**
  - `#STOPxx` 확장 정의 테이블 파싱 및 `09` 채널 이벤트 수집.
  - 타임라인 상 정지 시간(초) 누적 연산 및 타임라인 앵커링을 통해 STOP 기믹 이후의 모든 노트 싱크를 음악과 완벽히 일치하도록 보장.
- **인게임 주입 및 로깅 연동:**
  - `InGameRhythmHooks`에서 게임 난이도별 맞춤형 차트 주입 및 플레이 시작 로그에 난이도별 노트 수 상세 표기.

### 테스트 & 품질
- 오프라인 단위 테스트 4개 추가 (총 61개 전체 통과 확인): 난이도별 차트 키워드 파싱, 표준 5x 채널 롱노트 페어링, 고아 Head 단타 강등, #STOPxx 지연 누적, 16진 STOP 등.
- 빌드 경고 0 / 오류 0 (`-warnaserror`), 훅 대상 대조 0건.

## [1.1.7] - 2026-11-26

기준 게임 빌드는 그대로입니다: DEFLATE **1.2** (Steam Build `26402105`), Unity `2022.3.62f2`, MelonLoader **0.7.3**.

### 업데이트 내성 및 안정성 강화
- **복제 원곡 템플릿 탐색 3차 폴백 마련:** WindShifter 및 NM 차트 키 보유 블록이 모두 누락된 특이 환경에서도 임의의 유효 원곡 블록(`anyOriginalBlock`)으로 자동 폴백하여 곡 목록 사본 주입 파이프라인의 영속성 보장.
- **로딩 씬 UI 주입 리플렉션 내성 전면 강화:** `LoadingSceneHooks`에서 `NowTrackTitle`, `NowTrackPVAuthor`, `NowTrackDifficulty`, `NowTrackCover` 등 모든 UI 요소를 `Il2CppReflectionHelper` 백업 리플렉션으로 래핑하여 게임 업데이트로 멤버가 개명되어도 JIT 예외 없이 안전 주입.
- **MemberFallbacks 후보군 대폭 확장:** `LoadingUi` 전용 폴백 정의(Title, Author, Label, PvAuthor, Score, Difficulty, Cover, CoverBg) 및 `TrackBlock`/`GameData` 예비 후보군 보강.
- **BMS 다중 변속(BPM) 안정 정렬 및 타임라인 음수 방어:** 동일 틱에 여러 BPM 변경이 정의된 경우 원본 출현 순서(`OrderIndex`)를 보존하는 결정적 안정 정렬 적용 및 틱 음수 시간 예외 방어.
- **BGM 비동기 로딩 생명주기 및 경합 상태 가드:** `CustomSongEntry` BGM 로드 시 세대 카운터(`_loadGeneration`)를 도입하여 씬 전환이나 에셋 재스캔 후 이전 비동기 웹 요청이 뒤늦게 완료되어 생기는 상태 오염 및 누수 원천 차단.
- **일시정지 중 노트 흔들림(NoteSway) 가드:** `Time.timeScale <= 0f` 감지 시 연산을 즉시 건너뛰어 일시정지 메뉴에서의 미세 떨림 현상 방지.
- **SafePatcher 타입 로드 진단 로그 강화:** 패치 수집 시 `ReflectionTypeLoadException` 발생 시 내부 `LoaderExceptions`를 상세 로깅하도록 보강.

### 테스트 & 품질
- 오프라인 단위 테스트 4개 추가 (총 57개 통과 확인): 동일 틱 BPM 정렬 안정성, 역순 홀드 노트 고아 처리 방어, MemberFallbacks 무결성 검증 등.
- 빌드 경고 0 / 오류 0 (`-warnaserror`), 훅 대상 대조 0건.

## [1.1.6] - 2026-11-25

기준 게임 빌드는 그대로입니다: DEFLATE **1.2** (Steam Build `26402105`), Unity `2022.3.62f2`, MelonLoader **0.7.3**.

### 수정
- info.txt에 `subtitle:`(부제)이 `title:`보다 뒤에 있으면 **곡 제목이 부제로 바뀌던** 문제
- info.txt의 `album artist:`(앨범 아티스트)가 **곡 아티스트를 덮어쓰던** 문제(이제 무시합니다)

### 개선
- info.txt 키를 더 받습니다: `부제`/`subtitle`, `작곡`/`composer`(아티스트), `채보`(매퍼), 난이도 `ez`·`nm`·`hd`·`이지`·`노멀`·`노말`·`하드`. 난이도 값은 `★12`처럼 기호가 붙어도 읽습니다
- config.txt에 모르는 키(오타)가 있으면 조용히 무시하지 않고, 비슷한 키를 제안하며 경고합니다(예: `'NoteSwy'('NoteSway'?)`)
- 큰 커버(2048×2048 초과)는 메모리 사용량과 함께 경고합니다. 커버 스프라이트를 만들 때 윤곽 메시(Tight) 계산을 생략해 곡 목록 진입이 가벼워졌습니다(불투명한 자켓은 보이는 모양이 같음)
- 로그에 `hwa/` 스캔 시간과 곡 목록 주입 시간(ms)을 남깁니다. 곡이 많아 느려질 때 원인을 바로 볼 수 있습니다
- README에 info.txt 형식 설명을 추가하고 오래된 설명을 고쳤습니다

### 개발
- info.txt 파서를 게임 없이 테스트할 수 있게 분리했습니다(`Core/SongInfoParser.cs`). 단위 테스트 37 → 53개(info.txt, 중첩 `#IF`, 길이 0 마디, 짝 없는 홀드, UTF-16, 줄바꿈 처리)

### 확인 상태
- 빌드 경고 0 / 오류 0(`-warnaserror`), 단위 테스트 53/53, 훅 대상 대조 문제 0건.
- **인게임 확인은 1.1.5와 함께 다음 실기 때 합니다.** 이번 변경은 훅을 바꾸지 않았고, 게임 안에서 달라지는 것은 info.txt 해석·커버 스프라이트 생성 방식·로그입니다.

## [1.1.5] - 2026-11-25

기준 게임 빌드는 그대로입니다: DEFLATE **1.2** (Steam Build `26402105`), Unity `2022.3.62f2`, MelonLoader **0.7.3**.

### 업데이트 내성
- 게임 업데이트로 필드·프로퍼티가 사라지면, 모드가 그 멤버를 읽을 때마다 예외를 던진 뒤 백업 경로로 넘어갔습니다. 곡 목록을 만들 때 원곡마다 수십 번씩 반복돼 업데이트 직후 화면이 멈칫할 수 있었습니다. 이제 한 번 사라진 것으로 확인된 멤버는 기억해 두고 바로 백업 경로를 씁니다(경고는 1회)
- 쓰지 않는 원본 파라미터를 훅에서 지웠고, 읽기만 하는 훅 10개는 파라미터 **이름** 대신 **타입**으로 받습니다(PV 메뉴·곡 목록 영상·프리뷰 오디오·인게임 영상 동기화 등). 게임이 파라미터 이름만 바꿔도 이 훅들이 빠지지 않습니다
- 일반 플레이에서도 BMS 노트 주입이 빠지면 경고합니다. 게임 업데이트로 레인 로드 경로가 바뀌면 훅은 정상 적용되는데 호출되지 않아 원곡 차트로 플레이될 수 있었고, 예전에는 스모크 테스트를 돌려야만 알 수 있었습니다. 이제 곡 시작 때 주입 노트 수가 BMS와 다르면 `[커스텀 차트]` 경고가 남습니다

### 성능
- 곡 목록에 커스텀 곡을 넣을 때 복제할 원곡을 찾느라 원곡마다 18개 항목을 읽던 것을 3개로 줄였습니다

### 수정
- 레인을 불러올 때 게임 차트(Koreography)가 아직 준비되지 않았으면 BMS 노트 대신 레인이 통째로 비워질 수 있던 문제

### 확인 상태
- 빌드 경고 0 / 오류 0(`-warnaserror`), 단위 테스트 37/37, 훅 대상 대조 문제 0건.
- 게임 실행 시 훅 적용 `성공 45 / 건너뜀 8 / 실패 0`(LogLevel 1 기준, 바꾼 훅 포함), 빌드 지문 일치까지 확인했습니다. **인게임 스모크 테스트(PASS)는 아직 돌리지 않았습니다.**

## [1.1.4] - 2026-11-24

기준 게임 빌드는 그대로입니다: DEFLATE **1.2** (Steam Build `26402105`), Unity `2022.3.62f2`, MelonLoader **0.7.3**.

### 확인 상태
- **1.1.2·1.1.3 변경을 게임에서 확인했습니다.** 스모크 테스트 `PASS`(BMS 노트 552/552·9레인, 라인바 296, 커스텀 BGM 재생, BGA, 인게임 전환). 직접 플레이 경로(로딩 씬)로 145초 곡을 결과 화면까지 끊김 없이 재생(오토 552/552 Perfect), 결과 화면의 BGA·미리듣기 BGM·자켓도 확인. 오류·경고 0건.
- 빌드 경고 0 / 오류 0, 단위 테스트 37/37.

### 수정 (스모크 테스트, `SmokeTest=1` 전용)
- 노트 주입 집계를 곡마다 초기화합니다. 테스트 전에 다른 곡을 한 번 플레이하면 "1104개/18레인 ≠ 552"로 거짓 FAIL하던 문제
- 시작 직후 멈추는 진짜 원인은 게임 창 백그라운드가 아니라 **타이틀의 로그인 메뉴**였습니다(1.1.2의 안내가 틀렸음). 이제 "Offline mode를 골라 주세요"라고 안내하고, 고를 때까지 180초 기다렸다가 이어서 진행합니다

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
- 인게임 확인은 1.1.4 항목을 보세요(PASS).

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
