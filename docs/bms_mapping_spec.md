# DEFLATE BMS 채널 및 #WAV 매핑 규격 문서 (Hwa Custom Spec)

> [!NOTE]
> 본 문서는 **DEFLATE 커스텀 차트(BMS)** 제작 시 사용되는 **#WAV 샘플 테이블 정의** 및 **BMS 채널 ↔ DEFLATE 인게임 레인 매핑 규격**입니다.

---

## 1. BMS 채널 ↔ DEFLATE 레인 매핑

| BMS 채널 | DEFLATE 타겟 레인 ID | 레인 이름 | 비고 |
| :--- | :--- | :--- | :--- |
| **`16`** | `hihat_1` | 1번 레인 (HiHat 1) | 하이햇 1번 |
| **`11`** | `hihat_2` | 2번 레인 (HiHat 2) | 하이햇 2번 |
| **`12`** | `hihat_3` | 3번 레인 (HiHat 3) | 하이햇 3번 |
| **`13`** | `hihat_4` | 4번 레인 (HiHat 4) | 하이햇 4번 |
| **`14`** | `drop` | 드롭(Drop) 전용 레인 | 특수 드롭 심벌 전용 |

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

1. **채널 감지:** 
   * `16` ➔ `hihat_1`
   * `11` ➔ `hihat_2`
   * `12` ➔ `hihat_3`
   * `13` ➔ `hihat_4`
   * `14` ➔ `drop` (RhythmGameController의 `dropEventSamples` 및 드롭 전용 노트에 할당)
2. **롱노트 (Hold) 쌍 매핑:**
   * `#WAV00B`와 `#WAV00A`처럼 `홀드 시작` ➔ `홀드 끝` 순서로 쌍을 구성하여 인게임 `KoreographyEvent`의 `StartSample` 및 `EndSample`로 변환 주입합니다.
