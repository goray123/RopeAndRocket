# Track 03 - 로켓런처 구현

- **상태:** DRAFT
- **작성일:** 2026-07-10
- **버전:** v0.1
- **Sprint:** Sprint 1 - Core Prototype
- **관련 상위 문서:** Prototype Design Document
- **선행 기능:** Track 01 - 플레이어 이동, Track 02 - 로프 액션
- **관련 Track:** Track 04 - 몬스터
- **담당자:** 김동호
- **우선순위:** High

---

# 목적

로켓런처는 플레이어의 **핵심 이동 기술이자 공격 수단**이다.

플레이어는 로켓을 발사하는 순간의 **발사 반동**과 로켓이 폭발할 때 발생하는 **폭발 반동**을 이용하여 공중에서 이동 경로를 자유롭게 변경할 수 있다.

게임의 핵심 루프인

> 스윙 → 로켓 발사 → 반동 이동 → 폭발 이동 → 몬스터 처치 → 성장

에서 로켓은 이동과 공격을 동시에 담당하는 핵심 시스템이다.

---

# 플레이어 경험

- 스윙 도중에도 자유롭게 로켓을 사용할 수 있다.
- 로켓을 발사하는 순간 추진력을 얻는다.
- 원하는 위치에서 폭발시켜 추가 이동을 만든다.
- 로프와 로켓을 조합하여 빠르게 맵을 이동한다.
- 공격 자체가 이동 수단이 되는 독특한 플레이를 경험한다.
- 숙련될수록 더 빠르고 화려한 이동이 가능하다.

---

# 범위

## 포함

- 로켓 발사
- 발사 반동
- 로켓 이동
- 최대 사거리 체크
- 충돌 판정
- 폭발 생성
- 폭발 반동
- 플레이어 이동
- Inspector를 통한 수치 조정

## 제외

- 데미지 시스템
- 적 넉백
- 폭발 파티클
- 폭발 사운드
- 무기 강화
- 특수 탄환
- 탄약 시스템

---

# 상세 규칙

## 입력

| Action | Type | Binding | 동작 |
|---|---|---|---|
| Fire Rocket | Button | **E** | 로켓 발사 |

※ 좌클릭은 로프 액션에서 사용한다.

---

# 상태(State)

```
Idle
 │
 │ E
 ▼
Fire
 │
 │ 로켓 생성
 ▼
Cooldown
 │
 │ Timer 종료
 ▼
Idle
```

### Idle

- 발사 가능
- 입력 대기

### Fire

- 로켓 생성
- 발사 반동 적용
- 쿨타임 시작

### Cooldown

- 발사 입력 무시

---

# 동작 규칙

## 핵심 기능 1 : 로켓 발사

E 입력 시

- FirePoint에서 로켓을 생성한다.
- 카메라가 바라보는 방향으로 발사한다.
- 플레이어에게 발사 반동을 적용한다.
- 쿨타임을 시작한다.
- 로켓 런처 탄환 오브젝트가 사라진다.
  재장전 후 탄환 오브젝트가 다시 나타난다.

### 발사 반동

발사 순간 플레이어는

**발사 방향의 반대 방향**으로 Impulse를 받는다.

```
        Rocket
---------->

<----------
 Player
```

Rigidbody.AddForce(Impulse)를 사용한다.

---

## 핵심 기능 2 : 로켓 이동

로켓은

- 일정 속도로 직진한다.
- 중력의 영향을 받지 않는다.
- 이동 방향을 바라보도록 회전한다.

폭발 조건

- 적과 충돌
- 지형과 충돌
- 최대 사거리 도달

중 하나를 만족하면 즉시 폭발한다.

---

## 핵심 기능 3 : 폭발

이번 Sprint에서는

- 데미지 없음
- 파티클 없음
- 사운드 없음

폭발 시

Explosion Prefab(구체)을 생성하여

폭발 범위를 확인한다.

---

## 핵심 기능 4 : 폭발 반동

폭발 시

폭발 범위 안의 Rigidbody에게

폭발 힘을 적용한다.

플레이어 역시 영향을 받는다.

```
        ↑
     ↖     ↗

← Player   →

     BOOM
```

이를 이용하여

공중에서 추가 이동이 가능하다.

---

## 핵심 기능 5 : 이동 메커니즘

하나의 로켓은

플레이어를 총 **두 번 이동**시킨다.

### ① 발사 반동

```
E 입력

Rocket →

Player ←
```

### ② 폭발 반동

```
      Boom

Player ←→
```

플레이어는

발사 반동과 폭발 반동을 조합하여

원하는 방향으로 이동한다.

---

# 수치 및 조정값

| 항목 | 변수명 | 기본값 | 범위 | 비고 |
|---|---:|---:|---:|---|
| 로켓 속도 | rocketSpeed | 40 | 1~100 | |
| 최대 사거리 | maxDistance | 100 | 10~300 | |
| 발사 반동 | fireRecoilForce | 15 | 0~100 | |
| 폭발 힘(플레이어) | explosionForcePlayer | 25 | 0~100 | |
| 폭발 반경 | explosionRadius | 6 | 1~20 | |
| Upward Modifier | explosionUpwardModifier | 0 | 0~5 | Unity Explosion Force |
| 쿨타임 | cooldown | 0.8 | 0~10 | |

---

# 예외 조건

### 입력 충돌

쿨타임 중 E 입력은 무시한다.

---

### FirePoint 없음

FirePoint가 존재하지 않으면

에러 로그를 출력하고 발사하지 않는다.

---

### 목표 없음

카메라 방향으로 그대로 발사한다.

---

### 충돌하지 않는 경우

최대 사거리에 도달하면 자동 폭발한다.

---

### 플레이어가 폭발 범위 밖인 경우

폭발 반동은 적용되지 않는다.

---

# 다른 시스템과의 연동

## Track 01

- Rigidbody
- 플레이어 이동

---

## Track 02

- 로프 이동 중 발사 가능
- 스윙과 동시에 사용 가능

---

## Track 04

향후

폭발 데미지 적용

---

## UI

향후

- 쿨타임 UI

---

## Audio

Sprint 2 구현 예정

---

## VFX

Sprint 2 구현 예정

---

# 구현 요구사항

## 엔진 버전

Unity 6

---

## 필수 컴포넌트

### Player

- Rigidbody
- Camera
- RocketLauncher
- FirePoint

### Rocket

- Rigidbody
- Collider
- RocketProjectile

### Explosion

- Sphere Collider
- Explosion.cs

---

## Inspector 노출

- rocketPrefab
- explosionPrefab
- firePoint

- rocketSpeed
- maxDistance
- cooldown

- fireRecoilForce

- explosionForcePlayer
- explosionRadius
- explosionUpwardModifier

---

## 코드 규칙

### Player

- 입력 처리
- 발사 요청

### RocketLauncher

- 쿨타임 관리
- 로켓 생성
- 발사 반동 적용

### RocketProjectile

- 이동
- 충돌 판정
- 사거리 체크
- 폭발 호출

### Explosion

- 폭발 범위 생성
- Rigidbody 검색
- 폭발 힘 적용

---

## 성능 고려사항

Prototype 단계에서는 Instantiate를 사용한다.

정식 구현에서는

- Rocket Pool
- Explosion Pool

을 적용한다.

---

## 씬 구성

```
Player
 ├ Camera
 ├ FirePoint
 ├ Rigidbody
 └ RocketLauncher

RocketPrefab
 ├ Rigidbody
 ├ Collider
 └ RocketProjectile

ExplosionPrefab
 ├ SphereCollider
 └ Explosion
```

---

# 검증 방법

1. E를 누르면 로켓이 생성된다.
2. 플레이어가 즉시 반대 방향으로 밀려난다.
3. 로켓이 일정 속도로 이동한다.
4. 로켓이 충돌하면 폭발한다.
5. 사거리 끝에서도 폭발한다.
6. Explosion Prefab이 생성된다.
7. 플레이어가 폭발 범위 안에 있으면 밀려난다.
8. 발사 반동과 폭발 반동을 이용하여 공중 이동이 가능하다.
9. 로프를 타는 도중에도 정상 동작한다.
10. Inspector 수치 변경이 정상 반영된다.

---

# 완료 조건 (Definition of Done)

- [ ] E 입력으로 로켓 발사
- [ ] 발사 반동 구현
- [ ] 로켓 이동 구현
- [ ] 충돌 폭발 구현
- [ ] 사거리 폭발 구현
- [ ] Explosion Prefab 생성
- [ ] 폭발 반동 구현
- [ ] 로프와 함께 정상 동작
- [ ] Inspector 튜닝 가능
- [ ] 테스트 완료

---

# 설계 의도

로켓은 단순한 무기가 아니다.

플레이어의 이동 경로를 만들어주는 이동 기술이며,

스윙 액션과 조합하여

맵을 자유롭게 누비도록 설계한다.

공격보다

**이동의 재미를 우선하는 설계**를 목표로 한다.

---

# 성공 지표

- 로켓을 이동기로 사용하는 플레이가 자연스럽다.
- 스윙과 로켓을 연계하는 플레이가 자주 발생한다.
- 발사 반동과 폭발 반동을 모두 활용하게 된다.
- 이동 자체가 재미있다고 느껴진다.

---

# 기술적 제약

사용 API

- Rigidbody.AddForce
- Rigidbody.AddExplosionForce
- Physics.OverlapSphere

Prototype에서는

Instantiate 사용 가능

최종 버전에서는

Object Pool 적용 예정

---

# 리스크

- 반동이 너무 강하거나 약할 수 있다.
- 폭발 반동이 플레이어를 예상치 못한 방향으로 밀 수 있다.
- 스윙과 동시에 사용할 경우 힘이 과도하게 누적될 수 있다.

---

# TODO

Sprint 2

- 폭발 데미지
- 적 넉백
- 폭발 이펙트
- 폭발 사운드
- 화면 흔들림
- 조준 보조
- 로켓 업그레이드
- 다양한 탄종

---

# 변경 이력

| 버전 | 변경 내용 |
|---|---|
| v0.1 | 최초 작성 |

---

# 참고 자료

- Titanfall 2
- ULTRAKILL
- Quake
- Team Fortress 2 (Rocket Jump)
- Risk of Rain 2
```