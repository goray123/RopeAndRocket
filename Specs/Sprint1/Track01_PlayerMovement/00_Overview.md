# Track 1 - 플레이어 기본 이동

- **상태:** APPROVED
- **작성일:** 2026-07-09
- **버전:** 0.2
- **Sprint:** Sprint 1 - Core Movement Prototype
- **관련 상위 문서:** `Specs/Sprint1/README.md`, `Specs/GameDesign/게임_총_기획_문서_v0.1.md`

---

# 목적

플레이어가 1인칭 시점에서 이동, 시점 회전, 점프를 즉시 사용할 수 있는 기본
조작 상태를 만든다.

이 트랙의 이동 기반은 Track 2의 로프 스윙과 Track 4의 로켓 반동이 같은 속도와
모멘텀을 이어받을 수 있어야 한다. 따라서 플레이어 물리는 `Rigidbody`로
확정한다. `CharacterController`처럼 별도의 운동 계산으로 전환하지 않고,
이후 트랙에서 힘과 속도를 같은 물리 상태에 누적하기 위함이다.

# 플레이어 경험

- 마우스 입력에 시점이 즉시 반응한다.
- WASD 입력 방향과 실제 이동 방향이 1인칭 시점의 수평 방향과 일치한다.
- 지상에서는 원하는 방향으로 빠르게 움직이고, 공중에서도 이동 방향을 조절할
  수 있다.
- 점프와 착지 동작이 예측 가능하며 공중에서 추가 점프가 발생하지 않는다.
- 이후 로프와 로켓 반동이 추가되어도 현재 이동 흐름과 모멘텀이 끊기지 않는다.

# 범위

## 포함

- Unity 6 프로젝트에서 동작하는 Rigidbody 기반 플레이어 이동
- Input System 기반 WASD 이동, 마우스 시점 회전, 점프
- 1인칭 카메라의 Yaw 및 Pitch 회전
- 지상 판정
- 지상 이동과 공중 이동
- 이동, 점프, 공중 제어, 카메라 감도의 Inspector 튜닝값
- 플레이어 이동 중 Rigidbody 모멘텀 유지

## 제외

- 로프 발사, 연결, 스윙, 해제
- 로켓 발사, 폭발, 반동
- 달리기, 대시, 슬라이딩, 벽타기, 웅크리기
- 경사면 전용 이동 보정과 계단 오르기 보정
- 이동 애니메이션, 사운드, UI
- 게임패드 바인딩과 게임패드 감도
- 키 재지정 및 설정 저장
- 씬 오브젝트 생성, 컴포넌트 부착, Input Action 바인딩, Layer 설정 등
  Unity Editor 연결 작업

# 상세 규칙

## 입력

모든 런타임 입력은 Unity Input System을 사용한다. Legacy Input Manager API는
사용하지 않는다.

Action Map 이름은 `Player`이며 필요한 Action은 다음과 같다.

| Action | Action Type | Control Type | 기본 바인딩 | 처리 규칙 |
|---|---|---|---|---|
| `Move` | Value | Vector2 | WASD 2D Vector Composite | 매 물리 프레임에 현재 값을 사용한다. |
| `Look` | Value | Vector2 | Mouse Delta | 매 렌더 프레임에 현재 값을 사용한다. |
| `Jump` | Button | Button | Space | 해당 입력이 시작된 프레임에 1회 요청한다. |

- `Move.x`는 좌우, `Move.y`는 전후 입력이다.
- 대각선 입력의 크기는 1을 넘지 않도록 정규화하여 직선보다 빨라지지 않게 한다.
- 반대 방향 키를 동시에 누르면 해당 축 입력은 0이다.
- `Look.x`는 플레이어 본체의 Yaw, `Look.y`는 카메라 Pivot의 Pitch에 적용한다.
- 마우스를 위로 움직이면 위를 바라본다. 반전 Y 옵션은 이 트랙에 포함하지 않는다.
- `Jump`를 누르고 있는 동안 반복 점프하지 않는다. 새 버튼 누름마다 한 번만
  점프를 요청한다.
- 입력이 비활성화되거나 Player Input이 Disable되면 이동 입력과 대기 중인 점프
  요청을 즉시 0으로 초기화한다.

## 상태와 동작

### 카메라

1. Yaw는 플레이어 Rigidbody가 있는 루트의 Y축 회전으로 적용한다.
2. Pitch는 플레이어 루트의 자식인 Camera Pivot에만 적용한다.
3. Roll은 항상 0도로 유지한다.
4. Pitch 누적값은 `pitchMin` 이상 `pitchMax` 이하로 제한한다.
5. 커서 잠금과 숨김은 플레이 시작 시 적용한다.
6. 이 트랙에서는 커서 잠금 해제 입력과 일시정지를 제공하지 않는다.

### 지상 판정

1. 플레이어 발 아래의 `groundCheck` 위치에서 구형 겹침 검사로 지상을 판정한다.
2. 검사 대상은 `groundMask`에 포함되고 Trigger가 아닌 Collider만 인정한다.
3. 하나 이상의 유효한 Collider가 감지되면 `Grounded`, 아니면 `Airborne`이다.
4. 플레이어 자신의 Collider는 `groundMask`에서 제외해야 한다.
5. 경사면도 검사 구체에 닿으면 지상으로 간주한다. 최대 경사각 제한은 이 트랙에서
   적용하지 않는다.

### 지상 이동

1. 이동 희망 방향은 플레이어 루트의 수평 Forward/Right와 `Move` 입력으로
   계산한다.
2. 희망 방향에는 Y 성분을 포함하지 않는다.
3. 입력이 있으면 수평 속도를 `moveSpeed` 방향으로 `groundAcceleration`만큼
   가속한다.
4. 입력이 없으면 수평 속도를 0 방향으로 `groundDeceleration`만큼 감속한다.
5. 이동 계산은 Rigidbody의 수직 속도를 덮어쓰지 않는다.
6. Rigidbody의 수평 속도를 매 프레임 `moveSpeed`로 강제 Clamp하지 않는다.
   로프나 로켓이 추가한 외부 모멘텀이 `moveSpeed`보다 높을 수 있기 때문이다.
7. 플레이어 입력으로 만들어지는 목표 속도만 `moveSpeed`를 기준으로 삼는다.

### 공중 이동

1. 공중에서도 WASD 입력을 받는다.
2. 공중 이동은 현재 수평 속도에 `airAcceleration`만큼 가속을 추가한다.
3. 공중 입력으로 새로 만드는 수평 속도의 기준은 `moveSpeed`지만, 이미
   `moveSpeed`보다 빠른 모멘텀은 감소시키지 않는다.
4. 공중에서 입력이 없을 때 인위적인 수평 감속을 적용하지 않는다.
5. 중력과 수직 속도는 Rigidbody와 프로젝트 Physics 설정을 따른다.

### 점프

1. `Jump` 버튼이 새로 눌렸고 그 순간 `Grounded`일 때만 점프한다.
2. 점프 시 기존 하강 속도가 있으면 수직 속도를 0으로 정리한 뒤 위쪽 방향으로
   `jumpVelocity`를 적용한다.
3. 기존 상승 속도가 있다면 이를 중첩해 더 높은 점프를 만들지 않는다.
4. `Airborne` 상태의 점프 요청은 소비하고 무시한다. 착지 후 자동 실행하지 않는다.
5. 더블 점프, 코요테 타임, 점프 입력 버퍼, 가변 점프 높이는 이 트랙에 포함하지
   않는다.

### Rigidbody 동작

- 플레이어 루트에는 Rigidbody와 Capsule Collider를 사용한다.
- Rigidbody의 회전은 X축과 Z축을 고정하고 Yaw만 허용한다.
- Rigidbody 중력은 활성화한다.
- 물리 이동은 `FixedUpdate` 주기에 맞춰 처리한다.
- Rigidbody의 위치 또는 속도와 Transform 위치를 서로 경쟁적으로 갱신하지 않는다.
- 충돌 검출 모드 기본값은 `Continuous Dynamic`, 보간 기본값은 `Interpolate`로
  한다.

## 수치 및 조정값

아래 값은 프로토타입의 **초기 기본값**이며 Inspector에서 조정 가능해야 한다.
플레이테스트를 통한 최종 튜닝값은 아직 확정하지 않는다.

| 항목 | 필드명 | 기본값 | 허용 범위 | 단위/비고 |
|---|---|---:|---:|---|
| 지상 목표 속도 | `moveSpeed` | 7 | 0~30 | m/s |
| 지상 가속도 | `groundAcceleration` | 50 | 0~150 | m/s² |
| 지상 감속도 | `groundDeceleration` | 60 | 0~200 | m/s² |
| 공중 가속도 | `airAcceleration` | 15 | 0~100 | m/s² |
| 점프 초기 수직 속도 | `jumpVelocity` | 7 | 0~30 | m/s |
| X축 시점 감도 | `sensitivityX` | 0.1 | 0~2 | Mouse Delta 배율 |
| Y축 시점 감도 | `sensitivityY` | 0.1 | 0~2 | Mouse Delta 배율 |
| 최소 Pitch | `pitchMin` | -85 | -90~0 | degree |
| 최대 Pitch | `pitchMax` | 85 | 0~90 | degree |
| 지상 검사 반지름 | `groundCheckRadius` | 0.25 | 0.01~1 | m |

- 프로젝트 중력은 Unity 프로젝트 Physics 설정값을 사용하며 기본 권장값은
  `(0, -9.81, 0)`이다.
- 플레이어 Rigidbody의 질량 기본값은 `1 kg`이다.
- 카메라 감도는 프레임 시간과 곱하지 않는다. Mouse Delta는 프레임 사이 누적
  이동량이므로 별도의 `deltaTime` 적용으로 프레임률 의존성을 만들지 않는다.
- 허용 범위 밖 Inspector 값은 실행 중 안전한 범위로 제한하거나 에디터 검증으로
  보정한다.

## 예외 조건

- `Move` 입력이 0이면 입력에 의한 가속을 만들지 않는다.
- `Look` 입력이 0이면 현재 Yaw와 Pitch를 유지한다.
- 공중에서 `Jump`가 여러 번 입력되어도 수직 속도에 변화가 없어야 한다.
- 경사진 표면의 가장자리에서 Ground Check가 지면을 감지하지 못하면 Airborne으로
  처리한다.
- 벽 Collider는 `groundCheck` 구체와 겹치지 않는 배치가 전제다. 벽만 접촉한
  상태는 Grounded가 아니다.
- 낮은 천장에 머리가 닿는 경우 Rigidbody 충돌 결과를 따르며 추가 천장 보정은
  하지 않는다.
- 포커스를 잃었다가 돌아왔을 때 이전 이동 입력이나 점프 요청이 남지 않아야 한다.
- `groundCheck`, 카메라 Pivot, 입력 참조 등 필수 연결이 없으면 무시하고 계속
  동작시키지 말고 원인을 식별할 수 있는 오류를 한 번 명확히 보고한다.

# 다른 시스템과의 연동

## Track 2 - 로프 액션

- 로프 힘은 같은 플레이어 Rigidbody에 적용한다.
- 로프가 만든 수평 및 수직 속도는 기본 이동 코드가 일괄 덮어쓰거나
  `moveSpeed`로 제한하지 않는다.
- 로프 연결 여부와 로프 전용 공중 제어 계수는 Track 2에서 정의한다. 그 전까지는
  본 문서의 공중 이동 규칙을 사용한다.

## Track 4 - 로켓 반동

- 반동은 같은 플레이어 Rigidbody에 외력 또는 속도 변화로 적용한다.
- 반동으로 `moveSpeed`를 초과한 속도는 기본 이동 시스템이 즉시 제거하지 않는다.

## Input System

- 이후 Action은 동일한 `Player` Action Map에 추가할 수 있다.
- Track 1 코드는 `Move`, `Look`, `Jump` 외 Action의 존재를 요구하지 않는다.

# 구현 요구사항

- Unity 6 및 Unity Input System을 기준으로 한다.
- 런타임 역할은 최소한 플레이어 물리 이동과 카메라 시점 제어로 분리한다.
- Rigidbody 관련 변경은 물리 주기, 카메라 입력 및 회전은 렌더 프레임 주기로
  처리한다.
- 플레이어 이동 코드는 Input System Action에서 받은 값을 사용하며 Legacy
  Input API를 호출하지 않는다.
- 모든 튜닝값과 필수 참조는 Inspector에서 확인 가능해야 한다.
- 필수 참조 누락을 실행 초기에 검증해야 한다.
- 클래스는 향후 로프 및 반동 시스템이 플레이어 Rigidbody에 접근할 수 있게 하되,
  Track 1에서 해당 기능을 미리 구현하지 않는다.
- Unity 씬 구성과 Inspector 연결은 사용자가 직접 수행한다. 개발자는 필요한
  GameObject 계층, 컴포넌트, Layer, Input Action 연결 목록을 구현 결과 문서에
  정확히 제공한다.

권장 씬 계층은 다음과 같다.

```text
Player
├── Rigidbody
├── Capsule Collider
├── Player Input
├── Player Movement
├── Ground Check
└── Camera Pivot
    └── Main Camera
        └── Player Camera
```

# 검증 방법

Unity 씬 및 Inspector 연결은 사용자가 완료한 뒤 Play Mode에서 검증한다.

1. 평지와 벽이 있는 테스트 씬에 Player를 배치한다.
2. `Move`, `Look`, `Jump` Action과 필요한 참조를 연결한다.
3. 마우스를 좌우/상하로 움직여 Yaw와 Pitch 방향 및 제한값을 확인한다.
4. W, A, S, D를 각각 누르고 시점의 수평 방향 기준으로 이동하는지 확인한다.
5. W+D를 동시에 눌러 W 단독보다 이동 속도가 빨라지지 않는지 확인한다.
6. 이동 키를 놓아 지상 감속이 적용되는지 확인한다.
7. 지상에서 Space를 한 번 눌러 한 번만 점프하는지 확인한다.
8. Space를 누른 채 착지해 자동 재점프하지 않는지 확인한다.
9. 공중에서 Space를 반복 입력해 추가 점프가 발생하지 않는지 확인한다.
10. 공중에서 WASD로 이동 방향을 조절할 수 있는지 확인한다.
11. 외부에서 Rigidbody에 `moveSpeed`를 넘는 수평 속도를 준 뒤, 입력이 없을 때
    공중에서 해당 속도가 기본 이동 코드 때문에 즉시 제한되지 않는지 확인한다.
12. Inspector에서 각 튜닝값을 변경하고 다음 Play에서 동작에 반영되는지 확인한다.
13. 게임 창 포커스를 잃은 상태에서 키를 놓고 복귀해 이동이 계속되지 않는지
    확인한다.
14. 코드 검색으로 Legacy Input API 호출이 없음을 확인한다.

# 완료 조건

- Unity 6에서 컴파일 오류 없이 실행된다.
- 플레이어 루트가 Rigidbody와 Capsule Collider를 사용한다.
- 마우스 X 입력으로 Yaw, Y 입력으로 Pitch가 올바른 방향으로 회전한다.
- Pitch가 기본값 `-85°~85°`를 넘지 않고 Roll이 발생하지 않는다.
- WASD 이동이 플레이어 시점의 수평 방향 기준으로 동작한다.
- 대각선 입력으로 목표 이동 속도가 증가하지 않는다.
- 지상 가속과 감속, 공중 가속을 Inspector 값으로 각각 조정할 수 있다.
- 지상에서 새로 누른 Space 입력에만 점프한다.
- 공중 추가 점프와 착지 후 자동 점프가 발생하지 않는다.
- 공중에서도 WASD 제어가 가능하다.
- 외부 힘으로 얻은 `moveSpeed` 초과 모멘텀을 기본 이동이 즉시 제거하지 않는다.
- Ground Check가 지정된 지면 Layer만 판정한다.
- 모든 입력이 Input System Action을 통해 처리되며 Legacy Input을 사용하지 않는다.
- 사용자가 씬과 Inspector를 연결할 수 있도록 필요한 연결 목록이 구현 결과에
  제공된다.

# 미결정 사항

- 기본 수치는 구현 시작점이며 실제 플레이 감각에 맞춘 최종 튜닝은 사용자
  플레이테스트 후 결정한다.
- 로프 연결 중 공중 제어 강도와 최대 속도 정책은 Track 2에서 확정한다.
- 로켓 반동 이후의 감속 및 최대 속도 정책은 Track 4에서 확정한다.
- 코요테 타임, 점프 입력 버퍼, 경사면 이동 보정은 Sprint 1 핵심 이동 검증 후
  필요성이 확인될 때 별도 스펙으로 결정한다.
