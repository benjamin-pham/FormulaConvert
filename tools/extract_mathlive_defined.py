"""Dev tool (not part of the build): extract defined LaTeX commands and environments from MathLive bundle.

Usage: python3 -I tools/extract_mathlive_defined.py <mathlive.mjs> <out_file>

Inputs are untrusted downloads and are only read as text; nothing is executed.
"""
import re
import sys


def extract_mathlive_defined(mjs_path):
    with open(mjs_path, encoding="utf-8", errors="replace") as f:
        src = f.read()

    # 1. Remove INLINE_SHORTCUTS block
    m = re.search(r"var INLINE_SHORTCUTS\s*=\s*\{", src)
    if m:
        start = m.start()
        stack = 0
        end = len(src)
        for i in range(m.end() - 1, len(src)):
            if src[i] == "{":
                stack += 1
            elif src[i] == "}":
                stack -= 1
                if stack == 0:
                    end = i + 1
                    break
        src = src[:start] + src[end:]

    # 2. Remove mhchemParser block
    m = re.search(r"var mhchemParser\s*=\s*\{", src)
    if m:
        start = m.start()
        stack = 0
        end = len(src)
        for i in range(m.end() - 1, len(src)):
            if src[i] == "{":
                stack += 1
            elif src[i] == "}":
                stack -= 1
                if stack == 0:
                    end = i + 1
                    break
        src = src[:start] + src[end:]

    commands = set()
    environments = set()

    # 3. Environments in defineEnvironment / defineTabularEnvironment / defineRootEnvironment
    for m in re.finditer(
        r"(?:defineEnvironment|defineTabularEnvironment|defineRootEnvironment)\s*\(\s*(\[[^\]]+\]|['\"][^'\"]+['\"])",
        src,
    ):
        arg = re.sub(r"//[^\n]*", "", m.group(1).strip())
        names = re.findall(r"['\"]([^'\"]+)['\"]", arg)
        for name in names:
            environments.add(f"env:{name}")

    # 4. defineFunction(...)
    for m in re.finditer(
        r"(?<!function )defineFunction\s*\(\s*(\[[^\]]+\]|['\"][^'\"]+['\"]|Object\.keys\([^)]+\))",
        src,
    ):
        raw_arg = m.group(1).strip()
        arg = re.sub(r"//[^\n]*", "", raw_arg)
        if arg.startswith("Object.keys"):
            table_name = re.search(r"Object\.keys\(([^)]+)\)", arg).group(1).strip()
            tm = re.search(r"(?:var\s+)?\b" + re.escape(table_name) + r"\s*=\s*\{([^}]+)\}", src)
            if tm:
                body = re.sub(r"//[^\n]*", "", tm.group(1))
                keys = re.findall(r"(?:['\"]([^'\"]+)['\"]|([a-zA-Z0-9_]+))\s*:", body)
                for k1, k2 in keys:
                    k = k1 or k2
                    if k:
                        cmd = k if k.startswith("\\") else "\\" + k
                        commands.add(cmd)
        else:
            names = re.findall(r"['\"]([^'\"]+)['\"]", arg)
            for name in names:
                cmd = name if name.startswith("\\") else "\\" + name
                commands.add(cmd)

    # 5. String literals in arrays: ["\\x", ...]
    for m in re.finditer(r"\[([^\]]+)\]", src):
        content = re.sub(r"//[^\n]*", "", m.group(1))
        for s_match in re.finditer(r"['\"](\\\\(?:[A-Za-z]+|[^A-Za-z0-9\s]| ))['\"]", content):
            raw = s_match.group(1).replace("\\\\", "\\")
            commands.add(raw)

    # 6. defineSymbol(s)
    for m in re.finditer(
        r"(?<!function )defineSymbol\s*\(\s*['\"](\\\\(?:[A-Za-z]+|[^A-Za-z0-9\s]| ))['\"]\s*,",
        src,
    ):
        raw = m.group(1).replace("\\\\", "\\")
        commands.add(raw)

    # 6b. Parser syntax commands in this.match(...)
    for m in re.finditer(
        r"this\.match\(\s*['\"](\\\\(?:[A-Za-z]+|[^A-Za-z0-9\s]| ))['\"]\s*\)",
        src,
    ):
        raw = m.group(1).replace("\\\\", "\\")
        commands.add(raw)

    # 7. DEFAULT_MACROS and sub-macro tables
    macro_tables = ["DEFAULT_MACROS", "TEXVC_MACROS", "AMSMATH_MACROS", "BRAKET_MACROS"]
    excluded_keys = {
        "def",
        "args",
        "expand",
        "captureSelection",
        "primitive",
        "package",
        "mathtools",
        "texvc.sty",
        "amsmath.sty",
        "braket.sty",
    }

    for tbl in macro_tables:
        tm = re.search(r"(?:var\s+)?\b" + re.escape(tbl) + r"\s*=\s*\{", src)
        if tm:
            start = tm.start()
            stack = 0
            end = len(src)
            for i in range(tm.end() - 1, len(src)):
                if src[i] == "{":
                    stack += 1
                elif src[i] == "}":
                    stack -= 1
                    if stack == 0:
                        end = i + 1
                        break
            block = src[start:end]
            block_no_comments = re.sub(r"//[^\n]*", "", block)
            keys = re.findall(r"(?:['\"]([^'\"]+)['\"]|([a-zA-Z0-9_]+))\s*:", block_no_comments)
            for k1, k2 in keys:
                k = k1 or k2
                if k and k not in excluded_keys and not k.startswith("http"):
                    cmd = k if k.startswith("\\") else "\\" + k
                    commands.add(cmd)

    # 8. ACCENTS table
    am = re.search(r"(?:var\s+)?\bACCENTS\s*=\s*\{([^}]+)\}", src)
    if am:
        block = re.sub(r"//[^\n]*", "", am.group(1))
        keys = re.findall(r"(?:['\"]([^'\"]+)['\"]|([a-zA-Z0-9_]+))\s*:", block)
        for k1, k2 in keys:
            k = k1 or k2
            if k and k not in excluded_keys:
                cmd = k if k.startswith("\\") else "\\" + k
                commands.add(cmd)

    # 9. TEXT_SYMBOLS table
    ts = re.search(r"(?:var\s+)?\bTEXT_SYMBOLS\s*=\s*\{", src)
    if ts:
        start = ts.start()
        stack = 0
        end = len(src)
        for i in range(ts.end() - 1, len(src)):
            if src[i] == "{":
                stack += 1
            elif src[i] == "}":
                stack -= 1
                if stack == 0:
                    end = i + 1
                    break
        block = src[start:end]
        block_no_comments = re.sub(r"//[^\n]*", "", block)
        keys = re.findall(r"['\"](\\\\(?:[A-Za-z]+|[^A-Za-z0-9\s]| ))['\"]\s*:", block_no_comments)
        for k in keys:
            cmd = k.replace("\\\\", "\\")
            commands.add(cmd)

    return sorted(commands), sorted(environments)


def main(mjs_path, out_file):
    commands, environments = extract_mathlive_defined(mjs_path)
    all_lines = sorted(commands + environments)
    with open(out_file, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(all_lines) + "\n")
    print(f"Extracted {len(commands)} commands and {len(environments)} environments to {out_file}")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    main(sys.argv[1], sys.argv[2])
