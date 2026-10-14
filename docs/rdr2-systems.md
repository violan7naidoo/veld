# What makes a game like Red Dead Redemption 2 feel the way it does, and what Veld does about it

This is a design catalogue, written from playing and reading about the game, not from its files.
Nothing in Veld is extracted from RDR2: its archives, models, animations, audio and physics data are
Rockstar's copyrighted work and may not be copied into another game. Game *mechanics* are not
protected, so every system below is recreated as original code and original placeholder art.

Status: **done** = in the game now, **partly** = a simple version exists, **next** = a clear plan,
**later** = big job.

## Movement, camera and feel

| System | What RDR2 does | Veld | Where |
| --- | --- | --- | --- |
| Third-person camera | Over-the-shoulder, lags behind, pulls in to aim, FOV narrows | done | `CameraRig.cs` |
| Weighty movement | Acceleration and deceleration, turning takes time, run/sprint | done | `Player.Walk` |
| Dead Eye | Slow motion while aiming, a meter that drains and refills | done (Q while aiming) | `Player.SetDeadEye`, `DayNight.DeadEye` |
| Foliage reacts | Grass and bushes bend as you walk through | done for grass | `grass.gdshader` bend uniforms |
| Ragdoll | Bodies fall physically when shot | partly: posed fall | `HumanoidModel.SetDead` |
| Cinematic camera | Letterbox auto-camera on long rides | next: a toggle that orbits the horse | `CameraRig` |
| Dive, cover, melee | Context moves | later | |

## Horse

| System | RDR2 | Veld | Where |
| --- | --- | --- | --- |
| Stamina core | Gallop drains, trot restores | done | `Horse.Stamina` |
| Lean into turns | Body rolls, rider follows | done | `Horse.Animate` `_lean` |
| Whistle and come | From a distance that grows with bonding | partly (fixed 450 m) | `Horse.Whistle` |
| Bonding | Rises with riding, feeding, brushing; unlocks moves | next: bonding level from riding time, whistle range and stamina scale with it | `Horse` |
| Spooks | Rears at gunfire and predators | partly (runs off when shot at) | `Horse.Spook` |
| Dust, hoofbeats | Dust at a gallop, surface-based sounds | done (dust), partly (one hoof sound) | `Horse` |
| Horse health, death | | later | |

## Weapons and combat

| System | RDR2 | Veld | Where |
| --- | --- | --- | --- |
| Hitscan with spread, hip vs aimed | | done | `Weapon.Fire` |
| Bolt action, one shot per click; sidearm fires as fast as you click | | done | `Player.TryShoot` |
| Recoil, camera kick | | done | `Player.TryShoot` |
| Reload with timing | | done | `Weapon.StartReload` |
| Weapon degradation, cleaning | Dirty guns lose accuracy | next: a condition value per weapon that widens spread | `Weapon` |
| Ammo types, buying ammo | | done (shop dialogue) | `DialogueSystem` give_ammo |
| NPC gunfights: cover, flanking | | partly: approach, keep distance, shoot with faction accuracy | `Npc.Shoot` |
| Wanted, lawmen, bounty | Witnesses, heat, pay off | done (witness radius, decay, pay the fine) | `WantedSystem` |
| Honor | Choices shift a meter and change how people treat you | partly: Reputation affects burghers | `Game.Reputation` |

## World

| System | RDR2 | Veld | Where |
| --- | --- | --- | --- |
| Day and night | Sun, moon, stars, lamps that light at dusk | done | `DayNight` |
| Dynamic weather | Clear to storm, wet ground, thunder, wind in the grass | done (5 states, rain, wet terrain, thunder, wind) | `Weather.cs` |
| Volumetric light, fog, clouds | | done | `DayNight` |
| Map and minimap, waypoints | Full map, heading-up minimap, set a waypoint | done (M, click to mark) | `MapImage`, `Minimap`, `WorldMap` |
| Regions and place names | "You are in..." | done (nearest road and suburb) | `MapImage.LocationName` |
| Towns with daily life | Schedules, shops that open and close | partly: wander and patrol | `Npc` |
| NPC daily schedules | Home at night, work by day | next: a home spot per NPC, go there after dark | `Npc` |
| Wildlife and hunting | Animals with AI, tracking, skinning | later; springbok/meerkats in the Karoo, dogs and birds in Chatsworth | |
| Trains and coaches | Ride them, rob them | partly (a train that runs the loop) | `Railway.cs` |
| Vehicles (modern map) | | done: car and minibus taxi on VehicleBody3D, enter/exit on E, chase camera, headlights, horn, hit-and-run is a crime | `Vehicle.cs` |
| Camp, sleeping, eating | Cores to feed | partly (hotel room heals and sleeps) | dialogue actions |
| Fishing, poker, dominoes | | later | |
| Save anywhere, autosave | | done (save), next (autosave on mission complete) | `Game.Save` |

## Presentation

| System | RDR2 | Veld | Where |
| --- | --- | --- | --- |
| Character models and animation | Motion-captured | placeholder capsules with a walk, aim, ride and death pose | `HumanoidModel` |
| Facial animation, lip sync | | later; needs real models | |
| Voice acting | | later; dialogue is text | |
| Music that reacts to action | | next: a quiet guitar/bow loop by day, tension layer when hostile | `Sfx` |
| Cinematic missions | Scripted set pieces | partly: data-driven steps | `MissionManager` |

## Suggested order for the next pieces

1. Drivable car for the Chatsworth map (biggest change in how it feels to move around).
2. Horse bonding and the cinematic ride camera (the Driefontein map's heart).
3. NPC schedules so towns feel alive at different hours.
4. Weapon condition.
5. Wildlife, starting with harmless animals that run from you.
