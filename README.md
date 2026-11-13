# Aion2Heal – AION 2 Healer Overlay

<p>
  <img src="./assets/Aion2Heal.png" alt="Aion2Heal" width="128">
</p>

<p>
  <a href="https://github.com/rassau/AION2HealerOverlay/releases">
    <img alt="Release" src="https://img.shields.io/github/v/release/rassau/AION2HealerOverlay?display_name=release">
  </a>
  <a href="./LICENSE.txt">
    <img alt="License: GPL-3.0" src="https://img.shields.io/badge/License-GPLv3-blue.svg">
  </a>
</p>

A real-time combat meter overlay for **AION 2**, with extra tools for healers: a group frame with fast HP bars, shields,
buffs, debuffs, distances and a boss-target alert; healing, damage-taken and aggro columns in the meter; fight graphs
that show every boss pull second by second; and digital twins of players from the game's official website.

It is a modified version of [**Aion2Flow**](https://github.com/cloris-chan/Aion2Flow) by cloris-chan and, like it,
licensed under the [GNU GPL v3](./LICENSE.txt). The meter itself and everything listed under "From Aion2Flow" below is
cloris-chan's work; this fork adds the healer features. It is not affiliated with or endorsed by the original project.

## What this fork adds

The pictures below use made-up character names.

### Group frame

<img src="./assets/screenshots/group-frame.png" alt="The group frame: four party members with HP bars, a shield, buffs and a debuff, the boss's target framed in red, distances and direction arrows" width="377">

Turned on under Settings → **Group frame**:

- You and your party with HP bars that update faster than the game's party window, shields, debuffs, and the buffs and
  HoTs you cast, each with the seconds it has left.
- A direction arrow and the distance to each member, coloured by adjustable range marks; whoever is out of range fades.
- A red frame around whoever the boss is targeting, a warning when it's you, and an optional alert sound: a built-in
  chime or your own clip, cut from any sound or video file.
- Deaths per boss fight, and a side (left, right or off) and an opacity for each part of a row.

### Meter

- Healing per second and total healing (with or without overhealing), damage taken, UHS (missing HP × seconds) and
  aggro columns, with column headings that sort the meter with a click and a tooltip on every value. The aggro column
  shows by itself while you play a Gladiator or a Templar.
- Every boss pull is its own fight: boss DPS, aggro (how long the boss targeted each player), a stagger gauge and an
  optional DPS check against a time limit.
- Exporting, importing and renaming history entries, opacity sliders, and an eye that folds the meter away to its title
  and bottom bar.

### Fight graphs

<img src="./assets/screenshots/fight-graphs.png" alt="Fight graphs of a boss kill: an overview of your own metrics, then healing, damage taken and missing HP per second for you and the party" width="811">

Turned on under Settings → **Fight graphs**:

- Every boss pull second by second: damage on the boss, healing, damage taken, missing HP (UHS), aggro and stagger, each
  with you as an area, the party as a line and your ping; a curve for each party member if you like, and deaths marked.
  An overview shows your own metrics together, and the curves can be smoothed.
- Personal bests for each boss and character: your best pull as a dotted line in every graph, and a banner when a pull
  beats it.
- **Best runs** and **Stored runs** tabs, and a comparison of two to five stored runs, metric by metric.

### Digital twins

<img src="./assets/screenshots/twin-library.png" alt="The digital twin library: snapshots of looked-up characters with class, gear score and combat power" width="722">

<img src="./assets/screenshots/twin-comparison.png" alt="Three snapshots compared, with the differences to the reference in green and red" width="540">

The **digital twin library** opens with the people button in the meter's title bar:

- A player's character as the game's official website shows it: gear, Daevanion boards, skill levels and stats, in the
  details' **Digital twin** tab. Look a player up with a click, or let the overlay look up your party members as they
  show up (Settings → Digital twins, off at first).
- Every lookup is kept as a snapshot: mark favourites, give snapshots notes, or compare two to five side by side, with
  the differences to one of them. A lookup under way shows in the list with a progress bar.
- The group frame's buff tooltips give the numbers for the caster's skill level from their twin.

### Frames and navigation

- The meter's title bar turns the group frame, the skill monitor, the fight graphs and the Hidden Cubes on or off, and
  opens the digital twin library.
- Every frame has an eye that minimizes it to its title line and a cross that turns it off; a shortcut minimizes all of
  them at once (Settings → **Hotkeys**).
- An experimental **Hidden Cube** finder in a frame of its own: the cubes the server announces, with the distance and
  a direction arrow, and a sound for a new one if you like.

### Game data and languages

- Skill, NPC, boss and map data from the Steam client, so more bosses count as bosses, those of the Abyssal Forge: Ludra
  raid included.
- A German interface, with the game's German names.

Every change is listed in the [changelog](./CHANGELOG.md).

## From Aion2Flow

- Live DPS and total-damage ranking with animated contribution bars.
- Configurable player scope: self, party, force, or everyone in the encounter.
- Standard and boss-only scene modes, with boss HP and damage-share displays when the required data is available.
- Combatant details with outgoing/incoming views, target/source filters, and per-skill damage, healing, shield, and
  shield-absorption breakdowns.
- In-session history of recent encounters, with archived result browsing and experimental timeline playback.
- Capture status and game round-trip latency shown directly on the overlay.
- Interactive, click-through, and hidden overlay modes, plus configurable global shortcuts for display mode and battle
  reset.
- A skill monitor for your buffs and cooldowns.
- English, Korean and Traditional Chinese interface (the Chinese one shows the game's English names: the Steam client's
  data has no Chinese ones).

## Requirements

- Windows x64.
- The current AION 2 client.
- Administrator approval when the overlay starts. This is required for its bundled WinDivert capture driver.

Releases need no separate .NET installation. Npcap and WinPcap are not required.

## Download

[**Download the latest portable ZIP**](https://github.com/rassau/AION2HealerOverlay/releases/latest/download/Aion2Heal-stable-Portable.zip)

Other release files and the version history are on [GitHub Releases](https://github.com/rassau/AION2HealerOverlay/releases).

## Getting Started

1. Download the portable ZIP and unpack it into a folder of its own.
2. Launch `Aion2Heal.exe` and accept the Windows administrator prompt. It starts the newest installed version, which is
   kept in the `current` folder.
3. Start AION 2. The overlay waits for `Aion2.exe`, detects the active game connection, and begins updating when combat
   data arrives.
4. If no data appears, check the three status indicators at the bottom of the overlay: capture driver, game port, and
   battle connection.

## Updates

In Settings → **System**:

- **Update now** checks this project's GitHub Releases, downloads a newer version and restarts into it.
- **Update automatically** (off by default): when on, the overlay checks and downloads at every start and installs the
  update when you close it.

Your settings and alert sounds stay as they are.

## Using the Overlay

- Drag the title area to move the overlay; the eye on its right folds the meter away to its title and bottom bar.
- Use the reset button or your configured global shortcut to end the current encounter and start a fresh one.
- Select a combatant to open detailed outgoing/incoming statistics and skill breakdowns.
- Open **Settings** (the meter's gear) for a window with a tab each for general settings, the hotkeys, every frame's
  scale and opacity, sounds, the meter, the group frame, the skill monitor, the fight graphs, the Hidden Cubes, the
  digital twin library and the system (packet recording, updates). A frame's own gear opens its tab.
- Use the pin control or display-mode shortcut to cycle between interactive, click-through, and hidden modes.
- Open **History** to inspect, export or import an encounter. Its play button opens the experimental timeline playback.

Encounter history is kept in memory while the overlay runs; export an entry to keep it (a `.aion2heal` file; exports
from before the rename, `.aion2flow`, import too).

## Safety and Data

The overlay analyzes game network traffic locally.

- It does not modify game files.
- It does not inject into the game process.
- It does not read game memory.
- It connects to this project's GitHub Releases only to look for updates: with **Update now**, or at start with
  **Update automatically** on.
- It connects to the game's official character website (aion2.plaync.com) only for digital twins: when you look a
  player up, or for your party members with **Settings → Digital twins** on.

## Build from Source

Building, testing, or publishing from source requires the .NET 10 SDK; publishing (a Native AOT Windows x64 build) also
needs the Visual Studio C++ build tools.

```powershell
dotnet build Aion2Flow.slnx -c Release
dotnet test --solution Aion2Flow.slnx -c Release --max-parallel-test-modules 1
dotnet publish src/Aion2Flow/Aion2Flow.csproj -c Release
```

The default publish output is:

```text
src/Aion2Flow/bin/Release/net10.0-windows/win-x64/publish/
```

## Current Limitations

- Playback and encounter history are available only for encounters captured during the current session, or imported
  from an export.
- If the overlay starts after the game, it may miss initial data and temporarily be unable to show some names, icons,
  boss HP values, or scene names. Return to character selection and re-enter the game, or teleport once, to refresh that
  data.
- A game update can change combat data and temporarily affect statistics. Install the latest release after major client
  updates.

This project is not affiliated with, endorsed by, or sponsored by NCSOFT or any AION 2 publisher.

## Thanks

Aion2Flow by cloris-chan is the foundation of this overlay. If you'd like to support its author, see the
[Aion2Flow page](https://github.com/cloris-chan/Aion2Flow).

WinDivert by Basil captures the game traffic; see [third-party notices](./THIRD-PARTY-NOTICES.md).
