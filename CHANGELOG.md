# Aion2Heal – patch notes

Changes in this healer version of Aion2Flow, newest first. The build number is in brackets; build N is released as
version 1.0.N.
New meter columns start switched off: turn them on under **Settings → DPS meter → Main window metrics**.

## 13 Nov 2026 · build 65

**Fight graphs**
- **Compare**: the Stored runs tab has a tick box beside each run; tick two to five and click **Compare** to see them in the new Comparison tab: a section per metric with each run's graph one below the other (the first ticked on top), each labelled with the boss, when and how long. Folding a section folds that metric in the Fight graphs tab too, and the other way round. One legend on the right shows or hides You, the party, the ping and, with **Each party member**, every member by name across the runs, in every graph at once. [65]
- The overview's colours read as they mean: healing (HPS) green, damage taken red; missing HP (UHS) is violet, aggro aqua and stagger yellow, damage stays blue. Your deaths are white crosses in the overview now, as red is the damage taken there (in the graphs below they stay red). The set was checked for colour blindness on the frame's dark background. [65]
- For a cleric the overview's UHS is the whole group's missing HP now, as their runs are ranked by it: marked **UHS***, with the note in gold when hovering its legend entry, as in the runs tables. [65]
- **Smooth curves** (beside the frame's other options, and in Settings → Fight graphs): the curves follow an average of the seconds around each (about ±5 s, weighted towards the second itself), so single big hits and heals every few seconds no longer make them spiky; peaks stay where they were, as the average reaches as far back as ahead. Hovering still lists each second's own values. It costs next to nothing: the average is worked out once when a graph changes (at most once a second), and the curves have as many points as before. [65]
- **Equal time scale** (on top of the Comparison tab): a second is as wide in every graph, so the longest run fills the width and the shorter ones end early, and how long a damage spike lasts or how far healing trails damage taken compares directly. Off, each run fills the width. [65]

## 13 Nov 2026 · build 64

**Settings**
- The settings open in a window of their own, with tabs: **General** (language, topmost mode, time format, boss time limit, overhealing, clickable without focus, hotkeys), **Visuals** (every frame's scale and opacity), **Audio** (everything that makes a sound), **DPS meter**, **Group frame**, **Skill monitor**, **Fight graphs**, **Digital twin library** and **System** (packet recording, version, updates). The meter's gear opens it, the group frame's and the fight graphs' gears at their tab. It can be moved and opens where it was last. [64]
- A frame's tab has its switch at the top; while the frame is off, the rest of its settings are faded and can't be changed. The Visuals and Audio tabs fade a frame's rows the same way. The digital twin library's tab doesn't fade: the library opens with lookups off too. [64]
- New: an opacity for the fight graphs, the skill monitor and the digital twin library, and a scale for the library. The skill monitor's scale moved from its list of skills to the Visuals. [64]
- The boss alert (sound, volume, quiet time) is in the Audio tab as well as the group frame's; its volume is the cube finder's too. [64]

**Meter**
- A click on a column's head sorts the meter by that column, the highest first; a second click turns the order round, the lowest first. An arrow beside the head shows the column and the order (▾ highest first, ▴ lowest first), as in the fight graphs' runs tables. It sorts the boss fights' rows too, and it's the same setting as **Settings → DPS meter → Sort by**, which now lists every column, with a new **Sort order** row below it. [64]

**Who you are**
- An overlay started in the middle of a session knows who you are within seconds now. Until now only the game's own-character packet (0x3336) said so, and the game sends it only at login and on a map change, so an overlay started after it had no "you": the group frame left you out and the fight graphs had nothing of yours. A packet the game sends only about your own character (0x4a36, up to 50 a minute in fights) names you now; in 17 recordings it always matched. Until the next map change says otherwise you go by the character the overlay remembers from your last session. [64]

**Fight graphs**
- New **Fight graphs** frame (Settings → **Fight graphs** → On): a boss fight (pull) second by second, the value at 1:40 being what happened between 1:39 and 1:40. It shows the pulls of whatever the meter shows (live, or the history entry picked there), follows the newest pull, and the box at its top picks another one. Like the group frame it can be moved anywhere, has a gear that opens its settings, and lets clicks through while the game has focus. [64]
- A graph per meter column: damage per second on the boss, healing per second (only the HP it restored while overhealing is left out, like the meter), damage taken, unhealed health (missing HP), aggro (how much of each second the boss was after them) and stagger. Each shows you as a teal area, the party's total as a white line and your ping as a grey line on its own scale on the right (ms). With **Each party member** every member gets a line of their own, listed in the legend in its colour. Grave crosses mark when you died, and with **Other members' deaths** when the others did. Clicking an entry on the right hides or shows that line in every graph; clicking a graph's title folds it away. [64]
- Above them the **overview** shows your own metrics together, each scaled to its peak in the fight; the metrics on its right are picked by clicking. With none picked it hides itself, and **Show overview** brings it back. [64]
- Hovering a graph marks that second in all of them and lists the values then, with the skills your part came from. This works while the game has focus too. [64]
- The frame keeps its width, the time axis squeezing as the fight goes on, and its height follows what it shows: folding graphs away or hiding the overview makes it shorter. **Scale** in its settings sizes it (50–200 %, apart from the main UI scale, like the group frame) once the slider is let go. [64]
- **Personal bests**: each boss you kill is compared with your best pulls of it, per character. Only kills the overlay saw from the start count (the boss lost at least 90 % of its HP while it watched). More is better for damage, healing and stagger, less for damage taken and missing HP; for aggro more is better for Templars and Gladiators, less for everyone else. A pull is kept while it's your best in at least one of them, together with its history entry, in the `best-runs` folder next to the settings. [64]
- **Best pull by** in the frame's settings picks the metric for your best pull of the boss shown. In every graph it's drawn as a dotted teal line ("Your best pull" in the legend, hidden by a click like the others), in full even when it lasted longer than the pull shown. [64]
- A pull that beats your best in anything says so in a gold banner at the top of the frame when it ends, with the old value beside the new one. The first complete kill of a boss is kept as its best. [64]
- Your ping goes into the journal now, so history entries and the best pulls keep it. [64]
- The frame has tabs now: **Fight graphs**, **Best runs** and **Stored runs**. **Best runs** lists your best pulls of a dungeon's bosses (or of every dungeon), a column per metric, with the values a pull is your best in marked in gold. **Stored runs** lists the pulls you kept with **Store run** (next to the pull picker on the Fight graphs tab; from a history entry too), together with your bests, tinted gold. Clicking a column head sorts by it, the best first; a second click the other way round. [64]
- **Clerics go by the group's UHS**: unhealed health is a healer's key figure, so a cleric's runs are ranked by the whole group's (everyone's missing HP in the pull, the graphs' Party line), not by their own; every other class by their own. The runs tables mark it "UHS*", the dotted best line in the UHS graph is then the group's, and the banner says "UHS*" too. Runs kept before this have no group figure, so a cleric's next complete kill starts the group bests. [64]
- The column heads of the runs tables say what each column is, as the meter's do, and how the runs have it; for a cleric the UHS* head adds the group note in gold. [64]
- Each run opens its history entry in the meter and its graphs in the frame (the clock button). The trash removes a stored run (it asks once); a best among them stays a best. A stored run counts as a best only when it was a complete kill. [64]
- The tabs stay in the frame while the game has focus. [64]
- **Clickable without focus** (Settings → General, a toggle per frame; also in each frame's tab): a frame that's on takes clicks while the game has focus, one that's off lets the mouse through to the game until the overlay has focus. The digital twin library and the playback window are on, as they always took clicks; the group frame, the fight graphs and the skill monitor are off, as before. The meter keeps its pin button for that. [64]
- After the frame had been dragged, it could stop growing and shrinking with what it shows: the best-pull banner then pushed the last graph out of sight and made the graphs scroll. It grows downwards by the banner's height now, and shrinks back once the next pull starts. The same was fixed for the group frame, the skill monitor, the settings, the digital twin library and the alert sound editor. [64]
- While the game has focus the frame shows only what's in use: the pull box, the open graphs, the curves that are on and the options that are ticked. Folded graphs, curves turned off and the other options come back once the overlay has focus, as only then can they be clicked, and the frame shrinks to the rest. [64]

## 12 Nov 2026 · build 63

**Player details**
- Hovering a skill in the Outgoing and Incoming tabs (damage, healing, shields) shows what it does, like the buffs: for the player's own skills with the numbers at their skill level when their digital twin is known, else as a range from level 1 to max (as for the skills others used on them), and the specializations the skill's code names. [63]

**Digital twins**
- The Skills card (the Digital twin tab and the library) shows each skill's icon, and hovering a skill shows what it does with the numbers at the twin's level of it. [63]
- The comparison's skills have their icons too. Hovering a skill's name shows what it does with the numbers from level 1 to max; hovering a snapshot's level of it shows the numbers at that level. [63]
- Clicking a snapshot in the twin library opens it on a tab of its own, with the same cards as the Digital twin tab: the stats with their parts, base stats, gear, skills, Daevanion and titles. [63]
- New **Comparison** tab in the library: tick two to five snapshots and click **Compare** to see them side by side, a row per stat (level, combat power, gear score, the base stats, the stats from gear, Daevanion and titles, the skill levels, and the nodes open on each Daevanion board) and a column per snapshot. Skill levels are compared among the snapshots of the reference's class; the others show a dash. What the Daevanion nodes give is already in the stats (their "Daevanion" part) and in the skill levels. **Reference** picks the snapshot the others are measured against (the first one ticked, to start with). With **Difference to the reference** the others show how much more (green) or less (red) they have: 64 Dexterity against the reference's 10 reads +54, 10% Attack against 9.9% reads +0.1%; a stat a snapshot doesn't list counts as 0, and hovering a value shows the number itself and the change in percent. **Biggest differences first** sorts each section's stats by how much the others differ from the reference in percent, averaged over them (120, 140 and 80 Attack against 100 make +13.3%); a big drop counts as much as a big gain, and equal ones keep their order. Stats the website gives both as a number and as a percentage ("Attack" and "Attack %") have a row each. Snapshots of the same character show how they changed. [63]
- A click on one of the stats from gear, Daevanion and titles in the comparison (those marked ▸) unfolds where it comes from: a line for each source any of the compared snapshots has it from (each base stat's effect, the titles, each gear slot with its main and sub stats and enchantment, each arcana card by its kind, the magic stones, each Daevanion board), with each snapshot's part. A skill's level unfolds the same way: the levels put in with skill points, each gear slot, each arcana card kind (Chalice, Parchment, Compass, Bell, Mirror) and Daevanion. The sources always come the biggest difference first. With **Difference to the reference** the parts show as more or less than the reference's part. Another click folds it again. [63]
- New **Favourites** tab in the library: the ♥ beside a snapshot's trash button marks it as a favourite (filled) or takes it off again, and the tab lists the favourites with the same buttons. The marks are kept in the library folder. [63]
- The trash button beside **Compare** deletes every ticked snapshot at once, after asking ("Delete 3 entries?"; a second click deletes). Any number of snapshots can be ticked now, Compare takes two to five. On the Favourites tab only favourites stay ticked, so nothing that isn't shown gets compared or deleted. [63]
- Fixed: with snapshots in the twin library the overlay closed right after starting, before showing a window (since build 62, which loads the library at start). [63]

**Group frame**
- Hidden Cube finder: each cube's distance and arrow now sit right of its banner, whatever sides the members' distance and arrow are set to (they took the members' places, so with the members' arrow turned off a cube had none). With the auras on the left the frame is mirrored, and they go left of the banner. [63]
- The cube's arrow is three-dimensional, seen from behind you like the game's camera, and turned to the way you're heading: pointing away from you (up) means the cube is ahead, towards you (down) behind you. It tips up or down as the cube lies above or below you, and keeps its colours (green above, red below, blue level). The game doesn't say which way you or your camera face, so the way you last moved counts: the arrow shows once you've gone a few metres and keeps that way while you stand. Hovering it explains this. [63]
- The party members' arrows are three-dimensional now too, the same as the cube's: turned to the way you're heading and tipped up or down as the member stands above or below you, in the same colours. Like the cube's, they show once you've moved a few metres (the members' arrow has to be on in the frame's settings). [63]

## 11 Nov 2026 · build 62

**Game update of 11 Nov 2026**
- Map changes count again. Since the update the game says "arrived" with a different packet, so after the first map of a session the overlay no longer noticed entering or leaving a dungeon: the group frame kept the old party (everyone twice at the start of a dungeon, players who had left the party), the meter could keep someone from the run before, and runs weren't split by map. [62]
- The group frame now goes only by the newest party list: entering a dungeon the world server's list (with everyone's old ids) can come seconds before the dungeon's, which also showed everyone twice. [62]

**Boss fights**
- Boss fights in the meter and in history entries carry the boss's name: "Boss #1 (Kromede's Desolation)". Bosses the game data doesn't name, such as ones announced before the overlay was started and those in history entries from before build 61, keep the number alone. [62]

**Digital twins**
- New **Digital twin** tab in the player details, next to Buffs: the player as the game's official character website (aion2.plaync.com) shows them. It lists the sums of their stats from gear, Daevanion and titles, each with its parts; their base stats and what those give; every gear item with its stats, magic stones, theostone and the skill levels it adds; all skills with their level (the one the game uses them at, gear and Daevanion included) and how much of it gear and Daevanion add; the Daevanion boards with the nodes opened; and the equipped titles. The class's own base values and buffs aren't on the website, so the sums leave them out. [62]
- **Look up** in the tab asks the website for that player. With the new setting **Digital twins** on, party and force members (and you) are looked up automatically as they show up in the meter. A lookup takes about a minute per player, as the website is asked one thing at a time; each player is looked up once per session, **Look up again** fetches fresh data. Only characters on the global servers (NA, EU, Asia, Latin America). [62]
- History entries show each player's twin as it was looked up around that fight, and exported entries carry the twins along. [62]
- New **Digital twin library** (the people button in the meter's title bar): every twin looked up is kept, also over restarts (in the folder `twin-library` next to the settings), and listed the newest first with the character and server, the class with its icon, the gear score (the website's item level), combat power and when the snapshot was taken. **Add manually** looks up any character: pick the region and server (your own is chosen to start with), type the name, and the twin is added once the lookup is done (about a minute). Each snapshot has a trash button, which asks once ("Delete?") before it deletes. Comparing snapshots comes later. [62]
- The library is there from the start: each player's newest snapshot is loaded when the overlay starts, so the Digital twin tab and the buff numbers at a caster's level work right away; with automatic lookups on, each player still gets one fresh lookup per session. [62]
- New button in the meter's title bar, next to the library (a person with a refresh arrow): looks you up again on the official website, which adds a snapshot of your twin to the library. Its tooltip shows how the lookup goes, and it lights up while one runs. [62]
- The tab also lists the **specializations seen in the fight**: for every skill the player used, which of its five specializations they picked, with what each does (as the game words it, in English). The overlay reads them from the skill ids of what the player did, so they're there without a website lookup too, for the whole dungeon or, opened from a boss fight, for that fight. Active skills show the picks ("3 + 4"); stigmas show how far they're unlocked ("1–3"), the lower ones included (still to be confirmed in game). [62]

**Group frame**
- The overlay remembers your character. Started in the middle of a session it didn't know who you are, as the game only says so at login and on a map change: the group frame stayed empty when you weren't in a group, and the meter had no ⭐ for you. Now it recognizes you as soon as the game names the players in sight (every few seconds), so the frame shows you on your own, with your buffs. The very first start (nothing remembered yet) still needs one map change. [62]
- Hovering a buff or debuff icon now also shows what it does: the skill's tooltip as the game words it (in English), and the caster's specializations where the buff tells them. The numbers are those of the caster's skill level when their digital twin has been looked up (see Digital twins); otherwise, and for stigmas the website lists without a level, they show as a range from level 1 to max ("by 10.5–22.5%"). A boss's debuffs still show only their name. [62]

**Buffs**
- Each buff in the Buffs tab now shows its icon, and hovering it shows what it does, like in the group frame, at the caster's skill level when their digital twin is known. [62]
- Uptime counts buffs kept up by recasting. Recasting a buff that's still on renews it, and the game then names an origin that is no player and the effect by another number, so those stretches were dropped: a Light of Regeneration kept up all fight showed 38 % for its caster instead of 88 %. [62]
- The parts of one buff (the caster's own and the party's, like Light of Protection's or Earth's Blessing's) are one row now. [62]
- Hovering an effect that isn't its skill itself, like Earth's Blessing from Earth Punishment, says whose effect it is and lists the caster's specializations that shape it, instead of the skill's damage (also in the group frame). [62]
- **Hidden Cube finder (experimental)**: the frame lists the Hidden Cubes the game announces around you (from about 170 m), nearest first, each with its distance and direction like a party member, until one is opened. New setting **Hidden Cubes** in the frame's settings: Off, Show (default) or Show with sound (the Blip, at the boss alert's volume, once per new cube). Needs the group frame on. Two cubes recorded in different places had the same code, so it likely knows them all; a cube that doesn't show is worth a recording. [62]

## 11 Nov 2026 · build 61

**Boss fights**
- Bosses have their names again, and only NPCs the game's own data calls bosses start boss fights. Since a game patch at the end of October the game announces every NPC with a different packet; the overlay reads the NPC code from it again, for monsters, bosses, objects and summons alike. Until now bosses were only guessed from their HP, which also turned other NPCs with a lot of HP into "bosses". The guess stays for bosses the game announced before the overlay was started. [61]

**Buffs**
- New **Buffs** tab in the player details, next to Outgoing and Incoming: for the whole dungeon, or, opened from a boss fight, for that fight. It lists the buffs on the player (whose, and for how much of the time they were up) and the player's own buffs, on themselves and on the group. Group uptime looks, moment by moment, at each other group member who is within the group frame's range of the caster or has the buff (a HoT still running on someone who walked off counts), and shows the share of that time they had it. Time dead, or without a known position, counts for nothing. [61]
- For that, the overlay now keeps group members' positions with the history (once a second at most). History entries from before show the buffs and their uptime on each player, but no group uptime. [61]

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
