# DEFLATE Custom Chart & Song Injector Mod

DEFLATE (`dizzylab.castor`, Unity / Il2Cpp) 리듬게임을 위한 **MelonLoader + Harmony** 기반 모드입니다. 기존 곡(WindShifter)을 복제해 `hwa/` 폴더의 커스텀 곡마다 사본을 곡 목록에 추가하고, 사본에 커스텀 BMS 차트·BGM·BGA·자켓을 주입합니다. 곡/차트 메타데이터도 로그로 남깁니다.

## 주요 기능

- **곡 목록/인게임 로깅**: 전체 수록곡 카탈로그, 커서 이동, 난이도 변경, 플레이 진입 등을 상세 로그로 기록
- **앨범/곡 폴더 라이브러리 (`CustomSongLibrary`)**: `hwa/` 아래 폴더를 스캔해 여러 커스텀 곡을 앨범 단위로 카탈로그화
- **곡 복제 주입 (`SongInjectorHooks`)**: 기존 트랙을 `CustomTrackWrapper`로 캐스트·복사해, 커스텀 곡 수만큼 새 `MainTrackListBlock` 인스턴스를 곡 목록에 주입
- **BMS 차트 주입 (`BmsParser` / `BmsLaneMapper`)**: 곡 폴더의 BMS를 읽어 레인 노트·홀드·드롭 노트로 바꾸고, 라인바(마디선/박자선)도 BMS 기준으로 다시 만듦 — 규격은 [docs/bms_mapping_spec.md](docs/bms_mapping_spec.md)
- **커스텀 에셋 오버라이드 (`HwaAssetManager`)**: 선택된 커스텀 곡 폴더의 PNG 자켓·MP4 BGA·BGM 오디오를 주입된 사본에만 적용 (원본 곡은 그대로 유지)
- **곡 목록 프리뷰까지 연동**: 목록에서 커서를 옮길 때의 PV 자동 미리보기(BGA/BGM)에도 커스텀 에셋 적용
- **플레이 옵션 (`savecustomkey/config.txt`)**: 오토 플레이, 저장 차단, 노트 흔들림(NoteSway), 노트 카오스 배속(NoteSpeedChaos)
- **곡 시작 키 가이드 숨김 (`KeyIndicatorHooks`)**: 곡 시작 시 뜨는 `Key_indicator` UI를 모든 곡에서 항상 숨김 (설정으로 끌 수 없음)

## 폴더 구조

```
DEFLATE custom chart/   # 모드 본체 (MelonLoader Mod, .csproj)
  Core/                 # CustomSongLibrary/CustomSongEntry, HwaAssetManager, ModConfig, BMS 파서 등
  Hooks/                # Harmony 훅 (SongListHooks, AssetManagerHooks, InGameRhythmHooks ...)
SignatureDumper/         # 게임 Il2Cpp 어셈블리 시그니처 덤프 도구
docs/                    # 아키텍처/훅 포인트/모딩 가이드 문서
```

## 설치 및 사용

1. MelonLoader가 설치된 DEFLATE 게임 폴더의 `Mods/`에 빌드된 `.dll`을 배치합니다.
2. 게임 루트 `hwa/` 폴더에 곡을 배치합니다. 폴더 깊이는 자동 판별되며 세 가지 배치를 모두 지원합니다.

```
hwa/
  info.txt, music.ogg, video.mp4, cover.png   # (구방식) 루트 직접 배치 = 곡 1개
  단독 곡/                                     # 앨범 없는 곡
    info.txt, chart.bms, music.ogg, cover.png
  앨범 이름/                                   # 하위에 곡 폴더만 있으면 앨범 폴더
    곡 A/  info.txt, chart.bms, music.ogg, video.mp4, cover.png
    곡 B/  ...
```

  - 곡 폴더로 인정되는 조건: `info.txt`(또는 다른 `.txt`) · BMS 차트(`.bms`/`.bme`/`.bml`) · `.png` · `.mp4` 중 하나라도 폴더에 직접 있을 것 (`.wav`만 모아둔 키음 폴더는 자동으로 제외됩니다)
  - 메타데이터 파일은 `info.txt`를 우선하고, 없으면 처음 나오는 `.txt`를 씁니다
  - BGM은 `.ogg` › 이름에 `music`/`bgm`/`song`/`track`/`audio`가 들어간 파일 › 용량이 큰 파일 순으로 고릅니다 (지원: `.ogg`/`.wav`/`.mp3`)
  - 곡 제목은 `info.txt`의 `title`, 없으면 곡 폴더 이름 / 앨범명은 `info.txt`의 `album`, 없으면 앨범 폴더 이름을 씁니다
3. 첫 실행 시 `savecustomkey/config.txt`가 만들어집니다. 아래 [설정](#설정-savecustomkeyconfigtxt)을 참고하세요.
4. 자세한 훅 포인트와 트러블슈팅은 [`docs/`](docs/) 문서를 참고하세요.

## 설정 (`savecustomkey/config.txt`)

| 키 | 기본값 | 설명 |
| :--- | :--- | :--- |
| `AutoPlay` | `0` | 오토 플레이 강제 |
| `BlockSave` | `1` | 게임 세이브(`GameData.SaveGameData`) 차단 |
| `NoteSway` | `0` | 노트 좌우 흔들림 연출 (`NoteSwayAmplitude` / `NoteSwaySpeed` / `NoteSwayDamping` / `NoteSwayDampingTime`) |
| `NoteSpeedChaos` | `0` | 노트마다/레인마다 낙하 속도를 다르게 (`NoteSpeedChaosMin` / `NoteSpeedChaosMax` / `NoteSpeedChaosPerLane`) |

> **`BlockSave=1` 주의:** 점수만 막는 것이 아니라 커스텀 곡/원곡 구분 없이 `GameData.SaveGameData` 호출 자체를 건너뜁니다. `GameData`에 함께 들어 있는 키 설정·볼륨·입력 지연(InputDelay/VisualOffset)·즐겨찾기·플레이어 레벨/경험치·구매 목록·점수도 저장되지 않습니다. 게임 설정을 바꿔야 할 때는 `BlockSave=0`으로 두세요.

## 빌드

- 게임 설치 경로는 `.csproj`의 `GameDir` 속성 하나로 지정합니다 (기본값 `H:\steam\steamapps\common\DEFLATE\`). 다른 경로면 `dotnet build -p:GameDir=<게임 폴더>\`로 덮어쓰세요.
- 참조 DLL은 `MelonLoader/Il2CppAssemblies/`에서 가져옵니다. 이 폴더는 MelonLoader 설치 후 게임을 **한 번 실행해야** 생성됩니다.
- 빌드가 끝나면 `DeployToMods` 타깃이 `<GameDir>Mods\`로 DLL을 복사합니다.

## 문서

- [docs/CUSTOM_CHART_FOUNDATION.md](docs/CUSTOM_CHART_FOUNDATION.md) — 내부 데이터 구조, 씬 전환 파이프라인, 훅 포인트 전체 목록
- [docs/bms_mapping_spec.md](docs/bms_mapping_spec.md) — BMS 채널 ↔ 레인 매핑, `#WAV` 키음 테이블, 홀드 매칭 규칙
- [docs/custom_asset_modding_guide.md](docs/custom_asset_modding_guide.md) — 커스텀 에셋 주입 아키텍처 & 훅 매뉴얼
- [docs/drop_note_analysis.md](docs/drop_note_analysis.md) — 드롭 노트 구조와 잔여 노트 정리

## Contributors

- **화영왕 (Hwa-young-wang)** — 프로젝트 메인테이너
- **Antigravity**
- **Claude**
