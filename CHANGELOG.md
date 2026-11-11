# Aion2Heal – patch notes

Changes in this healer version of Aion2Flow, newest first. The build number is in brackets; build N is released as
version 1.0.N.
New meter columns start switched off: turn them on under **Settings → Main window metrics**.

## 10 Nov 2026 · builds 49–60

**Boss fights**
- After a kill the **Boss** column stays in the live view, with the killed boss's numbers, as the history shows it, until the next boss comes up or you reset. Before, it went away with the boss's HP bar 10 s after the kill, in the table and in the boss fight sections. A boss that is merely out of sight (not killed) still takes the column with it. [60]
- A boss hitting another boss-sized NPC no longer starts a fight for that NPC. It used to show up in the history as a failed attempt although nobody fought it; histories saved before keep that entry. [60]
- The **Boss** column (the share of the boss's HP each player took off) no longer adds up to more than 100 % when a boss gets HP back during the fight: the shares then split the damage the group did to it. [60]

**Group frame**
- Two more built-in boss alert sounds next to the chime: **Knock** (two wooden knocks, "tok-tok") and **Blip** (two soft rising "bloops"). They're listed under the chime in the **Boss alert** sound list; your own sounds follow, up to five as before. [60]
- Buffs/debuffs, the direction arrow and the distance each have their own setting: Left, Right or Off (a click cycles through them). Outwards from the HP bar the order is distance, arrow, buffs; on the left it's mirrored. With the buffs on the left the frame grows to the left, so the HP bars stay in place as buffs come and go. [49, 50]
- Distances no longer vanish 2 s after your last buff, HoT or shield: for up to 10 s they stay, faded, as an estimate (you may have moved since). [50]
- New **Opacity** submenu in the group frame settings (also behind the frame's gear), with a slider each for the HP bars (with shields, names and numbers), buffs/debuffs, direction arrows and distances. 100% is the look so far; lower values fade just that part. [51]
- New **Boss alert** submenu: a sound when the boss turns to you (with the red "Boss is targeting you" banner), off until you turn it on. Pick the built-in chime or a sound of your own: **Add from file…** opens any sound or video file (mp3, m4a, mp4, wav, …), shows its waveform, and you cut out up to 10 s between two edges (with zoom and arrow keys for fine cuts). Your last five sounds are listed, each with an export button to pass it on; whoever gets it adds it the same way. With a volume slider and **Play sound** to try it. [52]
- The boss alert's quiet time (no new alert within it after the last one) is a slider in the **Boss alert** submenu: 0 to 10 s, 5 s as before. At 0 s every switch to you sounds. [53]
- The boss alert's volume goes from 0 to 200 %: above 100 % a quiet sound of your own gets louder (an already loud one starts to distort). [54]
- Boss alert sounds can come from `.ogg` files too, such as Discord's soundboard sounds (Opus inside): Windows has the Opus decoder but can't open Ogg files, so the overlay repacks the sound into a container Windows reads. [59]

**Name**
- The program is now called **Aion2Heal** (`Aion2Heal.exe`). History exports are saved as `.aion2heal` and debug recordings as `.aion2healdebug`; older `.aion2flow` exports can still be imported. [56]
- Its own icon: a green healing cross on a dark teal tile, with a gold heartbeat line at the larger sizes. [57]
- The icon now has gold wings rising behind the green cross (from 48 px up; smaller sizes show the plain cross). The heartbeat version is kept as `assets/Aion2Heal-cross.ico`; `python assets/make_icon.py --style cross` switches back to it. [58]

**Updates**
- Two update settings at the bottom of the settings: **Update automatically** (Off by default; On checks and downloads at every start and installs when you close the overlay) and **Update now** (checks, downloads and restarts into the new version). Updates now come from the healer overlay's own release page, github.com/rassau/AION2HealerOverlay, never from the original Aion2Flow. They show up once the overlay is installed from a release package; a copied folder like today's can't update itself. [55]

## 9 Nov 2026 · builds 39–48

**Group frame**
- Distances follow members who run, glide or blink: their position now updates about every 100 ms instead of every 2 s, so the numbers no longer lag tens of metres behind or jump around. [48]
- The overlay only learns where *you* are when you cast an aura (buffs, HoTs, shields). Distances now hide 2 s after your last one (was 10 s) instead of showing numbers that are 20–50 m off while you move. [48]
- Distance to each member in metres, right of the arrow: green up to 25 m, orange up to 35 m, red up to 40 m, grey past that (out of range). All three marks can be changed in the Group frame settings. [42]
- Arrows, distance and out-of-range dimming now also work for members who are far away. Before, they often had no position. [43]
- Open world: the distance now shows for members who stand still. Members who leave your sight are dimmed, like in the game. [47]
- Hover an HP bar for the real numbers, e.g. "20.580 / 24.212 HP", and what's left of each shield. [44]
- A gear button under the frame opens the Group frame settings directly. [45]
- Death counter per boss fight next to each name (✝ 2), red while dead. It can be hidden ("Deaths per boss fight"). [46]
- The red "boss target" frame now follows the game's own boss frame: it moves when the boss switches target, about 2 s before the first attack, and clears when the boss drops its target. [39]

**Meter**
- Aggro % in boss fights now shows how long the boss was targeting each player. [40]
- Click the meter's title to fold it away. The title and footer stay, and the fold is remembered after a restart. [40, 41]
- With a boss time limit set, the footer shows whether the group's DPS is ahead or behind, e.g. "(+1.34k)" in green or "(−193)" in red, also while folded. [41]
- History: delete single entries, or use "Clear all". [40]
- Much less memory use in long sessions: fights that drop out of the history are no longer kept. [40]

## 8 Nov 2026 · build 38
- Group frame: a red frame around whoever the boss is attacking, and a pulsing "Boss is targeting you" warning when it's you. Only for bosses, never in PvP. [38]

## 6 Nov 2026 · builds 30–37
- A purple frame around the HP bar while that player has a debuff, even with debuff icons hidden. New setting "Buffs without duration" (Show/Hide). [30]
- Shields disappear exactly when they break or run out, and shrink as hits land (oldest shield first; damage over time ignores shields). [31]
- Direction arrow for each member (north up; green = above you, red = below, blue = same level), and the row dims past 40 m. Both hide when a position is older than 10 s. [32]
- Fewer arrow gaps: buffs you cast on yourself now also tell the overlay where you are. [35]
- New settings "Shield bar" (Left/Right) and "Shield animation": each shield is its own segment and blinks in its last 3 s. Any part of a shield beyond the HP is violet. [36]
- Shorter shield text: "(5,15) 20%". [37]
- Builds 33–34 briefly recorded outgoing game data in debug recordings. It was removed again because it's encrypted.

## 5 Nov 2026 · builds 25–29
- **New: Group frame** for healers (Settings → Group frame, off by default). It shows you and your party with HP bars that update faster than the game's party window, debuffs (red frame), and your own buffs and HoTs (gold) with seconds left. [25]
- Its own size slider, "Group frame scale". [26]
- Group frame submenu: Enable, Buffs cast by you, Debuffs, Buffs cast by others (blue frame). [27]
- Hover a buff or debuff icon to see its name. Shields show as a light-blue bar with "5% + 75%". Boss debuffs like Vakron's Gaze no longer show as your own buff. [28]
- Settings → Debugging: record all received game packets to a file, to analyse problems later. [29]

## 3 Nov 2026 · build 24
- "Detailed view opacity" slider for the details window. [24]

## 2 Nov 2026 · builds 17–23
- Detail view: a Stagger column per skill. Clicking a player in a boss fight section shows the details of just that fight. [17]
- Fixed: Benevolence's stamina no longer counts as 1.2M damage. Flat "self-heals" of tank skills (Rush Strike, Infiltrate, …) no longer count as healing. [18]
- Column headings above the table (DPS, Σ DMG, HPS, Σ Heal, Taken, UHS, Aggro, Stagger, Boss), and a tooltip on every value. "Column headings" and "Overhealing" moved directly into Settings. [19, 20]
- "Opacity" slider: 100% is fully solid, 20% barely visible. [21, 22]
- The Boss % column can be switched on and off in Main window metrics. [23]

## 1 Nov 2026 · builds 8–16
- Fixed: your own UHS started late after a zone change. [8]
- The overlay gets wider as you turn on more columns. [9]
- History: export, import and rename entries. [10]
- New Aggro column: each player's share of the boss's aimed attacks and spells. [11]
- Boss section under the boss bar: damage, DPS and aggro on the current boss. An optional "Boss time limit" shows the DPS needed, e.g. "416.6k/s required (316.6k/s missing)". [11]
- Bosses are recognised again after the game update broke it. They show an id instead of a name. [11]
- Every pull is its own boss fight. After a wipe it stays up as a "failed attempt" until the next pull, and earlier fights go into a folded "Boss fights" list that is also saved in the history. [12–14]
- Stagger: a gauge under the boss bar, a Stagger column showing who wore it down, and a stagger count per fight. [15]
- Fixed: nameless "players" that were really summons, and history exports cut off when the overlay was closed. [16]

## 31 Oct 2026 · builds 1–7
- **New:** HPS and total healing columns, which you can also sort by. [1]
- Summons like the Divine Aura turret count towards their owner's damage. [2]
- "Overhealing: Show/Hide": with Hide, only healing that actually restored missing HP counts. [2, 3]
- Healing skills in the detail view get share bars and follow the Overhealing setting. [4]
- New "Damage taken" column. [5]
- New UHS column ("unhealed health seconds": missing HP × seconds). [6]
- Party total UHS in the bottom bar. Your own UHS now works, and heals on summons no longer count as healing. [7]
