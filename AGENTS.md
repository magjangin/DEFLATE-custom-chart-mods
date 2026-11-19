# DEFLATE Custom Chart & Song Injector Mod - Handover Guide (AGENTS.md)

> 본 문서는 **DEFLATE (`dizzylab.castor`)** 커스텀 차트 및 곡 주입기 모드에 대한 멀티 에이전트 및 개발자 핸드오버 문서입니다.  
> 인게임 실기 검증 및 구조적 안정화가 완료된 최신 아키텍처와 개발 노하우를 기록합니다.
>
> **현재 검증 상태 (2026-11-19, 게임 1.2 / Build `26402105`):** 컴파일 성공(경고 0) · 훅 대상 48개 시그니처 대조 · 실행 시 `[SafePatcher] 성공 48 / 실패 0` · `[GameCompat] 일치` · `hwa/` 곡 스캔까지 확인.
> 곡 선택 화면 주입·BMS 노트 플레이 등 **게임플레이 실기 확인은 1.2에서 아직 수행하지 않음** (다음 작업자는 §6 4단계 체크리스트로 확인 후 이 줄을 갱신할 것).

---

## 1. 대상 게임 버전 & 환경 고정 여부
- **게임명:** DEFLATE (`dizzylab.castor`) — Steam App ID **`3921360`**
- **배포 플랫폼:** Steam (PC)
- **기준 게임 빌드:** 게임 버전 **`1.2`** / Steam Build ID **`26402105`** (GameAssembly.dll 2026-11-06 갱신분)
  - 이 값은 `Core/GameCompat.cs`의 `Reference*` 상수와 **항상 같이** 갱신합니다. 불일치 시 시작 로그에 `[GameCompat] … → 불일치!` 경고가 뜹니다.
- **Unity 버전:** `2022.3.62f2 (7670c08855a9)` (64-bit)
- **런타임 백엔드:** **Unity Il2Cpp (x64)**
- **모드 로더:** MelonLoader **v0.7.3 Open-Beta** (`net6` 런타임, 호스트는 시스템 .NET hostfxr 사용). v0.6대에서 개발되었고 0.7.3에서도 API 호환 확인.
- **버전 고정 & Steam 업데이트 관리 (사실관계 주의):**
  - 현재 `steamapps/appmanifest_3921360.acf`의 `AutoUpdateBehavior`는 `0`(항상 최신 유지)이며, 실제로 2026-11-06 자동 업데이트(1.2)로 모드가 깨졌습니다.
  - `AutoUpdateBehavior`를 `1`로 바꾸면 "실행할 때만 업데이트"가 될 뿐 **Steam으로 실행하는 순간 업데이트는 적용됩니다.** 완전 고정 수단이 아니므로, 업데이트를 감지하는 `[GameCompat]` 경고와 §6 플레이북을 1차 방어선으로 삼습니다.
  - 이전 빌드로의 롤백은 DepotDownloader로 특정 매니페스트를 받아 별도 폴더에 두는 방식만 확실합니다(원본 설치 폴더 덮어쓰기는 Steam 검증 시 되돌려짐).

---

## 2. 게임 런타임 백엔드 & 디컴파일/정적 분석 경로
- **런타임 백엔드:** Il2Cpp (C# 코드가 C++ 네이티브 바이너리 `GameAssembly.dll`로 AOT 컴파일됨).
- **시그니처 분석 도구:** 프로젝트 내 독립 콘솔 툴 **`SignatureDumper`** (`SignatureDumper/`)
  - **원칙:** 게임 런타임(CECIL, 키 트리거)에 의존하지 않고, MelonLoader가 생성한 `MelonLoader/Il2CppAssemblies/` 내 어셈블리(`Assembly-CSharp.dll`, `Il2CppSonicBloom.Koreo.dll` 등)를 `System.Reflection`으로 순수 오프라인 정적 분석.
  - **출력 경로:** `Decompiled/Il2Cpp/` 및 `Decompiled/Il2Cppdizzylab/`
  - **스켈레톤 복원 범위:** 클래스 접근 제어자, 상속/인터페이스, 파라미터(`ref`/`out`/기본값), Getter/Setter 프로퍼티 스켈레톤 복원 (메서드 바디는 네이티브 컴파일되어 존재하지 않음).
  - **실행:** `dotnet run --project SignatureDumper -c Release --framework net8.0` (net472/net8.0 멀티 타깃이라 `--framework` 없이 실행하면 실패).
  - **출력 형식 주의:** 현재 덤퍼는 중첩 타입을 `Outer+Inner.cs` 파일(상단 `// Nested in: Outer` 주석)로 분리하고 Il2Cpp 제네릭/델리게이트를 `Il2CppSystem.…`으로 완전 한정해 씁니다. 2026-11 이전 덤프는 중첩 타입을 최상위로 평탄화했으므로, 구 덤프와 처음 diff하면 파일 수백 개가 '변경'으로 보이는 노이즈가 생깁니다 → §6의 **훅 대상 시그니처 대조 스크립트**로 비교하세요.

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
  - `Il2CppReflectionHelper` & `MemberFallbacks`: `GetValueResilient`/`SetValueResilient`/`InvokeMethodResilient`로 `BaseType` 상속 체인까지 캐시 기반 리플렉션 탐색을 수행하며(`DeclaredOnly` 계층 순회, 파라미터 없는 메서드 캐싱, `WarnOnce` 중복 경고 억제, `TryAdaptValueForTargetType` 타입 안전 변환), 모든 백업 리플렉션 후보군을 `Core/Wrapper/MemberFallbacks.cs` 한 곳에 단일화(Single Source of Truth)하여 패치 시 수정 지점을 1곳으로 집약했습니다.
  - **Harmony 패치 등록 가드 (`Il2CppReflectionHelper.HasPatchTarget`)**: 문자열 기반(`[HarmonyPatch(typeof(...), "...")]`) 훅 9곳과 보조 UI/로깅 훅(`HUDHooks`, `KeyIndicatorHooks`)에 `Prepare()` 가드를 적용해 대상이 없으면 `[HarmonyGuard]` 경고 1회 후 '건너뜀'으로 처리합니다. 전체 훅의 실패 격리는 ⑤의 `SafePatcher`가 담당합니다.
  - `CustomTrackWrapper`: `SafeCast<MainTrackListBlock>` 실패(`null`) 시에도 `directGetter`/`directSetter`에 `null`을 전달하여 백업 리플렉션 경로가 실제로 동작하도록 보장합니다(`ReadBlockMember`/`WriteBlockMember`). `DiscID`(**`string`** — Il2Cpp 덤프 기준)를 포함한 전체 `MainTrackListBlock` 메타데이터를 래핑합니다.
  - `HwaAssetManager.ApplyMetaToGameData` & `CustomTrackWrapper.ApplySongMetadata`: 곡 메타데이터(제목/아티스트/매퍼 `NowTrackScore`/BGA 제작자/앨범)/커버/난이도 별 표기를 `MainTrackListBlock`과 `GameData`에 일관 주입합니다.
  - **엣지 케이스 & 파서 안전성:** `ModConfig`와 `CustomSongEntry.ParseInfoTxt`에서 UTF-8 BOM(`\uFEFF`)을 자동 제거하며, `InGameRhythmHooks`에서는 `playingKoreo.SampleRate <= 0` 방어 및 곡 시작 시 `NoteObject_UpdateNotePosition_Patch.ResetFrameCache()`로 프레임 캐시를 초기화합니다. `SongListHooks`의 카탈로그 스캔도 `CustomTrackWrapper` + 트랙 단위 예외 격리로 보호됩니다.
  - **성능 & 메모리 최적화:** `CustomSongLibrary`는 `TrackID`/`Title` O(1) 딕셔너리 인덱스를 유지하고 `Scan()` 재스캔 시 기존 `Texture2D`/`Sprite`/`AudioClip`을 `ResetLoadedAssets()`로 즉시 해제합니다. `BmsChart`는 파싱 시 레인별 노트 인덱스(`_notesByLane`)를 미리 구축하고(`GetNotesForLane` O(1)), `BmsLaneMapper.ResolveLaneId(trackID, object laneTypeValue)`로 열거형 **이름**만으로 레인을 판별하며(타입 비의존, ⑤ 표의 ⑥ 참고), `CustomSongEntry.LoadBgmCoroutine`은 `Time.realtimeSinceStartup` 실시간 타임아웃 가드로 고프레임 환경에서의 조기 타임아웃을 방지합니다.

### ④ BMS 시간 계산 공식
- 사용자 프로필 및 BMS 표준 공식 준수:
  $$\text{time} = \text{prevTime} + \frac{\Delta\text{tick} \times 240}{\text{bpm} \times \text{TicksPerMeasure}}$$
  (`TicksPerMeasure = 3840`, `TicksPerBeat = 960`).
- 마디선 및 4분음표 박자선(`BmsBarLine`)을 계산하여 인게임 라인바(`LineBarController`)에 완벽 주입.

### ⑤ 업데이트 내성 계층 (게임 1.2 업데이트 사고에서 도출)
게임이 업데이트되어도 "무엇이 깨졌는지 로그 첫 화면에서 알고, 깨진 훅만 빠지고 나머지는 계속 동작"하도록 다음 계층을 쌓았습니다.

| 계층 | 위치 | 방어 대상 | 로그 태그 |
| :--- | :--- | :--- | :--- |
| ① 빌드 지문 | `Core/GameCompat.cs` | 게임 버전·Unity·Steam Build ID가 기준과 다름을 시작 즉시 감지 | `[GameCompat]` |
| ② 패치 클래스 단위 격리 | `Core/SafePatcher.cs` + `[assembly: HarmonyDontPatchAll]` | 훅 하나의 실패가 나머지 훅 적용을 전부 중단시키는 Harmony `PatchAll()` 동작 | `[SafePatcher]` |
| ③ 훅 본문 사전 JIT | `SafePatcher.TryPrejitPatchMethods` / `WarnBrokenHelpers` | 훅 본문이 게임에서 사라진 필드·타입을 직접 참조하는 경우 | `[SafePatcher]` |
| ④ 대상 존재 가드 | 각 패치의 `Prepare()` → `HasPatchTarget` | 문자열 기반 훅의 대상 메서드 삭제·개명 | `[HarmonyGuard]` |
| ⑤ 내성 리플렉션 | `Il2CppReflectionHelper` + `MemberFallbacks` | 필드/프로퍼티 개명·접두사·대소문자 변경 | `[Get/Set/InvokeMethodResilient]` |
| ⑥ 열거형 타입 비의존 | `BmsLaneMapper.ResolveLaneId(string, object)`, `InGameRhythmHooks.ReadLaneTypeValue` | 열거형 선언 위치 이동(1.2의 `LaneType` → `LaneController.LaneType`) | — |
| ⑦ 핫패스 서킷 브레이커 | `NoteObject_UpdateNotePosition_Patch` | 매 프레임·노트마다 호출되는 훅이 깨져 예외 비용으로 프레임이 무너지는 상황 | `[NoteSway]` |

- **②가 필요한 이유:** Harmony `PatchAll()`은 패치 클래스를 순서대로 처리하다 하나라도 예외(대상 없음, 오버로드 추가로 인한 모호성, 파라미터명 변경)가 나면 그 뒤 훅이 **전부** 미적용됩니다. `nameof(X.Method)`도 컴파일 시 문자열로 박제되므로, 업데이트 후 재빌드 전 DLL에서는 문자열 훅과 똑같이 깨집니다. 그래서 자동 `PatchAll`을 끄고 `OnInitializeMelon`에서 클래스마다 `CreateClassProcessor(type).Patch()`를 try/catch로 돌립니다. `Prepare()=false`는 실패가 아니라 '건너뜀'으로 집계됩니다. **새 훅을 추가해도 별도 등록은 필요 없습니다**(`[HarmonyPatch]` 속성이 붙은 클래스를 자동 수집).
- **③이 필요한 이유:** .NET(CoreCLR)은 메서드를 **처음 호출할 때** JIT하며, 이때 참조 멤버가 없으면 `MissingMethodException`/`MissingFieldException`/`TypeLoadException`이 호출 지점(=Harmony가 만든 대체 메서드)에서 터집니다. 즉 훅 내부의 `try/catch`로는 잡히지 않고, Prefix라면 **원본 메서드까지 실행되지 못합니다.** `RuntimeHelpers.PrepareMethod`로 패치 전에 미리 JIT해 깨진 훅을 적용 단계에서 걸러냅니다(최선 노력 검사 — JIT가 해석을 지연하는 경로는 못 잡을 수 있음). 반대로 `GetValueResilient(..., () => gd.NowTrackID, ...)`처럼 **람다로 분리된 직접 접근**은 별도 메서드라서 그 JIT 실패가 래퍼 내부 try/catch에서 잡히고 백업 리플렉션으로 넘어갑니다 — 업데이트에 취약한 멤버 접근은 이렇게 람다/헬퍼로 분리해 두는 것이 원칙입니다.
- **⑥의 배경:** 1.2에서 최상위 `LaneType`이 `LaneController.LaneType`(중첩)으로 옮겨지고, 값 배치가 다른 `CommingNote.LaneType`이 새로 생겼습니다. 9월 빌드는 런타임에 최상위 `LaneType`을 찾지 못해 BMS 레인 주입 헬퍼가 무력화됐을 것입니다(훅 try/catch에 흡수되어 조용히 원본 차트로 플레이 — 1.2 실기 미확인 추정). 열거형 **멤버 이름**(`HiHat_3`, `Kick_Left` …)은 그대로이므로 `laneType` 값을 리플렉션으로 `object`로 읽어 `ToString()` 이름으로만 판정합니다. 레인 로드 시 레인당 1회 호출이라 할당 비용은 무시할 수준입니다.

---

## 4. 핵심 후킹 타깃 시그니처 & 주입 파이프라인

| 후킹 클래스 & 메서드 | 타이밍 | 역할 및 주입 로직 |
| :--- | :--- | :--- |
| `MainTrackList.Start` | Postfix | `High` 우선순위로 원본 `WindShifter` 복제 및 O(1) 등록, `Normal` 커버 주입, `Last` 카탈로그 로깅 |
| `TrackListBlockCtrl.SetSelected` | Postfix | 곡 목록 커서 이동 시 현재 활성 트랙(`HwaAssetManager.Active`) 갱신 및 커버 적용. 1.2에서 시그니처가 `(bool isSelected, bool animated = true)`로 확장됨 — Postfix는 `isSelected`만 받아도 Harmony 바인딩 유지 |
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
6. **게임 열거형 타입을 시그니처/비교식에 직접 사용 (1.2에서 붕괴):**
   - `ResolveLaneId(string, LaneType)`와 `lane.laneType == LaneType.HiHat_3`는 게임 1.2의 `LaneType` 중첩 이동으로 컴파일조차 안 되고, 기존 DLL에서는 런타임 TypeLoadException으로 레인 주입이 조용히 꺼집니다.
   - **해결:** 게임 쪽 열거형은 값을 `object`로 읽어 **이름**으로 판정(§3-⑤ 표의 ⑥). 새 코드에서도 게임 열거형 타입을 공용 헬퍼 시그니처에 노출하지 마세요.
7. **빌드 없이 커밋 (컴파일 불가 커밋 발생):**
   - `2c80477`은 `MainTrackListBlock.DiscID`(실제 `string`)를 `int`로 래핑해 **컴파일되지 않는 상태로 커밋·머지**되었습니다(당시 `Il2CppAssemblies` 부재로 빌드 검증 불가). 배포 DLL은 9/24 빌드에 머물러 있었습니다.
   - **해결:** 커밋 전 반드시 `dotnet build -c Release` 경고 0/오류 0 확인. 빌드할 수 없는 환경이면 커밋 메시지와 이 문서에 "미빌드"를 명시.
8. **`DEFLATE.exe` 직접 실행 (Fatal error: "Another instance is already running"):**
   - exe를 직접 실행하면 Steam DRM이 Steam 경유로 재실행시키고 첫 인스턴스는 곧 종료되는데, 그 사이 인스턴스 락에 걸린 두 번째 프로세스가 오류 대화상자를 띄웁니다(어셈블리 생성 자체는 첫 인스턴스에서 완료됨).
   - **해결:** 항상 `Start-Process "steam://rungameid/3921360"`로 실행.
9. **MelonLoader 자동 `PatchAll()`에 의존:**
   - 훅 하나의 예외로 그 뒤 훅이 전부 미적용되는 구조라, `Prepare()` 가드가 없는 `nameof` 훅 38개가 단일 장애점이었습니다.
   - **해결:** `[assembly: HarmonyDontPatchAll]` + `SafePatcher.PatchAll`(§3-⑤ 표의 ②).
10. **`DiffStarsCtrl.GenerateStarImages(int, Il2CppSystem.Nullable<Color> = null)`에서 기본 인자 생략 호출 (1.2 NRE 스팸):**
   - Il2CppInterop에서 `Il2CppSystem.Nullable<T>`는 박싱된 IL2CPP 값 타입 래퍼(C# 참조 타입)이므로, 기본 인자 `null`을 그대로 넘기면 `IL2CPP.Il2CppObjectBaseToPtrNotNull(null)`에서 `NullReferenceException`이 터집니다.
   - **해결:** `HwaAssetManager.SafeGenerateStarImages(ctrl, count)`를 통해 `new Il2CppSystem.Nullable<Color>()`(`HasValue == false`)를 명시 전달.
11. **인게임/곡 선택 이중 BGM 재생 (숙주 곡 BGM + 커스텀 곡 BGM 동시 출력):**
   - 인게임에서는 `RhythmGameController.EnsureAudioSourceBound`/`BindAudioClipWhenReady`가 숙주 Koreography의 `SourceClip`으로 `audioCom`을 덮어쓰고, 동시에 `LoadVideo` Prefix가 원본을 스킵하며 음소거 없이 `video.mp4`를 재생해 두 오디오가 겹쳤습니다. 곡 선택 씬에서도 `FavControl.PreloadNearbyTracks`로 `TrackAssetManager.audioCache`에 숙주 OGG가 캐시된 뒤 `FavControl.SafeLoadAndPlayAudio`/`PlayAudioWhenReady`가 숙주 클립을 재생하거나, 빠른 스크롤 시 지연 코루틴 콜백이 겹칠 수 있었습니다.
   - **해결:** 인게임 `EnsureAudioSourceBound`/`BindAudioClipWhenReady`/`StartAudioWhenReady` + `koreo.SourceClip` 교체 및 인게임/결과 `VideoPlayer` 강제 음소거(`MuteVideoPlayer`), 곡 선택 `FavControl.SafeLoadAndPlayAudio`/`PlayAudioWhenReady`/`TrackAssetManager.LoadVideoURL` 인터셉트, 빠른 스크롤 경쟁 상태 가드(`LoadCustomBgmForEntryCoroutine`), BGA 없는 커스텀 곡의 `VideoKey`·`DiscID` 초기화.

---

## 6. 게임 업데이트 대응 & 복구 플레이북 (Update Survival Playbook)

게임 패치 이후 일부 기능이 동작하지 않거나 경고가 뜰 때 다음 순서대로 복구합니다. (2026-11-19 1.2 대응을 이 순서로 수행함)

1. **로그 1차 진단 (`<GameDir>/MelonLoader/Latest.log`, 이전 실행은 `MelonLoader/Logs/`):**
   - `[GameCompat]` → `불일치!`면 게임이 바뀐 것. 현재 게임 버전/Build ID를 확인합니다.
   - `[SafePatcher] 훅 적용 요약` → `실패 N`과 실패 훅 이름, 바로 위의 개별 오류(대상 없음/모호성/파라미터명/본문 JIT 실패)를 확인합니다.
   - `[HarmonyGuard]` → `Prepare()`에서 대상 메서드를 못 찾아 건너뛴 훅.
   - `[GetValueResilient]` / `[SetValueResilient]` / `[InvokeMethodResilient]` → `WarnOnce`로 1회만 기록된 변경 필드/프로퍼티.
2. **어셈블리 갱신 & 시그니처 재덤프:**
   - **Steam 경유로** 게임을 1회 실행(`Start-Process "steam://rungameid/3921360"`)해 MelonLoader가 `MelonLoader/Il2CppAssemblies/`를 재생성하게 합니다. MelonLoader를 재설치했거나 버전을 올린 직후에는 이 폴더가 아예 없어 빌드가 불가능합니다.
   - 재덤프 전에 기존 `Decompiled/`를 스크래치 폴더에 복사해 둡니다(구/신 비교용, `Decompiled/`는 gitignore 대상).
   - `dotnet run --project SignatureDumper -c Release --framework net8.0`
3. **변경점 특정:**
   - `dotnet build -c Release`의 컴파일 오류가 가장 정확한 1차 목록입니다(단, 선언부 오류가 있으면 C#이 메서드 본문 오류 보고를 생략하므로 고친 뒤 반복 빌드).
   - 훅 대상 시그니처 대조(Git Bash, 저장소 루트):
     ```bash
     grep -ohE 'HarmonyPatch\(typeof\(([A-Za-z_]+)\), (nameof\([A-Za-z_]+\.([A-Za-z_]+)\)|"([A-Za-z_]+)")' "DEFLATE custom chart/Hooks/"*.cs \
       | sed -E 's/HarmonyPatch\(typeof\(([A-Za-z_]+)\), (nameof\([A-Za-z_]+\.([A-Za-z_]+)\)|"([A-Za-z_]+)")/\1 \3\4/' | sort -u \
       | while read t m; do f=$(find Decompiled -name "$t.cs" | head -1); [ -z "$f" ] && continue
           c=$(grep -cE "^\s+(public|private|protected|internal).* $m\(" "$f")
           [ "$c" = 0 ] && echo "MISSING $t.$m"; [ "$c" -gt 1 ] && echo "OVERLOADED($c) $t.$m"
           grep -hE "^\s+(public|private|protected|internal).* $m\(" "$f" | sed "s/^ */$t: /"; done
     ```
     `MISSING`(대상 삭제·개명), `OVERLOADED`(Harmony 모호성 예외 — `[HarmonyPatch]`에 인자 타입 배열 지정 필요), 그리고 Postfix/Prefix가 이름으로 받는 파라미터(`trackID`, `lane`, `currentSample`, `addresskey`, `thisplayer`, `isSelected` 등)의 개명 여부를 확인합니다.
4. **단일 지점 수정:**
   - 필드/프로퍼티 이름 변경 → `DEFLATE custom chart/Core/Wrapper/MemberFallbacks.cs`의 해당 후보 배열에 새 이름 추가 (모든 래퍼/훅에 자동 전파).
   - 메서드 이름/시그니처 변경 → 해당 훅의 `[HarmonyPatch]` 및 `Prepare()` 대상명 갱신.
   - 열거형/타입 이동 → 게임 타입을 직접 참조하지 않고 이름·리플렉션 기반으로 전환(§5-6).
   - 대응이 끝나면 `Core/GameCompat.cs`의 `Reference*` 상수와 §1 기준 빌드 표기를 새 빌드로 갱신.
5. **빌드 & 런타임 검증:**
   - `dotnet build -c Release` (경고 0 / 오류 0) → `Get-FileHash "<GameDir>Mods\DEFLATE custom chart.dll"`과 `Latest.log`의 `SHA256 Hash:` 일치 확인.
   - 로그에서 `[GameCompat] … → 일치`, `[SafePatcher] … 실패 0`, `[커스텀 곡 라이브러리] hwa/ 스캔 완료` 확인.
   - **게임플레이 체크리스트:** 곡 선택 화면에 커스텀 곡 표시(`[곡 목록 주입 완료]`) → 커서 이동 시 커버/BGM·BGA 프리뷰 → 플레이 진입 시 로딩 UI 메타데이터 → 인게임 `[★ HWA BMS 노트 주입 ★]` 레인별 로그와 실제 노트/라인바 → 결과 화면.

---

## 7. 주요 수정 이력 감사 요약 (Audit Trail)

| 커밋 | 초점 | 핵심 개선 내역 |
| :--- | :--- | :--- |
| `830728d` | 내성/문서 기반 구축 | 전역 훅 예외 가드, `AGENTS.md`·`CLAUDE.md` 핸드오버 최초 구축 |
| `252d68f` | 성능·메모리 최적화 | 프레임당 IL2CPP/GC 병목 제거, BMS O(N²) 파싱 개선, 에셋 누수 방지 |
| `ac9c671` | 핫패스·인덱스 최적화 | O(1) `TrackID`/`Title`·레인별 노트 인덱스, `Time.realtimeSinceStartup` 타임아웃 가드 |
| `8222cc3` | 내성·할당 제거 | `BmsLaneMapper` 열거형 박싱 제거, `InvokeMethodResilient`, `DeclaredOnly` 상속 체인 탐색 |
| `02341bd` | 래퍼 폴백 버그 수정 | `CustomTrackWrapper` `SafeCast` 실패 시 폴백 우회 버그 수정, `WarnOnce`, `TryAdaptValueForTargetType`, UTF-8 BOM 대응, `SampleRate <= 0` 가드 |
| `2c80477` | 감사·단일화·패치 격리 | `MemberFallbacks.cs` 단일화, 문자열/보조 훅 `Prepare()`(`HasPatchTarget`) 패치 격리 가드, `DiscID` 래핑, 인수인계 플레이북 추가. ⚠ **컴파일 불가 상태로 커밋됨**(`DiscID`를 `int`로 선언) — 다음 행에서 수정 |
| `5d6f2a9` | 게임 1.2 대응·업데이트 내성 계층 | 1.2(Build `26402105`) `LaneType` 중첩 이동 대응(이름 기반 레인 판정), `DiscID` `string` 수정, `SafePatcher`(자동 PatchAll 대체·클래스 단위 격리·사전 JIT 검사), `GameCompat` 빌드 지문 경고, NoteSway 서킷 브레이커, CS0162 경고 제거, 플레이북에 시그니처 대조 스크립트·Steam 경유 실행·덤퍼 실행 옵션 반영 |
| 현재 | DiffCtrl 스팸·이중 BGM·병목 수정 | `SafeGenerateStarImages`(`new Il2CppSystem.Nullable<Color>()`)로 `DiffCtrl`/`LoadingGamePlay` NRE 스팸 해결, 숙주+커스텀 이중 BGM 차단(인게임 `EnsureAudioSourceBound`/`BindAudioClipWhenReady`/`StartAudioWhenReady`+`SourceClip` 교체+`MuteVideoPlayer`, 곡 선택 `FavControl.SafeLoadAndPlayAudio`/`PlayAudioWhenReady`/`LoadVideoURL`+빠른 스크롤 가드+무BGA 곡 `VideoKey`/`DiscID` 초기화), `ModConfig.Initialize()` 선행 로드로 비활성 핫패스 훅(`NoteSway`/`NoteSpeedChaos`/`BlockSave`) `Prepare()` 스킵, 117곡 전체 카탈로그 덤프/커버 순회/BlockSave/프리로드 로그 스팸 및 할당 병목 제거 |

---

## 8. 빌드 명령, 사전 조건 및 배포 경로

### 빌드 사전 조건
- `<GameDir>MelonLoader\Il2CppAssemblies\` 폴더가 존재해야 모드 DLL이 컴파일됩니다.
- MelonLoader를 새로 설치하거나 게임을 재설치한 직후에는 **게임을 최소 1회 실행**해야 해당 폴더가 자동 생성됩니다.

### 빌드 명령
```powershell
# 기본 게임 경로(H:\steam\steamapps\common\DEFLATE\)로 빌드 및 자동 배포
dotnet build -c Release

# 다른 게임 설치 경로 지정 빌드
dotnet build -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\DEFLATE\"

# 독립 시그니처 덤퍼 빌드/실행 (멀티 타깃이라 실행 시 --framework 필수)
dotnet build SignatureDumper/SignatureDumper.csproj -c Release
dotnet run --project SignatureDumper -c Release --framework net8.0

# 게임 실행은 반드시 Steam 경유 (exe 직접 실행 시 중복 인스턴스 Fatal error)
Start-Process "steam://rungameid/3921360"
```

### 배포 경로
- 빌드 완료 시 `.csproj`의 `DeployToMods` 타깃에 의해 다음 경로로 자동 복사됩니다:
  `$(GameDir)Mods\DEFLATE custom chart.dll`
- 커스텀 곡 및 에셋 배치 경로:
  `$(GameDir)hwa\` (하위 앨범 폴더 또는 단독 곡 폴더)
- 사용자 설정 파일:
  `$(GameDir)savecustomkey\config.txt`
