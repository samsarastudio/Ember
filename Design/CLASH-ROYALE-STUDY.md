# Clash Royale flow study for Emberwake

Research checked 13 October 2026. Reference confirmed by the user: Clash Royale, specifically cards, regenerating Elixir and deployable units. This is a study of official public documentation and its application to Emberwake, not a claim to have played or inspected every current app screen.

## The decision

Emberwake becomes a 1v1 naval card-deployment game. Its primary player action is choosing what to deploy, where and when. The previous directly steered survival voyage is superseded. Keep the original video's small stylized ships and ocean presentation, but use the preparation, deployment, counterplay and replay loop of Clash Royale.

The user clarified that three minutes was a suggestion. Start with three minutes of regulation plus up to two minutes of overtime; test pacing after the first playable version. This is a duel, not a last-player-standing battle royale.

## What the reference flow does

| Stage | Reference behavior | Why it matters | Emberwake application |
| --- | --- | --- | --- |
| Prepare | Assemble a deck from a persistent card collection | Commit to strengths and counters before the fight | Eight fleet/order cards, four-card hand |
| Start | Battle button searches for an opponent | Minimal friction between intention and action | One prominent Battle action from Harbor |
| Match | Trophy Road pairs players by similar trophies; card levels are not part of that matching rule | Competitive context and persistent progression interact | Begin with equal-level tests; tune matching separately |
| Opening | Resources limit early deployment | Information gathering and commitment decisions | Observe enemy class and lane before spending all Supply |
| Exchange | Units act after deployment; counters and placement determine trades | Players manage resource efficiency and timing | Tank, swarm, splash, siege and support ship roles |
| Cycle | A played card returns after four more cards are played | The opponent's available counters change over time | Ordered draw queue and visible next card |
| Escalation | Resource generation accelerates as time passes | Raises decision frequency near the end | 1×, then 2×, then late-overtime 3× Supply |
| Result | Trophy and reward progression follows battle | Outcomes connect to the next session | Rating, gold, blueprint and season progress summary |
| Repeat | Play Again can start matchmaking directly after a battle | Avoids an unnecessary home-screen detour | Rematch queue without mandatory shop or crate animation |

The documented matchmaking, resource-trade and card-cycle rules are in [1v1 Battle](https://support.supercell.com/clash-royale/en/articles/1v1-battle-2.html). Deck collection, upgrades and special slot systems are described in [Cards & Decks](https://support.supercell.com/clash-royale/en/articles/cards-and-decks-6.html). The eight-card/four-hand structure is the standard duel baseline we are adopting; the detailed naval rules are specified independently in the v2 concept.

## Timing and ending

Supercell's current Elixir page specifies 1× generation during minutes one and two, 2× during minute three and minute four, and 3× in minute five. The 2019 rule update introduced a two-minute overtime period; it is useful historical context and agrees with that current timing table. Do not describe every match as exactly three minutes. [Current Elixir rules](https://support.supercell.com/clash-royale/en/articles/elixir.html), [November 2019 rule change](https://supercell.com/en/games/clashroyale/blog/release-notes/november-update/).

For Emberwake, winning by destroying the central headquarters ends a match early. Otherwise count destroyed outposts at regulation end, use sudden death if tied, and resolve the remaining tie after overtime. Exact simultaneous-destruction and timer-boundary rules must be implemented, not left to animation timing. The naval concept below defines those rules.

## The current reward loop differs from old guides

Clash Royale removed chest queues, chest timers and chest keys in its 31 March 2025 update. That update also added a direct Play Again action. An old four-slot chest-unlock screen would therefore be a historical reference, not an accurate reconstruction of the current core loop. [April update, published 31 March 2025](https://supercell.com/en/games/clashroyale/blog/release-notes/april-update).

The current support page describes Magic Lucky Chests for the first four daily wins, arena-dependent gold for a limited number of wins, and bonus Crowns for the first three battles. Catch-up rewards are explicitly described as a test that may vary between players. Apply the general lesson—immediate battle rewards and visible daily goals—without assuming every account has an identical screen or reward table. [Daily Battle Rewards](https://support.supercell.com/clash-royale/en/articles/daily-battle-rewards-3.html).

Lucky Chests use rarity upgrades and rewards that vary with progression. For Emberwake's prototype, use deterministic blueprint rewards first so we can measure the core economy without random rewards obscuring problems. Random reward presentation is a later commercial design choice, not necessary to validate fleet combat. [Lucky Chests & Boxes](https://support.supercell.com/clash-royale/en/articles/lucky-chests-6.html).

## Progression and collection

Trophy Road ties 1v1 performance to milestones and arena thresholds. Trophy changes depend on the rating difference between players, and arena gates protect certain progression boundaries. This provides both short-term competition and a longer unlock journey. [Trophy Road](https://support.supercell.com/clash-royale/en/articles/trophy-road-5.html).

Cards improve through collection and upgrades; the current documentation includes special Evolution, Hero and Wild slots. These increase the size of the design and balance space. Emberwake should first validate normal unit and order cards, then introduce one advanced mechanic at a time. A small launch fleet cannot support the same number of interacting progression layers as a mature game. [Cards & Decks](https://support.supercell.com/clash-royale/en/articles/cards-and-decks-6.html).

Gold and Gems serve distinct progression and purchase purposes. Crowns connect battle performance to Pass progress, including credit associated with destroyed and surviving towers. Preserve distinct meanings for our rank, spendable currency and seasonal progress; do not turn them into one confusing counter. [Gems & Gold](https://support.supercell.com/clash-royale/en/articles/gems-and-gold-8.html), [Crowns](https://support.supercell.com/clash-royale/en/articles/crowns-3.html).

## Season, shop and social return loops

The reference's pass system contains free and paid reward tracks. Its March 2026 changes added reward choices and revised Hero access, illustrating that live progression is continually changed. We should use a configurable reward track rather than hard-code a copy of one month's offers. [March 2026 update](https://supercell.com/en/games/clashroyale/blog/release-notes/march-update-2026/).

Friendly Battles allow direct challenges, with tournament-style level treatment and no standard resource rewards. Training and Practice modes offer bot opponents without ordinary reward changes; Practice can reproduce a previous opponent's deck. These are useful learning loops rather than additional monetization surfaces. [Friendly Battles](https://support.supercell.com/clash-royale/en/articles/friendly-battles-3.html), [Training and Practice](https://support.supercell.com/clash-royale/en/articles/training-camp-practice-mode.html).

The full product surface also includes clans, donations, trading, clan wars, events, ranked play, cosmetics and account features. These are mapped as later modules, not quietly implied to exist in an MVP. The official [support index](https://support.supercell.com/clash-royale/en/game/index.html) identifies those systems. October 2026 introduces more Hero, Evolution and event content; copying all of this at once would turn a prototype into a substantial live-service production. [October 2026 update](https://support.supercell.com/clash-royale/en/articles/watts-new-october-2026.html).

## What makes the duel worth repeating

The following are design interpretations, not measured causal claims about Supercell's business results:

- A loss can suggest a specific next adjustment: save a counter, change a card or switch lanes.
- The deck gives identity while the rotating hand prevents repeating one strongest action constantly.
- Regenerating resources create tension between acting now and building a supported push.
- Surviving defenders can become attackers, so a good defense has a visible payoff.
- A late resource increase changes the rhythm without introducing a completely new control scheme.
- Clear results and a direct replay action make another attempt easy.
- Collection and visible progress provide a reason to return after the immediate combat session.

Measure voluntary rematches, understanding of losses, enjoyment, frustration and retention. More time spent is not automatically evidence of a better game, and no mechanic guarantees revenue.

## Fidelity boundary

Match the strategic structure and end-to-end flow closely: deck, hand, energy, deployment, autonomous combat, two channels, defended objectives, escalation, result, progression and replay. Use our own naval art, terminology, audio, UI composition and class tuning. No Supercell characters, logos, card illustrations or copied screen assets are part of this project.

The prototype deliberately omits advanced heroes/evolutions, secondary game modes, clan wars and a complex rotating store. This is a staged implementation of the reference flow, not a claim that our first build has feature parity with all of Clash Royale.
