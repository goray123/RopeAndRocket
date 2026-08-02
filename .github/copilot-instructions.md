# AI Rules

- Unity 6 + New Input System 사용
- PlayerInput (Send Messages) 사용
- 입력은 OnMove(), OnJump(), OnLook() 등의 콜백에서만 처리
- InputAction.performed/start/canceled, event += 방식 사용 금지
- SerializeField를 사용하고 public 필드는 최소화
- 하나의 스크립트는 하나의 역할만 담당
- 기존 구조를 최대한 유지하고, 요청 없는 리팩터링은 하지 않음
- 불필요한 Singleton, 인터페이스, 추상화, 디자인 패턴 사용 금지
- 설명보다 코드를 우선 작성
