# Emberwake game concept

> Superseded gameplay proposal. The active card-deployment PvP design is [Fleet Duel V2](FLEET-DUEL-V2.md). Keep this file for the original video analysis and historical design context; its joystick, PvE, ad-revive and scheduling rules are no longer active.

Version 1 • 13 October 2026 • Working title

## Product direction

**Steer a tiny pirate ship, outmaneuver a dangerous fleet, and bring home the treasure.** Emberwake is a single-player, portrait mobile naval action game for short sessions. The player controls heading and positioning while side cannons fire automatically when enemies enter a broadside arc. Each three-minute voyage introduces an upgrade choice, a stronger encounter, and a final captain battle. Permanent dock upgrades and visible sail collections give players a reason to return.

Build for Android first, with iOS as the next platform once device performance and retention are validated. This platform order is a planning assumption, not a requirement supplied by the user. The intended audience is casual action players who enjoy readable combat and small progression decisions. The product should feel responsive and approachable, not like a naval simulator.

## What the supplied reference actually shows

Source: user-supplied video at `F:/Woods Assets/Video-54963.mp4`, approximately 45 seconds, 720 × 1280, 30 fps. Original shared post: [Instagram reference](https://www.instagram.com/p/DdyTbthAWBi/). The local video is the evidence used here. The contact sheet samples the visual sequence at approximately three-second intervals; audio was not assessed.

| Observed feature | Evidence from the clip | Design implication |
| --- | --- | --- |
| Portrait battlefield | Entire clip uses a tall playfield | Touch-first navigation and minimal HUD |
| High overhead oblique camera | Decks, sails and both sides of hulls remain readable | Orthographic or near-orthographic Unity camera |
| Small ship facing larger vessels | Clear differences in hull length and sail mass | Strong player versus enemy silhouettes |
| Movement and repeated cannon volleys | Projectile streaks, muzzle smoke and wake trails appear throughout | Positioning and shot anticipation should drive combat |
| Teal sea and volcanic rocks | Muted water, dark angular rocks, warm cracks and pale shore halos | Small material palette and modular environments |
| Floating bars and badges | Health bars, level-like numbers and reward-like icons above enemies | Brief status information can live over ships |
| Sparse environment | Open sea dominates most frames | Preserve reaction space and visual clarity |

The footage does not establish the game title, its actual input scheme, whether targeting is automatic, long-term progression, monetization, multiplayer, or revenue. Those cannot be inferred as facts from this clip. The apparent chase-and-circle combat is the basis for our proposed mechanic, not proof of the reference's control implementation.

The reference is visually effective because a bright small vessel is easy to track against subdued water, large enemies communicate danger immediately, and cannon streaks make attack direction visible. Its visible loop also risks becoming repetitive: extended circling needs meaningful changes in enemy behavior and encounters. Our design adds short voyage structure, telegraphed attacks and upgrade choices to test whether that fixes the problem.

## Identity and pillars

The Ember Shoals are an archipelago of cooled volcanic islands. Independent captains recover cargo while the Ash Fleet patrols the channels. Story is delivered through names, map landmarks and short captain introductions; the MVP needs no dialogue system or character cinematics.

1. **Movement creates the shot.** Turning your broadside toward the enemy is the central skill.
2. **Danger is readable.** Every heavy attack gives an understandable warning and a dodge opportunity.
3. **Small victories accumulate.** A voyage lasts roughly three minutes and always pays some earned progress.
4. **Ships feel collectible.** Hull, sail and wake changes are visible from the actual gameplay camera.

Borrow the reference's overhead naval genre, low-poly simplicity and warm/cool contrast. Use our own ship silhouettes, insignia, UI, level layouts, names and effects. The working title will need availability review before release.

## Controls and combat

Drag a floating joystick in the lower-left region to steer toward a direction. Stick magnitude controls throttle; releasing eases the ship to 35% cruising speed instead of stopping abruptly. Provide left/right control mirroring. A single Gust button briefly increases movement speed and turn response, with an eight-second cooldown. It grants no invulnerability. A short input tutorial teaches steering before any timed danger.

Cannons automatically fire on the nearest visible enemy inside a side arc when that side reloads. Left and right sides have independent timers. Each broadside is three projectiles from three cannon sockets, staggered over 0.18 seconds. No shooting directly through the bow or stern. A faint arc highlight appears only during onboarding or while an eligible target is selected. Projectiles collide with hulls and rocks, not sails. Target selection requires line of sight.

Prototype tuning values, subject to playtests:

| Parameter | Starting value |
| --- | --- |
| World scale | 1 Unity unit = 1 meter; hero hull about 6 units long |
| Player hull HP | 100 |
| Cruise / maximum speed | 4 / 6 units per second |
| Turn rate | 120 degrees per second |
| Broadside aim arc | ±35 degrees around each side normal |
| Cannon range | 16 units |
| Reload | 1.8 seconds per side |
| Projectile damage / speed | 8 HP / 18 units per second |
| Gust | 1.5 seconds at 1.6× movement speed; 8-second cooldown |
| Collision recovery | Slide along terrain; brief speed loss, no terrain damage in MVP |

Enemy attacks announce themselves with a coral outline, striped water overlay, wind-up sound and sail motion. Standard heavy volleys warn for 0.9 seconds; off-screen attacks are not permitted in the MVP. A hit briefly flashes the hull and nudges it without taking steering away. Disable camera shake and haptics independently in settings.

## Three-minute voyage

The voyage ends when the captain sinks, the player sinks, or the hard limit of four minutes is reached. At the time limit the player withdraws with earned coin but receives no captain bonus. A timer-based finale prevents endless circling from becoming the dominant strategy.

| Time target | Experience |
| --- | --- |
| 0–20 seconds | First raider in clear water; learn one clean broadside |
| 20–55 seconds | Two enemies with staggered attacks; recover coins by sailing nearby |
| Around 55 seconds | Pause and choose one of three voyage upgrades |
| 55–110 seconds | Rock cover, a brig and an optional salvage cache |
| Around 110 seconds | Second upgrade choice and small repair pickup |
| 110–180 seconds | Captain arrives; combine dodge and broadside positioning |
| After victory | Result screen, earned coins, blueprint progress, dock upgrade, replay |

The schedule is a target; encounters can finish early. Pause freezes combat during upgrade selection. Starting a voyage is always available; no energy system. If the player sinks, retain 50% of collected coins and already-earned blueprint progress. An optional single revive can be tested later, but the prototype simply offers retry.

Run upgrades reset at voyage end. Start with six choices: +20% cannon damage, −15% reload duration, +20 maximum HP with matching heal, +15% turn rate, one projectile ricochet from terrain, and a 15% slow for two seconds on cannon impact. Each stacks at most twice; reload cannot drop below one second. Avoid generating upgrade combinations that depend on unbuilt systems.

## Enemies and encounter design

| Enemy | Behavior | Counterplay | Art approach |
| --- | --- | --- | --- |
| Raider | Quick approach, tries to expose its side, single cannon shot | Cross its stern and fire | Small one-sail rust-colored hull |
| Broadside brig | Slow turn, three-shot side volleys after warning | Bait volley then turn into an attack | Two-mast charcoal-sailed ship |
| Rammer | Winds up and locks a straight charge | Dodge sideways or use a rock | Raider hull variant with reinforced bow |
| Ash Captain | Larger brig; alternates side volley and fan volley, summons one raider at half HP | Learn the pause between attacks | Brig variant with red pennant and wider silhouette |

Use authored encounter recipes and seeded variation, not unrestricted procedural generation. Each arena has at least two open escape lanes. Never spawn inside the visible player safety radius. Cap simultaneous enemies at eight and normal prototype encounters at three. Prevent overlapping telegraphs from leaving no viable safe region.

The first three mission recipes are open-water tutorial, rock-cover skirmish and captain duel. A later small soft-launch build can offer ten missions using these encounter rules, with explicit new combinations rather than only higher HP. Tropical ruins and cold-water wrecks are possible later biomes, outside the first release test.

## Progression and economy

Use two earned resources: coins for permanent ship improvements and blueprint fragments for alternate hull unlocks. Cosmetics use direct purchase or achievement unlocks; no paid random draws. Avoid a premium currency in the initial test so prices and player decisions stay understandable.

Prototype dock tracks are hull, cannon and handling, each with five levels. Coin costs per track are 100, 180, 300, 480 and 750. Each hull level gives +5% base HP, each cannon level +4% base damage, and each handling level +3% turn rate. These are additive to base, not exponential. A first clear should pay about 120–180 coins including a first-clear bonus; replay payouts target 70–110. Validate first-upgrade timing within the first two voyages. These are tuning proposals, not observed economy data.

The MVP uses only the starter cutter. For soft launch, earn a heavy brig and a fast skiff through mission blueprint milestones; each trades speed, durability and firing shape rather than simply outperforming the previous ship. Keep save versioning from day one. Store voyage rewards in one committed transaction to avoid duplication after a crash or repeated result-screen input.

## Interface and sound

Screens: title/continue, compact dock, voyage selection, combat, paused upgrade choice, result and settings. The dock is initially a simple 3D ship viewer with upgrade buttons, not a town-building game. Combat HUD contains hull HP, voyage objective/time, collected coin, pause, joystick and Gust cooldown. Enemy bars show only while relevant. Reward offers belong on results or recovery screens.

Use 48-dp minimum touch targets, safe-area handling, high-contrast text, symbols plus color, reduced-motion and haptic toggles. Test both 9:16 and taller phone ratios. Music should be a light rhythmic nautical motif; cannon thumps, sail snaps and water wakes carry most moment-to-moment feedback. Duck music under danger cues and limit simultaneous cannon voices.

## Revenue hypothesis and validation

Revenue is a product hypothesis to test, not an outcome established by this video. Begin with optional rewarded ads for one revive per voyage or doubling the earned coin reward, and direct sail/wake cosmetic purchases. Apply a revive with 40% HP and a two-second grace period only after the reward callback; show the exact benefit before the ad. An ad failure must preserve the ordinary retry or reward flow. Never gate basic progression behind an ad.

Launch the first monetization experiment without forced interstitials. Test their addition only if necessary, on natural result-screen breaks, after onboarding, with explicit frequency limits and comparison against the no-interstitial cohort. Do not sell a remove-ads product while the game has only optional rewarded ads unless its replacement benefits are specified clearly. Avoid subscriptions and a battle pass until there is sufficient recurring content.

Measure tutorial completion, first-voyage completion, second-voyage starts, D1/D7 retention, session length, ad offer/accept/complete rates, payer conversion, net revenue by cohort and crash-free sessions. Events: tutorial_step, voyage_start, encounter_end, upgrade_chosen, player_sunk, voyage_end, dock_upgrade, ad_offer, ad_reward, purchase_success. Attach build version, anonymous install ID, voyage ID and experiment group; avoid collecting unnecessary personal data.

Illustrative arithmetic, not a forecast: 1,000 daily active players × 1.5 rewarded opportunities × 0.8 completion/fill factor × $10 realized eCPM ÷ 1,000 = $12 daily ad revenue. Every input must be replaced with actual results by country and platform. Acquisition decisions should use net lifetime revenue after store fees, refunds and operating costs compared with cost per acquired user; downloads alone do not establish profitability.

Internal provisional gates: at least 8 of 10 fresh testers understand steering and broadside positioning without verbal help; at least 6 voluntarily choose another voyage. Before scaling paid acquisition, seek D1 retention of 30% and D7 of 10% in a sufficiently sized cohort and crash-free sessions above 99.5%. These are project decision targets, not claimed industry benchmarks. Review sample sizes and uncertainty; ten testers do not validate commercial retention.

Make three honest gameplay ad concepts once the prototype exists: narrowly escape a volley, use rock cover to beat a larger ship, and show a visible cutter upgrade. Use recorded Unity gameplay for acquisition tests. The AI renders are preproduction references, not evidence of implemented gameplay.

## Scope and delivery gates

| Stage | Deliverable | Exit condition |
| --- | --- | --- |
| Graybox, roughly 1–2 weeks | Movement, camera, one enemy, broadside, obstacle, win/loss | Combat is understandable and replayable with primitives |
| Vertical slice, roughly 2–3 more weeks | One biome, three enemy behaviors, captain variant, voyage upgrades, local save, final-style cutter | Complete three-minute loop on Android with stable performance |
| Small soft-launch build, roughly 3–5 more weeks | Ten mission recipes, progression, optional ads, purchase handling, analytics, audio, device QA | Cohort instrumentation and reward correctness verified |
| Iteration, roughly 2–4 more weeks | Balance, onboarding and monetization experiments | Evidence supports expansion or a deliberate redesign |

Planning estimate: 8–14 weeks for an experienced Unity developer with periodic art/audio help, assuming generated meshes can be cleaned efficiently. This is not a fixed delivery promise. Store setup, device availability, art rework and SDK integration can change the schedule.

Explicitly outside MVP: multiplayer, open-world travel, realistic buoyancy, boarding combat, crew simulation, town building, procedural oceans, cloud account systems and live-service seasons. Build the smallest attractive loop before producing an entire fleet.

## Main risks and responses

Combat may collapse into circling: test side-arc positioning, mixed enemies and terrain before adding content. Tripo ships may have fused sails or poor hidden geometry: generate one hero and validate the full import workflow before ordering the fleet. Beautiful renders may overpromise the mobile result: compare actual Unity captures at phone scale against the art rules. Monetization may undermine retention: keep a no-ad control cohort. Device cost may come from water transparency and particles rather than mesh counts: profile both CPU and GPU on target hardware.
