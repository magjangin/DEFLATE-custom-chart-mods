# DEFLATE BMS 채널 및 #WAV 매핑 규격 문서 (Hwa Custom Spec)

> [!NOTE]
> 본 문서는 **DEFLATE 커스텀 차트(BMS)** 제작 시 사용되는 **#WAV 샘플 테이블 정의** 및 **BMS 채널 ↔ DEFLATE 인게임 레인 매핑 규격**입니다.

---

## 1. BMS 채널 ↔ DEFLATE 레인 매핑

| BMS 채널 | LN 채널 | 대체 채널 | DEFLATE 타겟 레인 ID | 레인 이름 |
| :--- | :--- | :--- | :--- | :--- |
| **`16`** | `56` | — | `hihat_1` | 1번 레인 (HiHat 1) |
| **`11`** | `51` | — | `hihat_2` | 2번 레인 (HiHat 2) |
| **`12`** | `52` | — | `hihat_3` | 3번 레인 (HiHat 3) |
| **`13`** | `53` | — | `hihat_4` | 4번 레인 (HiHat 4) |
| **`14`** | `54` | — | `drop` | 드롭(Drop) 전용 레인 |
| **`15`** | `55` | `22` | `kick_left` | 좌측 킥 |
| **`18`** | `58` | `23` | `kick_right` | 우측 킥 |
| **`19`** | `59` | `24` | `snare_left` | 좌측 스네어 |
| **`17`** | `57` | `25` | `snare_right` | 우측 스네어 |

> [!TIP]
> **채널을 못 외워도 됩니다.** 위 표에 없는 채널에 찍힌 노트는 **키음 파일명으로 레인을 추론**합니다
> (`kick_left.wav`, `snare_right 홀드 시작.wav` → 각각 `kick_left`, `snare_right` 레인).
> 레인 판정 로직은 [`BmsLaneMapper`](../DEFLATE%20custom%20chart/Core/Bms/BmsLaneMapper.cs)에 모여 있으며, **채널 매핑 우선 → 없으면 키음 이름** 순으로 판정합니다.
> 어느 쪽으로도 레인이 안 잡히는 노트(예: BGM 표기용 `#00021:016`의 `music.ogg`)는 조용히 무시되므로 유령 노트가 생기지 않습니다.

---

## 2. #WAV 샘플 정의 테이블 (#WAV Table)

### 🥁 단노트 (#WAV001 ~ #WAV009)

| #WAV ID | 샘플 파일명 | 해당 레인 / 노트 종류 |
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

| #WAV ID | 샘플 파일명 | 해당 레인 / 홀드 구분 |
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
   * 1순위 — 채널 매핑표 (`16/11/12/13/14/15/18/19/17` + LN `5x` + 대체 `22~25`)
   * 2순위 — 키음 파일명 추론 (`hihat_1~4`, `kick_left/right`, `snare_left/right`, `drop` 문자열 포함 여부)
   * 어느 쪽도 안 걸리면 무시 (BGM 표기 등)
   * `drop` 레인 노트는 `RhythmGameController.dropEventSamples`에도 함께 등록됩니다.
2. **롱노트 (Hold) 쌍 매핑:** — ✅ 구현됨 (`BmsParser.PairHoldNotesByKeysound`)
   * `#WAV00B`와 `#WAV00A`처럼 `홀드 시작` ➔ `홀드 끝` 순서로 쌍을 구성하여 인게임 `KoreographyEvent`의 `StartSample` 및 `EndSample`로 변환 주입합니다.
   * **판정 기준은 채널이 아니라 노트가 참조하는 `#WAV` 파일명**입니다. 파일명에 `홀드 시작`(또는 `hold start`, `ln start`)이 들어가면 Head, `홀드 끝`(`hold end`, `ln end`)이 들어가면 Tail로 봅니다. 즉 홀드도 단노트와 **같은 레인 채널(11/12/13/16/14)에 그대로** 찍으면 됩니다.
   * 매칭 규칙 (GRC2 `HoldNoteProcessor.MatchHoldNotes` / sxtg2 `CalculateHoldNoteLengths`와 동일한 시맨틱):
     1. 같은 채널에서 Head보다 **뒤에 있는 가장 가까운 Tail** 하나와 짝 (마디를 넘어가도 매칭됨)
     2. 한 번 소비된 Tail은 다른 Head가 다시 가져가지 못함
     3. 짝이 맞은 Tail은 노트 목록에서 **제거** (남겨두면 홀드 끝 지점에 유령 단타가 하나 더 스폰됨)
     4. 짝을 못 찾은 Head는 **단타로 강등**, 고아 Tail은 제거 (둘 다 로그에 개수 출력)
   * 표준 BMS 방식인 **LN 채널(`51`~`69`)과 `#LNOBJ`도 그대로 동작**하며, 이미 그쪽으로 짝이 맞은 노트는 키음 이름 매칭이 건드리지 않습니다.
   * `hihat_1~4` / `drop` 뿐 아니라 **`kick_left/right`, `snare_left/right` 레인도 동일하게 홀드가 들어갑니다.**

```bms
; hihat_2(채널 11) 레인에 1/2마디 길이 홀드 하나
#00111:00D00C
;       ^   ^
;       │   └─ 00C = hihat_2 홀드 끝  (Tail, 매칭 후 제거됨)
;       └───── 00D = hihat_2 홀드 시작 (Head ➔ KoreographyEvent.StartSample)
```
