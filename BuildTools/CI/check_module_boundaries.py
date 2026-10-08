#!/usr/bin/env python3
"""모듈 경계 검증: asmdef 의존 방향(DAG)이 허용 규칙을 지키는지 확인.

CI에서 실행해 위반 시 비정상 종료(빌드 실패)한다.
외부 어셈블리(Fusion.*, Unity.* 등 Game.* 가 아닌 것)는 검사하지 않는다.
"""
import json
import sys
from pathlib import Path

# 허용된 내부(Game.*) 의존. 키가 참조할 수 있는 모듈 집합.
ALLOWED = {
    "Game.Core": set(),
    "Game.Networking": {"Game.Core"},
    "Game.Combat": {"Game.Core", "Game.Networking"},
    "Game.BossContent": {"Game.Core", "Game.Networking", "Game.Combat"},
    "Game.World": {"Game.Core", "Game.Networking", "Game.Combat"},
    "Game.UI": {"Game.Core", "Game.Networking"},
    "Game.BuildDeploy.Editor": {"Game.Core"},
    "Game.UI.Editor": {"Game.Core", "Game.UI"},
    # Editor 어셈블리는 자기 모듈이 참조하는 것까지 참조해야 한다 — asmdef 참조는 전이되지 않아서
    # 베이스 타입(예: BossNetState -> Game.Networking.ServerAuthoritativeBehaviour)이 안 보이면
    # 컴파일이 깨진다. DAG 의도(BossContent -> Networking 허용)를 넓히는 건 아니다.
    "Game.BossContent.Editor": {"Game.Core", "Game.BossContent", "Game.Networking"},
    "Game.Combat.Editor": {"Game.Core", "Game.Combat"},
    "Game.Minions": {"Game.Core", "Game.Networking", "Game.Combat"},
    "Game.MinionsGO": {"Game.Core", "Game.Networking", "Game.Combat"},
    "Game.Dungeon": {"Game.Core", "Game.Networking", "Game.Combat"},
    "Game.Dungeon.Editor": {"Game.Core", "Game.Dungeon"},
    "Game.Onboarding": {"Game.Core", "Game.Networking", "Game.Combat"},
    "Game.DataBases": {"Game.Core"},
    "Game.DataBases.Editor": {"Game.Core", "Game.DataBases"},
    "Game.SDK.Stove": {"Game.Core"},
    "Game.Sound": {"Game.Core"},
}

ROOT = Path(__file__).resolve().parents[2]


def main() -> int:
    # 출력 인코딩 강제: cp949 등 비-UTF-8 콘솔에서 메시지의 '—'(U+2014) 같은
    # 문자 출력 시 UnicodeEncodeError로 검사가 죽는 것을 방지한다.
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except (AttributeError, ValueError):
            pass

    violations = []
    seen = set()
    for asmdef in ROOT.rglob("*.asmdef"):
        if "Library/" in asmdef.as_posix():
            continue  # Unity 패키지 캐시(Library/PackageCache 등)는 프로젝트 asmdef가 아님
        data = json.loads(asmdef.read_text(encoding="utf-8-sig"))
        name = data.get("name", "")
        if not name.startswith("Game."):
            continue
        seen.add(name)
        if name not in ALLOWED:
            violations.append(f"[unknown module] {name} ({asmdef.relative_to(ROOT)}) — ALLOWED 맵에 등록 필요")
            continue
        for ref in data.get("references", []):
            ref_name = ref.split(":")[-1] if ref.startswith("GUID:") else ref
            if not ref_name.startswith("Game."):
                continue  # 외부 어셈블리는 무시
            if ref_name not in ALLOWED[name]:
                violations.append(
                    f"[boundary] {name} -> {ref_name} 는 허용되지 않음 ({asmdef.relative_to(ROOT)})"
                )

    for required in ALLOWED:
        if required not in seen:
            print(f"warning: asmdef 누락 — {required}")

    if violations:
        print("모듈 경계 위반 발견:")
        for v in violations:
            print("  -", v)
        return 1
    print("모듈 경계 OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
