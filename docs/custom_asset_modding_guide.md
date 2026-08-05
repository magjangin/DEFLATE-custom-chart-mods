# DEFLATE 커스텀 에셋 주입 모드 개발 & 훅 매뉴얼 (DEFLATE Custom Asset Modding)

*DEFLATE* (Unity / Il2Cpp) 게임에서 외부 미디어 에셋(`hwa/` 폴더)인 **BGM 오디오**, **BGA 비디오**, **PNG 자켓 커버**를 오프라인 오버라이드하고 인게임에 완벽히 연동하기 위해 구축된 **`HwaAssetManager`** 아키텍처 및 훅 포인트 기술 문서입니다.

---

## 1. 개요 및 폴더 구조

### 📁 Custom Asset Directory (`hwa/`)
게임 실행 루트 디렉터리에 위치하며, 모드가 실행될 때 `CustomSongLibrary`가 디렉터리를 스캔해 **커스텀 곡 카탈로그**를 만들고, 곡 수만큼 곡 목록에 사본을 주입합니다. `HwaAssetManager`는 그중 **현재 선택된 곡(Active)**의 에셋을 각 훅에 노출하는 파사드입니다.

```
DEFLATE/
├── MelonLoader/
├── Mods/
│   └── DEFLATE custom chart.dll
└── hwa/
    ├── info.txt / music.ogg / video.mp4 / cover.png ...   # (구방식) 루트 직접 배치 = 곡 1개
    ├── 홀드 모음/                                          # .wav만 있는 폴더 = 키음 보관함으로 간주, 무시됨
    ├── 단독 곡 폴더/                                        # 앨범 없는 곡 1개
    │   ├── info.txt / chart.bms / music.ogg / video.mp4 / cover.png
    └── 앨범 이름/                                           # 하위에 곡 폴더만 있으면 = 앨범 폴더
        ├── 곡 A/
        │   └── info.txt, chart.bms, music.ogg, video.mp4, cover.png
        └── 곡 B/
            └── ...
```

**폴더 판정 규칙 (`CustomSongEntry.TryCreate` / `CustomSongLibrary.Scan`)**

| 폴더 상태 | 판정 |
| :--- | :--- |
| `info.txt` · `.bms/.bme/.bml` · `.png` · `.mp4` 중 하나라도 직접 있음 | **곡 폴더** |
| 오디오 파일만 있음 (`.wav` 키음 모음 등) | 곡 아님 → 무시 |
| 에셋 없이 하위 폴더만 있음 | **앨범 폴더** (하위 폴더들을 곡으로 스캔) |

**메타데이터 우선순위**
- 곡 제목: `info.txt`의 `title/제목` → 곡 폴더 이름 (루트 직접 배치는 폴더 이름을 쓰지 않고 기본값 유지)
- 앨범명: `info.txt`의 `album/앨범` → 상위 앨범 폴더 이름 → 기본값 `custom albums`
- 곡 목록 주입 순서: 앨범 폴더명 → 곡 폴더명 오름차순 (게임의 `tracks` 배열은 평면 배열이라 앨범은 **그룹 UI가 아니라 `TrackAlbum` 라벨 + 주입 순서**로만 표현됩니다)

---

## 2. 핵심 에셋 관리자 아키텍처 (`HwaAssetManager.cs`)

모든 에셋 스캔, 메모리 로딩, 텍스처 변환, 스프라이트 캐싱, `VideoPlayer` URL 바인딩, `AudioSource` 비동기 스트리밍 재생 로직이 **`HwaAssetManager.cs`**에 캡슐화되어 있습니다.

### 🖼️ PNG Cover Sprite Caching & Filtering
- `ImageConversion.LoadImage`를 통해 PNG 바이트 데이터를 `Texture2D`로 읽어옵니다.
- 축소/확대 시 발생하는 계단 현상(자글거리는 그래픽 노이즈) 방지를 위해 `FilterMode.Bilinear` 및 `TextureWrapMode.Clamp`, Mipmap 활성화를 적용합니다.
- 중복 재할당 및 프레임 노이즈를 억제하기 위해 동일 스프라이트 할당 검사를 수행합니다.

### 🎬 VideoPlayer Addressables Override Pattern
- Unity Addressables 기반 `VideoPlayer`는 기본적으로 `VideoSource.VideoClip`으로 설정되어 있습니다.
- 외부 MP4 주입 시 반드시 `VideoSource.Url`로 전환 후 `url` 설정 및 `Prepare() → Play()` 호출을 수행합니다.

```csharp
player.source = VideoSource.Url;
player.url = bgaUrl;
player.Prepare();
player.Play();
```

### 🎵 BGM AudioSource Streaming Playback
- `UnityWebRequestMultimedia.GetAudioClip`을 사용하여 OGG/WAV/MP3를 비동기로 받아옵니다.
- 주입 직후 기존 `audioCom.Stop()` → `audioCom.clip = customClip` → `audioCom.Play()`를 호출하여 원본 오디오를 오버라이드합니다.
- 음원 길이에 맞춰 `RhythmGameController.UpdateSongDurationScrollbar` 진행률 바가 자동 연동됩니다.

---

## 3. 씬/컴포넌트별 훅 포인트 분석

### 1) 곡 선택 씬 (Song Select & Main Track List)
- **타겟 클래스**: `MainTrackList`, `MainTrackListBlock`
- **주입 메커니즘**:
  - `MainTrackList.Start` 시점에 원천 데이터 배열 `__instance.tracks` (`Il2CppReferenceArray<MainTrackListBlock>`)를 탐색합니다.
  - 타겟 곡 데이터 객체의 `MainTrackListBlock.TrackCover` 자체를 커스텀 PNG 스프라이트로 세팅합니다.
  - 이를 통해 **커서 위치와 상관없이 타겟 곡 카드와 메인 상세 자켓(`UI_Cover`)에 핀포인트로 자켓이 적용**되며, 리스트의 다른 원본 곡 카드들은 자기 고유 자켓을 유지합니다.

### 2) 로딩 씬 (Loading Scene)
- **타겟 클래스**: `LoadingGamePlay`
- **주입 메커니즘**:
  - `LoadingGamePlay.Start` 시점 및 비동기 데이터 수신 코루틴인 `WaitForGameDataAndUpdateUI` 완료 직후(`Postfix`) `NowTrackCover`, `NowTrackCover_bg`, `gameData.NowTrackCover`에 커스텀 PNG를 재적용하여 비동기 타이밍으로 인한 원본 복원 현상을 차단합니다.

### 3) 인게임 씬 (In-Game HUD & Controller)
- **타겟 클래스**: `HUDControl`, `RhythmGameController`, `CoverArtController`
- **주입 메커니즘**:
  - `HUDControl.Awake` 단 1회 시점에 `cachedCover` 및 `songCoverImage.sprite`를 세팅하여 스팸 갱신과 프레임 드랍 노이즈를 완전 제거합니다.
  - `RhythmGameController.TriggerAudioStartIfReady` 시점에 커스텀 BGM 스트리밍 및 강제 오디오 전환을 보장합니다.
  - `RhythmGameController.LoadVideo` 및 `PlayVideoWithOffset` 시점에 BGA 비디오 URL을 연동합니다.

### 4) 결과 화면 (Result Scene)
- **타겟 클래스**: `Panel_Result`
- **주입 메커니즘**:
  - `Panel_Result.Awake` 시점에 자식 렌더러 계층구조 스캔(`ApplyCustomCoverToHierarchy`) 및 내부 `VideoPlayer` URL 바인딩을 실행합니다.

---

## 4. 모딩 트러블슈팅 가이드

| 증상 | 원인 | 해결책 |
| :--- | :--- | :--- |
| **BGA 비디오 미출력/검은 화면** | `VideoPlayer.source`가 `VideoClip`인 상태에서 `url`만 설정함 | `player.source = VideoSource.Url;` 설정 후 `Prepare()` → `Play()` 호출 |
| **PNG 이미지 자글거림/계단현상** | `Texture2D` 기본 로딩 필터가 `FilterMode.Point`임 | `tex.filterMode = FilterMode.Bilinear; tex.wrapMode = TextureWrapMode.Clamp;` 적용 |
| **인게임/로딩 씬 커버 복원 현상** | 게임 내 비동기 코루틴이 후속으로 원본 `GameData` 커버를 재할당함 | `WaitForGameDataAndUpdateUI` 코루틴 직후 및 `HUDControl.Awake` 단 1회 세팅 훅 추가 |
| **곡 리스트 전체 커버 교체 문제** | `SetSelected` 혹은 전역 `TrackCover` Getter에 무차별 훅을 걺 | `MainTrackList.tracks` 타겟 데이터 객체의 `MainTrackListBlock.TrackCover` 핀포인트 주입으로 변경 |
| **HUD 갱신 시 이미지 떨림/프레임 드랍** | `RefreshSongMetaUI` 등 매 프레임 실행 루프에서 스프라이트 재할당 | `if (targetImage.sprite == sprite) return;` 중복 세팅 방지 검사 적용 |
