# DEFLATE BMS 채널 및 #WAV 매핑 규격 문서 (Hwa Custom Spec)

> [!NOTE]
> 본 문서는 **DEFLATE 커스텀 차트(BMS)** 제작 시 사용되는 **#WAV 샘플 테이블 정의** 및 **BMS 채널 ↔ DEFLATE 인게임 레인 매핑 규격**입니다.

---

## 1. BMS 채널 ↔ DEFLATE 레인 매핑

**채널이 곧 레인입니다.** 인식하는 채널은 아래 **5개가 전부**이며, 그 외 채널의 노트는 전부 무시됩니다.

| BMS 채널 | 레인 슬롯 ID | HiHat 모드 | KickSnare 모드 |
| :--- | :--- | :--- | :--- |
| **`16`** | `lane_1` | HiHat 1 | Kick Left |
| **`11`** | `lane_2` | HiHat 2 | Kick Right |
| **`12`** | `lane_3` | HiHat 3 | Snare Left |
| **`13`** | `lane_4` | HiHat 4 | Snare Right |
| **`14`** | `drop` | 드롭(Drop) 전용 레인 | — |

### 왜 킥/스네어 전용 채널이 없나

DEFLATE의 플레이 레인은 **4개뿐**입니다. 이 4개 레인이 [`DrumMode`](../Decompiled/Il2Cppdizzylab/castor/DrumMode.cs)(`HiHat` / `KickSnare`)에 따라 하이햇으로도, 킥/스네어로도 바뀝니다. 게임 내부적으로는 [`RhythmGameController`](../Decompiled/Il2Cppdizzylab/castor/RhythmGameController.cs)에 `lane_hihat_1~4`, `lane_kick_left/right`, `lane_snare_left/right`, `lane_drop` 9개의 `LaneController`가 있지만, 앞의 8개는 **같은 물리 레인 4개를 두 모드로 나눠 놓은 것**입니다.

따라서 차트 제작자는 **드롭을 제외한 모든 노트(하이햇 · 킥 · 스네어)를 `16` / `11` / `12` / `13` 네 채널에만 찍으면 됩니다.** 채널 `11`에 찍은 노트 하나는 `lane_hihat_2`와 `lane_kick_right` 양쪽에 함께 주입되고, 실제로 무엇이 보일지는 게임의 모드 전환이 결정합니다.

> [!IMPORTANT]
> **레인 판정은 오직 채널로만 합니다.** 키음(#WAV) 파일명으로 레인을 추론하는 폴백은 **없습니다**.
> 표에 없는 채널의 노트는 파일명이 무엇이든 조용히 무시되므로(예: BGM 표기용 `#00021:016`의 `music.ogg`) 유령 노트가 생기지 않습니다.
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
   * 표준 BMS의 LN 채널(`51`~`69`) 및 `#LNOBJ` 파싱 코드는 `BmsParser`에 남아 있지만, **5x 채널은 레인 매핑표에 없으므로 결과적으로 무시됩니다.** 홀드는 위의 키음 이름 방식으로 찍으세요.

```bms
; 레인 2(채널 11)에 1/2마디 길이 홀드 하나
#00111:00D00C
;       ^   ^
;       │   └─ 00C = 홀드 끝  (Tail, 매칭 후 제거됨)
;       └───── 00D = 홀드 시작 (Head ➔ KoreographyEvent.StartSample)
```
