# DEFLATE Custom Chart Documentation

이 디렉토리는 DEFLATE (`dizzylab.castor`) 게임의 커스텀 차트 및 에셋 주입 모드 관련 개발 기술 문서들을 담고 있습니다.

## 문서 목록

1. [**CUSTOM_CHART_FOUNDATION.md**](file:///h:/source/repos/DEFLATE%20custom%20chart/docs/CUSTOM_CHART_FOUNDATION.md)
   - DEFLATE 리듬게임 내부 메타데이터 및 에셋 파이프라인 분석
   - Koreography 엔진 구조 및 시간/노트 계산 공식 ($\text{Time} = \frac{\text{StartSample}}{\text{SampleRate}}$, BMS/Tick 공식)
   - MelonLoader / Harmony 주요 훅 포인트 (`SongListHooks`, `LoadingSceneHooks`, `AssetManagerHooks`, `InGameRhythmHooks`, `NoteHooks`)
   - 커스텀 차트 및 에셋 주입 워크플로우

---
*Created by Hwa-young-wang / DEFLATE Custom Chart Mod Project*
