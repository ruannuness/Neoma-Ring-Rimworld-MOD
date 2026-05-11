from pathlib import Path

f = Path(r'C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\FormgelCore\Source\1.6\Comp\CompFormgelSpawner.cs')
text = f.read_text(encoding='utf-8')

lines = text.split('\n')
print(f"Total lines: {len(lines)}")

open_count = text.count('{')
close_count = text.count('}')
print(f"Open braces: {open_count}")
print(f"Close braces: {close_count}")

# Find the last 20 lines
for i in range(max(0, len(lines)-20), len(lines)):
    print(f"{i+1:3d}: {lines[i]}")
