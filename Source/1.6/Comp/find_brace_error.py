from pathlib import Path

f = Path(r'C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\FormgelCore\Source\1.6\Comp\CompFormgelSpawner.cs')
text = f.read_text(encoding='utf-8')

# Count braces per section
stack = []
for i, ch in enumerate(text):
    if ch == '{':
        stack.append(i)
    elif ch == '}':
        if stack:
            stack.pop()
        else:
            # Extra close brace
            line_num = text[:i].count('\n') + 1
            print(f"Extra close brace at line {line_num}")
            # Show context
            lines = text.split('\n')
            for j in range(max(0, line_num-3), min(len(lines), line_num+2)):
                print(f"{j+1:3d}: {lines[j]}")
            break

if stack:
    print(f"Unclosed braces: {len(stack)}")
    for pos in stack[-3:]:
        line_num = text[:pos].count('\n') + 1
        print(f"Unclosed at line {line_num}")
