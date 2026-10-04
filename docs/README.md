# DEFLATE Custom Chart Documentation

이 디렉토리는 DEFLATE (`dizzylab.castor`) 게임의 커스텀 차트 및 에셋 주입 모드 관련 개발 기술 문서들을 담고 있습니다.

## 문서 목록

1. [**CUSTOM_CHART_FOUNDATION.md**](CUSTOM_CHART_FOUNDATION.md)
   - DEFLATE 리듬게임 내부 메타데이터 및 에셋 파이프라인 분석
   - Koreography 엔진 구조 및 시간/노트 계산 공식 ($\text{Time} = \frac{\text{StartSample}}{\text{SampleRate}}$, BMS/Tick 공식)
   - MelonLoader / Harmony 훅 포인트 전체 목록 (`Hooks/` 폴더의 파일별 정리)
   - 커스텀 차트 및 에셋 주입 워크플로우
2. [**bms_mapping_spec.md**](bms_mapping_spec.md)
   - BMS 채널(`16`/`11`/`12`/`13`/`14`) ↔ 인게임 레인 매핑 규격
   - `#WAV` 키음 테이블과 키음 이름 기반 홀드(롱노트) 매칭 규칙, 라인바 자동 생성
3. [**custom_asset_modding_guide.md**](custom_asset_modding_guide.md)
   - `hwa/` 폴더 구조와 곡/앨범 폴더 판정 규칙
   - 커버 PNG / BGA MP4 / BGM 오디오 주입 구조와 씬별 훅, 트러블슈팅
4. [**drop_note_analysis.md**](drop_note_analysis.md)
   - 드롭(Drop) 노트의 `dropEventSamples` 구조와 커스텀 차트 주입 시 잔여 노트 정리

---
*Created by Hwa-young-wang / DEFLATE Custom Chart Mod Project*
