# EveryWhere_AR

Unity 기반 AR 네비게이션 애플리케이션의 기술적 구현 문서입니다.

![AR 네비게이션 데모](images/ar-navigation-demo.png)

## 기술 스택

### Unity 환경
- **Unity 버전**: 2022.3 이상
- **렌더 파이프라인**: Universal Render Pipeline (URP) 17.0.3
- **타겟 플랫폼**: iOS (ARKit 6.0.6)

### 핵심 패키지
- **Immersal SDK**: 클라우드 기반 AR 공간 인식 및 Visual Positioning System
- **Unity AI Navigation** 2.0.4: NavMesh 경로 계산 및 에이전트 제어
- **AR Foundation (ARKit)** 6.0.6: iOS AR 기능 통합
- **GLTFast** 6.14.1: GLB/GLTF 3D 모델 비동기 로딩

## 핵심 기술 구현

### 1. 포인트 클라우드 기반 3D 특질점 추출 및 Reconstruction

#### 특질점 추출 프로세스

**카메라 프레임 수집**
- ARKit을 통해 실시간 카메라 프레임 캡처
- 각 프레임마다 카메라 포즈(위치, 회전), 내부 파라미터(intrinsics), 이미지 데이터 수집
- 좌표계 변환: ARKit 좌표계에서 Unity 좌표계로 변환 (`SwitchHandedness()`)

**이미지 처리 및 특질점 추출**
- Immersal Core 엔진에 이미지 데이터 전달
- 각 이미지에서 특징점(Feature Points) 추출
- 카메라 포즈와 내부 파라미터를 함께 전달하여 3D 공간 좌표 계산

**포인트 클라우드 생성**
- 추출된 특질점들을 3D 공간 좌표로 변환
- 다중 뷰에서 동일한 특징점을 매칭하여 3D 포인트 생성
- Sparse 포인트 클라우드 형성 (`.ply` 파일로 저장)

**Reconstruction 과정**
- Structure from Motion (SfM) 알고리즘 적용
- Bundle Adjustment를 통한 카메라 포즈 및 3D 포인트 최적화
- 다중 이미지 간 특징점 매칭 및 삼각측량(Triangulation)
- 최종적으로 정밀한 3D 맵 데이터 생성 (`.bytes` 바이너리 형식)

**데이터 구조**
- `.bytes`: 압축된 바이너리 맵 데이터 (특질점, 카메라 포즈, 메타데이터)
- `metadata.json`: 맵 ID, 이름, 좌표계 정보, 생성 시간 등
- `sparse.ply`: 시각화용 포인트 클라우드 데이터

### 2. 유저 위치 VPS (Visual Positioning System)

#### VPS 아키텍처

**로컬라이제이션 파이프라인**
```
ARKit 카메라 프레임 → PlatformSupport → ImmersalSDK → Localizer → 위치 추정 결과
```

**실시간 위치 추정 프로세스**

1. **카메라 데이터 수집**
   - ARKit에서 현재 카메라 프레임 캡처
   - 카메라 포즈, 이미지 데이터, 내부 파라미터 추출
   - `IPlatformUpdateResult` 인터페이스를 통해 플랫폼 독립적 데이터 구조로 변환

2. **맵 데이터 로드**
   - 사전에 생성된 Immersal 맵 데이터를 메모리에 로드
   - 맵 ID를 기반으로 해당 공간의 특질점 맵 참조
   - 온디바이스(`DeviceLocalization`) 또는 서버 기반(`ServerLocalization`) 처리 선택

3. **특질점 매칭**
   - 현재 카메라 프레임에서 특질점 추출
   - 로드된 맵의 특질점과 매칭 수행
   - Nearest Neighbor 검색 및 RANSAC 알고리즘으로 아웃라이어 제거

4. **포즈 추정 (Pose Estimation)**
   - 매칭된 특질점 쌍을 기반으로 카메라 포즈 계산
   - PnP (Perspective-n-Point) 문제 해결
   - Bundle Adjustment를 통한 포즈 최적화

5. **좌표계 동기화**
   - 추정된 포즈를 Unity AR 좌표계로 변환
   - `SceneUpdater`를 통해 AR 씬의 루트 좌표계 업데이트
   - AR 콘텐츠가 실제 공간과 정확히 정렬되도록 보장

**로컬라이제이션 방법**

- **DeviceLocalization**: 모든 처리를 디바이스에서 수행. 빠른 응답 속도, 오프라인 동작 가능
- **ServerLocalization**: 서버에서 처리. 더 정확한 결과, 네트워크 의존성

**추적 상태 분석**
- `TrackingAnalyzer`를 통해 로컬라이제이션 성공률 모니터링
- 추적 실패 시 재로컬라이제이션 시도
- ARKit 추적 상태와 Immersal 로컬라이제이션 결과를 종합 분석

### 3. 길찾기 로직

#### NavMesh 기반 경로 계산

**NavMesh 생성**
- AR 환경에서 탐색 가능한 영역을 마킹
- 지면 평면 감지 및 장애물 식별
- NavMesh Surface 컴포넌트를 사용하여 NavMesh 빌드
- 탐색 가능한 영역을 폴리곤 메시로 변환

**경로 계산 알고리즘**
- A* (A-Star) 알고리즘 기반 경로 탐색
- 시작점(현재 사용자 위치)과 목표점(목적지) 사이의 최적 경로 계산
- NavMesh의 노드 그래프를 따라 경로 생성
- 장애물 회피 및 최단 거리 경로 선택

**경로 최적화**
- 경로의 각 코너 포인트(waypoint) 추출
- 불필요한 중간 노드 제거로 경로 스무딩
- 경로 길이와 이동 난이도 고려

**실시간 경로 업데이트**
- 사용자 위치가 변경될 때마다 경로 재계산
- 동적 장애물 감지 시 경로 재계획
- 목표점 변경 시 즉시 새로운 경로 생성

**경로 데이터 구조**
- `NavMeshPath`: 계산된 경로를 저장하는 데이터 구조
- `corners`: 경로의 주요 포인트 배열
- 각 포인트는 Unity 월드 좌표계의 3D 위치

### 4. AR 네비게이션

![AR 네비게이션 경로 시각화](images/ar-navigation-demo.png)

#### AR 경로 시각화

**경로 렌더링**
- `LineRenderer` 컴포넌트를 사용하여 경로를 3D 라인으로 표시
- NavMesh 경로의 각 코너 포인트를 연결하여 시각화
- 녹색 라인으로 경로 표시 (시작점에서 목표점까지)

**AR 공간 정렬**
- Immersal VPS로 추정된 사용자 위치를 기준으로 경로 배치
- AR 좌표계와 실제 공간 좌표계 동기화
- 경로가 실제 환경의 지면과 정확히 일치하도록 보장

**실시간 업데이트**
- 매 프레임마다 사용자 위치 추적
- 경로 재계산 및 시각화 업데이트
- 카메라 움직임에 따라 경로가 올바른 위치에 유지

#### 네비게이션 UI 요소

**방향 안내**
- 현재 위치에서 다음 경로 포인트까지의 방향 표시
- 화살표 또는 3D 모델을 사용한 방향 가이드
- 거리 정보 표시

**경로 진행 상황**
- 전체 경로 중 현재 진행률 표시
- 목표점까지의 남은 거리 계산 및 표시

**AR 콘텐츠 배치**
- 경로를 따라 안내 마커 배치
- 목표점에 3D 모델 또는 마커 표시
- 중요한 경로 포인트에 정보 오버레이

#### 통합 시스템

**VPS + NavMesh 통합**
- Immersal VPS로 정확한 사용자 위치 추정
- 추정된 위치를 NavMesh 좌표계로 변환
- NavMesh를 사용하여 경로 계산
- 계산된 경로를 AR 좌표계로 다시 변환하여 시각화

**성능 최적화**
- 경로 계산은 필요할 때만 수행 (목표점 변경 시)
- 경로 시각화는 매 프레임 업데이트하되, 경로 데이터는 캐싱
- 비동기 처리로 메인 스레드 블로킹 방지

## 시스템 아키텍처

### 전체 데이터 흐름

```
1. 맵 생성 단계
   ARKit 카메라 → 이미지 수집 → 특질점 추출 → 포인트 클라우드 생성 → 맵 데이터 저장

2. 런타임 로컬라이제이션
   ARKit 카메라 → 현재 프레임 캡처 → 맵 데이터 로드 → 특질점 매칭 → 위치 추정 → 좌표계 동기화

3. 네비게이션
   사용자 위치 (VPS) → 목표점 설정 → NavMesh 경로 계산 → AR 경로 시각화 → 실시간 업데이트
```

### 좌표계 변환

- **ARKit 좌표계**: 오른손 좌표계, Y-up
- **Unity 좌표계**: 왼손 좌표계, Y-up
- **변환 방법**: `SwitchHandedness()` 확장 메서드로 Z축 반전
- **Immersal 맵 좌표계**: Unity 좌표계와 동일하게 저장

## 프로젝트 구조

```
Assets/
├── Script/
│   └── NavMesh.cs                    # NavMesh 경로 시각화 컴포넌트
├── Map Data/                         # Immersal 맵 데이터
│   ├── {mapId}.bytes                 # 바이너리 맵 데이터 (특질점, 포즈)
│   ├── {mapId}-metadata.json         # 맵 메타데이터
│   └── {mapId}-sparse.ply            # 포인트 클라우드 시각화 데이터
├── Samples/Immersal SDK/
│   └── Core Samples/
│       ├── Scripts/
│       │   ├── CustomLocalization.cs      # 커스텀 로컬라이제이션 구현
│       │   ├── Mapping/RealtimeCaptureManager.cs  # 실시간 매핑
│       │   └── MapListController.cs      # 맵 로딩 관리
│       └── Scenes/                        # 샘플 씬
└── XR/
    ├── Settings/ARKitSettings.asset      # ARKit 설정
    └── Resources/                         # XR 리소스
```

## 성능 고려사항

### 메모리 관리
- 맵 데이터는 필요할 때만 로드하고 사용 후 해제
- 카메라 이미지 데이터는 참조 카운팅으로 관리
- 포인트 클라우드 시각화는 최대 버텍스 수 제한

### 비동기 처리
- 맵 로딩 및 로컬라이제이션은 비동기로 처리
- 네이티브 호출은 별도 스레드에서 실행
- UI 업데이트는 메인 스레드에서 수행

### 최적화 기법
- 이미지 다운샘플링 옵션 제공
- 로컬라이제이션 빈도 조절 가능
- 포인트 클라우드 렌더링 최적화 (LOD, 프러스텀 컬링)
