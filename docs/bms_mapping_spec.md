# DEFLATE BMS 채널 및 #WAV 매핑 규격 문서 (Hwa Custom Spec)

> [!NOTE]
> 본 문서는 **DEFLATE 커스텀 차트(BMS)** 제작 시 사용되는 **#WAV 샘플 테이블 정의** 및 **BMS 채널 ↔ DEFLATE 인게임 레인 매핑 규격**입니다.

---

## 1. BMS 채널 ↔ DEFLATE 레인 매핑

**노트 하나는 정확히 한 게임 레인에만 들어갑니다.**
- **열(1~4열)** 은 BMS **채널**이 정합니다.
- **하이햇인지 킥/스네어인지** 는 그 노트의 **키음(#WAV) 파일명**이 정합니다. 파일명에 `kick`·`snare`(대소문자 무시) 또는 `킥`·`스네어`(v1.1.3~)가 들어 있으면 킥/스네어, 그 외(`hihat_*.wav` 등)는 전부 하이햇입니다.

| BMS 채널 | 화면 열 (패드) | 하이햇 키음일 때 | `kick`/`snare` 키음일 때 |
| :--- | :--- | :--- | :--- |
| **`16`** | 1열 (KEY_1) | `hihat_1` | `snare_left` |
| **`11`** | 2열 (KEY_2) | `hihat_2` | `kick_left` |
| **`12`** | 3열 (KEY_3) | `hihat_3` | `kick_right` |
| **`13`** | 4열 (KEY_4) | `hihat_4` | `snare_right` |
| **`14`** | 드롭 | `drop` (키음 무관) | — |

> [!IMPORTANT]
> **킥/스네어는 열 배치가 이름과 다릅니다(게임 1.2 실측).** `kick_left`는 1열이 아니라 **2열**, `snare_left`가 **1열**입니다.
> 열은 항상 채널이 정하므로, 예를 들어 `kick_left.wav`를 `16`(1열)에 찍으면 1열의 킥/스네어 레인인 `snare_left`로 들어갑니다.
> 이런 노트 수는 로그의 `[BMS 파싱] … 킥·스네어 키음 이름과 열이 다른 노트 N개`로 알려 줍니다.
>
> 하이햇 번호(`hihat_1~4.wav`)는 열을 바꾸지 않습니다. 모든 열에 `hihat_1.wav`를 공통 하이햇 소리로 써도 됩니다.

### 실측 근거 (2026-11-20, 게임 1.2 / Build 26402105)

- `LaneController`마다 연결된 패드(`thisPad`)와 화면 x좌표를 기록한 결과, 1열 패드 `KEY_1`에는 `hihat_1`과 `snare_left`, 2열 `KEY_2`에는 `hihat_2`와 `kick_left`가 함께 묶여 있었습니다(3·4열도 위 표와 같음).
- 숙주 원곡(WindShifter) 차트를 분석하니 **킥/스네어 노트 258개와 하이햇 노트 358개가 같은 시각(±15ms)에 함께 나오는 경우는 0건**이었습니다. 즉 두 세트는 "같은 노트의 두 모드 표현"이 아니라 **별개의 노트**입니다.

### 예전 규칙이 틀렸던 점 (2026-11-20 수정)

예전 문서와 코드는 "레인 4개를 하이햇/킥·스네어 모드가 공유하니 노트를 양쪽에 다 넣으면 된다"고 보고, 채널 `16` 노트를 `hihat_1`과 `kick_left` **양쪽에 복제**했습니다. 그 결과 다음 문제가 있었습니다.
- 노트 하나가 **서로 다른 두 열**에 나왔습니다(`hihat_1`은 1열, `kick_left`는 2열).
- 모든 노트가 **두 번 생성·판정**됐습니다(게임 `AllNoteCount`가 BMS 노트 수의 2배).

수정한 뒤에는 `AllNoteCount`가 BMS 노트 수와 같습니다. 무인 스모크 테스트(`SmokeTest=1`)가 이 조건(주입 노트 수 = 차트 노트 수)을 매번 검사합니다.

### 타이밍 기준

노트 시각은 BMS 표준 공식 `time = prevTime + Δtick × 240 / (bpm × 3840)`으로 계산하고, 게임 샘플 위치로 바꿀 때는 **실제로 재생되는 커스텀 음악의 샘플레이트**를 씁니다. 게임 시계가 재생 중인 클립의 `timeSamples`이기 때문입니다.
- 음악 샘플레이트가 숙주 차트(44100Hz)와 다르면 노트 위치는 맞춰지지만, 게임이 44100Hz로 계산하는 **판정 범위와 진행률 표시는 그 비율만큼 어긋납니다**(48000Hz면 8.8%). 이 경우 로그에 `[BMS 타이밍]` 경고가 한 번 뜹니다.
- **음악(`.ogg`)을 44100Hz로 변환해 두면 완전히 일치합니다.**

> 판정 로직은 [`BmsLaneMapper`](../DEFLATE%20custom%20chart/Core/Bms/BmsLaneMapper.cs)에 모여 있습니다.

---

## 2. #WAV 샘플 정의 테이블 (#WAV Table)

> [!NOTE]
> #WAV 파일명은 **레인을 결정하지 않습니다.** 레인은 채널이 정하고, 파일명은 ① 재생될 키음 ② **홀드 시작/끝 표시** 두 가지 역할만 합니다.

### 🥁 단노트 (#WAV001 ~ #WAV009)

| #WAV ID | 샘플 파일명 | 노트 종류 |
| :--- | :--- | :--- |
| `#WAV001` | `hihat_1.wav` | HiHat 1 단노트 |
| `#WAV002` | `hihat_2.wav` | HiHat 2 단노트 |
| `#WAV003` | `hihat_3.wav` | HiHat 3 단노트 |
| `#WAV004` | `hihat_4.wav` | HiHat 4 단노트 |
| `#WAV005` | `kick_left.wav` | Kick Left (좌측 킥) 단노트 |
| `#WAV006` | `kick_right.wav` | Kick Right (우측 킥) 단노트 |
| `#WAV007` | `snare_left.wav` | Snare Left (좌측 스네어) 단노트 |
| `#WAV008` | `snare_right.wav` | Snare Right (우측 스네어) 단노트 |
| `#WAV009` | `drop.wav` | Drop 전용 단노트 |

---

### ⏳ 홀드 / 롱노트 쌍 (#WAV00A ~ #WAV00P)

| #WAV ID | 샘플 파일명 | 홀드 구분 |
| :--- | :--- | :--- |
| `#WAV00B` | `hihat_1 홀드 시작.wav` | HiHat 1 **롱노트 시작 (Head)** |
| `#WAV00A` | `hihat_1 홀드 끝.wav` | HiHat 1 **롱노트 끝 (Tail)** |
| `#WAV00D` | `hihat_2 홀드 시작.wav` | HiHat 2 **롱노트 시작 (Head)** |
| `#WAV00C` | `hihat_2 홀드 끝.wav` | HiHat 2 **롱노트 끝 (Tail)** |
| `#WAV00F` | `hihat_3 홀드 시작.wav` | HiHat 3 **롱노트 시작 (Head)** |
| `#WAV00E` | `hihat_3 홀드 끝.wav` | HiHat 3 **롱노트 끝 (Tail)** |
| `#WAV00H` | `hihat_4 홀드 시작.wav` | HiHat 4 **롱노트 시작 (Head)** |
| `#WAV00G` | `hihat_4 홀드 끝.wav` | HiHat 4 **롱노트 끝 (Tail)** |
| `#WAV00J` | `kick_left 홀드 시작.wav` | Kick Left **롱노트 시작 (Head)** |
| `#WAV00I` | `kick_left 홀드 끝.wav` | Kick Left **롱노트 끝 (Tail)** |
| `#WAV00L` | `kick_right 홀드 시작.wav` | Kick Right **롱노트 시작 (Head)** |
| `#WAV00K` | `kick_right 홀드 끝.wav` | Kick Right **롱노트 끝 (Tail)** |
| `#WAV00N` | `snare_left 홀드 시작.wav` | Snare Left **롱노트 시작 (Head)** |
| `#WAV00M` | `snare_left 홀드 끝.wav` | Snare Left **롱노트 끝 (Tail)** |
| `#WAV00P` | `snare_right 홀드 시작.wav` | Snare Right **롱노트 시작 (Head)** |
| `#WAV00O` | `snare_right 홀드 끝.wav` | Snare Right **롱노트 끝 (Tail)** |

---

## 3. BMS 파서 처리 규칙 (Engine Parser Logic)

1. **레인 감지 (`BmsLaneMapper.ResolveNoteLane`):**
   * 채널 매핑표(`16` / `11` / `12` / `13` / `14`) **단 하나의 기준**으로 판정합니다.
   * 표에 없는 채널은 무시 (BGM 표기 등).
   * `drop` 레인 노트는 `RhythmGameController.dropEventSamples`에도 함께 등록됩니다.
2. **롱노트 (Hold) 쌍 매핑:** — ✅ 구현됨 (`BmsParser.PairHoldNotesByKeysound`)
   * `#WAV00B`와 `#WAV00A`처럼 `홀드 시작` ➔ `홀드 끝` 순서로 쌍을 구성하여 인게임 `KoreographyEvent`의 `StartSample` 및 `EndSample`로 변환 주입합니다.
   * **Head/Tail 판정 기준은 노트가 참조하는 `#WAV` 파일명**입니다. 파일명에 `홀드 시작`(또는 `hold start`, `ln start`)이 들어가면 Head, `홀드 끝`(`hold end`, `ln end`)이 들어가면 Tail로 봅니다. 홀드도 단노트와 **같은 레인 채널(`16`/`11`/`12`/`13`/`14`)에 그대로** 찍으면 됩니다.
   * 매칭 규칙 (GRC2 `HoldNoteProcessor.MatchHoldNotes` / sxtg2 `CalculateHoldNoteLengths`와 동일한 시맨틱):
     1. 같은 채널에서 Head보다 **뒤에 있는 가장 가까운 Tail** 하나와 짝 (마디를 넘어가도 매칭됨)
     2. 한 번 소비된 Tail은 다른 Head가 다시 가져가지 못함
     3. 짝이 맞은 Tail은 노트 목록에서 **제거** (남겨두면 홀드 끝 지점에 유령 단타가 하나 더 스폰됨)
     4. 짝을 못 찾은 Head는 **단타로 강등**, 고아 Tail은 제거 (둘 다 로그에 개수 출력)
   * 표준 BMS의 LN 채널(`51`~`69`)은 **지원하지 않습니다** (레인 매핑표에 없어 노트 자체가 만들어지지 않음). 홀드는 위의 키음 이름 방식으로 찍으세요.
   * `#LNOBJ xx`는 동작합니다: 레인 채널(`16`/`11`/`12`/`13`/`14`) 안에서 `xx` 오브젝트가 **같은 채널의 바로 앞 노트**를 홀드로 만들고(그 노트가 Head, `xx` 위치가 Tail), `xx` 노트 자체는 제거됩니다. `#LNOBJ`로 짝이 맞은 노트는 키음 이름 매칭에서 건너뜁니다.
3. **마디선(라인바) 자동 생성:** — `BmsParser.GenerateBarLines`
   * 노트와 함께 흘러내려오는 가로줄(라인바)은 원래 복제 원본 곡의 `beat` 트랙을 따라가므로, 커스텀 BMS 곡에서는 BMS 기준으로 새로 만들어 교체합니다. 별도 채널을 찍을 필요는 없습니다.
   * 마디 시작마다 마디선, 마디 안에서는 4분음표(960틱)마다 박자선이 들어갑니다. BPM 변경(`03`/`08`)과 마디 길이(`#XXX02`)가 그대로 반영되고, 마지막 마디 뒤에 끝 마디선이 하나 붙습니다.
   * 인게임 주입은 `RhythmGameController.LoadLineBarEvents` Postfix에서 합니다 (재시작 경로인 `ReplaceLineBarEventsFromTrack(AtSample)`도 같이 처리).

```bms
; 레인 2(채널 11)에 1/2마디 길이 홀드 하나
#00111:00D00C
;       ^   ^
;       │   └─ 00C = 홀드 끝  (Tail, 매칭 후 제거됨)
;       └───── 00D = 홀드 시작 (Head ➔ KoreographyEvent.StartSample)
```
