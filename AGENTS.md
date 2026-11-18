# DEFLATE Custom Chart & Song Injector Mod - Handover Guide (AGENTS.md)

> 본 문서는 **DEFLATE (`dizzylab.castor`)** 커스텀 차트 및 곡 주입기 모드에 대한 멀티 에이전트 및 개발자 핸드오버 문서입니다.  
> 인게임 실기 검증 및 구조적 안정화가 완료된 최신 아키텍처와 개발 노하우를 기록합니다.

---

## 1. 대상 게임 버전 & 환경 고정 여부
- **게임명:** DEFLATE (`dizzylab.castor`)
- **배포 플랫폼:** Steam (PC)
- **Unity 버전:** `2022.3.62f2 (7670c08855a9)` (64-bit)
- **런타임 백엔드:** **Unity Il2Cpp (x64)**
- **모드 로더:** MelonLoader v0.6+ (`net6.0` 런타임 환경)
- **버전 고정 & Steam 업데이트 관리:**
  - Steam 자동 업데이트 방지를 위해 `steamapps/appmanifest_<appid>.acf` 파일의 `AutoUpdateBehavior`를 `1`(게임 실행 시에만 업데이트)로 설정 권장.
  - 버전 다운그레이드가 필요한 경우 DepotDownloader를 사용해 특정 매니페스트 버전으로 롤백.

---

## 2. 게임 런타임 백엔드 & 디컴파일/정적 분석 경로
- **런타임 백엔드:** Il2Cpp (C# 코드가 C++ 네이티브 바이너리 `GameAssembly.dll`로 AOT 컴파일됨).
- **시그니처 분석 도구:** 프로젝트 내 독립 콘솔 툴 **`SignatureDumper`** (`SignatureDumper/`)
  - **원칙:** 게임 런타임(CECIL, 키 트리거)에 의존하지 않고, MelonLoader가 생성한 `MelonLoader/Il2CppAssemblies/` 내 어셈블리(`Assembly-CSharp.dll`, `Il2CppSonicBloom.Koreo.dll` 등)를 `System.Reflection`으로 순수 오프라인 정적 분석.
  - **출력 경로:** `Decompiled/Il2Cpp/` 및 `Decompiled/Il2Cppdizzylab/`
  - **스켈레톤 복원 범위:** 클래스 접근 제어자, 상속/인터페이스, 파라미터(`ref`/`out`/기본값), Getter/Setter 프로퍼티 스켈레톤 복원 (메서드 바디는 네이티브 컴파일되어 존재하지 않음).

---

## 3. 핵심 설계 의도 & Rationale (우회 및 방어 설계 배경)

### ① 에셋 번들 변조 대신 런타임 GameObject 복제 & 메모리 주입 채택
- **이유:** Addressables 에셋 번들(`.bundle`)을 직접 변조하면 Steam 파일 무결성 검사 시 깨지고 게임 패치 시 모드가 파괴됩니다.
- **방식:** 원곡 트랙(1순위 `WindShifter`, 2순위 첫 번째 유효 원곡 블록 자동 폴백)의 `MainTrackListBlock` GameObject를 런타임에 `UnityEngine.Object.Instantiate`로 복제하고 고유 ID를 부여한 뒤, `MainTrackList.tracks` (`Il2CppReferenceArray<MainTrackListBlock>`) 배열을 `N -> N + 커스텀곡수`로 확장 주입합니다. 원본 곡과 커스텀 곡이 완벽히 공존합니다.

### ② 프리뷰 컨텍스트 분리 (`HwaAssetManager.IsInSongSelectContext`)
- **이유:** 곡 목록 씬에서 커서를 옮길 때 곡 BGA/BGM 프리뷰를 위해 `TrackAssetManager.LoadAudioClip`을 후킹해야 합니다. 그러나 이 훅이 로딩 씬/인게임 씬까지 무차별로 동작하면, 실제 게임플레이 오디오 로드 요청을 가로채 원본 로직을 스킵시켜버려 인게임 로딩 무한 대기 또는 크래시를 유발합니다.
- **해결책:** `LoadingGamePlay.Start` 진입 시 `IsInSongSelectContext = false`로 설정하여 프리뷰 훅을 즉시 비활성화하고, 곡 선택 씬(`MainTrackList.Start`) 복귀 시 다시 `true`로 켭니다.

### ③ 내성 강화 리플렉션 래퍼 및 오타/인덱싱 최적화 (`CustomTrackWrapper` & `Il2CppReflectionHelper`)
- **이유:** Unity Il2Cpp 게임은 버전 패치 시 필드/프로퍼티의 이름이나 대소문자, 접두사(`_`, `m_`), 메서드 시그니처가 변경되거나 상속 계층 구조가 바뀔 수 있습니다. 또한 게임 원본 코드(`GameData`, `LoadingGamePlay`)에 `NowTrackAurthor`, `NowTracLabel` 같은 오타 필드가 존재합니다.
- **해결책:**
  - `Il2CppReflectionHelper`: `GetValueResilient`/`SetValueResilient`/`InvokeMethodResilient`로 `BaseType` 상속 체인까지 캐시 기반 리플렉션 탐색을 수행하며(`DeclaredOnly` 계층 순회, 파라미터 없는 메서드 캐싱, 정적 폴백 배열 캐싱으로 힙 할당 제거), `NowTrackAuthor`/`NowTrackLabel`/`NowTrackScore` 등 정정 표기 폴백으로 패치 내성을 확보합니다.
  - `HwaAssetManager.ApplyMetaToGameData` & `CustomTrackWrapper.ApplySongMetadata`: 곡 메타데이터(제목/아티스트/매퍼 `NowTrackScore`/BGA 제작자/앨범)/커버/난이도 별 표기를 `MainTrackListBlock`과 `GameData`에 일관 주입합니다.
  - **성능 & 메모리 최적화:** `CustomSongLibrary`는 `TrackID`/`Title` O(1) 딕셔너리 인덱스를 유지하고 `Scan()` 재스캔 시 기존 `Texture2D`/`Sprite`/`AudioClip`을 `ResetLoadedAssets()`로 즉시 해제합니다. `BmsChart`는 파싱 시 레인별 노트 인덱스(`_notesByLane`)를 미리 구축하고(`GetNotesForLane` O(1)), `BmsLaneMapper.ResolveLaneId(trackID, LaneType)`로 열거형 박싱/문자열 할당 없이 레인을 판별하며, `CustomSongEntry.LoadBgmCoroutine`은 `Time.realtimeSinceStartup` 실시간 타임아웃 가드로 고프레임 환경에서의 조기 타임아웃을 방지합니다.

### ④ BMS 시간 계산 공식
- 사용자 프로필 및 BMS 표준 공식 준수:
  $$\text{time} = \text{prevTime} + \frac{\Delta\text{tick} \times 240}{\text{bpm} \times \text{TicksPerMeasure}}$$
  (`TicksPerMeasure = 3840`, `TicksPerBeat = 960`).
- 마디선 및 4분음표 박자선(`BmsBarLine`)을 계산하여 인게임 라인바(`LineBarController`)에 완벽 주입.

---

## 4. 핵심 후킹 타깃 시그니처 & 주입 파이프라인

| 후킹 클래스 & 메서드 | 타이밍 | 역할 및 주입 로직 |
| :--- | :--- | :--- |
| `MainTrackList.Start` | Postfix | `High` 우선순위로 원본 `WindShifter` 복제 및 O(1) 등록, `Normal` 커버 주입, `Last` 카탈로그 로깅 |
| `TrackListBlockCtrl.SetSelected` | Postfix | 곡 목록 커서 이동 시 현재 활성 트랙(`HwaAssetManager.Active`) 갱신 및 커버 적용 |
| `MainTrackList.GotoSceneData` | Prefix | 곡 플레이 시작 확정 시 `ApplyMetaToGameData`로 커스텀 메타데이터(제목, 아티스트, 커버 등) 주입 |
| `TrackAssetManager.LoadAudioClip` | Prefix | 곡 목록 프리뷰 중 커스텀 BGM 비동기 로드 및 콜백 호출 (원본 로드 스킵) |
| `SimpleRandomVideo.LoadVideoSmart` | Prefix | 곡 선택/결과/인게임 BGA VideoPlayer에 커스텀 MP4 URL 주입 |
| `LoadingGamePlay.Start` | Postfix | 프리뷰 컨텍스트 비활성화(`IsInSongSelectContext = false`), 로딩 UI 메타데이터 주입(오타 필드 폴백 포함) |
| `RhythmGameController.Start` | Postfix | 인게임 BGM 0ms 즉시 할당(`EnsureInGameCustomBgm`), 오토플레이(`AutoMode`) 강제, 판정 파라미터 로깅 |
| `RhythmGameController.InitializeKoreographyTracks` | Postfix | `dropEventSamples`에 O(1) 드롭 레인 노트 주입 및 원본 잔여 이벤트 정리 |
| `RhythmGameController.LoadKoreographyEvents` | Postfix | **[핵심]** 각 레인별(`lane_1`~`lane_4`) BMS 차트 이벤트(`KoreographyEvent`) O(1) 레인 인덱스 기반 주입 |
| `RhythmGameController.ReplaceLaneEventsFromTrack` / `AtSample` | Postfix | 재시작·모드 전환 시 레인 이벤트가 원본 차트로 복원되지 않도록 커스텀 BMS 노트 재주입 |
| `RhythmGameController.LoadLineBarEvents` / `ReplaceLineBarEventsFromTrack` / `AtSample` | Postfix | BMS 마디/박자 기반 커스텀 라인바 이벤트 주입 및 재시작 복원 방지 |
| `GameData.SaveGameData` | Prefix | `BlockSave=1`일 때 세이브 방지 (스코어 덮어쓰기 방지) |
| `Key_indicator.Start` | Prefix | 곡 시작 시 거슬리는 키 가이드 UI 강제 숨김 |

---

## 5. 시도했다가 실패한 것들 (Dead Ends / Failed Attempts - 중복 삽질 방지)

1. **`InitializeKoreographyTracks` Prefix에서 차트 주입 시도 (실패):**
   - 시점상 Prefix에서는 `playingKoreo`가 아직 null 상태입니다. 이 시점에 이벤트를 주입하려 하면 NullReferenceException이 발생하거나 무시됩니다.
   - **해결:** 차트 로드가 완료된 직후 각 레인별로 불리는 `RhythmGameController.LoadKoreographyEvents(trackID, lane)` Postfix에서 주입해야 합니다.
2. **`LaneController.AddEventToLane`으로 대량 노트 주입 시도 (비효율/실패):**
   - 개별 단건 노트를 1개씩 처리하는 메서드이므로 대량 차트 로드 시 성능 저하 및 동기화 누락이 발생합니다.
   - **해결:** `lane.laneEvents.Add(newEvt)`로 리스트에 직접 주입하는 것이 가장 안정적입니다.
3. **`System.Action` 대입 시도 (타입 불일치 크래시):**
   - `TrackAssetManager.LoadAudioClip` 콜백 파라미터는 `Il2CppSystem.Action<AudioClip>` 및 `Il2CppSystem.Action`입니다. 표준 닷넷 `System.Action`을 직접 넘기면 IL2CPP 런타임에서 마샬링 예외가 터집니다.
4. **로딩 씬에서 BGM 프리뷰 훅 공유 (크래시):**
   - 곡 선택 씬 프리뷰용으로 오디오를 가로채는 로직이 로딩 씬의 실제 BGM 로드까지 가로채어 원본 로더가 먹통이 됨.
   - **해결:** `IsInSongSelectContext` 플래그로 씬 전환 시점을 철저히 격리.
5. **사본 블록의 ID 재생성 실패 시 단순 Destroy 폐기 (개선 완료):**
   - 게임 패치로 `RegenerateID()`가 오동작할 때 사본이 전부 파괴되던 문제를, `CUSTOM_` 접두사의 `Guid.NewGuid()` 폴백 ID 부여 로직으로 완벽 방어.

---

## 6. 빌드 명령 및 배포 경로

### 빌드 명령
```powershell
# 기본 게임 경로(H:\steam\steamapps\common\DEFLATE\)로 빌드 및 자동 배포
dotnet build -c Release

# 다른 게임 설치 경로 지정 빌드
dotnet build -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\DEFLATE\"
```

### 배포 경로
- 빌드 완료 시 `.csproj`의 `DeployToMods` 타깃에 의해 다음 경로로 자동 복사됩니다:
  `$(GameDir)Mods\DEFLATE custom chart.dll`
- 커스텀 곡 및 에셋 배치 경로:
  `$(GameDir)hwa\` (하위 앨범 폴더 또는 단독 곡 폴더)
- 사용자 설정 파일:
  `$(GameDir)savecustomkey\config.txt`
