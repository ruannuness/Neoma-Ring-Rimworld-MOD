from pathlib import Path

p = Path(r'C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\FormgelCore\Source\1.6\Comp\CompFormgelSpawner.cs')
text = p.read_text(encoding='utf-8')

# Find and replace the broken ReceiveCompSignal and CompTick
broken = '''public override void ReceiveCompSignal(string signal)
		{
			if (signal == "PowerTurnedOff" || signal == "FlickedOff")
			{
			if (Consciousness != null)
			{
				DespawnFormgel(false);
			}
		}
		else if (signal == "PowerTurnedOn" || signal == "FlickedOn")
		{
			// Power restored; formgel can be respawned manually.
		{
			base.CompTick();
			if (Consciousness != null && !Consciousness.Spawned)
			{
				// Off-map ticking is skipped because Tick() is non-public in this RimWorld version.
			}
		}
	}'''

fixed = '''public override void ReceiveCompSignal(string signal)
	{
		if (signal == "PowerTurnedOff" || signal == "FlickedOff")
		{
			if (Consciousness != null)
			{
				DespawnFormgel(false);
			}
		}
		else if (signal == "PowerTurnedOn" || signal == "FlickedOn")
		{
			// Power restored; formgel can be respawned manually.
		}
	}

	public override void CompTick()
	{
		base.CompTick();
		if (Consciousness != null && Consciousness.Spawned && !HasPower)
		{
			DespawnFormgel(false);
		}
		if (Consciousness != null && !Consciousness.Spawned)
		{
			// Off-map ticking is skipped because Tick() is non-public in this RimWorld version.
		}
	}'''

if broken in text:
    print("Found broken section, applying fix...")
    text = text.replace(broken, fixed)
    p.write_text(text, encoding='utf-8')
    print("Fixed!")
else:
    print("Broken section not found. Searching for variations...")
    i = text.find('public override void ReceiveCompSignal')
    if i >= 0:
        print(repr(text[i:i+500]))
