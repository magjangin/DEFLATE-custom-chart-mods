#!/usr/bin/env python3
"""
훅 대상 시그니처 대조 (AGENTS.md §6 3단계).

게임 업데이트 후 SignatureDumper로 Decompiled/를 다시 만든 뒤 실행하면, 게임을 켜기 전에
"어떤 훅이 깨질지"를 알려줍니다. Hooks/*.cs의 [HarmonyPatch(typeof(T), nameof(T.M) | "M")]마다:

  MISSING     대상 메서드가 덤프에 없음(삭제·개명) → SafePatcher/HarmonyGuard가 건너뛰거나 실패 처리
  OVERLOADED  같은 이름 메서드가 여럿 → Harmony 모호성 예외. [HarmonyPatch]에 인자 타입 배열을 지정해야 함
  PARAM       Prefix/Postfix가 '이름'으로 받는 파라미터가 대상 시그니처에 없음 → 패치 단계 예외
              (object[] __args로 받는 훅은 이름에 의존하지 않으므로 검사하지 않음, AGENTS.md §3-⑤ ⑧)
  SKIP        대상 타입이 덤프에 없음(Unity 엔진 타입 등) — 정보용

사용:  python tools/check_hook_targets.py            (저장소 루트에서, Decompiled/ 필요)
       python tools/check_hook_targets.py --verbose  (정상 훅의 대상 시그니처도 출력)
종료 코드: 문제(MISSING/OVERLOADED/PARAM)가 있으면 1.
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
HOOKS_DIR = os.path.join(ROOT, "DEFLATE custom chart", "Hooks")
DUMP_DIR = os.path.join(ROOT, "Decompiled")

PATCH_RE = re.compile(r'\[HarmonyPatch\(typeof\((\w+)\),\s*(?:nameof\(\w+\.(\w+)\)|"(\w+)")\)\]')
HOOK_METHOD_RE = re.compile(r'public\s+static\s+\w+\s+(Prefix|Postfix|Finalizer)\s*\(([^)]*)\)')
# Harmony가 이름과 무관하게 채우는 특수 파라미터
SPECIAL_PARAM = re.compile(r'^(__\w+|___\w+)$')


def index_dump():
    """타입 이름(중첩이면 바깥+안쪽 모두) → 덤프 파일 경로 목록."""
    index = {}
    for dirpath, _, files in os.walk(DUMP_DIR):
        for f in files:
            if not f.endswith(".cs"):
                continue
            stem = f[:-3]
            for name in {stem, stem.split("+")[-1]}:
                index.setdefault(name, []).append(os.path.join(dirpath, f))
    return index


def method_decls(path, method):
    text = open(path, encoding="utf-8", errors="replace").read()
    pat = re.compile(r'^\s*(?:public|private|protected|internal)[^;{=]*?\s' + re.escape(method) + r'\s*\(([^)]*)\)', re.M)
    return [m.group(0).strip() for m in pat.finditer(text)], [m.group(1) for m in pat.finditer(text)]


def param_names(param_list):
    names = []
    for p in param_list.split(","):
        p = p.split("=")[0].strip()
        if not p:
            continue
        names.append(p.split()[-1])
    return names


def parse_hooks():
    """(파일, 대상 타입, 대상 메서드, [(Prefix|Postfix, [파라미터 이름])]) 목록."""
    hooks = []
    for f in sorted(os.listdir(HOOKS_DIR)):
        if not f.endswith(".cs"):
            continue
        text = open(os.path.join(HOOKS_DIR, f), encoding="utf-8").read()
        matches = list(PATCH_RE.finditer(text))
        for i, m in enumerate(matches):
            end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
            body = text[m.end():end]
            methods = [(hm.group(1), param_names(hm.group(2))) for hm in HOOK_METHOD_RE.finditer(body)]
            hooks.append((f, m.group(1), m.group(2) or m.group(3), methods))
    return hooks


def main():
    verbose = "--verbose" in sys.argv
    if not os.path.isdir(DUMP_DIR):
        print(f"Decompiled/ 폴더가 없습니다: {DUMP_DIR}\n먼저 `dotnet run --project SignatureDumper -c Release --framework net8.0`로 덤프하세요.")
        return 2

    dump = index_dump()
    problems = 0
    for f, typ, method, hook_methods in parse_hooks():
        where = f"{typ}.{method}  ({f})"
        files = dump.get(typ)
        if not files:
            print(f"SKIP       {where} — 덤프에 타입 없음")
            continue

        decls, params = [], []
        for path in files:
            d, p = method_decls(path, method)
            decls += d
            params += p

        if not decls:
            print(f"MISSING    {where}")
            problems += 1
            continue
        if len(decls) > 1:
            print(f"OVERLOADED {where} — {len(decls)}개")
            for d in decls:
                print(f"             {d}")
            problems += 1

        available = set()
        for p in params:
            available.update(param_names(p))
        for kind, names in hook_methods:
            if "__args" in names:
                continue  # 타입 바인딩(HookArgs)이라 이름 변경에 영향 없음
            for n in names:
                if SPECIAL_PARAM.match(n) or n in available:
                    continue
                print(f"PARAM      {where} — {kind}의 '{n}'이 대상 시그니처에 없음: {decls[0]}")
                problems += 1

        if verbose and len(decls) == 1:
            print(f"OK         {where}: {decls[0]}")

    print(f"\n문제 {problems}건" + (" — AGENTS.md §6 4단계(단일 지점 수정)로 대응하세요." if problems else " (모든 훅 대상이 덤프와 일치)"))
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
