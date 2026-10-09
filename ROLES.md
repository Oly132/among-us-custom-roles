# Role guide

This guide covers every custom role in this mod. **The current public installer, v0.15.0, includes the first ten roles below. Thief is implemented in the local development version and is not included in that installer yet.**

Every player needs the mod enabled and a compatible matching version. Download the installer from [Releases](https://github.com/Oly132/among-us-custom-roles/releases/latest). Steam Deck support is experimental; Android support is deferred.

## Roles at a glance

| Role | Team | Main ability | Win condition |
| --- | --- | --- | --- |
| [Forger](#forger) | Impostor | Change the location evidence a Detective receives | Impostor victory |
| [Puppeteer](#puppeteer) | Impostor | Remotely control a crewmate and kill through them | Impostor victory |
| [Faker](#faker) | Impostor | Complete one convincing visual task | Impostor victory |
| [Silencer](#silencer) | Impostor | Block a player's chat, outgoing voice and emergency meetings | Impostor victory |
| [Cannibal](#cannibal) | Evil neutral | Eat players, leaving scent instead of a normal body | Remain alive with at most one other survivor |
| [The Fat Guy](#the-fat-guy) | Crewmate | Survive one ordinary impostor stabbing | Crewmate victory |
| [Dog Owner](#dog-owner) | Crewmate | Send a dog to investigate recent deaths | Crewmate victory |
| [Terrorist](#terrorist) | Impostor | Explode and kill nearby players | Impostor victory |
| [Binocular](#binocular) | Crewmate | Use a movable binocular view from anywhere | Crewmate victory |
| [Evil Engineer](#evil-engineer) | Impostor | Travel through a connected network of vents | Impostor victory |
| [Thief — upcoming](#thief--upcoming) | Crewmate initially | Steal another player's role once | Changes to the stolen role's win condition |

## Choosing roles in the lobby

Use the normal role selector. Set each custom role's **number of players** and **assignment chance** on the screen showing all roles. Open the individual role's settings to change its abilities.

Each custom role currently supports zero or one assigned player: zero disables it. A 100% chance still depends on available team slots; enabling more special roles than available slots cannot give everyone every role. Settings below are defaults and can be changed where listed.

Crewmate roles do normal tasks and win with the crew. Impostor roles retain ordinary impostor abilities unless an active ability explicitly restricts them. Cannibal has its own victory condition.

## Forger

After killing someone, press **CHANGE EVIDENCE** and choose a room on the current map. The Detective receives that false location when questioning you about that kill. You must change the evidence before the allowed window expires and before it becomes unavailable through a report.

The map picker uses the rooms on the current map. Forging changes the investigation evidence; it does not move the victim or their body.

**Settings:** 20-second forgery cooldown, 3 forgeries per game, 10-second window after a kill.

## Puppeteer

Open the player tablet and choose a living crewmate to possess remotely. You control their movement and can kill through their body, making it look as though that crewmate killed the victim. The controlled player does not learn your identity. Their task closes if they were doing one, and their own movement input is blocked during possession.

Your real body stays in its original location, stops walking and shivers together with its clothing. You cannot sabotage while possessing someone. Use **RETURN** to end possession early, or wait for the duration to expire. F opens or closes the selection tablet.

Dead players, impostors and players inside vents cannot be selected. Dead player cards show the voting-style cross.

**Settings:** 10-second possession duration, 30-second cooldown, 3 possessions per game.

## Faker

The fake-task button stays on screen and lights up when you are close enough to a supported visual task. Press it to open and actually perform that task:

- **MedBay scan:** undergo the scan.
- **Weapons / asteroids:** shoot the asteroids in the task interface.
- **Storage trash ejection:** open the task and pull the lever.

Other players see the native visual animation, as though a crewmate completed the task. Tasks must exist on the current map. You can complete **only one of these fake tasks in the whole game**. Closing the task before completion keeps your use; finishing consumes it.

**Settings:** no extra ability settings; one completed fake task per game.

## Silencer

Silence a nearby living crewmate. From that moment, they cannot send in-game chat, transmit voice through the supported **Silencer CrewLink** copy, or call an emergency meeting. They can still hear everyone else and can still report bodies.

The effect lasts through the next meeting, then for another **10 seconds after that meeting ends**. The remaining seconds count down on screen. You cannot silence the same player twice in one game, even after their first silence expires.

**Settings:** 30-second cooldown and 3 silences per game.

**Voice setup:** use the separate Silencer CrewLink installed by the PC installer for the voice integration. Ordinary BetterCrewLink alone does not enforce this role's outgoing voice mute. Hearing others remains enabled while silenced.

## Cannibal

An independent evil neutral who can **EAT** nearby players from either team. A normal eaten victim leaves scent and no ordinary reportable body. Eating The Fat Guy leaves half a body instead.

The Cannibal wins when they are alive and **at most one other living player remains**. Other players may have been killed or voted out; the Cannibal does not have to personally eat everyone. They do not share the crew's or impostors' victory. EAT is their attack, and they do not have ordinary impostor sabotage abilities.

**Settings:** 30-second eat cooldown. There is a 10-second initial delay before eating.

## The Fat Guy

A passive crewmate who survives the **first ordinary impostor stabbing** against them each game. They receive no notification. The attacker spends that kill and must wait through their kill cooldown again. A subsequent ordinary stabbing kills normally.

Cannibal eating and Viper killing bypass that protection and leave **half a body**. A Viper kill leaves the native saliva/acid on the remains. A Terrorist explosion also bypasses the stabbing protection.

If a Dog Owner investigates a Cannibal-killed Fat Guy's half body, the findings reveal the Cannibal's exact identity. A Viper-killed half body reveals the cause, but not the Viper's identity.

**Settings:** no extra ability settings; one protected stabbing per game.

## Dog Owner

Unleash a dog **twice per game**. It appears with a poof and runs to the nearest Cannibal or Viper death trace from the current round, even if the ordinary body has disappeared. These traces take priority over normal bodies. If none exist, it investigates the closest remaining normal body from that round.

A round is the period between meetings. Previous-round deaths are not investigated.

Only the owner receives the findings:

- The victim's **player name and role**.
- How they died and the room where they died.
- The **three closest living players at the moment of death**, without distances. The killer can be among them, but is not guaranteed to be. Fewer names appear if fewer players were available.

For a Cannibal-killed Fat Guy's half body, the dog also reveals the Cannibal's exact identity. Ordinary Cannibal scent and Viper remains do not identify the killer.

After delivering the findings, the dog disappears with a poof. If there is nothing eligible to investigate, it appears and sinks into the floor; that use is still spent.

**Setting:** **Dog visible to everyone**, enabled by default. Disable it to show the dog only to its owner. Findings stay private in either mode. The two-use limit is fixed.

## Terrorist

You can kill normally or use your one explosion. Exploding kills nearby living players within the game's **short kill distance**, including impostor teammates. Targets must be in line of sight and outside vents.

If the explosion eliminates everyone else, the impostors win immediately. Otherwise, you die too, and surviving players continue under the usual win conditions.

**Settings:** no extra ability settings; one explosion per game.

## Binocular

Open the native Fungle-style binocular view from anywhere, including on other maps. Move the view to look around the map. Your character remains where you opened it and can still be attacked.

The view closes when its duration ends, or you can close it early with F. You then wait for the cooldown before opening it again.

**Settings:** 10-second viewing duration and 30-second cooldown.

## Evil Engineer

Use the native vent arrows to travel through a connected vent network across the map. Every vent has a possible route to every other vent, with intermediate stops. This does not give each vent a direct connection to every other vent; each has at most three neighbors.

You retain normal impostor killing and sabotage abilities.

**Settings:** no extra ability settings.

## Thief — upcoming

**Availability: local development version v0.15.1; not in the public v0.15.0 installer.**

Start as a crewmate. Walk up to a living, visible player outside a vent and press **STEAL**. This can be used **once per game**, within normal kill targeting range and line of sight.

You receive their native or custom role, abilities and team. They become a plain crewmate. For example, if a Thief steals an impostor's role, the Thief becomes an impostor and the former impostor becomes a crewmate. Stealing Cannibal makes you an independent Cannibal. Stealing plain Crewmate consumes your use and makes you a plain crewmate.

The stolen custom role keeps its remaining ability uses and cooldowns rather than refilling them. Active abilities are stopped during the transfer. Your win condition becomes that of the stolen role.

**Settings:** no extra ability settings; one steal per game.

## Native roles and interactions

The game's vanilla roles remain available through their normal role settings. Two native roles are particularly relevant to this pack:

- **Detective:** questions players about locations associated with a death. Forger can falsify the evidence the Detective receives.
- **Viper:** can make victims' bodies disappear. Dog Owner can investigate the remaining death trace; The Fat Guy instead leaves a half body with Viper saliva/acid.

These are native Among Us roles, not additional roles created by this mod.

## Release status

This page describes the intended role rules implemented by the mod. Check [release notes](https://github.com/Oly132/among-us-custom-roles/releases/latest) for known issues and testing limitations. In particular, multiplayer death/ghost synchronization is still under investigation in v0.15.0; the guide does not imply every interaction has been verified in a live multiplayer match.

Updating this guide does not trigger an automatic game update. Players are notified when a newer stable release with an update manifest is published.
