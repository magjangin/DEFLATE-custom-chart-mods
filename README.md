# DEFLATE Custom Chart & Song Injector Mod

DEFLATE (`dizzylab.castor`, Unity / Il2Cpp) 리듬게임을 위한 **MelonLoader + Harmony** 기반 모드입니다. 곡/차트 메타데이터를 로그로 남기고, 기존 곡을 복제해 커스텀 BGM·BGA·자켓을 주입할 수 있는 테스트 트랙을 곡 목록에 추가합니다.

## 주요 기능

- **곡 목록/인게임 로깅**: 전체 수록곡 카탈로그, 커서 이동, 난이도 변경, 플레이 진입 등을 상세 로그로 기록
- **곡 복제 주입 (`SongInjectorHooks`)**: 기존 트랙을 `CustomTrackWrapper`로 캐스트·복사해 새 `MainTrackListBlock` 인스턴스로 곡 목록에 주입
- **커스텀 에셋 오버라이드 (`HwaAssetManager`)**: 게임 루트의 `hwa/` 폴더에 있는 PNG 자켓·MP4 BGA·BGM 오디오를 주입된 사본에만 적용 (원본 곡은 그대로 유지)
- **곡 목록 프리뷰까지 연동**: 목록에서 커서를 옮길 때의 PV 자동 미리보기(BGA/BGM)에도 커스텀 에셋 적용

## 폴더 구조

```
DEFLATE custom chart/   # 모드 본체 (MelonLoader Mod, .csproj)
  Core/                 # HwaAssetManager, ModConfig, BMS 파서 등
  Hooks/                # Harmony 훅 (SongListHooks, AssetManagerHooks, InGameRhythmHooks ...)
SignatureDumper/         # 게임 Il2Cpp 어셈블리 시그니처 덤프 도구
docs/                    # 아키텍처/훅 포인트/모딩 가이드 문서
```

## 설치 및 사용

1. MelonLoader가 설치된 DEFLATE 게임 폴더의 `Mods/`에 빌드된 `.dll`을 배치합니다.
2. 게임 루트에 `hwa/` 폴더를 만들고 `bgm.(wav|mp3|ogg)`, `video.mp4`, `cover.png`를 넣으면 자동으로 감지되어 주입된 테스트 곡에 적용됩니다.
3. 자세한 훅 포인트와 트러블슈팅은 [`docs/`](docs/) 문서를 참고하세요.

## 문서

- [docs/CUSTOM_CHART_FOUNDATION.md](docs/CUSTOM_CHART_FOUNDATION.md) — 내부 데이터 구조 및 씬 전환 파이프라인
- [docs/custom_asset_modding_guide.md](docs/custom_asset_modding_guide.md) — 커스텀 에셋 주입 아키텍처 & 훅 매뉴얼

## Contributors

- **화영왕 (Hwa-young-wang)** — 프로젝트 메인테이너
- **Antigravity**
- **Claude**
