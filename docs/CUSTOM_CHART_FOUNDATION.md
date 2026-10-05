# DEFLATE Custom Chart Foundation Guide
> **DEFLATE 게임 (dizzylab) MelonLoader Il2Cpp 기반 커스텀 차트 개발 & 내부 아키텍처 기초 문서**  
> **작성자:** 화영왕 (Hwa-young-wang) | **버전:** v1.0.0

---

## 1. 개요 (Overview)

본 문서는 Unity 및 SonicBloom Koreography 엔진 기반으로 제작된 rhythm game **DEFLATE** (`dizzylab.castor`)의 내부 데이터 구조와 런타임 파이프라인을 분석하고, **MelonLoader (Il2Cpp)** 및 **Harmony Patching**을 활용하여 커스텀 차트(Custom Chart), 오디오(BGM), BGA(Video)를 인터셉트 및 주입하기 위한 기초 가이드입니다.

Mod의 핵심 목표는 다음 세 가지입니다:
1. **곡 및 차트 메타데이터 탐색/로그화:** 게임 내 등록된 전체 트랙 카탈로그 및 차트 key 스캔
2. **런타임 오디오/비디오 에셋 인터셉트:** Addressable 에셋 로딩 과정 모니터링 및 커스텀 에셋 교체 포인트 확보
3. **Koreography 및 노트(Lane) 이벤트 통제:** 런타임 차트 데이터 파싱 및 노트 생성 로직 제어

---

## 2. 내부 데이터 아키텍처 & 파이프라인

### 2.1 곡 메타데이터 구조 (`MainTrackList` & `MainTrackListBlock`)
DEFLATE는 `MainTrackList` 컴포넌트 내에 전체 곡 데이터 배열 (`MainTrackListBlock[] tracks`, Il2Cpp 쪽 타입은 `Il2CppReferenceArray<MainTrackListBlock>`)을 관리합니다. 곡 하나 = `MainTrackListBlock` 컴포넌트 하나입니다.

| 필드 / 프로퍼티 | 타입 | 설명 |
| :--- | :--- | :--- |
| `uniqueID` | `string` / `int` | 곡의 고유 식별자 ID |
| `TrackTitle` | `string` | 곡 제목 |
| `TrackAuthor` | `string` | 곡 아티스트 / 작곡가 |
| `TrackAlbum` | `string` | 앨범명 |
| `DiscID` | `int` | 디스크 번호 |
| `TrackCover` | `Sprite` | 곡 커버 / 자켓 이미지 스프라이트 (`sprite.name`, `texture`) |
| `audioClip_Key` | `string` | BGM 오디오 에셋 키 (Addressables Key) |
| `videoClip_Key` | `string` | BGA 비디오 에셋 키 (Addressables Key) |
| `EZ_TrackKore_Key` | `string` | Easy 난이도 Koreography 차트 키 |
| `NM_TrackKore_Key` | `string` | Normal 난이도 Koreography 차트 키 |
| `HD_TrackKore_Key` | `string` | Hard 난이도 Koreography 차트 키 |
| `EZ_Star` / `NM_Star` / `HD_Star` | `int` | 각 난이도 난이도 표기 (표기 별 개수) |

---

### 2.2 씬 전환 파이프라인 (Scene Transition Pipeline)

```mermaid
graph TD
    A["Song Selection Scene<br/>(MainTrackList / TrackListBlockCtrl)"] -->|GoToTrack / SelectDifficulty| B["Difficulty & Track Selected<br/>(DiffCtrl)"]
    B -->|GotoSceneData Confirm| C["Loading Scene Entry<br/>(LoadingGamePlay / LoadingManager)"]
    C -->|Prepare GameData| D["In-Game Play Scene<br/>(RhythmGameController)"]
    D -->|InitializeKoreographyTracks| E["Koreography Event Parsing<br/>(Koreography / LaneController)"]
```

1. **곡 선택 씬 (`MainTrackList`)**:
   - `GoToTrack(index)`: 사용자가 커서를 이동할 때 트랙 선택 변경
   - `TrackListBlockCtrl.SetSelected(true)` / `OnPointerClick`: 트랙 UI 블록 선택
   - `DiffCtrl.SelectNextDifficulty()`: 난이도(EZ/NM/HD) 변경
   - `GotoSceneData()`: 곡 플레이 최종 확정
2. **로딩 씬 (`LoadingGamePlay` / `LoadingManager`)**:
   - `LoadingGamePlay.Start()`: 확정된 `gameData` (`NowTrackID`, `NowTrackTitle`, `targetKorePath`, `audioClip_Key`, `pv_Key`) 전달받아 씬 및 에셋 준비
3. **인게임 씬 (`RhythmGameController`)**:
   - `Start()`: 게임 컨트롤러 초기화
   - `InitializeKoreographyTracks()`: Koreography 객체 수신 및 트랙/이벤트 파싱
   - `TriggerAudioStartIfReady()`: BGM 오디오 재생 시작
   - `LoadVideo()` / `PlayVideoWithOffset()`: BGA 영상 로드 및 딜레이 오프셋 동기화 재생

---

### 2.3 Koreography 차트 데이터 구조 (SonicBloom Koreography)

DEFLATE는 Unity 전용 리듬게임 미들웨어인 **SonicBloom Koreography**를 사용합니다.

- **`Koreography`**: 차트의 최상위 클래스
  - `SampleRate`: 오디오 샘플 레이트 (예: `44100` Hz)
  - `Tracks`: `List<KoreographyTrack>` (각 트랙은 레인 또는 특정 이벤트 채널 담당) — **로드된 원본 차트 데이터**이며, 런타임에 `LaneController.laneEvents`를 아무리 비우거나 갈아끼워도 이쪽은 그대로 유지된다(실측 확인됨).
    - 예외: 이 모드는 **BMS가 없는 커스텀 곡**일 때 `InitializeKoreographyTracks` Postfix에서 `hihat_3` 외 트랙의 `mEventList`를 직접 비운다 ([drop_note_analysis.md](drop_note_analysis.md) 4절). BMS가 있는 곡은 원본 트랙을 건드리지 않는다.
  - ⚠️ 주의: "`laneEvents`가 진짜 노트 데이터니까 거기만 건드리면 된다"는 식으로 **프로퍼티 자체를 정답으로 오해하기 쉽다.** 실제 핵심은 프로퍼티가 아니라 **타이밍**이다 — 자세한 내용은 4.5.1 참고.
- **`KoreographyTrack`**:
  - `EventID`: 트랙 식별자
  - `mEventList`: `List<KoreographyEvent>`
- **`KoreographyEvent`**:
  - `StartSample`: 노트 시작 오디오 샘플 타임스탬프 (`int`)
  - `EndSample`: 노트 종료 오디오 샘플 타임스탬프 (롱노트/Hold 처리 시 사용)
  - 파라미터 없는 public 생성자(`new KoreographyEvent()`)가 있어 런타임에 직접 생성 가능. `StartSample`/`EndSample`은 `set` 가능.

> ✅ **실측 확인된 트랙 구성 (곡 `074_ez` 기준, 총 10트랙)**
>
> | EventID | 담당 레인 (`RhythmGameController` 필드) | 노트 수(예시) |
> | :--- | :--- | :--- |
> | `hihat_1` ~ `hihat_4` | `lane_hihat_1` ~ `lane_hihat_4` | 86 / 102 / 94 / 76 |
> | `kick_left` / `kick_right` | `lane_kick_left` / `lane_kick_right` | 83 / 84 |
> | `snare_left` / `snare_right` | `lane_snare_left` / `lane_snare_right` | 41 / 50 |
> | `drop` | `lane_drop` | 6 |
> | `beat` | `linebar` (`LineBarController`, `LaneController`와 별개 타입) | 524 |
>
> `EventID` 문자열이 위 9개(±`beat`)와 정확히 일치해야 게임이 해당 레인에 노트를 스폰한다. `beat`는 `LineBarController`가 처리하므로 `LaneController` 기준 훅으로는 잡히지 않는다.

---

## 3. 핵심 시간 계산 및 차트 파싱 공식

### 3.1 Sample - 시간(초) 변환 공식
Koreography의 타임스탬프는 **Audio Sample** 단위로 저장됩니다.

$$\text{Time (seconds)} = \frac{\text{StartSample}}{\text{SampleRate}}$$

$$\text{StartSample} = \text{Time (seconds)} \times \text{SampleRate}$$

---

### 3.2 BMS / Tick 기반 시간 공식 연동
BMS 또는 표준 MIDI/Tick 기반 커스텀 차트를 Koreography 이벤트로 변환할 때 사용되는 기본 시간 계산 공식입니다:

$$\text{Time (seconds)} = \frac{\text{tick} \times 240}{\text{bpm} \times \text{resolution}}$$

여기서 `240`은 4/4 한 마디(4박 × 60초)이므로 **resolution은 "1마디당 틱 수"**입니다. 이 모드의 `BmsParser`는 `TicksPerMeasure = 3840`(1박 = 960틱)을 씁니다:

$$\text{Time (seconds)} = \frac{\text{tick} \times 240}{\text{bpm} \times 3840}$$

*(resolution을 "1박당 틱 수"(예: 960)로 넣으면 4배 틀린 값이 나옵니다. 1박 기준으로 쓰려면 `tick × 60 / (bpm × 960)`)*

BPM이 바뀌는 곡은 직전 BPM 이벤트까지의 누적 시간에 위 공식을 이어 붙입니다 (`BmsParser.CalculateTimeAtTick`):

$$\text{Time} = \text{prevTime} + \frac{(\text{tick} - \text{prevTick}) \times 240}{\text{bpm} \times 3840}$$

오디오 샘플 연동 변환 공식:
$$\text{StartSample} = \text{Time (seconds)} \times \text{SampleRate}$$

*(현재 `BmsParser`는 `SampleRate = 44100`으로 고정해 `SamplePosition`을 계산합니다.)*

---

## 4. 모드 아키텍처 및 훅 지점 (Hooking Points)

본 레포지토리 모드(`DEFLATE_custom_chart`)는 다음과 같은 훅 클래스들로 모듈화되어 구성되어 있습니다:

### 4.1 `SongListHooks.cs` (곡 목록 및 패널 모니터링)
- `MainTrackList.Start` (Postfix): 수록된 모든 트랙 카탈로그 정보를 스캔하여 제목, 아티스트, 각 에셋 Key 로그 출력. 곡 목록 프리뷰 컨텍스트(`IsInSongSelectContext`)를 다시 켬.
- `MainTrackList.GoToTrack` / `TrackListBlockCtrl.OnPointerClick`: 곡 탐색 커서/클릭 로그.
- `TrackListBlockCtrl.SetSelected` (Postfix): 선택된 블록의 ID로 활성 커스텀 곡(`Active`)을 갱신하고, 커스텀 곡이면 목록 UI 자켓을 교체.
- `MainTrackList.GotoSceneData` (Prefix): 플레이 확정 시 커스텀 곡이면 `gameData`의 제목/아티스트/BGA 제작자/앨범/커버를 커스텀 값으로 미리 주입.
- `DiffCtrl.UpdateStarDisplay` / `SelectNextDifficulty` / `SelectPreviousDifficulty` (Postfix): 커스텀 곡의 난이도 별 개수(info.txt의 easy/normal/hard)를 UI에 반영.

### 4.2 `SongInjectorHooks.cs` (커스텀 곡 사본 주입)
- `MainTrackList.Start` (Postfix): `WindShifter` 트랙을 복제해 `hwa/` 커스텀 곡 수만큼 `MainTrackListBlock` 사본을 만들고 `tracks` 배열을 확장. 사본마다 `RegenerateID()`로 새 ID를 받고(원본과 같으면 사본을 버림), 메타데이터/커버/난이도 별을 입힘. 사본 GameObject 이름에는 `_Custom_`이 붙고, `tracks`에 이미 같은 곡의 사본이 있으면 새로 만들지 않고 재사용함 (원곡은 제목+앨범이 같아도 재사용 대상이 아님).

### 4.3 `LoadingSceneHooks.cs` (로딩 및 씬 전환)
- `LoadingGamePlay.Start` (Postfix): 곡 목록 프리뷰 컨텍스트를 끄고, 커스텀 곡이면 로딩 UI 메타데이터 주입 + BGM 사전 로딩 시작.
- `LoadingGamePlay.InitializeAndPlayAnimations` / `HandleEmptyFields` (Postfix): 로딩 UI가 원본 값으로 되돌아가지 않도록 커스텀 메타데이터(제목/아티스트/매퍼/앨범/난이도/커버)를 다시 적용.
- `LoadingManager.LoadScene` (Prefix): 전환되는 씬 이름 로그.

### 4.4 `AssetManagerHooks.cs` (에셋 관리자 인터셉터)
- `TrackAssetManager.LoadAudioClip` (Prefix): 곡 목록 프리뷰에서 커스텀 곡의 오디오 Key가 요청되면 원본 로드를 건너뛰고 커스텀 BGM을 `onSuccess`로 넘김. 로드에 실패하면 `onFail`을 호출.
- `TrackAssetManager.LoadVideoClip` / `LoadVideoURL` (Prefix): BGA 비디오 로드 요청 key 로그.
- `Bank_PV_Ctrl.PlayPVVideo` / `LoadVideoAndAudio`, `SimpleRandomVideo.LoadVideoSmart` / `LoadVideo` (Prefix): 커스텀 곡이면 커스텀 BGA로 교체.
- `VideoPlayer.Play` / `Prepare` (Prefix, 전역): 커스텀 곡이 활성 상태면 모든 VideoPlayer의 소스를 커스텀 BGA URL로 강제.
- `MainTrackList.Start` (Postfix, `Priority.Last`): 사본 블록의 `TrackCover`를 각자의 커스텀 PNG로 교체.
- `Panel_Result.GetPreviewClip` / `Awake` / `ApplyText`: 결과 화면 미리듣기 BGM, 자켓, BGA를 커스텀 에셋으로 교체.

### 4.5 `InGameRhythmHooks.cs` (인게임 리듬 게임 제어)
- `RhythmGameController.Start` (Postfix): 인게임 진입 로그, 활성 커스텀 곡 갱신, `AutoPlay` 설정 시 오토 모드 강제, 커스텀 BGM/BGA/커버 사전 할당.
- `RhythmGameController.InitializeKoreographyTracks` (Postfix): 트랙 수/총 노트 수 로그. 커스텀 곡이면 `dropEventSamples`를 비우고 BMS 14번 채널 드롭 노트를 다시 채움 (BMS가 없는 곡은 `hihat_3` 외 원본 트랙 이벤트를 비움).
  > ⚠️ **실측 확인:** 이 메서드의 **Prefix 시점에는 `__instance.playingKoreo`가 아직 `null`**이다. `playingKoreo`는 `InitializeKoreographyTracks` 메서드 내부에서 세팅되므로, Prefix에서 `playingKoreo`를 읽거나 트랙을 미리 조작하려는 시도는 전부 조용히 실패한다(예외 없이 null 체크에 걸려 리턴됨). 노트 데이터를 만지려면 4.5.1의 `LoadKoreographyEvents`를 노려야 한다.
- `RhythmGameController.LoadKoreographyEvents` (Postfix): 레인 노트를 BMS 노트로 교체 (4.5.1).
- `RhythmGameController.LoadLineBarEvents` / `ReplaceLineBarEventsFromTrack` / `ReplaceLineBarEventsFromTrackAtSample` (Postfix): 라인바를 BMS 마디선/박자선으로 교체.
- `RhythmGameController.TriggerAudioStartIfReady` (Prefix): BGM 재생 직전에 커스텀 BGM 클립 할당 (아직 로딩 중이면 음소거 후 로드되면 재생).
- `RhythmGameController.LoadVideo` (Prefix) / `PlayVideoWithOffset` (Prefix) / `OnVideoStarted` (Postfix): BGA 비디오 URL을 커스텀 BGA로 교체, 재생 로그.
- `CoverArtController.Initialize` (Postfix): 인게임 커버 이미지 계층을 커스텀 PNG로 교체.
- `RhythmGameController.ChangeDrumMode` / `ChangeDrumModeScore` / `UpdateSongDurationScrollbar`: 드럼 모드 전환, 재생 진행률(10초마다) 로그.

#### 4.5.1 `RhythmGameController.LoadKoreographyEvents` — ✅ 실전 검증된 노트 주입 "타이밍"

> ❗ **정정:** 처음엔 "`LaneController.laneEvents`가 실제 플레이되는 노트 리스트니까 그 프로퍼티 자체가 조작 지점"이라고 정리했는데, 이건 절반만 맞는 얘기다. `laneEvents`는 게임 곳곳(`Update`, `CheckSpawnNext`, `pendingEventIdx` 진행 등)에서 계속 읽고 쓰이는 **살아있는 런타임 상태**라서, 아무 시점에나 건드린다고 되는 게 아니다. 진짜 핵심은 **"언제 건드리느냐"**다 — `koreo.Tracks[].mEventList`(원본, 항상 유효)가 `laneEvents`(런타임 사본)로 **옮겨지는 그 순간**을 정확히 잡아야 하고, 그 순간이 바로 아래 `LoadKoreographyEvents`다. 프로퍼티 이름이 아니라 **호출 타이밍**을 찾은 게 이번에 검증된 내용이다.

`InitializeKoreographyTracks` 내부에서 `playingKoreo`가 세팅된 직후, **트랙(EventID)마다 한 번씩** 비공개 메서드 `LoadKoreographyEvents(string trackID, LaneController lane)`가 호출되어 `koreo.Tracks[].mEventList`의 내용을 각 `LaneController.laneEvents`로 실제로 옮겨 담는다. 이 메서드의 **Postfix가 발동하는 그 순간**이 게임 전체를 통틀어 **"차트 데이터 → 실제 플레이될 노트 리스트"로 전환되는 유일한 타이밍**이며, 다음이 실측으로 확인됐다:

- Postfix에서 `lane.laneEvents`를 비우면(`Clear()`) 그 레인은 노트 없이 정상 플레이된다 (크래시/예외 없음, BGM·BGA·판정 로직 전부 정상 동작).
- `koreo.Tracks[].mEventList`(원본)는 이 시점에도 항상 온전하므로, 여기서 원본 데이터를 참고해 원하는 노트만 골라 `lane.laneEvents`에 다시 채워 넣으면 된다.
- `laneEvents`의 실제 타입은 `Il2CppSystem.Collections.Generic.List<KoreographyEvent>`다 (Il2CppInterop은 mscorlib 타입을 `Il2CppSystem.*`으로 생성한다. 예전 디컴파일 덤프는 네임스페이스를 지운 채 `List<KoreographyEvent>`로 출력해서 `System.Collections.Generic.List`처럼 보였다). `Clear()` / `Add()`는 그대로 동작하고, 리스트를 통째로 바꿀 때는 `new Il2CppSystem.Collections.Generic.List<KoreographyEvent>()`를 넣어야 한다 (라인바 교체 코드 참고).
- `beat`(라인바) 트랙은 `LaneController`가 아닌 `LineBarController`가 처리하므로 이 훅으로는 안 잡힌다. 라인바는 `LoadLineBarEvents` 훅에서 따로 교체한다.

현재 모드의 BMS 주입 부분 (요약):

```csharp
// DEFLATE custom chart/Hooks/InGameRhythmHooks.cs — RhythmGameController_LoadKoreographyEvents_Patch.Postfix 중 BMS 분기
var bmsChart = HwaAssetManager.LoadedBmsChart;
if (bmsChart != null && bmsChart.Notes.Count > 0)
{
    lane.laneEvents?.Clear();

    // trackID("hihat_2") 또는 laneType("Kick_Right") ➔ 레인 슬롯("lane_2")
    string targetLane = BmsLaneMapper.ResolveLaneId(trackID, lane.laneType.ToString());
    if (targetLane != null)
    {
        foreach (var bmsNote in bmsChart.Notes)
        {
            // BMS 채널 ➔ 레인 슬롯 (16/11/12/13 ➔ lane_1~4, 14 ➔ drop)
            if (!string.Equals(BmsLaneMapper.ResolveNoteLane(bmsNote), targetLane, StringComparison.OrdinalIgnoreCase)) continue;

            int startSample = bmsNote.SamplePosition;
            int endSample = bmsNote.IsLongNote
                ? bmsNote.LongNoteEndSamplePosition                      // 홀드: 짝이 맞은 Tail 위치
                : startSample + (int)(koreo.SampleRate * 0.1f);          // 단타: 0.1초 길이

            var newEvt = new KoreographyEvent();
            newEvt.StartSample = startSample;
            newEvt.EndSample = endSample;
            lane.laneEvents.Add(newEvt);
        }
    }
}
```

#### 4.5.2 롱노트(Hold Note / Long Note) 런타임 생성 원리 및 검증 — ✅ 실전 검증 완료

Koreography 엔진 기반의 DEFLATE에서 일반 단타(숏노트)와 롱노트(Hold Note)를 구분하는 핵심 프로퍼티는 `KoreographyEvent`의 **`StartSample`과 `EndSample` 오디오 샘플 차이(`Duration = EndSample - StartSample`)**입니다.

- **단타 노트 (Short Note):** `EndSample == StartSample` 이거나 지속 시간 오프셋이 매우 작음.
- **롱노트 (Hold Note):** `EndSample - StartSample >= TargetDurationInSamples`
  - 오디오 샘플 레이트가 `SampleRate = 44,100Hz` 일 때 **1.5초 지속되는 롱노트**를 생성하려면 `longNoteDuration = (int)(koreo.SampleRate * 1.5f)` = `66,150` 샘플을 지정합니다.
  - 즉, `new KoreographyEvent { StartSample = start, EndSample = start + longNoteDuration }` 형태로 생성하여 `lane.laneEvents`에 추가하면, 런타임 `LaneController` 및 `NoteObject`가 이를 자동으로 인식하여 롱노트 렌더링(Hold Visual Ribbon) 및 롱노트 홀드 판정 로직을 실행합니다.

아래는 이 검증에 쓴 코드로, 지금은 **BMS 파일이 없는 커스텀 곡**의 대체 경로로 `InGameRhythmHooks.cs`에 남아 있습니다 (곡 전체에서 `StartSample > 0`인 가장 빠른 노트 위치 `globalFirstStart`부터 5초 간격으로 10개).

```csharp
// 실전 검증 완료: 특정 레인(hihat_3)에 1.5초 롱노트 10연발 런타임 주입 (BMS가 없는 곡의 대체 경로)
bool isTargetLane = string.Equals(trackID, "hihat_3", StringComparison.OrdinalIgnoreCase)
    || string.Equals(lane.laneType.ToString(), "HiHat_3", StringComparison.OrdinalIgnoreCase);

lane.laneEvents?.Clear();

if (isTargetLane && globalFirstStart != int.MaxValue)
{
    int longNoteDuration = (int)(koreo.SampleRate * 1.5f); // 1.5초 길이 롱노트
    int intervalSamples = koreo.SampleRate * 5;             // 5초 간격

    for (int copy = 0; copy < 10; copy++)
    {
        int start = globalFirstStart + intervalSamples * copy;
        var newEvt = new KoreographyEvent();
        newEvt.StartSample = start;
        newEvt.EndSample = start + longNoteDuration;
        lane.laneEvents.Add(newEvt);
    }
}
```

### 4.6 `NoteHooks.cs`
- `RhythmGameController.RecalculateAllNoteCount` (Postfix): 총 노트 수 재계산 시점 추적.
- `GameData.SaveGameData` (Prefix): `BlockSave=1`이면 세이브 호출 자체를 건너뜀 (원곡 포함, `GameData` 전체가 저장되지 않음).
- `NoteObject.Initialize` (Postfix): `NoteSpeedChaos=1`이면 `moveStartSample`을 당기거나 미뤄 노트별/레인별 낙하 속도를 바꿈 (판정 시점 `moveEndSample`은 그대로).
- `NoteObject.UpdateNotePosition` (Postfix): `NoteSway=1`이면 노트 x 위치에 사인파 흔들림을 더함 (낙하 진행률 기준, 노트 하나당 `NoteSwaySpeed × 5`번 왕복).

> `LaneController.AddEventToLane(KoreographyEvent evt)`도 존재하지만 **개별 노트 하나씩** 받는 시그니처라 노트 여러 개를 넣으려면 반복 호출이 필요하다. 차트 전체를 한 번에 갈아끼우는 용도로는 4.5.1의 `LoadKoreographyEvents` 쪽이 더 적합하다(검증 완료). `AddEventToLane`은 실시간으로 노트 하나를 즉석 추가하고 싶을 때 정도만 고려.

### 4.7 `HUDHooks.cs` / `KeyIndicatorHooks.cs`
- `HUDControl.RefreshSongMetaUI` (Postfix): 인게임 HUD의 커버/제목이 바뀔 때만 로그 (값은 바꾸지 않음).
- `Key_indicator.Start` (Prefix): 곡 시작 키 가이드 UI를 끄고 원본 `Start`를 막음 (모든 곡, 설정 없음).

---

## 5. 커스텀 차트 & 에셋 주입 단계별 가이드 (Custom Injection Workflow)

DEFLATE에 완전히 새로운 커스텀 곡과 차트를 주입하는 단계별 가이드입니다.

```mermaid
flowchart LR
    Step1["1. 에셋 및 차트 준비<br/>(Audio .wav / Chart .json or Koreo)"] --> Step2["2. AssetManager / Key Hooking<br/>(에셋 로드 요청 인터셉트)"]
    Step2 --> Step3["3. TrackData / GameData Modifying<br/>(곡 제목, Koreo Key 변조)"]
    Step3 --> Step4["4. LaneController / Koreo Injection<br/>(런타임 이벤트 주입)"]
```

### 5.1 곡 선택 트랙 런타임 복제 & 생성 (Track Instantiation & Expansion)
DEFLATE의 곡 메타데이터 컴포넌트는 `MainTrackListBlock`이며, `MainTrackList` 내의 `tracks` (`Il2CppReferenceArray<MainTrackListBlock>`) 배열로 통제됩니다.

**복제 기법 (Instantiate & Array Injection):**
1. 기존의 `MainTrackListBlock.gameObject`를 `UnityEngine.Object.Instantiate`로 런타임 복제
2. 복제된 `MainTrackListBlock`의 프로퍼티 (`uniqueID`, `TrackTitle`, `TrackAuthor`, `audioClip_Key`, `EZ_TrackKore_Key` 등)를 커스텀 값으로 변조
3. `MainTrackList.tracks` 참조 배열을 `Il2CppReferenceArray<MainTrackListBlock>(oldLen + 1)` 형태로 확장하여 런타임 곡 추가

```csharp
[HarmonyPatch(typeof(MainTrackList), nameof(MainTrackList.Start))]
public static class MainTrackList_Start_Patch
{
    public static void Prefix(MainTrackList __instance)
    {
        var originalBlock = __instance.tracks[0];
        var clonedGo = UnityEngine.Object.Instantiate(originalBlock.gameObject, __instance.transform);
        var clonedBlock = clonedGo.GetComponent<MainTrackListBlock>();

        clonedBlock.uniqueID = "custom_cloned_track_999";
        clonedBlock.TrackTitle = "[CUSTOM] 복제된 커스텀 곡";
        clonedBlock.TrackAuthor = "화영왕 (Hwa-young-wang)";

        // tracks 배열 N -> N+1 확장 주입
        var oldTracks = __instance.tracks;
        var newTracks = new Il2CppReferenceArray<MainTrackListBlock>(oldTracks.Length + 1);
        for (int i = 0; i < oldTracks.Length; i++) newTracks[i] = oldTracks[i];
        newTracks[oldTracks.Length] = clonedBlock;
        __instance.tracks = newTracks;
    }
}
```

> 위 코드는 원리 설명용 최소 예제입니다. 실제 구현(`SongInjectorHooks`)은 다음이 다릅니다:
> - **Postfix**에서 실행하고, 복제 원본은 `tracks[0]`이 아니라 제목/ID에 `WindShifter`가 들어간 트랙입니다.
> - `hwa/`에서 스캔된 커스텀 곡 **수만큼** 사본을 만들고, 원본과 같은 부모(`sourceBlock.transform.parent`) 아래에 둡니다.
> - ID는 하드코딩하지 않고 `RegenerateID()`로 새로 받습니다. 차트/오디오/비디오 Key는 원본(WindShifter) 값을 그대로 씁니다.

### 단계 1: 커스텀 에셋 준비
- **오디오:** OGG / WAV / MP3 파일
- **차트:** BMS 파일 (`.bms`/`.bme`/`.bml`) — 채널/키음 규격은 [bms_mapping_spec.md](bms_mapping_spec.md). `BmsParser`가 노트를 틱 ➔ 초 ➔ 샘플 위치로 바꾸고, 인게임 훅이 `KoreographyEvent`로 만들어 넣습니다.

### 단계 2: 오디오 교체
- 곡 목록 프리뷰: `TrackAssetManager.LoadAudioClip` Prefix에서 커스텀 곡의 오디오 Key가 요청되면 원본 로드를 건너뛰고 커스텀 BGM `AudioClip`을 콜백으로 넘깁니다.
- 인게임: `RhythmGameController.Start` / `TriggerAudioStartIfReady`에서 `audioCom.clip`을 커스텀 BGM으로 바꿉니다.

### 단계 3: 곡 메타데이터 변조
`MainTrackList.Start` Postfix에서 만든 **사본 블록**의 `TrackTitle`, `TrackAuthor`, `TrackAlbum`, `TrackCover`, 난이도 별 등을 info.txt 값으로 바꾸고, 플레이 확정 시(`GotoSceneData`)와 로딩 씬에서 `gameData`에도 같은 값을 넣습니다.

### 단계 4: 노트 이벤트 런타임 주입 (검증됨)
`RhythmGameController.LoadKoreographyEvents(trackID, lane)` 훅(Postfix)에서 `trackID`에 맞는 커스텀 `KoreographyEvent` 목록을 `new KoreographyEvent { StartSample = ..., EndSample = ... }`로 생성해 `lane.laneEvents`에 채워 넣습니다. (4.5.1 참고 — `InitializeKoreographyTracks` Prefix에서는 `playingKoreo`가 아직 null이라 이 방식은 동작하지 않음.)

---

## 5.5 ⚠️ IL2CPP 모딩할 때 헷갈리는 지점들 (실전에서 실제로 삽질한 것들)

이 게임(Il2CppInterop + MelonLoader) 모딩은 일반 C# 리버싱보다 훨씬 헷갈린다. 아래는 이번 작업에서 실제로 시간을 잡아먹었던 함정들이다.

1. **디컴파일 헤더엔 바디가 없다.** `Decompiled/` 폴더의 `.cs` 파일들은 필드/프로퍼티/메서드 **시그니처만** 있고 실제 로직(네이티브 IL2CPP 코드)은 안 보인다. "이 메서드가 무슨 일을 하는지"는 코드를 읽어서 알 수 없고, **직접 로그를 찍어서 실행 순서와 상태를 실측해야만** 확인된다. 이번에 `playingKoreo`가 `InitializeKoreographyTracks` Prefix 시점엔 null이라는 것도 코드 리딩으로는 절대 못 알아냈고, 로그 찍어보고서야 알았다.

2. **비슷하게 생긴 리스트가 3개나 있고, 서로 다른 레이어다.** 헷갈리기 딱 좋다:
   - `KoreographyTrack.mEventList` — 차트 **원본 데이터**. 뭘 하든 안 건드리는 한 항상 그대로.
   - `LaneController.laneEvents` — 그 원본에서 복사된 **런타임 노트 큐**. `LoadKoreographyEvents` 시점에 채워지고, 게임이 진행되며 `pendingEventIdx`로 소비됨.
   - `RhythmGameController.activeNotes` — 그마저도 아니고, **실제로 화면에 스폰된 노트 오브젝트(GameObject)** 리스트. 노트 데이터가 아니라 비주얼.
   
   셋 다 "노트 리스트"라고 부를 수 있어서 이름만 보고 아무거나 건드리면 원하는 효과가 안 난다.

3. **Prefix에서 null인 게 버그가 아니라 정상일 수 있다.** `InitializeKoreographyTracks`처럼, 훅 대상 메서드가 "그 값을 세팅하는 당사자"인 경우 Prefix 시점엔 아직 없는 게 당연하다. 이럴 때 흔한 실수는 null 체크에 걸려 **조용히 리턴**하는 코드를 짜놓고 "왜 로그가 안 찍히지?"를 반나절 고민하는 것 — 초반엔 무조건 모든 분기에 로그를 남겨서 "어디서 멈췄는지"부터 확인하는 게 빠르다.

4. **빌드가 성공해도 게임에 실제로 반영됐는지는 별개 문제다.** MelonLoader Mods 폴더에 파일을 복사했다고 끝이 아니다. 게임이 이미 켜져 있으면 새 dll을 읽지 않고, 로그 파일도 여러 개(`Logs/타임스탬프.log`, `Latest.log`)가 쌓여서 옛날 로그를 보고 착각하기 쉽다. **`Get-FileHash`로 배포된 dll과 빌드 산출물의 SHA256을 대조**하고, MelonLoader 로그의 `SHA256 Hash:` 줄과도 맞춰보는 습관이 디버깅 시간을 크게 줄여준다.

5. **훅 하나 잘못 걸면 예외 없이 그냥 아무 일도 안 일어난다.** Harmony 패치가 실패해도, 조건문에서 조용히 return해도 게임은 멀쩡히 돌아간다. "크래시가 안 났으니 됐다"가 아니라 "의도한 로그 줄이 실제로 찍혔는지"까지 확인해야 진짜 검증이다.

---

## 6. 결론 및 향후 모딩 방향

본 기초 문서는 DEFLATE 모드의 현재 구조(v1.0.0)를 바탕으로 작성되었습니다.
- **다음 단계:** Addressables 에셋 번들 자체 교체 방식 대신 런타임 메모리 주입(Memory Injection) 방식을 안정화하여, 게임 에셋 훼손 없이 완벽한 오프라인 커스텀 차트 플레이 환경을 구성할 수 있습니다.
