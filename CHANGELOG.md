# Aion2Heal – patch notes

Changes in this healer version of Aion2Flow, newest first. The build number is in brackets; build N is released as
version 1.0.N. From build 75 on, each version starts with a short summary (**In short**), then every change in detail
(**In detail**); the builds before have the details only.
New meter columns start switched off: turn them on under **Settings → DPS meter → Main window metrics**.

## 27 Nov 2026 · build 85

### In short

- Hovering over the odyle energy now shows how many rewards it pays for (40 odyle each at first), how many runs that makes with premium (2 rewards per run, so 120 odyle is 1.5 runs), and the time to the next +15.
- The bottom bar can show the rewards or the runs with premium instead of the odyle energy, and the price of a reward can be changed: both in the new **Odyle energy** section in **Settings → DPS meter**.

### In detail

**Odyle energy**
- Hovering over the odyle energy in the bottom bar shows all of it, whatever the bar shows: the energy as the game shows it (for example 120(+160)/840), the rewards it pays for at the set price (7 rewards at 40 odyle each; the odyle and what's in brackets count together), the runs with premium, which gives 2 rewards per run (3.5 runs; "run" up to 1, "runs" above), and the time to the next +15 (for example 1h 30m, in its last minute in seconds). The game sends no time for the +15: it comes every 3 hours from 01:00 UTC (02:00 in Germany in winter), as a recording showed, and the overlay counts down to that. [85]
- New section **Odyle energy** in **Settings → DPS meter**: **In the bottom bar** picks what the bar shows beside the FPS: **Odyle energy** as the game shows it (at first), **Rewards** or **Runs with premium**. **Odyle per reward** sets the price of one reward, from 5 to 200, 40 at first. [85]

## 27 Nov 2026 · build 84

### In short

- The meter's bars now fill the whole row: each player is a rounded card, and their colour fills it from the left as far as their value in the column you sort by goes against the top player's. A boss fight's rows look the same, a little smaller.
- The bars are coloured by class now: every cleric has the same colour, every gladiator another. **Bar colours** in the settings switches back to a colour per player.
- New: the game's frames per second beside the ping, counted the way the Xbox Game Bar counts them. A click shows the last ten minutes as a graph with the 1 % low (the slowest frames of the 2 seconds before), on its own or beside the ping graph. **Settings → DPS meter** sets how often they're measured (every 0.1 to 2 seconds).
- New: your odyle energy beside the FPS, as the game shows it, e.g. 120(+160)/840.
- The ping and FPS graphs now draw smooth lines and have their own size and opacity, all set in their new sections in **Settings → DPS meter**; at first, both side by side are as wide as the meter. Their hint shows only while the overlay has focus.
- The fight graphs show their option boxes only while the overlay has focus, and the curves' legend only while a graph is open.
- Overhealing is hidden at first now, for new settings: healing and HPS count only the HP a heal restored.

### In detail

**DPS meter**
- New look for the bars: each row is a rounded card on a dark track, and the bar is its background, in the player's colour, fading in from the left to full strength at its end. It reaches as far as the row's value in the sorted column goes against the top row's, so the top row fills its card; before, the bar was a thin slanted line along the row's bottom. The players' colours are darker now and all equally bright, so every value stays readable on them (white and the columns' own colours at 4.6:1 or more where a bar is at full strength), and the track keeps text readable over a bright game behind the meter. Cards stand 3 px apart (a boss fight's 2 px), so the meter grows by 2 px per row; the selected row's glow shows over the bar, and the row under the cursor lightens. [84]
- **Bar colours**, with the meter's look in **Settings → Visuals** and **Settings → DPS meter**: **By class** (at first) gives every player of a class that class's colour, so all clerics look alike and gladiators have another; **By player** is the colour per player as before, handed out in the order they appear, new each fight. The class colours take another meter's for the Cleric (teal), Sorcerer (steel blue), Elementalist (indigo) and Gladiator (plum) and put the other classes evenly between them, just as soft: Ranger green, Assassin olive, Templar gold, Chanter orange, Brawler red. All are as bright as the bars' other colours, so every value stays readable, and the class icon beside the name tells them apart too. [84]
- **Settings → General → Overhealing** is hidden at first now: healing and HPS (in the meter, the player details and the fight graphs) count only the HP a heal actually restored. Settings from before keep their choice; switch it to **Hide** there for the new way. [84]

**Game FPS**
- The meter's bottom bar now shows the game's frames per second beside the ping while the game runs. Windows reports every frame a program hands over to be shown (DXGI's Present), and the Xbox Game Bar and PresentMon count these reports; the overlay counts the game's own and reads or changes nothing in the game. A click on the figure opens the last ten minutes' frame rate below the meter, two minutes at a time: on its own at the meter's left edge, or to the right of the ping graph while that's open. Each graph closes with a click on its icon, both with the meter's folding. The mouse wheel or a drag moves through them, and the pointer reads off a moment. Dashed like the ping's 99 %, the **1 % (2 s)** line is each value's 1 % low: the rate that 99 % of the frames in the 2 seconds before it reached, one over the frame time that 99 % of them took no longer than. A frame still on screen counts with its time so far, so a freeze pulls the line down at once. [84]
- How often a new value comes: **FPS measured every** in **Settings → DPS meter → Game FPS**, from 100 ms to 2 s, 250 ms at first. Each value covers the time since the last one and goes by the frames' own time stamps, so a short one is as exact as a long one; values show about a quarter of a second late, as Windows hands the reports over in batches. Checked in the game: a steady 92–97 fps at both 100 ms and 250 ms. [84]
- **Settings → DPS meter** has a **Ping** section and a **Game FPS** section now, each with **Smooth curve**, on at first. It draws the graph's two lines (the ping with its 99 %, the FPS with its 1 % low) as an average of the values around each, as the fight graphs' **Smooth curves** do: about 5 values on either side, and never across a gap. Single jumps no longer make the lines jagged, dips and peaks stay where they were, and hovering still shows each value's own. [84]
- The ping and FPS graphs have their own **Scale** and **Opacity**, in **Settings → DPS meter → Ping and FPS graphs** and in **Settings → Visuals**. At **Scale** 100 % (at first) both side by side are as wide as the meter above them, and they grow and shrink with its columns; one alone is half that. Before, each was 320 px wide at the UI scale. **Opacity** works like the meter's (60 % at first: a see-through panel with lines and text at full strength); before, the graphs went by the meter's. [84]
- The hint under each graph ("Scroll or drag to move through…") shows only while the overlay has focus. [84]
- The graphs' scale and clock times are in the meter's own text colour with its dark halo now, and the lines have the halo too, so they stay readable over a bright game behind them. [84]

**Odyle energy**
- The meter's bottom bar shows your odyle energy to the right of the FPS, as the game does: its icon, what you have, in blue brackets what recharge items added, and the most there can be (for example 120(+160)/840). The game sends it when you log in and whenever it changes (a recharge item, the regular refill), so it shows from the first of those after the overlay started, while the game runs. The game doesn't send the maximum; 840 is the one it shows. [84]

**Fight graphs**
- While the game has focus, the frame no longer shows its option boxes, ticked or not: **In thousands (K)**, **Each party member**, **Other members' deaths**, **Smooth curves** and **Equal time scale** come back with the overlay's focus, as only then can they be clicked. Before, the ticked ones stayed. [84]
- The curves' legend (**You** and the others) and the members' options (**Each party member**, **Other members' deaths**) show only while at least one metric's graph is open, in the Fight tab and the Comparison tab. With every graph folded they no longer stand alone under the overview. **Smooth curves** stays with the overview, which it smooths too. [84]

## 26 Nov 2026 · build 83

### In short

- Your frames now stay where you put them after **Update now**, and after Windows shuts down or restarts with the overlay open. They used to come back in the top left corner, and a skill monitor at a size above 100 % came back wider. Updating to this version with **Update now** still moves them one last time; after that they stay.
- The meter, closed while minimized to the taskbar, no longer opens off the screen the next time.
- The fight graphs' overview can now show DPS, HPS, damage taken and UHS in thousands on one scale, so you can compare their heights: **Overview in thousands (K)** in **Settings → Fight graphs**, or the **In thousands (K)** box beside **Hide overview** above the overview. Aggro, stagger and recovery time keep their 0–100 % scale, now on the right.
- The fight graphs' overview can now also mark the other party members' deaths, not only yours: **Other members' deaths in the overview** in **Settings → Fight graphs**. Their crosses are grey-blue; yours stay white.
- Hovering over a death's cross in the fight graphs now always names who died there.

### In detail

**Fight graphs**
- New: **Overview in thousands (K)** in **Settings → Fight graphs**, also as **In thousands (K)** beside **Hide overview** above the overview. Off (the default), every curve of the overview goes up to its own peak (100 %), as before. On, DPS, HPS, damage taken and UHS (also the group's, with *) share one scale on the left, labelled like the metric graphs below (50K, 1.2M), so their heights compare; aggro, stagger and recovery time (a share, gauge points and seconds) keep going up to their own peak, read on a 0–100 % scale on the right. With none of those four shown, the overview stays as with the setting off. Hovering shows each value as before. [83]
- New: **Other members' deaths in the overview** in **Settings → Fight graphs**, hidden at first. Shown, the overview marks the other members' deaths too, as grey-blue crosses that stand apart from your white ones, even with colour blindness; your crosses are drawn on top. Hovering over one says who died. **Other members' deaths** stays the setting for the graphs below and the Comparison tab, so either can be on without the other; its box in the frame now says so when you hover over it. [83]
- Fixed: hovering over a death's cross often showed the card of the second before it, which left that death out or named only someone who died just before. The card now names everyone whose cross the cursor is on, in every graph and in the Comparison tab. [83]

**Frames**
- Fixed: after **Update now**, the group frame, skill monitor, fight graphs and Hidden Cubes frame came back in the top left corner, a bug as old as that button. Its shutdown, which a Windows shutdown, restart or logoff with the overlay open goes through too (closing the overlay with the meter's cross doesn't), closed each frame once more after the meter had closed it, and on that second close the frame saved the 0,0 a closed window reports as its place; the skill monitor also saved its width as if at 100 %, so at a size of 150 % it came back half as wide again. Now a frame saves nothing once it's closed. The update to this version still runs the old version's shutdown, so with **Update now** the frames move one last time: drag them back or apply a scene. With **Update automatically** on, closing the overlay with the meter's cross installs the update instead, and nothing moves. [83]
- Fixed: the meter, minimized to the taskbar with its − button and then closed (from the taskbar, for example, or by Windows shutting down), saved the place Windows gives minimized windows, far off every screen, and opened there the next time. It now keeps the place where it last showed, and a place off every screen that an older version saved (or a screen that's gone since) no longer counts at the start: the meter then opens where Windows puts it. Folding it with its eye never moved it. [83]

## 25 Nov 2026 · build 82

### In short

- In the new version of Ludra's raid from the 18 Nov update, the overlay now shows the bosses' names, as long as it is running when they appear: the Central Abyss Fusion Reactor (the cube at the start), Abyssal Wings Ketu or Isolation Specialist Rahu, and Eternal Ludra. The raid also gets a history entry of its own, Abyssal Forge: Ludra, instead of staying in the one of the map you came from.
- When your group wipes at Eternal Ludra, the pull now ends there as a failed attempt, and the next pull counts as his next attempt instead of a new boss.

### In detail

**Game data**
- The game data is now the Steam client's of 18 Nov 2026 (upstream Aion2Flow's Steam data, client build 26728906), where it was that of 12 Nov. It adds the version of Ludra's raid that came with that update: its map, Abyssal Forge: Ludra, and its 19 NPCs, among them the bosses Isolation Specialist Rahu, Abyssal Wings Ketu, the Central Abyss Fusion Reactor and Eternal Ludra. Until now these bosses had no names, and as the overlay didn't know the map, the whole raid stayed in the history entry of the map you came from (Altgard in the histories of 22 Nov), and a workflow set to start when **you enter a dungeon** didn't start there; now it does. Ketu's and Eternal Ludra's pulls now start with the first hit on them: the overlay took them for bosses only at the HP update that first hit brought, so their pulls began with the next hit, up to 0.6 seconds later, and a pull missed up to 136,170 damage. A boss's name comes only with the message that brings it into sight (its spawn), so the overlay has to be running when it appears: the recording of 24 Nov began during the cube's fight, so there the cube stays without a name. A few names changed too: in English 16 NPCs (Grudge Bearing became Grudge-Bearing, Snowfield Lagur Tundra Lagur) and 20 skills (the monster skill Smite Ground is Ground Smash now, and two Brawler skills got new names), in German those two Brawler skills and two holiday treats. In the 116 other recordings and histories no number moves. [82]

**Boss fights**
- Fixed: Eternal Ludra doesn't heal back to full when the group wipes. In the new version of his raid he is taken away when the group dies and sent anew 2 to 3 seconds after the last death, as a new monster, which the overlay took for another boss: the pull stayed on as if he were still fought until five minutes after its last hit, and the next pull got a boss number of its own with attempt 1. Now a monster arriving with the code of a boss whose pull is on ends that pull as a failed attempt at its last hit, and the newcomer's pulls are that boss's next attempts. A boss fought within the last 1.5 seconds isn't ended this way, as the game also sends a boss's code when a twin of it comes into sight. It takes the boss's code: a boss sent anew that the game data doesn't know yet still counts as a new boss each time, and the first wipe after the overlay missed his spawn (started late) isn't caught. In a recording of 24 Nov, Eternal Ludra's 9 pulls were 9 bosses with one attempt each; now they're his failed attempts 1 to 8 and the kill on attempt 9, and each wipe shows as a failed attempt 2 to 3 seconds after the last death. The deaths beside the names now start over with each of his pulls; before, they ran on into the next pull after the 5 of the 8 wipes that came less than a minute before it. With **Boss battles only** each pull stays a history entry of its own, as before. A failed attempt's aggro now counts only until he is sent anew; before, the pulls that came within five minutes after the wipe counted in it too, so in that recording a player's aggro in a failed attempt moves by up to 24 points. Apart from that, what each pull counts stays the same, and in the 116 other recordings and histories, as in history entries saved before, nothing changes. [82]

## 23 Nov 2026 · build 81

### In short

- Each scene in **Settings → Scenes** has a new button with four arrows next to **Overwrite**: it saves only where your frames are now into the scene, and keeps everything else, such as which frames are open, the shortcut and the scene's place in the list.
- **Settings → System** now says who made what, with links to both GitHub pages: Aion2Flow by cloris-chan, the foundation, and Aion2Heal, this healer version.
- The fight graphs' overview can show **Taken***, the whole group's damage taken per second. A cleric now sees it there in place of their own.
- Three new switches in **Settings → Fight graphs** choose, for UHS, damage taken and recovery time, between the whole group's value (marked *) and your own. **Auto**, the default, gives a cleric the group's and everyone else their own. UHS and recovery time also decide what your best runs are ranked by; damage taken never does, your best runs always go by your own.
- Two new skill monitor settings, both off at first: **Debuffs from anyone** shows everyone's debuffs on your target, not only yours, and **Buffs from anyone** everyone's buffs there. What's on you shows as before.
- When the game hands one creature's id to another, such as a dead spirit's to another player's summon, the overlay now keeps the two apart in new fights, so damage and healing count for whoever owned the creature at the time; and a player's spirit whose owner the overlay never saw no longer counts as a monster.
- In PvP the group frame now shows enemy players' debuffs on your group as debuffs, with the violet frame, also in battlefields, where enemies carry your faction: a player who fought you or your group counts as an enemy, for the skill monitor's new settings too.

### In detail

**Scenes**
- New: a positions button with four arrows beside each scene's **Overwrite** in **Settings → Scenes**. The first click asks (**Positions?**), the next saves only the frames' positions into the scene: where each open or folded frame is now, and for a closed one where it will open next (its last place, or one a scene applied since gave it). Everything else stays as the scene had it: which frames are open, folded or closed, where they take clicks, the scene's name, its place in the list, its shortcut and the workflows that use it. A frame whose corner (the top left one; the group frame's top right one while its auras are on the left) is off every screen now keeps its position from the scene, even if part of the frame still shows, and so does the twin library while it's closed. As with **Overwrite**, a double-click only asks, and during the tutorial it does nothing. **Overwrite** still saves the whole scene as the frames are now. [81]

**Settings**
- New: **Credits** in **Settings → System**, above the patch notes. Aion2Flow by cloris-chan is the foundation: the packet capture and combat parsing, the game data (names, icons and the boss list), the DPS meter (without its title bar navigation) with its boss bar, **Boss** column and **Boss battles only**, the player details, the history of recent fights with its playback, the skill monitor, the global shortcuts, the automatic updates, the language setting and Aion2Flow's own texts in English, Korean, Chinese and German. Aion2Heal, this healer version, adds everything else, including the group frame, the digital twin library, the meter's HPS, healing (with or without overhealing), UHS, recovery time, damage taken, aggro, stagger and kill columns, the fight graphs and best runs, the boss fights per pull, scenes and workflows, the Hidden Cubes, the boss alert, the tutorial and the meter's title bar navigation. Each comes with a link to its GitHub page, which opens in your browser through Windows Explorer, so the browser normally doesn't run as administrator like the overlay. [81]
- Two small slips in the translations are fixed: the Korean tutorial's step about the title bar buttons said 보관함를 (now 보관함을), and the Traditional Chinese hover text of a frame's close button called the meter DPS 計量表, where everything else says DPS 統計表. [81]

**Fight graphs**
- New: **Taken*** in the overview, the whole group's damage taken per second: the Taken graph's **Party** line, in Taken's colour. Its legend entry's hover note says it's the group's, and that your best runs still go by your own damage taken. [81]
- New: three switches in **Settings → Fight graphs**, right under **Overview**: **Group or own UHS**, **Group or own damage taken** and **Group or own recovery time**. A click goes through **Auto**, the group's value (UHS*, Taken*, Rec.*) and your own (UHS, Taken, Rec.); a row is ticked while it gives the group's value for your class. **Auto** gives a cleric the group's values and every other class their own, as UHS and Rec. had it, so on Auto a cleric's overview changes only in showing Taken* in place of their own damage taken. [81]
- The UHS and recovery time switches also decide what your best runs are ranked by: new bests and their banner, the bests in the title line, the dotted best line, and the runs tables' values, best marks, sorting and the * in their column heads. The damage taken switch never does: your best runs always go by your own damage taken, as how much the group takes isn't up to you, and while the damage taken switch gives your class Taken*, a note under the **Best runs** table says so. A UHS* or Rec.* set to the group's by hand says in its hover note that the setting made it so, not your class. [81]
- Trying a switch costs no best: while a switch says otherwise, the runs that are bests by Auto's values stay kept, and a new run that beats one of them is kept too, without a banner, so they're all there when you go back to Auto. Runs kept only as bests by a switch's values leave the runs tables when you switch back, and go for good the next time a run of that boss is kept while the switches give Auto's values. [81]

**Skill monitor**
- New: two settings in **Settings → Skill monitor**, right under its on/off switch, both off at first: **Buffs from anyone** and **Debuffs from anyone**. Off, the monitor shows what it did: the buffs on you whoever cast them, and on your target only yours, as debuffs. The settings change only what shows on your target: what's on you and the cooldowns stay as they were. [81]
- **Debuffs from anyone**: the debuffs ticked under **Debuffs on target** show on your target whoever cast them. **Buffs from anyone**: the ticked buffs on your target show whoever cast them, and your own buffs there count as buffs (gold), where they showed as debuffs (violet). Each skill keeps one buff and one debuff timer: a buff on you keeps the buff timer, one on your target shows there only when there's none on you, and of several, the one with the most time left shows. [81]
- Your target is whom your last aimed skill went at, as the overlay can't see the one you picked. Heals don't change it, so for a healer it's practically never a party member, and outside PvP **Buffs from anyone** seldom adds anything; in PvP, with an enemy player as your target, it shows their own buffs and their side's. Buffs on you already showed whoever cast them. [81]
- On your target, with these settings, the game data says which auras are buffs and which are debuffs: it labels many class skills' own effects as one or the other. Where it labels a skill both or neither, the caster's side decides: an aura cast from the other side is a debuff. On a friendly player that's an enemy player's; on an enemy player everyone's but its own and its side's; on an NPC every player's, the other faction's too, as at an Abyss boss both factions fight. Sides go by faction, with two exceptions: your party and force always count as yours, and a player who fought you or your party or force on this map counts as the other side whatever their faction, as in battlefields, where the other side carries your faction too. A fight is a hit the game data calls damage between that player themselves (not their summon) and you or one of your party or force, either way, once the overlay knows both as players; a new map starts over, a meter reset doesn't. Any other player of no known faction counts as yours, and so does everyone else while the overlay doesn't know your faction. A summon is on its owner's side, whether it cast the aura or is your target. In 35 of the 37 recordings where the game data labels class skill auras on your target one or the other, both agree on every such aura (the skill monitor shows class skills only); in the other two, both PvP, the game data is right on a few buffs whose caster the overlay had on the wrong side. In two battlefield recordings this makes 83 auras your party or force put on enemies of your faction debuffs, which the faction alone would have taken for buffs. [81]

**DPS meter**
- Fixed: the game gives a creature's id to a new one later on the same map: a dead spirit's id to another player's spirit or summoned object, a monster's to a player's summon, a summon's to an object. The overlay told the two apart only when the game data knew both codes, and never by their owners, so it took the newcomer for the earlier creature: all the hits on that id went to its last owner, and a monster whose id a summon took counted as that summon, with no boss bar and the hits on it lost. Now every spawn that carries a code is checked against the id's last one: another code (also one the game data doesn't know) or another owner makes it a new creature that takes nothing over, so a summon's hits, heals and shields count for the player who owned it when it dealt them. A monster or boss whose spawn the overlay saw with its code keeps its bar and the hits on it when a summon later takes its id; one whose spawn it missed (started too late) or saw without a code is still taken for the summon. In 68 recordings numbers move in 3. In a Draupnir run a Wind Spirit's 8,839 damage and 682 healing now count for its owner, not for the player whose creature got its id later, in the Transcendent Bakarma fight too; in another dungeon run 13,644 damage does. In the PvP boss recording about 0.8 million damage moves between players' rows (Sorcerers +582,771, Elementalists −545,722, Clerics −96,019), 9,890 of it to a creature whose spawn named no owner, on a row of its own that the meter doesn't show. Eternal Ludra coming back under the id of a player's Fire Spirit (build 80) should now get a bar, without a name, like his comeback before it: that spawn carried a code the game data lacks, which the history doesn't keep, so this is shown with made-up packets only. History entries saved before keep what they had, also in playback. [81]
- Fixed: a player's spirit, trap or Divine Aura whose spawn named no owner counted as a monster: hits on it counted as damage, and finishing it as a kill and a last hit. Now its game-data code decides: the players' Fire, Water, Wind, Earth and Ancient Spirits, Ensnaring and Explosion Traps and Divine Auras, known by their codes, are players' summons also without an owner (NPCs of those names stay NPCs). In 68 recordings 349 such creatures appeared and 33 of them took damage that counted. In the meter one player in an Abyss recording loses 2,121 damage, and in that and the PvP boss recording 62 last hits go; the rest of that damage, and 1 kill, was on rows the meter doesn't show. In boss fights the PvP boss recording's two fights lose 16 and 6 last hits (the fix above gives one of the 6 back), and the Abyss recording's Executor Tamasa fight 1 kill and 20 last hits. [81]

**Group frame**
- Fixed: in PvP, an enemy player's debuff on someone in your group showed as another player's buff (under **Buffs cast by others**, hidden at first), or not at all when the overlay didn't know the caster as a player. Now an aura the game data labels a debuff is one, whoever put it there: an enemy's Chain of Torment, Heal Block or Fire Mark, or the Theostone proc Khrudgelmir's Silence. One it labels both or neither, such as Poach, Grapple or Divine Punishment, is a debuff when it comes from a player who fought you or your party or force on this map (a fight as under **Skill monitor**), whatever their faction, or from that player's summon; before that player's first fight with you or them it still shows as another player's buff. The faction couldn't tell: none of the 101 players who fought a group in the recordings and saved histories read as the other faction. 32 carried your faction, all from other servers (in battlefields both sides carry yours), and 69 came while the overlay didn't know yours yet. Of 629 enemy players' auras on a group there, 622 now show as debuffs with the violet frame, where none did: 247 by the game data's label and 375 because the caster had fought the group. In PvE nothing changes. [81]
- Fixed: a buff or debuff from a creature on an id the game had given another creature before counted as the earlier creature's, and so its owner's: in the PvP boss recording 9 of a member's own spirit's auras on the member showed as another player's. Now an aura comes from the creature that has the id when it's put on. [81]

## 23 Nov 2026 · build 80

### In short

- At Ludra's last boss, Eternal Ludra now has a boss bar with his name and a boss fight, and the meter counts your damage on him: it left all of it out. After a wipe the game sends him back under a code its data doesn't know, so that bar has no name; and if he comes back under an id a player's summon had before, the overlay still misses him.
- Damage on adds the game data files as summons, such as the Sniping Crokas in Krao Cave, now counts too: up to 2.8 % of a run's damage was missing.
- If the overlay starts while you're already playing, it no longer forgets who you are as soon as the game names you (in the party list, or in your own kill message in the Abyss): the group frame, the skill monitor and training dummy pulls keep working, and the overlay now remembers your character for the next start.
- In PvP, the game's message that one player killed another no longer puts the killer's name on someone else's row, and a row with damage no one dealt, which that let in, is gone.
- At the sword boss event, the boss bar no longer shows an extra nameless boss at 0 % for about half a minute, and the map's history no longer lists it.
- Behind the scenes, nearly two thirds of all hits are now looked up in the game data by their real effect; that changes no numbers.

### In detail

**Players**
- Fixed: an overlay started after the game said which character you play (it says so only at login and on a map change) knows you by your id alone at first. Unless it remembered the character you play, the first packet naming you with your server took "you" away again, and nothing gave it back until the next map: the group frame had no row, buffs or position of yours, the skill monitor, training dummy pulls, best runs and the Hidden Cubes finder didn't know you, and the meter lost its "you" marker, with the Self, Party or Force scope your own row too. In 9 of 68 recordings this happened 14 times: 13 times through the party list and once through your own kill message (a minute of Abyss farming, 18 s in). Now a packet naming the id the game pointed you out by names you: you stay "you", and that name and server become the character the overlay remembers, also over another one it remembered before. A packet with only a server other than the remembered character's no longer gets mixed with that character's name, and once the game's own-character packet has named you, its name stays. The id you had on the map before, carried over when the next map's id came only after the change, is still dropped as soon as the game names someone else under it. In the 68 recordings "you" is no longer lost: the Abyss farm keeps you to the end, with your 6 kills, 4 last hits and 20 credited in the row marked "you". This changes no number in the meter or in a boss fight. [80]
- Fixed: in PvP, the game's message that a player killed another player gave the killer's name and server to the wrong player. The message starts with who died, and a player's id takes only 1 or 2 bytes there where an enemy's takes 3, so the overlay read the killer a byte or two too late: as an id from 1 to 127 (the killer's divided by 128) or from their server's bytes (1150 or 1151), and 11 such messages didn't read at all. In 68 recordings there were 1,709 of them, all but one in two PvP recordings. In those two, 227 ids carried two or more names over a map: at its end 19 rows in the meter showed a killer's name, and 26 killers seen only through their kills had no name. Now the overlay reads the killer where the message has them, and every id carries one name. Two numbers change with it. A row with 7,936,727 damage that no one dealt is gone: three stray "hits" the overlay found inside other packets, which it takes only for ids it knows, and only the misread had made those known. And a player seen only through their kills now counts as a player: 731 of their own drain healing counts, and a kill they took the most HP off is theirs, where it had gone to the runner-up. With the overlay started mid-session, such a message could also take "you" away from a player with a small id. Enemy kill messages, every other number and "you" in the 68 recordings stay as they were. [80]

**Boss fights**
- Fixed: at Ludra's last boss, Eternal Ludra had no boss bar, no name and no boss fight. The game data files him (2920772) as a summon, like hundreds of objects around him that carry his name, many of them with his code or his 194,512,500 HP, so the overlay never took him for a boss; unlike a player's summon, he has no owner. Now the HP check that finds bosses the game data doesn't name also judges a summon with no owner at its first HP update: it becomes a boss at 3,000,000 HP or more, unless that update says 0 HP, it has the HP of a boss the game data names (that boss's helpers carry it), or it comes while a boss this check found is being fought (alive, in battle, with an HP update in the last 10 seconds), and then it is that boss's add, like the Fire Temple's Gas Rocks when the overlay missed their boss's spawn. Eternal Ludra passes and shows with his name, his objects stay as they are, and he stays the boss when his spawn comes again. When the group wiped with him at 1.8 %, he came back as a new creature with his HP but a code the game data doesn't know: as before, it gets a boss bar and a boss fight of its own, without a name. When that one had killed the only player fighting it, he came back once more, under the id a player's Fire Spirit had had earlier on the map, and the overlay still takes such a creature for that spirit: pulled, it would have had no boss bar or boss fight, and hits on it wouldn't have counted. Checked with 68 recordings and 45 saved histories: only Eternal Ludra becomes a boss, and every other boss and boss fight stays as it was, also with the overlay started late (every 5 s through the Fire Temple and Watchdog Kwapo histories, every 15 s through all). History entries saved before keep what they had. [80]
- Fixed: at the sword boss event (map 510026), an object with 45,000,000 HP that nobody hits showed in the boss bar as a nameless boss at 0 %. It stayed there for about 30 seconds from its only HP update (0), and was still there when the event boss came up. Its 45,000,000 HP joined the boss shares, so the killed-boss share showed 0 % until the event boss came up. The map's history entry listed it as a second boss. The game data doesn't know the object, so the overlay judged it by its HP pool, and its attacks on players kept its bar up. Now an NPC the overlay first sees at 0 HP never becomes a boss this way. In 68 recordings and 38 saved histories, none of the NPCs first seen at 0 HP was ever hit, and all the others carry the HP pool of a boss on their map, so they already counted as that boss's helpers. Checked with all 68 recordings: of 102 scenes this changes only the one with this object, which loses only this object from its bosses; every other boss and boss fight, and every number, stays as it was. History entries saved before keep it. [80]

**DPS meter**
- Fixed: hits on an NPC the game data files as a summon, with no owner or an NPC owner, didn't count. At Ludra's last boss that left out all of the players' 185,461,007 damage on Eternal Ludra, and elsewhere up to 2.8 % of a run's damage on adds, such as the Sniping Crokas in Krao Cave. Now such NPCs count like any enemy in damage, DPS, shares and details. Hits on a player's summon still don't count, as in the kill columns: the overlay takes a summon for a player's when it links it to a player, to an owner it doesn't know or to another player's summon. A player's spirit the overlay never saw linked to an owner now counts as an enemy: in the recordings, Elementalist spirits hit during the Abyss boss fights (5,047 damage) and a Fire Spirit in Draupnir (111). Boss-fight rows, fight graphs and best runs count only hits on the boss, so of these only Eternal Ludra's new fight changes. Checked with 68 recordings: players' damage goes up by 1,501,623 in 8 of them (1,495,914 of it on Dratona adds in a July dungeon, 1.5 % of its damage), and every boss fight's rows stay as they were. History entries saved before keep their numbers; played back, most of them now count these hits too (Krao Cave, 6 Nov: 2.9 % more at its end). [80]
- Fixed: in PvP, a Divine Aura whose caster the overlay had taken for a summon of an owner it doesn't know counted as an NPC, so destroying it gave a Kill. A summon whose owner is a player's summon now counts as a player's summon too. In 68 recordings this takes 1 Kill off a player in a battlefield recording; nothing else changes. [80]
- Under the hood: nearly two thirds of all hits (108,481 of 169,596 in 68 recordings) come with a 10-byte detail that starts with flag bytes: the hit's action flags, a regeneration amount if there is one, and its direction. Only after these comes the hit's effect. The overlay looked the effect up from the first four bytes, so the game data never saw these hits' real effects, and more than half of those values (56,172) weren't valid effect numbers at all. Now and then the flag bytes matched another skill's effect by chance: 7 hits in 68 recordings. That chance match decided 2 of them, as damage, which is what they are. One landed on an effect that heals and shields, and only the shield part kept that hit from counting as a heal. Now the overlay reads the effect after the flag bytes. The game data now recognizes 108,101 of the 108,481 hits as damage: 108,001 by their exact effect and 100 by their skill. The other 379 still count as damage, as before. Reading it this way changes no number: in all 68 recordings every player's damage, healing, real heal, damage taken and shields stay exactly as they were. [80]

## 20 Nov 2026 · build 79

### In short

- In the Abyss and the PvP boss fights the group frame shows the right distance and arrow for party members who come into sight: they could show up 2,147,483,647 m away with a red arrow until they moved.
- A rare form of a common move no longer puts a player light-years away, and positions no map can have are now left out wherever the overlay reads one.
- The group frame's settings have a **Buffs and debuffs** section with all buff and debuff settings together, and a new one there: **Scroll and food buffs** (Show/Hide).
- The skill monitor shows Earth Punishment and your other debuffs on every boss: on some, like Ferocious Horn Nuakum, it showed none.
- In the Fire Temple, the second boss's helpers no longer show up in the boss bar as extra bosses, 4 or 5 at a time.
- Three new meter columns, off at first: **Kills** (who took most of the HP off each enemy that died), **Last hits** (who landed the killing blow) and **Credited** (your kills as the game counts them).
- Hits of a few skills, such as Ashy Call, Resonance Crush and some boss attacks, now count as damage: they were left out of it, and some showed up as heals.

### In detail

**Group frame**
- In the Abyss and the PvP boss fights, a party member who comes into sight no longer shows up 2,147,483,647 m away with a red arrow. In these areas the game puts one byte more in front of where a player stands as they come into sight, so the overlay read that position from the wrong bytes: always wrong, and nearly half the time (5,927 of 12,984) more than 100 km off, up to light-years. It held until the member moved or cast a buff, so one standing still kept it; 2,147,483,647 is the largest number the distance can show. Now the overlay skips that byte. Checked with 25 recordings: each of the 12,984 times a player came into sight in PvP areas now gives a position on the map, and in the 9,956 times they moved within 2 seconds their next step was close by: half within 1.7 m, all within 42 m, as elsewhere. The 993 times players came into sight elsewhere read as before. [79]
- One rare form of a common move (224 of about 180,000 such moves in 25 recordings, at a world boss and in the Abyss) has a byte more too. Read without it, the player was put somewhere else, mostly light-years away, until their next move. Now it's read like the other moves with such a byte, and puts the player where their move before it did. [79]
- Wherever the overlay reads a position (players coming into sight, moves, the party's positions, where a buff's caster stood, Hidden Cubes), a position more than 100 km out in any direction is left out, as no map is that big. So a packet laid out in a way the overlay doesn't know yet can no longer put anyone light-years away; a player whose position is left out as they come into sight gets one from their next move. A player coming into sight also comes with the direction they face; unless it reads between 0° and 360°, their position is left out too. In the 25 recordings this leaves out nothing. [79]

**Players**
- In PvP areas the overlay reads the server and legion of a few more players coming into sight: of the 12,984 times players came into sight there, 17 had no server before and 2 the wrong one; all of them now agree with the same player's other appearances. [79]

**Group frame settings**
- **Settings → Group frame** has a new **Buffs and debuffs** section with every buff and debuff setting together: **Side** (where they go, until now the row "Buffs and debuffs"), **Buffs cast by you**, **Buffs cast by others**, **Buffs without duration**, the new **Scroll and food buffs**, **Debuffs** and their **Opacity** (until now in this tab's **Visuals** section; **Settings → Visuals** keeps it too). [79]
- New setting **Scroll and food buffs** (Show/Hide, shown at first): with Hide, the group frame leaves out the buffs of scrolls, food and drinks on each member, and of other items you use up, such as potions and talismans, yours and others'; Theostone procs and debuffs aren't affected. The game files all of these as items. In a recording with three scrolls and a meal, the Speed, Courage and Benediction Scrolls and the Artisan's Roasted Fogus all count as such; your class buffs don't. [79]

**Skill monitor**
- Your debuffs on your target, such as Earth Punishment, now also show on a boss whose id belonged to another creature first. The overlay rightly tracks such a boss under an id of its own, with its hits and debuffs, but the target your aimed skill named kept the old id, so none of your debuffs matched. In a dungeon run of 20 Nov this hid Earth Punishment for the whole fight against the third boss, Ferocious Horn Nuakum, while it showed on the first two. [79]

**Boss fights**
- Fixed: the nameless helpers the Fire Temple's second boss brings in during his fight (4, 5 and 4 in a run of 20 Nov, most likely his explosions under the players) showed up in the boss bar as extra bosses, and one became a boss fight of its own. Like the helpers fixed in build 73 they carry the boss's HP pool, but they come in a little under it (22,790,524 to 22,805,235 of 22,809,600), so the exact match missed them. A nameless NPC up to 1 % under the HP pool of a boss of the same map now also counts as that boss's helper while the boss is being fought (hit and not dead); one with the exact pool counts as before, also once the boss is dead. Only a boss in a fight counts for the 1 %, so an idle boss with the same HP pool nearby can't make another boss lose its bar. Checked with 68 recordings and 38 saved histories: only the 23 such helpers in 3 Fire Temple histories change, all to helpers, and no boss loses its bar. History entries saved before keep these helpers, and that extra boss fight, as they were. [79]

**DPS meter**
- New column **Kills**: an enemy that dies in sight counts for the player who took most of its HP off. Their summons' hits count for them; the killing blow counts only up to the HP that was left, and hits after the death don't count at all. Hits of other enemies, such as a boss's blast on its own adds, take HP off but make no one's kill. The game hands out loot by damage counted the same way, a force counting as one side: in a minute of Abyss farming with 78 kills in sight, all 72 drops went to the side that came out on top like this. Adds that the game data files as summons count like any enemy: in a 10-player dungeon run of July they were 101 of the 110 enemies the group hit before they died, and the 110 kills match the game's own count of the run's kills. [79]
- New column **Last hits**: the killing blows, as the game's own kill message names the killer (a summon's kill goes to its owner). In that minute it named the killer of each of the 78. Where the game sends its kill message without a killer, as in some dungeons, the column stays at 0. [79]
- New column **Credited**: your kills as the game counts them for you, every enemy you or your party hit before it died, also out of your sight. Only yours: the game sends no one else's. It sends them in the Abyss and, in recordings from July to October, in dungeons and the open world too. In that minute a Cleric had 6 kills, 4 last hits and 20 credited. [79]
- All three are off at first: turn them on under **Settings → DPS meter → Main window metrics**. A click on a heading sorts by it, each value explains itself on hover, and every boss fight, its Sum row and the history have them too; history entries saved before have none. [79]
- With **Boss battles only**, which stops recording the moment the boss dies, the boss's kill, its kill message and your credit for it, which come a moment later, still count: in the meter, in the boss fight and in its history entry. Credits for enemies killed while it waits for the next pull stay out of it. [79]
- Fixed: hits of a few skills counted as healing or not at all. The overlay took one byte of such a hit for "HP" or "MP", but that byte is only the end of the hit's effect number, so about 1 effect in 128 matched by chance: Ashy Call, Resonance Crush, Hellfire, Haymaker, Roundhouse and two NPC attacks among them. Now the game data decides, as for every other hit. In 68 recordings this was 64 hits (243,219), all missing from damage; now they count like any other hit, in the damage taken of those they hit too, and in the group frame they wear down shields. In two PvP recordings they added 28,769 to five players' healing; in a recording of 8 Nov a boss's attacks on three players (7 hits, 62,417) showed as the boss healing them and were missing from their damage taken; in a minute of Abyss farming 4 hits (16,518) were missing from two players' damage. Healers whose heals landed around such a hit can get a little more real heal, as the hit now accounts for HP its target lost: in one PvP recording two healers gained 1,971 and 2,611. The full HP and MP refill after some boss kills matched by chance too: its HP still counts as healing (5 times in 4 recordings, 174,562), now because of how it's cast, as with a serum, and its MP still doesn't. Potions and serums are unchanged. The playback's **Resources** track listed these hits and the refill as HP or MP gains, and the details' **Resources** part the MP ones; nothing else the overlay reads goes there, so from now on both stay empty. History entries saved before keep these hits as they were, in their numbers and their **Resources** parts. [79]

## 19 Nov 2026 · build 78

### In short

- If a game update again renames the packet with which the game says you've arrived, as the updates of 11 and 18 Nov did, map changes keep counting. With today's game nothing changes.

### In detail

**Map changes**
- If a game update renames the packet with which the game says you've arrived, as the updates of 11 and 18 Nov did, map changes keep counting. When the game has announced a map change, a teleport or a respawn and the known arrival packet hasn't come, the overlay takes as the arrival the first packet it doesn't otherwise read that repeats the announced position, within a minute of the announcement. It switches to the new map right there and notes that packet's code in app.log, so the overlay can be fixed quickly after such an update. Checked against 24 recordings with 47 map changes, teleports and respawns: each time this picked the arrival itself, under all three names the game has used. Most arrivals came within 6 seconds of the announcement, one after 33 seconds. With today's game it never steps in. [78]

## 19 Nov 2026 · build 77

### In short

- Map changes count again: since the game's update of 18 Nov, dungeons, instances and other maps all counted as the map you logged in on.
- On every teleport and respawn the group frame and the Hidden Cubes frame forget the place you left again.

### In detail

**Game update of 18 Nov 2026**
- Map changes count again. The update renamed the packet with which the game says you've arrived, as the update of 11 Nov had done, so since then the overlay noticed no map change after the first map of a session. Entering or leaving a dungeon or an instance, a teleport to another map and a respawn on another map all stayed in one scene on the map you logged in on: the meter's title bar kept that map's name, a boss in an instance was filed under it, no history entry was saved when you left a map, and with **All fights** the time ran on from map to map. Now each map gets its own scene and history entry again, and the group frame's party status starts afresh on the new map. Checked with a recording of four teleports and a death with its respawn: the overlay notices each of them again, where it noticed none. [77]
- Every teleport and respawn, also within the same map, again clears what the group frame and the Hidden Cubes frame knew of the place you left: who was in sight there, and the cubes there. Since the update both kept it, so cubes from the place you left could stay listed. [77]

## 18 Nov 2026 · build 76

### In short

- **Settings → System** shows the patch notes of every version: pick one from the drop-down; yours is marked.
- From 1.0.75 on, each version's notes start with a short summary like this one, then every change in detail.
- Each new release page on GitHub shows its patch notes, and the README links them beside the download.
- The direction arrows of the group frame and the Hidden Cubes tip all the way to show height: straight up for someone right above you, straight down for someone right below.

### In detail

**Settings**
- **Settings → System** ends with **Patch notes**: a drop-down of every version, the newest first, and below it the notes of the one picked, first in short, then in detail (up to 1.0.74 in detail only). The version you have installed says "(installed)" and shows at first. Early builds listed together share one entry, such as "1.0.49–1.0.60". [76]
- The notes come with the program, so they show without internet and always match what you have. They're in English in every language of the overlay; in German, Korean and Chinese a line under the heading says so. Their text can be selected and copied, a line at a time. [76]

**Patch notes**
- From build 75 on, each version's notes start with **In short**, a few lines on what roughly changed, followed by **In detail** with every change as before; build 75 got its short version afterwards, the builds before keep the details only. [76]
- The release page of each new version on GitHub shows its patch notes, those of every build since the release before, and a version can't be released without its short version. The README has a **Patch notes** link beside the download. [76]

**Group frame and Hidden Cubes**
- The direction arrows to your party members and to Hidden Cubes are now drawn as if seen from just behind and a little above you, no longer from high above, and they tip all the way: the more steeply someone or something lies above or below you, the steeper the arrow points up or down (before, it tipped 30° at most). Straight above you, it points straight up; straight below, straight down. For a target at your height the arrow still points up when it's ahead and down when it's behind, and the colours are as before: green above, red below, blue level. [76]
- One weak spot is known: for a target in front of you and somewhat lower, about 20° down when it's straight ahead and more the further it's off to the side, the arrow is seen from its edge and shows only as a thin line; the same goes for a target behind you and somewhat higher, about 20° up when it's straight behind. Arrows to targets at about your height look flatter than before, those to your left and right the thinnest. [76]

## 18 Nov 2026 · build 75

### In short

- With **All fights** the meter's time keeps running between pulls: DPS and HPS sink slowly on the way to the next boss instead of dropping at its first hit.
- Boss fights list no more players than the table's visible rows and scroll the rest: a 20-player event boss no longer stretches the meter.
- Digital twins show the skill levels the game sent in your fights, in italics, and level ranges for stigmas (**Use last battle's skill levels**).
- **Update myself** moves from the meter's title bar into the twin library, also at every start if you like; the library gets a gear for its settings and an adjustable pause between website calls.
- Scenes can be overwritten with the frames as they are now (**Overwrite**).
- Every setting and button in the settings explains itself on hover, and hover texts wrap in a box.
- Fixed: updates deleted the overlay's log, and a change of the Windows clock upset the timing of fights and frames.

### In detail

**DPS meter**
- With **Settings → DPS meter → Meter records** on **All fights**, the time in the bottom bar no longer stands still while nobody fights. It runs from the first hit after a reset or map change until you leave the map, between pulls and after the last boss too, a second at a time, also while you're alone and idle. If the overlay hears nothing from the game for 15 s (the game was closed, say), it stops. Hover over the time to see what it counts. [75]
- DPS, HPS and the other per-second values go by that time, in a player's details too. So they now sink slowly while the group walks to the next boss, instead of dropping all at once when the first hit there lands: damage and healing between pulls always counted, but the time jumped ahead at that hit. [75]
- A history entry saved when you leave the map keeps the time up to then, so the **All fights** entry of a dungeon run reads longer than before and its DPS and HPS lower (a Vakron run: 10:48 instead of 10:02, 7 % lower). Played back, its time runs the same way and ends on the time it was saved with. Entries saved before keep their old time and end at their last hit. [75]
- UHS, the recovery time and the **Buffs** tab in the details still go from the first hit to the last, and **Boss battles only** is unchanged: the time is the pull's. [75]
- A boss fight lists no more players than the table shows (**Settings → DPS meter → Visible rows**); scroll the rest with the mouse wheel over the fight, as in the table. An event or open-world boss with 20 players no longer makes the meter 20 rows taller. This goes for the current pull, the earlier pulls under **Boss fights** and the boss fights of a history entry, and changing the setting changes them at once. Fights with fewer players look as before, and the **Sum** row stays under the players. [75]
- Fixed: when Windows moved its clock while the overlay ran (a time sync, or a change by hand), the overlay timed fights against the wrong clock. The boss bar at the top of the meter could vanish in **All fights** or stay too long, a **Boss battles only** pull could freeze right after it started or too late, a training dummy pull could end at once, and the first seconds after a reset or a switch of **Meter records** were timed wrong. The group frame, the skill monitor and the Hidden Cubes frame went by the wrong time as well (the time left of heals over time and buffs, for example). Changes to the Windows clock no longer affect any of this, the meter's running time included. [75]

**Scenes**
- Each scene has an **Overwrite** button beside **Apply now**. It saves the frames as they are now into that scene, as **Add current scene** would: which are open, folded or closed, where they are and where they take clicks (a scene saved before build 74 then sets those clicks too). The scene keeps its name, its place in the list, its shortcut and the workflows that use it. The first click asks (the button turns into a red **Overwrite?**), the next one overwrites; asking about another scene, or about deleting one, takes the question back. [75]
- A double-click on **Overwrite** or on a scene's trash only asks: a second click within Windows' double-click time doesn't count as the answer, so it takes one more click to overwrite or delete a scene. [75]
- Deleting a scene deletes its shortcut too, and the workflows that use it do nothing until you pick another scene for them; the trash's hover text says so now. [75]
- Fixed: **Add current scene**, clicked during the tutorial, saved the tour's frames as a scene. While the tour runs it does nothing now, and neither does **Overwrite**. [75]

**Digital twins**
- The button that looked you up again (the person with a refresh arrow in the meter's title bar) is now **Update myself** in the twin library, at the left above the list. The line under the buttons says how the lookup goes, as for **Add manually**, or that the overlay doesn't know your character yet, and the lookup shows in the list with its progress bar. The map's name in the meter's title bar has more room now. [75]
- Beside it, **Update myself on overlay start**, off at first (also in **Settings → Digital twin library**): ticked, the overlay looks your character up once each time it starts, as soon as it knows who you play, and a character you switch to later gets one lookup too. Ticked while you play, it looks you up right away. A character already looked up since the overlay started isn't asked for again. [75]
- New in **Settings → Digital twin library**: **Pause between website calls**, how long the overlay waits before each call to the official character website, 0.7 s at first as before, from 0.5 s to 10 s. A lookup takes about 34 calls: about a minute at 0.7 s, and each second more adds about half a minute. A red line under it warns that a shorter pause raises the risk of being blocked by NCSOFT's website. A new pause counts from the next call on, also for lookups already waiting. [75]
- The time a lookup should take now follows that pause, in the details' **Digital twin** tab and under the library's buttons: "about 1 min" at 0.7 s, "about 5 min" at 7 s. Before, it always said about a minute. [75]
- The twin library has a gear in its title line, left of the hand: it opens **Settings → Digital twin library**, as the other frames' gears open their tabs. Like the hand it shows while the overlay has focus, and not while the library is minimized. [75]
- A player's **Digital twin** tab in the details shows skill levels as the game sent them for that player in the fight shown, in italics: live or in a history entry, and for a boss fight opened from its row up to its last blow. The website lists one of a player's three skill setups; the fight shows the one they played, gear and Daevanion included. Where the website's level differs, it's under the skill's name ("Website: 21 (2 from gear)"), and so is a skill the website lists as not equipped ("Website: not equipped"). The skill's hover text gives the numbers at the fight's level and names the fight, and the twin's summary line names it too. [75]
- The game sends a skill's level only with buffs, debuffs, heals and damage over time, shields and passives (a player's passives as soon as they come into sight; with **Boss battles only**, only what it sends during the pull), so skills that only hit or heal keep the website's level, apart from stigmas. Battlefields don't count, as every level reads 20 there, also when the overlay was started inside one. A player without a twin gets a **Skill levels seen in this fight** card. [75]
- A stigma the game sent no level for gets a level range from the fight instead, read from how many of its specializations its hits show unlocked. Stigmas unlock them at levels 5, 10, 15 and 20, and gear and Daevanion never add to a stigma's level, so three unlocked means level 15–19 and four 20+, open as the fifth's level isn't known. Either way it's in italics: if the website's level is inside the range, it stays, with "Fight: 15–19" under the skill's name; otherwise the range shows, with the website's level under the name ("Website: 12"). The hover text names the fight and the range. The ranges go by the hits of what the tab shows (live or a history entry, or a boss fight opened from its row); a level the game did send wins over a range, and without a twin the ranges are on the **Skill levels seen in this fight** card. [75]
- In the twin library, a snapshot's skills, on its tab and in the comparison, show the levels of your last boss fight with that player, in italics, and the comparison's differences and **Biggest differences first** go by them. Stigma ranges come along, from the hits of that fight alone. In the comparison a stigma shows as on its tab: where that's the range (the website's level outside it), it has no difference, and where the reference shows a range the others in that row show their levels. For **Biggest differences first** a range counts as the website's level moved into it (12 against 15–19 as 15). A player whose snapshots sit in more than one column keeps the website's levels there, so you still see what changed between them. Each boss fight or training dummy pull that ends while the overlay runs counts, a wipe too, but not history entries; a later fight replaces a player's levels. [75]
- **Use last battle's skill levels**, on at first, turns these levels and ranges on and off: a check box at the top of the details' **Digital twin** tab and of the library's twin and comparison tabs, and a row in **Settings → Digital twin library**. Off, every skill shows the website's level again. [75]
- The **Specializations seen in this fight** card now leads with the specializations a skill's hits showed; "none" only shows under "also seen" for such a skill, as some hits of a stigma come without them, and there can be more of those. That a stigma's number shows how far its specializations are unlocked is confirmed now: wherever the game also sent the stigma's level, the number matched it, bar a few hits in crowds. [75]
- Fixed: in the library's list, a long character name ran into the class; it ends in "…" now and shows in full on hover. The German heading **Ausrüstungswert** is no longer cut off. [75]

**Settings**
- Every setting and button in the settings window explains itself on hover now: what it does and, for most settings, how it's set at first (**Compact main numbers** and **Target status bar**, for example). That goes for the meter's columns and player names, each frame's rows (also in **Visuals** and **Audio**), the hotkeys, scenes and workflows, **Start the tutorial** and **Update now**. They show on a greyed-out button too, such as **Save recorded packets…**, and on the faded tab of a frame that's off. In the twin library, **Add manually**, **Look up and add** and the cross have hover texts now too. [75]
- Hover texts are a box now instead of one long line across the screen: they wrap at about 380 wide (wider with the UI scale or the frame's **Scale**) and have a little more room around the text. This goes for every hover text of the overlay; those of skills and buffs and the fight graphs' legends keep their own layout. [75]
- The Traditional Chinese interface calls the overlay 覆蓋層 everywhere; some texts, the tutorial's among them, said 浮動視窗 or 覆蓋視窗. [75]

**Settings and files**
- Fixed: in the downloaded version, the overlay's own log (app.log) went into its program folder (current), which every update replaces, so each update deleted it. It now goes into the logs folder beside settings.json, in the folder you start Aion2Heal.exe from (the one with Update.exe), where a packet recording also waits until you save it. [75]
- The skill levels the twin library shows from your fights are kept in seen-levels.txt in the twin-library folder, beside notes.txt: for each player, those of your last boss fight together. [75]

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
