# Veld: setting and design notes

## The pitch

A frontier game in the mould of the big American open-world Westerns, but on the veld: a drifter with
a Mauser arrives by train in a small Karoo dorp under British occupation in 1901, during the guerrilla
phase of the Second Boer War, and takes work from whoever pays. The player is nobody's soldier. Both
armies, the burghers, the townsfolk and the people who worked the land before any of them are all
present and all have reasons of their own.

## Why 1901

The war has two halves. From October 1899 to mid 1900 it is a conventional war of sieges and set-piece
battles. From then until May 1902 it is a guerrilla war: Boer commandos raid the railways, and the
British answer with blockhouse lines along the track, mounted columns, farm burning and the camps.
That second half is the open-world half: small garrisons in small towns, patrols, deserters turned
bandits, a railway that matters, and a countryside nobody fully controls. The prototype's clock says
April 1901 so that blockhouses, armoured trains, martial law in the Cape border districts and the camps
can all exist at once.

## What is real and what is invented

Real: the Cape Government Railways, blockhouses of corrugated iron on stone bases (the "Rice pattern"),
armoured trains, Mauser Model 1896 rifles on the Boer side and Lee-Enfields on the British, the
Webley revolver, the oath of neutrality and the word *hensopper* for those who took it, martial law,
Indian traders in the dorps, Basotho transport riders, the concentration camp at Norvalspont, and
pre-decimal money.

Invented: Driefontein (there are several real Driefonteins; this one is nowhere in particular),
Welgevonden farm, every person in the game, the 2nd Battalion of nothing in particular, and the exact
geography. The map is procedural and the dorp is laid out for play, not surveyed.

## Tone and the things to handle with care

This is a war that killed tens of thousands of civilians in the camps, most of them children, Boer and
Black South African alike. The prototype does not depict the camps; it mentions them once, through
Annie Steyn, whose mother is at Norvalspont, because pretending they did not exist would be worse.
If you build the game out, treat that subject the way a good historical novel would: present, serious,
never a mission objective or a set piece.

Black South Africans were everywhere in this war, as scouts, drivers, labourers, armed auxiliaries on
both sides and as farm workers who lost everything when farms burned, and they were written out of
most of the histories for a century. The prototype has one named Black character with his own voice
(Thabo Mokoena, transport rider) and a herdsman killed off-screen by the Bosmans. That is thin. Add
more, give them agency and their own missions, and do not make them scenery.

Boers and British are both playable employers. Neither is written as the villain. The outlaws are the
villains because that is what a bounty mission needs.

Language: the dialogue is in English with Afrikaans and Dutch words where a character would use them
(*dorp*, *kopje*, *oom*, *dominee*, *hensopper*). Signs are bilingual where the real ones were.

## The Chatsworth map

The second map is a real place: Montford, Unit 7 of Chatsworth, Durban, today. It exists because
the owner of this project grew up with it and wanted to play in his own streets. That changes the
rules of tone. Driefontein is invented and 125 years away; Montford is where real people live now.

- Streets, fields, schools, the stadium, the police station and the shopping centre are real and keep
  their names. They are public places.
- Every character is invented. No resident, shopkeeper, teacher, coach or police officer is a real
  person, and nothing in the game should imply one is.
- Shops get generic signs (SUPERETTE, HAIR SALON) even where OpenStreetMap records a trading name.
  Real small businesses did not sign up to be in a game.
- The missions are small-town stories, not crime-genre caricature. The thieves are thieves; they are
  not a comment on anyone. Keep it that way if you add more.
- Chatsworth was built under the Group Areas Act in the 1960s for people forcibly moved from places
  like Cato Manor. Uncle Raj says so in two sentences. That history is the community's to tell; the
  game should acknowledge it, not perform it.

## Pillars for a full game

1. **The line.** The railway is the spine: travel, missions, the train as a moving set piece, blockhouses
   as checkpoints with sentries who remember you.
2. **Two masters.** Reputation with the British garrison and with the burghers pull against each other.
   Doing well for one costs you with the other; the magistrate sits in the middle.
3. **The veld itself.** Distance, water, horses and weather. Riding should feel like the point, not a
   loading screen.
4. **Small places, real people.** A dorp of twenty named characters you can learn, not a city of
   thousands you cannot.

## Systems reference

- **Money** is pence (240 to the pound, 12 to the shilling). `Money.Format` prints `£2 3s 6d`.
- **Reputation** is a single integer in `Game`. Below -2 the burghers turn hostile. Harming civilians
  lowers it.
- **Wanted** heat rises when you attack soldiers, or when a soldier sees you attack a civilian. Levels
  1 to 3. It decays after six seconds out of sight of any soldier. The magistrate clears it for £5.
- **Factions**: civilian, british, boer, outlaw. Outlaws are always hostile; soldiers while wanted;
  burghers while reputation is low; anyone armed you shoot at.
- **Time**: one day is 720 seconds. `T` skips two hours; the hotel sleeps eight.
