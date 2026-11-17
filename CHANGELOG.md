# Aion2Heal – patch notes

Changes in this healer version of Aion2Flow, newest first. The build number is in brackets; build N is released as
version 1.0.N.
New meter columns start switched off: turn them on under **Settings → DPS meter → Main window metrics**.

## 17 Nov 2026 · build 74

**DPS meter**
- The pin at the meter's bottom right is now a hand, and the three display modes (interactive, click-through with its faded look, and hidden) are gone. Instead the meter has **Clickable without focus** like the other frames, on at first, also in your settings from before: the meter takes clicks while the game has focus, as it did in interactive mode. Turned off, the mouse goes through the meter to the game while the game has focus, through the ping graph and the details too, except over the meter's title bar while its **Title line always clickable** is on, as it is at first (see **Frames**): a click there gives the overlay focus, and the whole meter takes clicks again until you click into the game. [74]
- The hand shows while the overlay has focus: a hand while the meter is clickable without focus, crossed out while it isn't. A click on it switches the setting, and its tooltip says which it is and what a click does. The setting is also in **Settings → General** (with the other frames), **Settings → Visuals** and **Settings → DPS meter**. [74]
- The meter always keeps the opacity you set (**Settings → Visuals → Opacity**): it no longer fades while the mouse goes through it or is over it, and neither does the ping graph below it. **Settings → DPS meter → Click-through title bar**, which hid the title bar while the meter was click-through, is gone too: the title bar always shows. [74]
- The **Display mode shortcut** (**Settings → Hotkeys**) is now **DPS meter: clickable without focus** and switches that setting, like the hand. Keys you had set for it stay. [74]
- Each boss fight can have a **Sum** row under its players, from two players on. Every column shown is added up over the fight, the aggro, stagger and boss columns give the group's share, and the recovery time is worked out as in the bottom bar (your group only). It keeps up with the pull live, and the earlier pulls under **Boss fights** and the boss fights of a history entry have it too. It explains itself on hover. It's off by default; turn it on with **Settings → DPS meter → Add boss sum row**. [74]

**Frames**
- Every frame has the hand now, in its title line left of the eye: the group frame, the skill monitor, the fight graphs, the Hidden Cubes and the twin library. Like the meter's it shows while the overlay has focus (also when the frame is minimized), a hand while the frame is clickable without focus and crossed out while it isn't, and a click on it switches that frame's **Clickable without focus**. [74]
- A click on a title line folds the frame (or the meter) only while the overlay already has focus: the click that brings the overlay to the front from the game just gives it focus, so clicking the meter's title bar to focus the overlay no longer folds the meter. Nor does that click switch a hand, though it lands where the hand appears as the overlay gets focus (at the meter's bottom right, say). Dragging works as before, and the eye still folds with one click. [74]
- Every frame has a new setting, **Title line always clickable**: the DPS meter, the group frame, the skill monitor, the fight graphs, the Hidden Cubes and the twin library. It matters while the frame isn't clickable without focus. On, the frame's title line still takes clicks while the game has focus, so a click there gives the overlay focus (without folding the frame), and the rest of the frame lets the mouse through to the game; a folded frame is only its title line, so it takes clicks then (the folded meter keeps its bottom bar, which still lets the mouse through). Off, the mouse goes through the whole frame. It's on for the DPS meter, whose title bar always took clicks, and off for the other frames, also in your settings from before. It's in **Settings → Visuals** and in each frame's own tab, under **Clickable without focus**. [74]

**Scenes**
- Each scene's frames have two more columns, **Clickable without focus** and **Title line always clickable**, with a check box per frame (the settings window is a little wider for them). Adding a scene, saving it again under its name and the **Save scene** shortcut keep both settings of every frame, and applying the scene (with **Apply now**, its shortcut or a workflow) sets them. Scenes saved before have a dash in those boxes: they leave the settings as they are until you tick or untick a box. A click goes from unticked to ticked to the dash and round again. [74]

**Skill monitor**
- The time left of your debuff on your target is easy to read now: light violet on a dark square in the middle of the slot. Before, it was violet straight on the icon and got lost on purple and bright icons. [74]
- A gear in the title line, left of the hand, opens **Settings → Skill monitor**, as the gears of the group frame, the fight graphs and the Hidden Cubes open their tabs. Like the hand, the eye and the cross it shows while the overlay has focus; while the monitor is minimized it's hidden, like the other frames' gears. [74]
- Hovering a skill in **Settings → Skill monitor** (its icon, its name or its row; the ticks keep their own hover texts) shows what it does, as the game words it (in English), like the buffs in the group frame: with the numbers at your level of the skill when your digital twin has been looked up, otherwise as a range from level 1 to max ("by 10.5–22.5%"). The line below says which levels the numbers are for. [74]

**Fight graphs**
- When you fold the fight graphs, the title line now names the metrics the pull shown set a new personal best in, in the banner's gold after the frame's name (for example "Fight graphs ★ DPS · HPS"). Hover over them to see the full banner with the values. A first kill of a boss adds nothing there, since there was no earlier best to beat. If the frame is narrow, the list is shortened with "…" and the hand, the eye and the cross stay where they are. It's on by default; turn it off with **Settings → Fight graphs → New best runs in the title line**. [74]

**Digital twins**
- A twin's skills are now grouped under their own titles: active skills, passive skills and stigmas. This applies to a single snapshot, to the comparison and to the Digital twin tab in the details. With **Biggest differences first** on, the comparison sorts each group separately. The line under a skill's name no longer repeats its kind ("Active", "Passive", "Dp"): it only says **not equipped** for an active skill or stigma that isn't equipped, and is left out otherwise (passives can't be equipped). [74]
- The drop-down boxes of the twin library (**Reference** in the comparison, and region and server under **Add manually**) open from a click anywhere on them. Before, only their arrow opened them. [74]

**Settings**
- The **Hotkeys** tab explains itself at the top: how to set or change a shortcut (click its row, then press Ctrl, Alt, Shift or Win together with one more key; Esc leaves it as it was), how to clear one (the × at the row's right), why a row can stay on Press keys… (another row or the row itself has the keys, Windows or another program uses them, or the key isn't supported; keys another row has must be cleared there first), and that shortcuts work in every program while the overlay runs, so pick keys you don't need elsewhere. [74]

## 16 Nov 2026 · build 73

**Tutorial**
- A short tour of the overlay. The "?" in the meter's title bar starts it until you've been through it once; after that **Settings → General → Start the tutorial** starts it again. In 21 steps it rings each part it's about (the meter's rows, sorting, UHS, a player's details, the boss fight, history and playback, ping, click-through, the frames, the group frame and its loot bag, the skill monitor, the fight graphs, the digital twins, scenes, workflows and hotkeys) and explains it on a card beside it, with Back, Next and End tour. [73]
- It shows a made-up fight: HappyCat (a cleric, "you" for the tour), TankyBear, FlameFox and AngryRaccoon kill Vakron. While the tour runs, the fight is an entry in the history that can be played back but not renamed, exported or removed; the group frame shows the group as it stood at the kill, and HappyCat is a twin in the library. None of it is saved. When the tour ends, the entry and the twin are gone, and the frames, the history entry you were looking at and the overlay's mode are as they were. Workflows and scene shortcuts wait while the tour runs, and should the overlay close mid-tour, the next start puts the frames back. [73]

**Frames**
- A click on a frame's title line folds it or brings it back, like its eye; dragging the title line still moves the frame. In frames you can drag by any spot, only the title line folds them. This works for the meter, the group frame, the skill monitor, the fight graphs, the Hidden Cubes and the twin library. [73]

**Meter**
- The ping graph stays open and in front of the game: clicking into the game or switching the overlay to click-through no longer closes or hides it. It closes when you click the wifi icon again or fold the meter; while the overlay is hidden it hides too and comes back with it. It stays in front of the other frames too. While the overlay is click-through, the mouse goes through it to the game. It now sits under the whole meter and moves with it as the meter grows. [73]
- The ping graph is as see-through as the meter now: **Settings → Visuals → Opacity** fades it and fills it in the way it does the meter, also while you move the slider, and while the overlay is click-through it fades with the meter. Before, it stayed nearly solid. It grows and shrinks with the UI scale like the meter, as before. [73]
- New **recovery time** column (Rec.): a player's UHS ÷ the damage they took, how many seconds damage stayed unhealed on average (2M UHS for 400K damage taken is 5.0s). Below 10 s it shows tenths (0.1s, 5.0s), from 10 s whole seconds (13s). It shows by itself while you play a Cleric (Auto; Show or Hide under **Settings → DPS meter → Main window metrics**) and sorts like the other columns. Someone who took no damage, or whose missing HP the overlay can't know (players outside your group), shows "–" and comes last whichever way you sort. [73]
- The bottom bar shows the group's recovery time left of the UHS total, with the recovery time column; players outside your group are left out of it. The recovery time, UHS and DPS totals there explain themselves on hover. [73]
- **Settings → DPS meter → Scene mode** is now called **Meter records**, with **All fights** (was Standard) and **Boss battles only**, so it isn't mixed up with the Scenes tab. [73]
- With **Boss battles only**, a new map starts the meter empty too, as with All fights, and its first boss pull fills it. Before, the meter kept showing the last map's boss fight until the next pull; that fight is in the history. [73]
- Fixed: extra rows with a class icon and a number instead of a name. They were summons (Elementalist spirits, Sorcerer Bittercold Wind) whose owner the game never sent because you were far from your group at a pull (the Fire Temple's first boss, 80–250 m away). With two players of that class in the group the overlay can't tell whose they are, so they show in no row now. Their damage, about 3 % of their owners' in that fight, goes to neither owner. A player taken for such a summon before their name arrived shows normally once it arrives. [73]

**Scenes** (a new tab in the settings)
- A scene keeps which frames are open, folded or closed, and where they are. **Add current scene** saves the frames as they are now under a name. Each scene lists its frames with Open, Folded or Closed to change later, and has **Apply now**, rename and delete (click twice). [73]
- Each scene can have its own shortcut (**Scene shortcut**, also under Shortcuts as "Apply scene: …"). Pressed in game, it opens, folds, closes and moves the frames as the scene has them. The **Save scene** shortcut asks for a name and saves the frames as they were when you pressed it; a name you already have saves that scene anew. [73]
- A scene that opens the twin library doesn't take the focus from the game, and the name prompt gives it back after Enter or Esc. A place off every screen (a window minimized to the taskbar, a monitor since gone) is neither saved nor applied. [73]

**Workflows** (a new tab in the settings)
- A workflow applies one of your scenes when something happens in game: a boss fight starts (the first pull while none is on), a boss fight ends (the last pull that was on is over) or you enter a dungeon. Workflows can be switched off and on, and deleted (click twice). [73]
- Dungeons are the maps 600000–699999 (Krao Cave to Vakron Sky Island, their hard modes, Ludra, the temples) and the tiered instances 910000–919999 ("Nightmare Altar (Easy)" to "(Hell)"). Towns, fields, the Legion airships and the Shugo Festival's maps don't count, and a training dummy starts no boss fight. [73]

**Group frame**
- A loot bag above the right end of the HP bars shows whether your share of the boss is enough for its loot. Red: at your pace you stay under it. Yellow: not there yet, but your share of the damage dealt so far would get you there at the kill. Green: reached. With a boss and its adds (boss-kind NPCs pulled during the fight) the boss counts. After a kill it shows the final result until the next pull or map, after a wipe nothing; while the boss's HP isn't known, yellow at best. Hover over it for the numbers. [73]
- The title line is laid out like the rows now: the boss alert banner runs across the HP bars, up to the loot bag, and reads a little shorter ("⚠ Boss targets you"). The eye and the cross take room only while the overlay has focus. [73]
- The share the loot needs is 3.5 % at first and goes in 0.5 % steps; the warning can be switched off. Both are under Settings → Group frame and General (**Boss participation (loot)**). [73]

**Skill monitor**
- Your debuffs on your target: while a debuff of yours is on it, its skill's slot gets a violet frame (the violet the group frame marks debuffed members with) and the time left in violet in the middle. A skill that also buffs you (Earth Punishment with its Earth's Blessing) keeps its gold ring outside, with the violet frame inside it. **Settings → Skill monitor** has a third column, **Debuff**, with Select all and Clear all; all skills are ticked at first. [73]
- The game doesn't tell the overlay which target you've selected, so your target is the one your last aimed skill went at: selecting another without casting at it doesn't switch, and skills aimed at yourself don't count. Only debuffs you cast yourself show, not yet those of your Elementalist spirits. After a meter reset or on a new map, a debuff shows again once it has run out and is put on anew; recasting one that is still on doesn't bring it back. [73]

**Boss fights**
- Fixed: nameless helpers of a boss, missing from the game data and with the boss's own HP (Vakron's, Ketu's, Bakarma's), showed up as extra boss bars for a while, some at 0 %, and the boss share column counted their HP too. A helper whose HP matches a boss of the same map no longer counts as a boss; a boss the game data doesn't know yet (after a game update) still gets its bar. History entries saved before keep them in their playback. [73]

**Fight graphs**
- A **recovery time** graph and overview line: the UHS so far ÷ the damage taken so far, second by second. As with UHS, a cleric's overview line is the whole group's (Rec.*). [73]
- Best runs also keep your pull with the shortest recovery time (a cleric's by the group's), and the **Best runs** and **Stored runs** tabs have a column for it; the frame is a little wider for it. It can pick the best pull too (**Best pull by**). Pulls kept before have none: they show "–" and come last whichever way you sort. [73]
- With **Each party member** on, the legend lists your group as soon as the group frame has their names, before a dungeon's first pull, so you can pick whose curves to show beforehand. Whom you hide stays hidden, by name and server: through every pull and boss, in the next dungeon or after a requeue with some of the same players, and after a restart; a new player shows. Before, a member came into the legend only once they had acted in the pull, and each boss started with everyone shown again. [73]
- Fixed: a history entry saved at a map change showed an empty overview, with no You curve and no best line. The game sends your character's id for the next map just before the old map ends, and the entry took that id, which never fought, for you. New entries keep you; entries saved before find you by your name and server when they're imported or opened, and the meter marks you (⭐) in them again. [73]

**Digital twins**
- In the comparison, a lower cooldown or MP cost counts as better now: -6 % against the reference's -2 % reads -4 % in green. "Cooldown Reduction" stays better the more there is. [73]

**Settings**
- A build that Velopack didn't install (a local one) shows its version under System too. [73]

## 15 Nov 2026 · build 72

**Meter**
- The meter does less on each refresh. While the same players fight in the same language it no longer looks every name, class icon and map up again (nor in the open details), and the earlier pulls in the list below the table are filled in again only when something they show can change. On its own, the meter does 17–30 % less UI work. [72]
- A row that moves up keeps what it has: when a player rises in rank or a row above leaves, the row keeps its value widths, a long name scrolls on where it was, a row fading in goes on fading in, and an open value tooltip stays open. Before, such a row was built anew. The same goes for the skill tables in the details (the Damage table's value / rate pairs stay put) and the lists in the playback window. [72]
- As rows now keep their widths across the pulls of a scene, a value column that grew wide in one pull stays that wide in the later ones, so a long name may scroll where it fitted after a reset before. A click released within about a frame of a rank change now opens the details; one released later is lost, as before. [72]
- Fixed: when a map change pushed the history entry you were looking at out of the 10 kept, the meter went on showing it until you clicked **Live**. It goes back to live now, with everything the Live button switches: the open details (cleared if the player isn't in the live table), the boss sections and the target status bar. [72]

**Frames**
- What you can't see costs far less. A minimized group frame or skill monitor no longer builds its rows until it shows again, then with current rows from its first paint; the boss alert still sounds, and auras and cooldowns are still tracked. The fight graphs no longer work out a pull that's still going on while they're minimized or on another tab, and animations that aren't drawn pause (the boss bar and the row highlight while the meter is folded or minimized, the cooldown rings while the skill monitor is minimized). With every frame minimized the overlay does 60–75 % less UI work. [72]
- The group frame needs far less temporary memory on each screen refresh, and makes its aura tooltips only when you hover an icon. [72]
- Fixed: a buff's tooltip in the group frame could name the wrong caster. With **Buffs cast by others** on, or after switching to another of your characters, a second caster of the same buff at the same level got the first one's name; the numbers were right. Each names its own caster now. [72]
- After a language switch, the group frame's aura tooltips (names, level notes, the specialization prefix, "Effect of …", the "picks unknown" note, the name of an unknown skill) change on the next frame; before, only once the frame was opened again. What an effect does stays in English, as before. An aura that came up while the group frame was minimized gets its texts when the frame shows again. The "Hidden Cube" label in the Hidden Cubes frame follows a language switch on the next frame too; before, a cube that stayed in view kept the old language. [72]

**Fight graphs**
- Shown again while its pull is still going on (un-minimized, or back on the **Fight graphs** tab), the frame shows its last graphs for 1–3 screen refreshes, until the new ones are worked out (if those were an earlier pull's, they keep its own best line and time axis meanwhile); before, they were at most a second old at that moment. A pull counts as going on until it's a kill or a reset, or until it ends 5 minutes after the boss's last action (build 71). [72]

**Start-up**
- With Windows or the overlay in another language than English, the overlay loads its game data (skills, NPCs, maps) for one language at start instead of two, and it loads leaner in any language. On a German Windows loading the game data needs 36–40 % less temporary memory, and the game data then takes about 7 MB instead of 11.6 MB; on an English Windows 14 % and 17 % less. [72]

**Digital twins**
- Deleting ticked snapshots reloads the twin library once instead of once per snapshot, and a reload reads only the files written since the last one. [72]
- With the library window open, a snapshot that becomes unreadable without being written (after its file's rights changed, or when OneDrive made it online-only while offline) stays listed until the window is opened again. Before, the next reload dropped it. [72]

**Capture**
- While the overlay waits for the game, looking for it costs next to no CPU: 0.08 ms a second instead of 21–32 ms (under 0.01 % of a core instead of 2–3 %), and in game 0.25–0.28 ms a second instead of 4.7–6.9 ms. This needs Windows 11 24H2 or later; an older Windows looks for the game as before. [72]
- Fixed: the game port indicator could stick on "Waiting for game ports", or stay ready after the game closed, until the game's next port change; and a game process starting or ending at the wrong moment could end the capture until a restart. [72]

**Builds 70 to 72 together** (against build 69, replaying recordings of real play)
- The meter's work every 40 ms takes 30–40 % less time, and the meter alone does 28–33 % less UI work. [70–72]
- With the skill monitor, the group frame and the fight graphs shown, the overlay does 30–47 % less UI work; the group frame's part needs 1.8 KB of temporary memory per screen refresh instead of 26 KB. With every frame minimized it does 62–78 % less. [70–72]
- With the fight graphs open through a session, the overlay's memory peaks 59–68 % lower (153–170 MB instead of 388–478 MB, the windows themselves not counted), and after a 21-minute session the live scene holds 52 MB instead of 72 MB. [70–72]

## 15 Nov 2026 · build 71

**Boss fights**
- A boss you hit and then leave (in the open world, say) no longer keeps its fight open until the next map change. Once the boss has done nothing for 5 minutes (no hit on it, no hit by it, no aimed cast), the pull ends as a failed attempt at its last action, live and in the history, and its numbers stop there: duration, damage, healing, damage taken, UHS, the DPS check, the fight-only details and the fight graphs. It can show under the boss bar as a wipe does, with the DPS-check gap when a time limit is set. Before, such a pull took in the rest of the session's time and healing, and the meter worked it out again on every refresh, which cost more and more as the session went on. [71]
- Hitting that boss again starts a new attempt: the death counts beside the group frame's names start over, and a kill is a best run only if that attempt took at least 90 % of the boss's HP. [71]
- Fights in which the boss acts at least every 5 minutes don't change: in the 23 pulls of the recordings checked, the longest quiet was 228 s. A boss that waits its turn for longer than 5 minutes gets two attempts, so its kill may miss its best run, or the second attempt counts as a best that leaves out the wait; a boss-type add that never dies shows as a failed attempt 5 minutes after its last action. [71]

**Meter**
- Fixed: with names hidden, the overlay could crash when a new player came in while the meter numbered the hidden names, mostly in Boss mode. [71]

**Player details**
- The live details no longer hold back events after the first enemy cast aimed at a player or the first stagger of a scene. From then on, new events often waited for the next full refresh (on 26–36 % of the refreshes in the recordings checked): heals on players at full HP, HoT ticks, shields between hits. They show at once now, and totals and per-second values no longer dip and jump back. [71]
- The Buffs tab works its uptimes out in 38–62 % less time (in two of three recordings; in the third the gain is small). [71]
- Closing a player's details frees what they held, 5–10 MB for the busiest player of a session; opening them again fills them anew. [71]

**Fight graphs**
- After you pick another pull, the graphs keep showing the one before, with its own best line and legend, until the new one is worked out (10–40 ms). Before, the old pull's curves could show for a moment with the new pull's best line. Picking the shown pull again in that moment keeps it, a click in the legend or the settings then takes effect when the new pull is there, and a setting switched and switched back in that moment (Overhealing, say) keeps the curves shown; before, the other setting's curves could stay for up to a second. [71]
- The dotted best line comes with a pull's first graphs; it was missing from them. [71]
- Fixed: **Compare** could fail, most likely closing the overlay, when a ticked best run had just been dropped from the best runs. It compares the other runs now, or does nothing. [71]
- Moving the mouse over a graph redraws only the hover line and its dots, not every graph: 26–67 % less time and about half the temporary memory per move (three quarters less after the Comparison tab was open). [71]
- **Stored runs**: a pull stored with **Store run** while it was still going on, or while its kill was being saved as a best, is one entry if it becomes a best, not two. [71]
- When the best runs' list (index.json) can't be saved, for example while another program holds it, runs that drop out of it keep their file, so every run listed at the next start opens; before, one could say "This run's history entry couldn't be read." Once a later save works, a kept file stays in the folder, unlisted. [71]

**Playback**
- Opening a history entry in the playback window needs 62–81 % less CPU (0.08–0.13 s instead of 0.27–0.56 s for the biggest scenes measured): it no longer replays the whole entry a second time in the background, which nothing used. [71]
- In a history entry, jumping forward on the timeline takes 1–2 ms instead of 60–90 ms (in 10 s steps), as it goes on from where it is instead of replaying from the start; jumping back to the start in 10 s steps takes 26–70 % less time in all. Playing needs 80–88 % less temporary memory (3 minutes at 60 Hz: 166–180 MB instead of 850–1,430 MB), and the event list reads its events in 96–98 % less time. [71]
- A click or drag on the timeline moves the playhead and the time at once, and frames that come in during the jump no longer move them; before, a drag showed nothing until the pointer rested. The ±1 s and zoom buttons pressed during a jump act from where it goes: paused at 10 s, +1 s right after a click at 50 s goes to 51 s, not 11 s. The event-step buttons still step from the frame shown before the jump: pressed during one, they take the playhead back to the event next to where it was. [71]
- The strips are drawn again when a frame moves a zoomed view while the window is loading; they stayed as they were. [71]
- Playing a live scene no longer takes the meter's pending changes, which left the meter showing only every fourth or fifth of its refreshes while it played. The window's own refresh (four times a second) and jumps in a live scene still take them, so the meter can still skip a refresh then. [71]
- A history entry's playback no longer applies its frame once more when that unused replay in the background ended (0.3–0.7 s after opening, several seconds for long boss scenes): a timeline zoomed with the mouse wheel right after opening stays zoomed, and a jump or step that ends at that moment keeps its frame. A live scene's playback applies its frame once instead of twice when the scene ends. [71]
- An error in the status line right after opening (an event list that a damaged history file can't give, say), or when a live scene in the window ends, stays until the next frame change; before, the extra frame replaced it (after opening, with "Paused"). [71]

**History**
- The overlay keeps its record of the session's packets, from which history entries, best runs and the fight graphs are made, in a more compact form: after a 21-minute session the live scene holds about a quarter less memory, and a large history file (79 MB) takes 417 MB instead of 644 MB once read. [71]
- A long session on one connection no longer grows: played three times over, the overlay's memory stayed at 66 MB, where it grew from 73 to 76 MB. [71]
- A history entry made of a scene that goes on (a best run, **Store run**, the boss scene Boss mode keeps) no longer takes in real-heal or stagger credits that come in after it, so its details agree with its totals. [71]
- Fixed: after a stored or best run of the live scene was opened from the fight graphs and the scene then went into the history at a map change, removing the map-change entry and opening the run again added a second, identical row. Now it adds none. If the run's own entry was the one that went (removed, or the 11th dropped), the history shows the map-change entry, the whole scene, instead of bringing the run's entry back. [71]

**Settings and files**
- The last settings change before closing the overlay is no longer lost when settings.json is held by another program at that moment: the overlay tries again as it closes, up to 5 times within 250 ms. [71]
- Packet recordings (Settings → System) reach the disk every second, so after a crash, or ending the overlay in Task Manager, only about the last second is missing; before, about the last 100 KB of lines, and a short recording stayed empty. The files are 0.1–0.3 % larger. A write that Windows refuses (a virus scanner can cause that) ends the recording, as a full disk does, instead of going on into a broken file, and the packet still reaches the meter. [71]
- The times in app.log look the same whatever the Windows region (2026-11-15 21:30:05.123): a Finnish region wrote dots, a Thai one the year 2569. [71]

## 15 Nov 2026 · build 70

**Fight graphs**
- The fight graphs frame no longer makes the overlay stutter during a boss pull. It used to work the whole pull out again every second, 7–9 ms each time with 21–40 MB of temporary memory, and the overlay had to stop for a full memory clean-up about every 2 seconds. Now each refresh adds only what came in since the last one, usually in under 0.1 ms: over a 21-minute session with seven bosses that is 3 full clean-ups instead of over 600, and under 1 GB of temporary memory instead of 34 GB. The graphs are the same, value for value. [70]
- Showing a history entry's pulls in the graphs, or comparing runs, needs about 80 % less temporary memory. [70]
- Fixed, and rare: when the map changed, the battle was reset or, in Boss mode, the next pull began just as the graphs were worked out, the new scene's first hits could count into the old pull for a second, in the graphs and in a live boss fight's details; when such a change came just as a kill was worked out, the kill could be kept as a best run with the next scene's numbers or history entry. Such a refresh shows no events for that second now, and such a kill is left out of the best runs, with a line in app.log. In Boss mode, **Store run** clicked within a second after the next pull began stores the pull before without its curves, which is still not a usable run; it used to put the new pull's first hits in them. [70]

**Capture**
- Downloads, streams or Steam next to the game no longer hold up the capture. The overlay read through such a connection's data again with every packet that came in, up to half a second for a single packet, and meanwhile the queue it shares with the game's packets could overflow and lose some. Now it reads only what's new: at most about 9 ms for a packet, and about 90 times less CPU per MB of such traffic. [70]
- A packet the overlay can't make sense of no longer stops the capture for the rest of the session, which froze the meter until a restart. It's skipped and noted in app.log ("Capture skipped …", at most every 10 s, and once more when the capture stops). Only if 1,000 packets in a row fail, with none read in between, does the capture still stop as before, with its indicator red; that has never been seen. [70]
- Fixed: a malformed cast packet (0x0238) froze the meter, the frames and the fight graphs until a restart, and a cut-off 0x4536 (someone coming into sight) ended the capture. Now the cast gets no cooldown entry and the cut-off packet is ignored. [70]
- Combat packets of 126 bytes or more are read now; they were dropped or lost a byte. None were in November's recordings. [70]
- Fixed, though never seen in the data: a compressed batch inside another could garble the rest of the outer batch, and a summon packet without an owner (0x4036 inside another packet) could mark another NPC, even a boss, as a summon. [70]

**Player details**
- The player details open in about a third of the time (0.35–0.65 s instead of 1.1–1.9 s, opening them again) with a quarter of the temporary memory. They look exactly as before. [70]

**Settings**
- A closed settings window is let go. Each window opened and closed used to stay in memory (about 20 MB) and went on reacting to every change: after ten opens, a language switch took 11–12 s. Now closed windows don't react, the open one rebuilds its rows once on a language switch instead of six times, and it opens with about 40 % less temporary memory. [70]
- Fixed: on the **Hotkeys** tab, the "None" of an unset **Minimize all frames** shortcut stayed in the old language after a language switch. [70]

**History**
- History files are read 18–40 % faster, when you import one or open a best or stored run, with less than half the temporary memory; writing one (an export, a best or stored run) needs about 30 % less. The files are the same, byte for byte. [70]
- Opening or comparing stored runs in the fight graphs no longer holds up the overlay for 10–35 ms per run: the file is read in the background. [70]
- At a map change the overlay makes the scene's history entry in about 2 ms instead of 17–19 ms (a 21-minute scene), time in which the capture had to wait. [70]
- Importing a file that's broken inside its first entry gives a shorter place in the error message: it ends at the broken entry, not at the field inside it, and names the entry's type. A cut-off file or a single bad value still gives the same line and position; a hand-edited value with two errors now reports the second, a syntax error, at its own position. [70]

## 14 Nov 2026 · build 69

**Training dummies**
- Hitting a training dummy makes a pull of its own, like a boss: it starts with your first hit (your summons' count too) and ends when you pause for the cut-off (5 s at first), or when its length is up. Other players' hits on it neither start nor extend one. A dummy can't die (it stops at 1 HP and is full again a moment later), so its HP plays no part: no boss HP, no DPS check, never a failed attempt. [69]
- The pulls are in the meter's fight list as "Dummy #1 (Training Scarecrow)", from the second pull on a dummy with its number ("run 2"). The newest stays in the section under the bar until the next pull, so you watch the one under way. A row's details give your damage per skill from your first hit to your last. In the fight graphs they're pulls like any other: your DPS second by second, **Store run**, the comparison. [69]
- Settings → General → **Training dummies**: **Pull length** (until you stop, or 0:30, 1:00, 2:00, 3:00 or 5:00) and **Ends after a pause of** (2 to 30 s). With a length set, a pull ends when it's up, and hitting on starts no new one until you pause for the cut-off. [69]
- Personal bests on dummies: a pull that ran its full length can be your best, by damage alone and against pulls of the same length only (each length has its own best), with the dotted best line and the banner as for bosses. All dummies share their bests, so it doesn't matter which one you hit: the Training Scarecrow and the Melee Training Scarecrow take the same damage, hit for hit. In the runs tabs they're all "Training dummy". Pulls set to run until you stop aren't bests. [69]
- A dummy the overlay didn't see appear (it was there before the overlay started) is recognized when it fills up again from 1 HP; its first pull starts with your first hit after that. [69]

**Meter**
- An overlay started after you logged in knows you from your first two skills now. It used to wait for a map change or for you to take damage, and at a dummy neither comes: it made no dummy pulls, and with one other player of your class around it took you for their summon and listed your hits under them. [69]
- Beside your ping in the bottom bar, in brackets: what 99 % of your pings in the last 2 minutes stayed at or below, as in "23 ms (41)". The game measures your ping every 10 s, so that's the highest of the last 12 or so. [69]
- A click on the ping (or its wifi icon) opens your ping over the last hour below it, 10 minutes at a time: the mouse wheel or dragging moves along the hour (wheel up or dragging to the right goes back in time), the pointer shows a moment's ping, and the 99 % value is drawn too, dashed. Gaps are times without a ping (a loading screen, no capture). [69]

**Windows**
- The settings, the twin library and the alert sound editor follow **Topmost mode** (Settings → General) like the frames: with "While game is active" they're on top of the game, but no longer in front of every program you switch to. Before, they were always on top. [69]

## 13 Nov 2026 · build 68

**Fight graphs**
- The pull to show, **Store run** and **Hide overview** sit in a row under the tabs now, like the dungeon in the runs tabs: in the header, the pull's box ran into the tabs and the buttons beside it. [68]

**Settings**
- The Visuals tab has each frame's **Clickable without focus** too, under its size and opacity: the group frame, the skill monitor, the fight graphs, the Hidden Cubes and the twin library. It's the same setting as in the General tab and the frame's own tab. [68]

**Program file**
- The exe names **rassau** as its company (in Explorer's tooltip and its properties, where it said Cloris), the one responsible for this version; cloris-chan is still credited for Aion2Flow in the README and the third-party notices. [68]
- Released exes carry their version as their file version (1.0.68.0 for version 1.0.68) instead of 1.0.0.0. [68]

## 13 Nov 2026 · build 67

**Frames**
- Every frame has an **eye** and a **cross** at the top right of its title line (while the overlay has focus, like its gear): the eye minimizes the frame to that line, a click on the shut eye brings it all back, and the cross turns the frame off (the twin library's closes it). Each frame remembers whether it's minimized. The DPS meter has the eye too, on the right of its title bar, in place of its fold arrow and the click on its title (dragging the title only moves the meter now); folded, it shows its title bar and bottom bar as before. [67]
- **Minimize all frames**, a new shortcut: every frame minimized and the meter folded, or with all of them minimized all back again. The shortcuts have a tab of their own in the settings now, **Hotkeys**, with the battle reset and the display mode too. [67]
- The meter's title bar is the overlay's navigation: buttons turn the group frame, the skill monitor, the fight graphs and the Hidden Cubes on or off (in gold while on), beside the twin library's, which is gold while the library is open (minimized too). They're narrower than the other buttons, and the meter is 34 px wider at its narrowest, so the map's name keeps some room beside them and the eye. [67]
- The **Hidden Cube** finder is a frame of its own now, away from the group frame: put it wherever you like, with its own scale and opacity, a tab in the settings (on or off, the sound for a new cube, clickable without focus) and a button in the meter's title bar. Whoever had it shown with the group frame on has the new frame on. [67]

**Digital twins**
- A lookup shows in the twin library's list as soon as it's asked for, above the snapshots, whether started with **Add manually**, from a player's Digital twin tab or for the party: the character and server, their class once the website's profile is in (a second or two after the lookup starts; at once for a player with a snapshot already), and a bar that fills as the website answers the lookup's requests (about 34, one every 0.7 s), outlined to its full length, with the percentage done in the middle. Lookups still waiting for their turn show an empty bar at 0 %. When done, the row gives way to the new snapshot. [67]
- A **Note** column in the twin library: the new pencil beside a snapshot's heart opens a box for its note (Enter or a click elsewhere keeps it, Esc drops it, emptying it removes the note). The list shows each note, in full when you hover it; the notes are kept beside the snapshots (notes.txt in the twin-library folder), and deleting a snapshot deletes its note. The library window is wider for the column. [67]

**Meter**
- The aggro column has a third setting, **Auto**, now the default: shown while your character is a Gladiator or a Templar (the tanks, as in the best runs' aggro), hidden for the other classes, and switching along when you play another character. A click on its row (Settings → DPS meter → Main window metrics) goes through Auto, Show and Hide. From older settings a shown column stays shown, a hidden one becomes Auto. [67]

## 13 Nov 2026 · build 66

**Game data from the Steam client**
- The skill, NPC, map and server names and the boss list now come from the Steam client's own data (upstream Aion2Flow's Steam data, client build 26581260, 12 Nov 2026) instead of the Korean client's. Class skills are the same in both. [66]
- 156 NPCs the old data had as normal monsters are bosses now, so they get their own boss fights, fight graphs and best runs: among them Ashpile Vasma, Abandoned Apostle Shuska, Executioner Vidala, Akula of Chaos, Guardian Lord Nahma and the 32nd Division's officers. The Central Abyss Fusion Reactor in front of Ketu counts as a boss too, as the game marks it. [66]
- New: the bosses of the Abyssal Forge: Ludra raid (Isolation Specialist Rahu, Abyssal Wings Ketu, Eternal Ludra and their reactor), and the raid's map name instead of "Raid Test". 142 monster skills and 14 NPCs carry Steam's names (Prideful Atiel is Atiel, Kabloom is Alluring Dionae), as do three maps (Chaotic Outer Reshanta). [66]
- About 430 NPCs only the Korean data knows stay known, with their old names: Eternal Ludra's summons, which the Steam client spawned in the raid in October, would otherwise have shown up as players in the meter. [66]
- The Chinese interface shows the English game names: the Steam data has none in Chinese. [66]

**Deutsch**
- New language **Deutsch** (Settings → General → Language): the whole overlay in German, with the game's own German names for skills, NPCs, bosses and maps from the Steam client's data (Vollendete Berk, Kromedes Gier, …). The class names follow the German client (Beschwörer, Waldläufer, Magier, …); the meter's short column heads stay as players write them (DPS, Σ DMG, Σ Heal, UHS) with German tooltips. A new install on a German Windows starts in German; existing settings keep their language. [66]

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
