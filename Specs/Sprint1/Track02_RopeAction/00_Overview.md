# Track 2 - 로프 액션

- **상태:** READY_FOR_REVIEW
- **작성일:** 2026-07-09
- **버전:** 0.1
- **Sprint:** Sprint 1 - Core Movement Prototype
- **관련 상위 문서:** `Specs/Sprint1/README.md`, `Specs/GameDesign/게임_총_기획_문서_v0.1.md`
- **선행 기능:** `Specs/Sprint1/Track01_PlayerMovement/00_Overview.md`

---

# 목적

플레이어가 조준한 지점으로 앵커를 발사하고, 지형에 고정된 앵커와
`SpringJoint`로 연결되어 자유롭게 스윙할 수 있는 로프 이동을 구현한다.

로프 연결 중 Space를 누르면 앵커 방향으로 가속하여 높은 곳으로 빠르게
접근할 수 있어야 한다.

# 플레이어 경험

- 화면 중앙으로 원하는 지형을 조준하고 즉시 앵커를 발사한다.
- 앵커가 날아가 지형에 박히는 과정이 눈에 보인다.
- 로프가 연결되면 기존 속도를 잃지 않고 원호를 그리며 스윙한다.
- 이동 입력과 카메라 조작을 함께 사용해 스윙 방향을 조절한다.
- Space를 누르면 로프를 타고 앵커 쪽으로 빠르게 끌려간다.
- 마우스 버튼을 놓는 즉시 로프가 해제되고 현재 모멘텀으로 날아간다.

# 범위

## 포함

- 카메라 조준 방향으로 앵커 발사
- 이동 중인 앵커의 충돌 판정
- 벽, 바닥, 천장을 포함한 지정 지형에 앵커 고정
- 플레이어 Rigidbody와 고정 지점 사이의 `SpringJoint`
- 왼손 위치와 앵커 사이의 `LineRenderer`
- 로프 연결 중 물리 기반 스윙
- Space 입력을 통한 앵커 방향 가속
- 버튼 해제, 사거리 초과, 잘못된 충돌 시 로프 해제
- 로프 관련 플레이테스트 튜닝값

## 제외

- 로프 길이 수동 조절
- 로프 감기와 풀기 애니메이션
- 앵커가 적이나 움직이는 Rigidbody에 연결되는 기능
- 로프가 지형 모서리에 감기거나 꺾이는 기능
- 로프 절단, 내구도, 쿨타임 및 사용 횟수
- 로프 공격과 적 끌어오기
- 손과 앵커의 애니메이션 및 최종 아트
- 사운드, 파티클, 카메라 흔들림, UI 조준점

# 상세 규칙

## 입력

모든 입력은 Unity Input System의 `Player` Action Map을 사용한다.

| Action | Action Type | 기본 바인딩 | 동작 |
|---|---|---|---|
| `Grapple` | Button | 마우스 오른쪽 버튼 | 누르면 발사하고, 누르는 동안 유지하며, 놓으면 해제한다. |
| `Jump` | Button | Space | 연결 중 누르고 있으면 앵커 방향으로 가속한다. |

- 로켓 발사가 추가될 Track 3와 충돌하지 않도록 로프는 마우스 오른쪽 버튼을
  기본값으로 사용한다.
- `Grapple`은 기존 Input Action 에셋에 새로 추가해야 한다.
- FPS 카메라가 커서를 잠그므로 마우스 화면 좌표가 아니라 **화면 중앙에서
  카메라 정면으로 나가는 Ray**를 조준 기준으로 사용한다.
- 로프가 연결된 동안 Space는 앵커 가속을 우선하며 일반 점프를 실행하지 않는다.
- 앵커가 날아가는 동안 Space는 아무 동작도 하지 않는다.

## 상태와 동작

로프는 다음 세 상태 중 하나만 가진다.

```text
Ready
  └─ Grapple 누름 → Firing

Firing
  ├─ 유효 지형 충돌 → Connected
  ├─ Grapple 해제 → Ready
  └─ 최대 사거리 도달 → Ready

Connected
  ├─ Grapple 유지 → 스윙 유지
  ├─ Space 유지 → 앵커 방향 가속
  └─ Grapple 해제 → Ready
```

### Ready

- 앵커와 로프가 존재하지 않는다.
- `SpringJoint`와 `LineRenderer`는 비활성 상태다.
- `Grapple`을 새로 누르면 앵커 발사를 시작한다.

### Firing

1. 앵커는 카메라 위치에서 화면 중앙 방향으로 발사한다.
2. 앵커의 시작 위치는 카메라지만, 로프 선의 시작점은 플레이어의 왼손 위치다.
3. 앵커는 `anchorSpeed` 속도로 직선 이동한다.
4. 빠른 이동 중 지형을 통과하지 않도록 이전 위치와 다음 위치 사이를
   Sphere Cast 또는 동등한 연속 충돌 검사로 확인한다.
5. 앵커가 이동하는 동안에도 왼손과 앵커 사이에 로프를 표시한다.
6. 발사 시작점부터 이동한 거리가 `maxGrappleDistance`에 도달하면 실패 처리하고
   앵커와 로프를 즉시 제거한다.

### 앵커 충돌

- `grappleMask`에 포함되고 Trigger가 아닌 Collider만 유효한 지형이다.
- 벽, 바닥, 천장은 Layer 조건만 만족하면 모두 연결할 수 있다.
- 유효 지형에 충돌하면 Raycast Hit 지점을 월드 앵커 위치로 사용한다.
- 앵커 방향은 충돌 표면의 Normal을 기준으로 정렬할 수 있다.
- 플레이어 자신의 Collider와 움직이는 Rigidbody에는 연결하지 않는다.
- 유효하지 않은 Collider와 충돌하면 연결하지 않고 발사를 종료한다.
- Track 2에서는 앵커가 고정된 뒤 원래 Collider가 움직여도 앵커 월드 위치는
  따라가지 않는다.

### Connected

1. 유효 지형에 앵커가 박히면 플레이어 Rigidbody에 `SpringJoint`를 연결한다.
2. `SpringJoint.connectedBody`는 사용하지 않고 `connectedAnchor`에 충돌 지점의
   월드 좌표를 지정한다.
3. 연결 직전 플레이어의 선형 속도는 초기화하거나 감소시키지 않는다.
4. 연결 시 플레이어와 앵커 사이의 거리를 초기 로프 길이로 사용한다.
5. `SpringJoint.maxDistance`는 `초기 거리 × maxDistanceRatio`로 설정한다.
6. `SpringJoint.minDistance`는 `초기 거리 × minDistanceRatio`로 설정한다.
7. Joint의 `spring`과 `damper`로 로프의 탄성과 흔들림을 조절한다.
8. 플레이어는 Track 1의 WASD 공중 제어를 계속 사용할 수 있다.
9. 기본 이동 시스템은 로프가 만든 속도를 `moveSpeed`로 Clamp하지 않는다.
10. 로프 선은 매 프레임 왼손 위치와 고정된 앵커 위치를 잇는다.

### 앵커 방향 가속

1. `Connected` 상태에서 Space를 누르고 있는 동안만 동작한다.
2. 플레이어에서 앵커로 향하는 정규화 방향으로 `pullAcceleration`을 적용한다.
3. 힘은 Rigidbody의 물리 주기에서 가속도 방식으로 적용해 질량에 영향받지 않는다.
4. 가속으로 얻은 속도는 Track 1의 `moveSpeed` 제한을 받지 않는다.
5. 앵커까지 거리가 `pullStopDistance` 이하가 되면 추가 가속을 중단한다.
6. 가속을 중단해도 로프 연결은 `Grapple` 버튼을 놓을 때까지 유지한다.
7. Space를 놓으면 가속만 중단하고 현재 속도와 스윙은 유지한다.

### 로프 해제

- `Grapple` 버튼을 놓으면 상태와 관계없이 즉시 해제한다.
- 해제할 때 앵커, `SpringJoint`, 로프 표시를 제거한다.
- 플레이어 Rigidbody의 현재 선형 속도는 유지한다.
- 해제 직후 별도의 점프 힘이나 추가 추진력을 적용하지 않는다.
- 버튼을 다시 누르면 새 앵커를 발사할 수 있다.

## LineRenderer

- 시작점은 플레이어 왼손을 나타내는 `ropeOrigin` Transform이다.
- 끝점은 이동 중이거나 고정된 앵커의 월드 위치다.
- `Firing`과 `Connected` 상태에서만 표시한다.
- 로프는 항상 두 점을 잇는 직선 한 구간으로 표현한다.
- 폭, Material, 색상은 Inspector에서 연결하고 조정한다.
- 왼손 오브젝트의 애니메이션은 범위 밖이며 Transform 위치만 따른다.

## 수치 및 조정값

아래 값은 프로토타입 시작값이다. 최종값은 Play Mode에서 반복 조정한다.

| 항목 | 필드명 | 기본값 | 허용 범위 | 단위/비고 |
|---|---|---:|---:|---|
| 앵커 발사 속도 | `anchorSpeed` | 60 | 1~150 | m/s |
| 최대 연결 거리 | `maxGrappleDistance` | 35 | 1~100 | m |
| 앵커 충돌 반경 | `anchorRadius` | 0.08 | 0.01~0.5 | m |
| SpringJoint 탄성 | `spring` | 45 | 0~200 | Joint spring |
| SpringJoint 감쇠 | `damper` | 7 | 0~50 | Joint damper |
| 최대 거리 비율 | `maxDistanceRatio` | 0.8 | 0.1~1 | 초기 거리 기준 |
| 최소 거리 비율 | `minDistanceRatio` | 0.25 | 0~1 | 초기 거리 기준 |
| 앵커 가속도 | `pullAcceleration` | 35 | 0~150 | m/s² |
| 가속 중단 거리 | `pullStopDistance` | 1.5 | 0.1~5 | m |
| 로프 폭 | `ropeWidth` | 0.03 | 0.005~0.2 | m |

- `minDistanceRatio`는 `maxDistanceRatio`보다 클 수 없다.
- Inspector `Tooltip`은 위 표의 플레이테스트 튜닝값에만 작성한다.
- 앵커 발사 속도, Joint 탄성, 감쇠, 거리 비율, 가속도는 특히 우선적으로
  튜닝한다.

## 예외 조건

- 카메라가 앵커 발사 시작점보다 벽에 가까이 붙어 있으면 시작 지점에서 즉시
  충돌 검사를 수행해 벽 뒤로 앵커가 통과하지 않게 한다.
- 플레이어 자신의 Collider는 앵커 충돌과 Ground Layer에서 제외한다.
- `Grapple`을 빠르게 눌렀다 놓아도 앵커와 Joint가 남지 않아야 한다.
- 연결 중 다시 `Grapple` performed 이벤트가 들어와도 두 번째 앵커나 Joint를
  만들지 않는다.
- 앵커 발사 중 연결 대상 Collider가 비활성화되면 발사를 취소한다.
- 연결 후 고정 지점의 Collider가 사라져도 버튼을 놓기 전까지 현재 월드 위치에
  연결을 유지한다.
- `ropeOrigin`, 카메라, Rigidbody, LineRenderer, 입력 참조가 없으면 기능을
  비활성화하고 누락된 참조를 명확히 보고한다.
- 게임 창이 포커스를 잃으면 입력 상태를 초기화하고 로프를 안전하게 해제한다.

# 다른 시스템과의 연동

## Track 1 - 플레이어 이동

- Track 1과 동일한 플레이어 Rigidbody를 사용한다.
- 로프 연결 중에도 WASD 공중 이동을 허용한다.
- Space가 앵커 가속에 사용되는 동안 `PlayerMovement`의 일반 점프 요청은
  차단해야 한다.
- 로프 해제 후 Rigidbody 속도를 그대로 Track 1 이동에 넘긴다.

## Track 3/4 - 로켓과 반동

- 마우스 왼쪽 버튼은 향후 로켓 발사용으로 남겨둔다.
- 로켓 반동은 로프 연결 중에도 같은 Rigidbody에 누적될 수 있어야 한다.
- 로켓 반동으로 얻은 속도를 로프 시스템이 일괄 초기화하지 않는다.

# 구현 요구사항

- Unity 6, Input System, 3D Physics를 사용한다.
- 플레이어 Rigidbody에 런타임으로 `SpringJoint`를 하나만 생성하고 해제 시
  제거한다.
- 앵커 비행과 로프 상태를 하나의 명확한 상태로 관리한다.
- 앵커 충돌은 고속 이동에서도 누락되지 않는 연속 검사를 사용한다.
- 로프 시각 표현은 물리 계산과 분리한다.
- `Tests/`, `.asmdef`, `.asmref`는 생성하거나 사용하지 않는다.
- 클래스와 메서드에는 한국어 XML 문서 주석을 작성한다.
- 일반 멤버 변수에는 XML 문서 주석을 작성하지 않는다.
- 플레이테스트 중 반복 조정할 Inspector 수치에만 한국어 `Tooltip`을 작성한다.
- Scene, Prefab, Layer, Input Action, Material 연결은 사용자가 수행한다.
- 개발자는 구현 보고서에 필요한 계층과 Inspector 연결 절차를 작성한다.

권장 Player 계층은 다음과 같다.

```text
Player
├── Rigidbody
├── Capsule Collider
├── Player Movement
├── Rope Controller
├── Rope Line Renderer
├── Ground Check
├── Left Hand (Rope Origin)
└── Camera Pivot
    └── Main Camera
```

# 검증 방법

1. 정면의 벽을 조준하고 마우스 오른쪽 버튼을 눌러 앵커 비행을 확인한다.
2. 앵커가 날아가는 동안 왼손에서 앵커까지 로프가 표시되는지 확인한다.
3. 유효한 벽, 바닥, 천장에 앵커가 고정되는지 확인한다.
4. 허공으로 발사했을 때 최대 거리에서 앵커와 로프가 제거되는지 확인한다.
5. 로프 연결 후 이동 속도가 초기화되지 않고 스윙으로 이어지는지 확인한다.
6. 스윙 중 WASD로 방향을 조절할 수 있는지 확인한다.
7. 연결 중 Space를 누르고 있으면 앵커 방향으로 계속 가속하는지 확인한다.
8. 앵커 가까이 도착하면 추가 가속이 중단되는지 확인한다.
9. 연결 중 Space를 눌러도 일반 지상 점프가 함께 실행되지 않는지 확인한다.
10. 마우스 오른쪽 버튼을 놓으면 즉시 해제되고 기존 속도가 유지되는지 확인한다.
11. 발사 직후 버튼을 놓거나 반복 입력해도 앵커와 Joint가 중복되지 않는지 확인한다.
12. Inspector 튜닝값을 변경해 다음 Play에서 스윙 감각에 반영되는지 확인한다.

# 완료 조건

- 화면 중앙의 카메라 방향으로 앵커가 발사된다.
- 앵커가 지정 Layer의 벽, 바닥, 천장에 고정된다.
- 앵커가 최대 거리에 도달하거나 입력을 놓으면 정상적으로 제거된다.
- 발사와 연결 중 왼손에서 앵커까지 LineRenderer가 표시된다.
- 연결 시 SpringJoint를 통해 물리 기반 스윙이 가능하다.
- 연결 직전과 해제 순간의 Rigidbody 모멘텀이 보존된다.
- 연결 중에도 WASD 공중 제어가 가능하다.
- 연결 중 Space를 누르면 일반 점프 대신 앵커 방향으로 가속한다.
- 플레이어가 앵커 근처까지 접근할 수 있고 근거리에서 과도한 가속이 발생하지 않는다.
- 한 번에 앵커와 SpringJoint가 하나만 존재한다.
- 모든 입력이 Input System Action을 통해 처리된다.
- 사용자가 필요한 Unity 연결 작업을 구현 보고서만 보고 재현할 수 있다.

# 미결정 사항

- 표에 정의된 물리 수치는 플레이테스트 전 임시 기본값이다.
- SpringJoint의 최종 탄성, 감쇠와 거리 비율은 스윙 속도와 조작감을 확인한 뒤
  확정한다.
- 로프 연결 중 Track 1의 `airAcceleration`을 그대로 사용할지 별도 배율로
  낮출지는 첫 플레이테스트 후 결정한다.
- 움직이는 플랫폼, 적, 동적 Rigidbody에 연결하는 기능은 Sprint 1 이후에
  검토한다.
