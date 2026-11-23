# DEFLATE Custom Chart & Song Injector Mod - Handover Guide (AGENTS.md)

> 본 문서는 **DEFLATE (`dizzylab.castor`)** 커스텀 차트 및 곡 주입기 모드에 대한 멀티 에이전트 및 개발자 핸드오버 문서입니다.  
> 인게임 실기 검증 및 구조적 안정화가 완료된 최신 아키텍처와 개발 노하우를 기록합니다.
>
> **현재 검증 상태 (2026-11-20, 게임 1.2 / Build `26402105`):** 컴파일 경고 0 · `[SafePatcher] 성공 52 / 건너뜀 2(설정상 비활성) / 실패 0` · `[GameCompat] 일치`.
> **게임플레이 실기 검증 완료:** ① 무인 스모크 테스트(`SmokeTest=1`, §3-⑥) `PASS` — BMS 노트 1104개/9레인·라인바 296개 주입, 커스텀 BGM 재생, BGA URL 일치. ② 사용자 수동 플레이(로딩 씬 경유) 로그에서 곡 목록 주입 → 로딩 UI 메타데이터 → 인게임 노트/라인바/BGM/BGA 주입 확인.
> **2026-11-20 추가 수정·검증:** 노트 이중 생성(하이햇+킥·스네어 양쪽 복제, 열도 어긋남) 및 48000Hz 음악 타이밍 드리프트 수정 → 스모크 `PASS`, 게임 `AllNoteCount` 552 = BMS 552, 화면에서 노트가 BMS 열대로 한 번씩만 표시됨을 캡처로 확인.
> **2026-11-23 전수 감사/리팩토링:** §9 참고. 컴파일 경고 0 · 오류 0, `[SafePatcher] 성공 45 / 건너뜀 8(로그 레벨 LogLevel=1 기준) / 실패 0`, 스모크 테스트 `PASS`(BMS 552/552, 라인바 296, 커스텀 BGM·BGA, 컨텍스트 인게임).
> 미확인: 결과 화면, 롱노트(테스트 차트에 홀드 0개), 드롭 레인(테스트 차트 14번 채널 0개), 킥/스네어 키음 노트(테스트 차트에 없음).

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

### ② 곡 선택 / 인게임 컨텍스트 분리 (`HwaAssetManager.EnterSongSelect` / `EnterGameplay`)
- **이유:** 곡 목록 프리뷰용 훅(`FavControl` 프리뷰, 빠른 스크롤 시 지연 콜백 취소 등)이 인게임의 실제 로드 요청에 끼어들면, 콜백이 취소돼 게임이 오디오를 영원히 기다리거나 원본 로직이 스킵됩니다(구버전에서 로딩 무한 대기/크래시 발생).
- **1.2의 씬 흐름 (2026-11-20 실기 확인):**
  - **사용자 플레이:** 곡 목록 → 로딩 씬(`LoadingGamePlay.Start`) → 인게임. 이 경로는 `MainTrackList.GotoSceneData`를 **호출하지 않습니다**.
  - **`GotoSceneData()` 직접 호출(스모크 테스트):** 로딩 씬을 **건너뛰고** 곧장 인게임으로 갑니다. 예전처럼 `LoadingGamePlay.Start`에서만 플래그를 끄면 인게임 내내 '곡 선택' 상태로 남습니다(실제로 발생, 수정 완료).
- **해결책:** 상태 전환을 두 메서드로만 하고, 진입 지점을 여러 겹으로 둡니다.
  - `EnterSongSelect()` ← `MainTrackList.Start`(두 훅 모두). `IsInSongSelectContext=true`, `IsInGameplayContext=false`.
  - `EnterGameplay(reason)` ← `GotoSceneData` **Postfix**(원본이 예외 없이 끝났을 때만) / `LoadingGamePlay.Start` / `RhythmGameController.Start` **Prefix**(최후 안전망). 가장 먼저 도달한 곳에서 1회만 전환·로그(`[HwaAssetManager] 컨텍스트 전환`).
- **Key 일치 가로채기 정책 (`ShouldOverrideAudioKey` / `ShouldOverrideVideoKey`):** `TrackAssetManager.LoadAudioClip`/`LoadVideoURL`은 "커스텀 곡 활성 + 요청 Key == 사본이 숙주 곡에서 물려받은 Key"일 때만, **곡 선택과 인게임 모두에서** 커스텀 에셋으로 바꿉니다.
  - 1.2 인게임은 숙주 Key(`_track/074/074clip.ogg`, `pv/074…mp4`)로 BGM/BGA를 다시 요청하므로, 여기서 바꾸지 않으면 숙주 곡이 섞입니다(이중 BGM의 근원).
  - 인접 곡 사전 로드 등 다른 Key는 절대 건드리지 않습니다(예전 `LoadVideoURL`은 Key 검사가 없어 곡 선택 중 모든 영상 요청을 커스텀 BGA로 바꿨음).
  - 빠른 스크롤 취소(`requireStillActive`)는 곡 선택에서만 적용하고, 인게임에서는 반드시 성공/실패 콜백 중 하나를 호출합니다.

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
- **⑥의 배경 (1.2 실기 확인):** 1.2에서 최상위 `LaneType`이 `LaneController.LaneType`(중첩)으로 옮겨지고, 값 배치가 다른 `CommingNote.LaneType`이 새로 생겼습니다. 9월 빌드는 런타임에 최상위 `LaneType`을 찾지 못해 BMS 레인 주입 헬퍼가 무력화됐을 것입니다(훅 try/catch에 흡수되어 조용히 원본 차트로 플레이 — 1.2 실기 미확인 추정). 열거형 **멤버 이름**(`HiHat_3`, `Kick_Left` …)은 그대로이므로 `laneType` 값을 리플렉션으로 `object`로 읽어 `ToString()` 이름으로만 판정합니다. 레인 로드 시 레인당 1회 호출이라 할당 비용은 무시할 수준입니다.

### ⑥ 무인 스모크 테스트 (`Core/SmokeTest.cs`, config `SmokeTest=1`)
- **목적:** 게임 업데이트 직후 "곡 목록 주입 → 커스텀 곡 선택 → 인게임 노트/BGM/BGA 주입"을 사람 손 없이 1회 돌리고 `[SmokeTest] PASS/FAIL — …` 한 줄로 판정합니다. 오토플레이 강제, `BlockSave` 설정은 그대로 따릅니다.
- **동작:** 곡 목록이 준비되면(`MainTrackList.Start` Last 훅) 첫 커스텀 블록을 찾아 **`Next()`/`Last()`로 사용자처럼 이동** → `hasKoreHandle` 대기 → `GotoSceneData()` → 인게임 12초 후 점검.
- **판정 기준:** 커스텀 곡 활성, `audioCom.clip`이 커스텀 클립이고 재생 중(음소거 아님), BMS가 있으면 **레인 주입 노트 수 = BMS 노트 수**(중복·누락 없음), BGA가 있으면 VideoPlayer URL 일치, 컨텍스트가 인게임으로 전환됨. 상세 줄에 라인바 수·활성 노트 수·VideoPlayer 오디오 모드도 기록.
- **설계 포인트:** 게임은 시작 직후 `MainTrackList`를 **두 번** 만듭니다(인트로 → 곡 목록 재로드). 새 목록이 오면 세대(`_generation`)를 올려 이전 실행을 조용히 폐기하고 새 목록에서 다시 시작합니다. 플레이 확정 후에는 재실행하지 않습니다(결과 화면 후 복귀 시 반복 방지).
- **한계:** `GotoSceneData` 직접 호출은 로딩 씬을 건너뛰므로 사용자 경로(로딩 씬 경유)와 다릅니다. 로딩 씬 UI 주입은 이 테스트로 검증되지 않습니다 → `[씬 전환 탐지] MainTrackList.GoToScene(...)` 진단 로그로 사용자 경로의 씬 이름을 확보하면 그 경로로 바꾸는 것이 다음 개선 과제입니다.

### ⑦ BMS → 게임 레인 배치와 타이밍 기준 (2026-11-20 실측으로 재설계)
- **노트 하나 = 게임 레인 하나.** 열(1~4)은 채널(16/11/12/13), 하이햇/킥·스네어는 키음 파일명(`kick`/`snare` 포함 여부)으로 정합니다. 열별 킥·스네어 레인은 **실측 배치** `snare_left / kick_left / kick_right / snare_right`(1~4열)입니다(`BmsLaneMapper.KickSnareByColumn`).
- **근거:** `ChartDiagnostics`(LogLevel 2)로 `LaneController.thisPad`(KEY_1~4)·x좌표를 기록했고, 숙주 원곡 차트에서 킥·스네어와 하이햇의 동시 출현이 0건임을 확인했습니다. 예전 "양쪽 복제" 설계는 노트를 두 열에 두 번 만들었습니다(`AllNoteCount` 2배).
- **타이밍 기준 샘플레이트:** 게임 시계는 `audioCom.timeSamples`(재생 중인 클립 단위)이므로 노트/라인바/드롭 샘플은 **커스텀 음악 클립의 주파수**로 계산합니다(`InGameRhythmHooks.TimelineSampleRate`). 숙주 Koreography(44100Hz)로 계산하던 예전 방식은 48000Hz 음악에서 노트가 8.8%씩 일찍 나왔습니다. 판정 범위·진행률 표시는 게임이 Koreography 샘플레이트로 계산하므로, 완전 일치를 원하면 음악을 44100Hz로 변환하도록 `[BMS 타이밍]` 경고로 안내합니다.
- **숙주 에셋 원복:** 커스텀 곡 플레이 중 바꾼 숙주 `Koreography.SourceClip`은 곡 목록 진입 시 `InGameRhythmHooks.RestoreHostKoreography()`로 되돌립니다(공유 Addressables 에셋이라 그대로 두면 같은 세션의 원곡 플레이에 영향).

### ⑧ 로그 상세도 (`Core/ModLog.cs`, config `LogLevel`)
- `0` 요약(기본): 시작 요약(`[GameCompat]`, `[SafePatcher]`, `[ModConfig]`), 곡 라이브러리·`[BMS 파싱]` 한 줄, `[곡 목록 주입 완료]`, 플레이마다 `[커스텀 차트]` 한 줄, 경고/오류. `1` 상세: 레인별 주입·에셋 오버라이드·씬별 메타데이터. `2` 디버그: 커서 이동/클릭/난이도/드럼 모드/10초 진행률·`[진단]` 레인 배치와 원곡 차트 분석.
- 로그만 찍는 훅(진행률, 드럼 모드, 커서 이동, 클릭, 씬 전환, 노트 수, 영상 시작)은 `ModLog.Prepare(level)`로 **레벨이 모자라면 패치 자체를 생략**합니다. 기본값에서 `[SafePatcher]` 성공 수가 줄어드는 것은 정상입니다.
- 경고/오류는 레벨과 무관하게 항상 남깁니다. 새 로그를 추가할 때는 `ModLog.Info/Verbose/Debug` 중 하나를 고르고, 이벤트마다 반복되는 로그는 `Debug`로 두세요.

---

## 4. 핵심 후킹 타깃 시그니처 & 주입 파이프라인

| 후킹 클래스 & 메서드 | 타이밍 | 역할 및 주입 로직 |
| :--- | :--- | :--- |
| `MainTrackList.Start` | Postfix | `High` 우선순위로 원본 `WindShifter` 복제 및 O(1) 등록, `Normal` 커버 주입, `Last` 카탈로그 로깅 |
| `TrackListBlockCtrl.SetSelected` | Postfix | 곡 목록 커서 이동 시 현재 활성 트랙(`HwaAssetManager.Active`) 갱신 및 커버 적용. 1.2에서 시그니처가 `(bool isSelected, bool animated = true)`로 확장됨 — Postfix는 `isSelected`만 받아도 Harmony 바인딩 유지 |
| `MainTrackList.GotoSceneData` | Prefix / Postfix | Prefix: `ApplyMetaToGameData`로 커스텀 메타데이터 주입. Postfix: `EnterGameplay`. ⚠ 1.2 **사용자 플레이 경로에서는 호출되지 않음**(로딩 씬 경유) — 메타데이터는 `LoadingGamePlay` 훅이 담당 |
| `MainTrackList.GoToScene` | Prefix | 진단 전용: 전환 씬 이름 로깅(`[씬 전환 탐지]`) |
| `TrackAssetManager.LoadAudioClip` / `LoadVideoURL` | Prefix | 활성 커스텀 곡의 **숙주 Key와 일치하는 요청만** 커스텀 BGM/BGA로 대체(곡 선택·인게임 공통, §3-②). 1.2 인게임 BGA는 이 경로로 적용됨 |
| `SimpleRandomVideo.LoadVideoSmart` | Prefix | 곡 선택/결과/인게임 BGA VideoPlayer에 커스텀 MP4 URL 주입 |
| `LoadingGamePlay.Start` | Postfix | `EnterGameplay`, 로딩 UI 메타데이터 주입(오타 필드 폴백 포함) |
| `RhythmGameController.Start` | Prefix / Postfix | Prefix: `EnterGameplay`(최후 안전망). Postfix: 인게임 BGM 0ms 즉시 할당(`EnsureInGameCustomBgm`), 오토플레이(`AutoMode` 또는 스모크 테스트) 강제, 판정 파라미터 로깅, 스모크 테스트 점검 예약 |
| `RhythmGameController.InitializeKoreographyTracks` | Postfix | `dropEventSamples`에 O(1) 드롭 레인 노트 주입 및 원본 잔여 이벤트 정리 |
| `RhythmGameController.LoadKoreographyEvents` | Postfix | **[핵심]** 게임 레인(`hihat_1~4`, `snare_left`/`kick_left`/`kick_right`/`snare_right`, `drop`)별로 BMS 노트를 O(1) 인덱스로 주입. 노트는 파싱 시 **채널=열, 키음=하이햇/킥·스네어**로 정확히 한 레인에 배정(`docs/bms_mapping_spec.md`) |
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

12. **`GoToTrack(index)` + `GotoSceneData()`로 곡 자동 선택·시작 (네이티브 NRE):**
   - `GoToTrack`은 인덱스만 옮기고 `ChangeTrack`/`LoadKoreography`를 거치지 않아 `currentKoreHandle`이 비어 있고, `GotoSceneData` 내부에서 `NullReferenceException`이 납니다(2026-11-20 확인). 커스텀 곡 활성화(`SetSelected`)도 일어나지 않습니다.
   - **해결:** 사용자처럼 `Next()`/`Last()`로 이동하고 `hasKoreHandle == true`를 기다린 뒤 호출(`SmokeTest.SelectAndPlay`).
13. **곡 목록 첫 인스턴스에 코루틴 고정:**
   - 게임 시작 직후 `MainTrackList.Start`가 두 번 불리고 첫 인스턴스는 곧 파괴됩니다. 첫 인스턴스를 붙잡고 기다리는 로직은 중간에 대상이 사라집니다.
   - **해결:** 목록이 새로 생길 때마다 세대 토큰으로 재시작(`SmokeTest._generation`). 곡 목록 관련 지연 작업을 새로 만들 때도 같은 패턴을 쓰세요.
14. **'프리뷰 전용' 가로채기를 씬 플래그 하나로만 막기:**
   - 로딩 씬 훅에서만 플래그를 끄는 구조는 로딩 씬을 건너뛰는 경로에서 인게임 내내 '곡 선택'으로 남고, 반대로 인게임에서 꼭 필요한 숙주 Key 대체(BGM/BGA)는 막아버립니다. 1.2에서 `EnforceCustomBgaUrl`의 인게임 VideoPlayer 음소거도 이 플래그에 의존합니다.
   - **해결:** §3-②의 `EnterSongSelect`/`EnterGameplay` 다중 진입점 + Key 일치 정책.

15. **BMS 노트를 하이햇·킥/스네어 레인 양쪽에 복제 (노트 이중 생성·열 어긋남):**
   - "4개 레인을 두 드럼 모드가 공유한다"는 가정은 틀렸습니다. 킥/스네어 열 배치가 다르고(kick_left=2열), 원곡에서 두 세트는 동시에 나오지 않는 별개 노트입니다. 복제하면 노트가 두 열에 두 번 나오고 `AllNoteCount`가 2배가 됩니다.
   - **해결:** 노트 하나 = 레인 하나(채널=열, 키음=세트). 레인 배치를 다시 의심할 땐 `LogLevel=2` + 스모크 테스트로 `[진단]` 레인 배치·원곡 동시 출현 통계를 다시 뽑으세요.
16. **노트 샘플 위치를 숙주 Koreography 샘플레이트로 계산 (타이밍 드리프트):**
   - 게임 시계는 재생 중인 클립의 `timeSamples`라서, 커스텀 음악이 48000Hz면 44100Hz로 계산한 노트가 곡 진행에 비례해 8.8%씩 일찍 나옵니다(로그상 게임 시간 10초가 실제 9.2초).
   - **해결:** `TimelineSampleRate` = 커스텀 클립 주파수.
17. **로그를 기본값에서 이벤트마다 남기기 (로그 스팸):**
   - 커서 이동·클릭마다, 레인마다, 10초마다 여러 줄씩 기본으로 찍혀 콘솔·로그가 묻혔습니다. **해결:** §3-⑧ `LogLevel`.

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
   - **무인 스모크 테스트(권장):** `<GameDir>savecustomkey\config.txt`를 백업 → 맨 끝에 `SmokeTest=1` 추가 → Steam 경유 실행 → `Latest.log`에서 `[SmokeTest] PASS` 확인 → **config.txt 원복**. 게임 시작 후 약 25~30초면 판정이 나옵니다. 테스트 중에는 게임을 조작하지 마세요(사용자 입력이 끼면 로딩 씬 경로로 진입해 점검이 건너뛰어집니다).
   - **수동 게임플레이 체크리스트:** 곡 선택 화면에 커스텀 곡 표시(`[곡 목록 주입 완료]`) → 커서 이동 시 커버/BGM·BGA 프리뷰 → 플레이 진입 시 로딩 UI 메타데이터(`[로딩 씬 커스텀 UI 주입]`) → `[HwaAssetManager] 컨텍스트 전환` → 인게임 `[★ HWA BMS 노트 주입 ★]` 레인별 로그와 실제 노트/라인바 → 결과 화면.

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
| `b25a513` | DiffCtrl 스팸·이중 BGM·병목 수정 | `SafeGenerateStarImages`(`new Il2CppSystem.Nullable<Color>()`)로 `DiffCtrl`/`LoadingGamePlay` NRE 스팸 해결, 숙주+커스텀 이중 BGM 차단(인게임 `EnsureAudioSourceBound`/`BindAudioClipWhenReady`/`StartAudioWhenReady`+`SourceClip` 교체+`MuteVideoPlayer`, 곡 선택 `FavControl.SafeLoadAndPlayAudio`/`PlayAudioWhenReady`/`LoadVideoURL`+빠른 스크롤 가드+무BGA 곡 `VideoKey`/`DiscID` 초기화), `ModConfig.Initialize()` 선행 로드로 비활성 핫패스 훅(`NoteSway`/`NoteSpeedChaos`/`BlockSave`) `Prepare()` 스킵, 117곡 전체 카탈로그 덤프/커버 순회/BlockSave/프리로드 로그 스팸 및 할당 병목 제거 |
| `809250d` | 1.2 실기 검증·컨텍스트 결함 수정·스모크 테스트 | `SmokeTest`(무인 커스텀 곡 자동 플레이·PASS/FAIL 판정, 목록 이중 생성 대응) 추가 및 1.2 실기 PASS, `EnterSongSelect`/`EnterGameplay` 다중 진입점으로 곡 선택 컨텍스트 누수 수정, `LoadAudioClip`/`LoadVideoURL`을 숙주 Key 일치 정책으로 통일(인게임 포함·인게임 콜백 취소 금지·`LoadVideoURL` Key 검사 신설), `MainTrackList.GoToScene` 진단 로그, 커스텀 블록 판정(`SongInjectorHooks.IsCustomBlock`) 단일화 |
| 이전 | 노트 이중 생성·타이밍·로그 정리 | BMS 노트 1개=게임 레인 1개(채널=열, 키음=하이햇/킥·스네어, 킥·스네어 실측 열 배치), 노트 샘플 위치를 커스텀 음악 주파수 기준으로 계산(48kHz 드리프트 수정)+`[BMS 타이밍]` 안내, 숙주 `SourceClip` 원복, `LogLevel`(0/1/2)과 로그 전용 훅 Prepare 생략, 곡당 한 줄 `[커스텀 차트]` 요약, `ChartDiagnostics`(레인 배치·원곡 동시 출현), 스모크 테스트에 중복·누락 검사 |
| 현재 | 전수 감사·정리 | §9 참조: 무동작 `LoadVideoClip` 훅·미사용 코드(`CustomAlbumWrapper` 등) 삭제, 활성 곡 판정 중복 통합, 죽은 디버그 분기 제거, NoteSway 감쇠를 클립 주파수 기준으로 통일, 스모크 PASS |

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

---

## 9. 코드 감사 기록 (2026-11-23)

범위: `Core/`·`Hooks/` 전 파일(SignatureDumper·docs 제외)을 읽고 핫패스, 중복, 죽은 코드, 예외 삼킴을 점검했습니다. 빌드(경고 0/오류 0) 후 **스모크 테스트 PASS**까지 확인했습니다(`LogLevel=1`, BMS 노트 552/552, 라인바 296, 컨텍스트 인게임, DLL SHA256이 `Latest.log`와 일치). NoteSway는 기본 꺼짐이라 수정(아래)은 스모크로 검증되지 않았습니다.

**적용한 변경**
- 무동작 훅 `TrackAssetManager.LoadVideoClip` 삭제(`Prefix`가 항상 `true`).
- `InGameRhythmHooks`: GameData로 활성 곡을 판정하던 3중 복붙 → `EnsureActiveTrackFromGameData`, 항상 꺼져 있던 `NoteManipulationEnabled` 디버그 분기 삭제, `BuildBmsLineBarEvents` 들여쓰기 정리.
- NoteSway 감쇠 시간 계산: 숙주 Koreography 샘플레이트 → 재생 클립 주파수 우선(§3-⑦ 원칙, 48kHz에서 약 8.8% 오차). **`NoteSway=1`로 인게임 확인은 아직 안 함.**
- 호출처가 없던 코드 삭제: `CustomAlbumWrapper.cs`(`CustomAlbumManager` 포함, 등록·조회 호출 0건), `HwaAssetManager.ResetCache/Rescan/TargetTrackID/TargetAudioKey/TargetVideoKey`, `HwaMetaInfo.ChartMaker`, `CustomSongLibrary.ClearInjectionState/Fallback`.
- `BmsParser`: BPM 타임라인의 `ContainsKey`+인덱서 이중 조회를 `TryGetValue`로 교체(파싱 시 1회성이라 효과는 미미).
- `MuteVideoPlayer` 빈 `catch`에 `LogLevel 2` 로그, `BmsNote.ResolvedLane` 주석 정정(`lane_1~4` → 실제 레인 ID).

**점검했지만 바꾸지 않은 것 (재논의 방지)**
- 핫패스(`NoteSway`)의 프레임 캐시·서킷 브레이커는 충분하고, `transform.localPosition` 읽기/쓰기는 효과의 본질이라 줄일 수 없습니다.
- `Il2CppReflectionHelper`·`GameCompat`·`SmokeTest`의 빈 `catch`는 리플렉션 후보 탐색/최선 노력 점검용이라 의도된 것입니다.
- `SafePatcher`·`CustomSongLibrary.LogCatalog`의 LINQ는 시작 시 1회뿐이고, `FindByTitle/FindByTrackID`의 미스 경로 선형 탐색은 엔트리 수(곡 수)가 작아 무시할 수준입니다.
- `BmsParser`(~700줄)·`InGameRhythmHooks`(~1000줄)는 길지만 응집도가 높아 분할 이득이 작습니다. 분할하려면 스모크 테스트로 회귀를 막을 수 있어야 하며, 지금은 그 안전망이 있으므로 필요해지면 가능합니다.
- 훅 이름 중복(`MainTrackList_Start_Patch`가 `AssetManagerHooks`·`SongListHooks`에 각각 존재)은 바깥 클래스가 달라 충돌하지 않으며, 로그의 `DescribeType`도 `Outer.Inner`로 구분합니다.

**알려진 한계 (미해결)**
- `BmsParser.ParseFile`은 `File.ReadAllText`(UTF-8/BOM 자동 감지)만 씁니다. Shift-JIS BMS는 제목·키음 파일명이 깨질 수 있습니다(키음 이름 기반 홀드/킥·스네어 판정이 영향받을 수 있음). 필요해지면 U+FFFD 감지 후 코드페이지 폴백을 검토하세요.
- `CustomSongEntry.LoadBgmCoroutine`은 `streamAudio=true` 클립을 만든 뒤 요청을 즉시 `Dispose`합니다. 현재 실기(스모크 PASS)에서는 재생되지만, 긴 곡 끝까지 재생되는지는 이 문서 시점에 장시간 검증하지 않았습니다.
- 결과 화면·롱노트·드롭 레인·킥/스네어 키음 노트는 여전히 실기 미확인(테스트 차트에 없음).
