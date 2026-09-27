# Driveborn

**A roguelite whose dungeon is your own file system.**

Every dungeon crawler generates levels from a random seed. Driveborn does not use one.
It reads the folders you explicitly hand it and turns them into a dungeon: folders become
rooms, files become creatures, nesting depth becomes floor number, and the biggest file in
a tree becomes its boss.

The result is a world nobody else has, that you already half-remember, and that mutates
every time you save, download or delete something.

> **Driveborn never modifies anything.** It is read-only by construction — see
> [Safety](#safety) below.

```
╔══════════════════════════╗
║  .  .  ▓▓▓  .  .  $     ║   ▓  setup_nvidia.exe · 780 MB · Construct
║  .  .  ▓▓▓  *  *  .     ║      armour 6 · HOT (3d) · 3x3 · 420 hp
║  @  .  ▓▓▓  *  .  .     ║   *  document(1..6).pdf · Swarm
║  .  .  .   *  .  u      ║   u  invoice_2024.pdf · DORMANT (400d)
║  ══[ drivers ]══  ══[ old_2019 ]══                        ║
╚══════════════════════════╝
@  the Reader · integrity 84/100 · cycles 3/3
```

---

## How your disk becomes a dungeon

| Real thing | In game |
|---|---|
| Folder | A room. Grid size scales with how full the folder is |
| Subfolder | A labelled door, showing its real entry count |
| Nesting depth | Floor number |
| File size | Health **and physical bulk** — a 5 GB image is a 5x5 obstacle you fight around |
| `.exe` `.dll` `.sys` | Armoured constructs |
| `.iso` `.vmdk` | Immobile leviathans |
| `.pdf` `.txt` `.md` | Scribes — ranged, and they carry the file's real first line as lore |
| `.jpg` `.png` | Caches — loot |
| Read-only file | Sealed vault |
| Hidden file | Wraith |
| Modified > 3 years ago | **Dormant.** Never acts until you touch it. Sneak past, or strike it asleep for 3x damage |
| Modified < 24 hours ago | **Hot.** Acts twice per round |
| Largest file in the folder | Floor boss |
| Thousands of tiny files | Folded into one Swarm body |

Generation is deterministic: an entity's identity is hashed from
`path + size + last-write-time`, so the same file is always the same creature — until
the file changes, at which point the creature changes with it.

---

## The five combat levers

1. **Parsers.** Your weapons. Each is tuned to one file family. A *matched* parser pierces
   armour entirely and hits for 2.5x; a mismatched one is halved and eats full armour.
   You carry three. Walking into a photo archive with a binary loadout is a disaster — and
   because it is *your* drive, you can plan for it. This is the strategic core.
2. **Bulk is physical.** Size decides footprint, not just hit points. Big files block lines
   of movement.
3. **Read cost.** Striking a large entity costs more of your three Cycles per turn, so hulks
   are slow to chew rather than merely tanky.
4. **Temperament.** Age decides aggression. Your `Downloads` folder is genuinely dangerous;
   a 2013 archive is a slow, rich tomb.
5. **Extraction.** Loot banks **only** if you walk back out to the territory root and extract.
   Die and the haul is gone — experience and mapping are kept.

## The daily Rift

On launch, Driveborn diffs your territories against the last 24 hours. Any folder you
actually touched becomes a **Rift**: double loot, gone at midnight.

Your own work generates tomorrow's level. There is no content pipeline, no server, and
no live-ops — the operating system is the content generator.

---

## Safety

Driveborn points itself at a real person's drive, so the guarantees are structural rather
than promised:

- **The probe interface has no write, delete, move or create member.** No amount of game
  logic can modify your files, because there is no code path that could. A unit test
  asserts this via reflection.
- **Opt-in only.** Nothing is scanned until you add a folder as a Territory. There is no
  default.
- **Permanent deny-list**, which overrides any allow: `Windows`, `Program Files`,
  `ProgramData`, `AppData\Local\Microsoft`, `System Volume Information`, `$RECYCLE.BIN`,
  `.ssh`, `.gnupg`, `.aws`, `.azure`, `.kube`, `.docker`, password stores, keychains.
- **Secret files never become entities or lore:** `.env*`, `*.pem`, `*.key`, `*.pfx`,
  `*.p12`, `id_rsa*`, `id_ed25519*`, `credentials`, `.npmrc`, `.netrc`, `*.kdbx`.
- **Content reads are capped.** Only small text files, only the first meaningful line,
  only for flavour text. Opened read-only with full share so it can never lock a file you
  have open elsewhere.
- **No network code exists in the project.** Nothing leaves the machine.
- **The save file** stores paths and game state — never file contents.

Run `dotnet test` and look at `tests/Driveborn.Core.Tests/SafetyTests.cs`. If those go red,
the app should not ship.

---

## Build and run

Requires the .NET 8 SDK on Windows (WPF).

```bash
dotnet test
dotnet run --project src/Driveborn.App
```

On first launch, pick one or more folders as Territories. Somewhere with a real history
works best — a projects folder, a photo archive, an old backup drive.

### Controls

| Key | Action |
|---|---|
| `W A S D` / arrows | Move; step onto a door to descend |
| `Tab` | Cycle target |
| `1` `2` `3` | Strike the selected target with that parser |
| `E` | Open a cache |
| `Space` | End turn |
| `X` | Extract (only at the territory root) |

Hover anything for its real path, size and age. The game never lies about what a
monster actually is.

---

## Layout

```
src/
  Driveborn.Core/      scanning, generation, combat, runs, rifts — no UI, no I/O beyond the probe
    Scanning/          ScanPolicy (the safety gate), IFileSystemProbe (read-only surface)
    Generation/        deterministic seeding, file-to-entity, folder-to-room
    Combat/            damage maths
    Run/               the descent engine
    Rifts/             daily discovery
  Driveborn.Data/      JSON profile persistence, written atomically
  Driveborn.App/       WPF shell
tests/
  Driveborn.Core.Tests/
```

The core has no dependency on WPF, so the whole game is playable and testable headless.

## Status

Vertical slice. Everything described above is implemented and tested. Not yet built:
parser drops and upgrades, vault keys, archive entities splitting into real children,
the world-map fog-of-war view, and thumbnail rendering for raster relics.

## Licence

MIT. See [LICENSE](LICENSE).
