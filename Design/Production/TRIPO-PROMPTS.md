# Emberwake Tripo asset prompts

> V1 prompt archive. The original Cutter remains relevant; use [V2 production class specifications](FLEET-DUEL-PRODUCTION-V2.md) for the active fleet, scale, team colors and new asset prompts. Do not produce the former PvE boss fleet from this list by default.

These are proposed model-generation prompts, not completed Tripo jobs. Generate each asset independently. For image-to-model, use a clean isolated asset render; the mood board is a human style reference, not an appropriate single-model input. The hero image is available at ../Art/player-cutter-v1.png.

## Shared style suffix

Append this to each model prompt: Stylized low-poly mobile game prop, broad clean faceted surfaces, matte flat colors, chunky readable silhouette, limited material palette, no realistic surface noise, no tiny decorative detail, complete object from every side. No environment, water, wake, base, text, UI, lighting effects or floating particles. Clean separable functional parts where possible. Final topology and texture budget will be validated and cleaned after generation.

## Player cutter

Use the isolated player-cutter-v1.png as the design input. Compact pirate cutter, warm walnut wooden hull, amber timber rim, one main mast carrying two vertically stacked oversized ivory square sails with muted teal edge bands. Small teal pennant above. Flat amber diamond insignia on lower sail. Short bowsprit. Three simple black cannon muzzles on EACH side, six total. Broad stern deck, no crew, no intricate ropes or rail spindles. Complete lower hull and keel. Keep sails separate from hull if possible. Target final proportions: hull length 6, beam 2.4, waterline-to-mast height 5.5. Reference image controls silhouette; the diamond emblem should remain flat, not raised sculptural geometry.

## Raider

Small agile pirate raider for the same low-poly fleet. Narrow warm brown hull, one short mast, one large rust-red triangular sail, tiny dark pennant. One black cannon muzzle per side. Short simple bow, clean raised stern, no skull symbols or crew. Visually smaller and lighter than the ivory-sailed player cutter. Complete hull, no water. Approximate hull length 4.8 units. Prioritize the single triangular sail silhouette when viewed from high above.

## Broadside brig

Heavy compact pirate brig for the same low-poly fleet. Broad dark walnut hull, two clearly separate masts, oversized charcoal square sails, small rust-red pennants, restrained amber timber accents. Three cannon muzzles per side. Broad stern and short bowsprit. Bold chunky geometry and simple matte materials. No fine ropes, people or intricate carvings. Approximate hull length 8.5 units and beam 3.3 units. Complete lower hull. Must read as heavier than the single-mast ivory cutter without becoming a photoreal ship.

## Basalt ridge

One isolated narrow cluster of five to seven large angular basalt rocks, low-poly volcanic island prop. Dark charcoal facets, sparse narrow orange fissures integrated into the rock surface, no bright glow baked across entire mesh. Low jagged ridge silhouette with one taller spire. Complete geometry to below expected waterline. No ocean, sand plane, pedestal, vegetation, smoke or lava particles. Approximate footprint 9 by 4 units. Shore and water effects will be authored separately.

## Basalt rounded cluster

One isolated compact rounded arrangement of angular charcoal basalt blocks, stylized low-poly volcanic island prop. One squat central mass, three smaller shoulder rocks, subtle orange cracks on one side only. Distinct broad silhouette, not a copy of the narrow ridge. Complete geometry below waterline. No ocean, base, sand platform or effects. Approximate footprint 6 by 6 units.

## Basalt twin spires

One isolated small pair of irregular angular charcoal basalt spires with two low rocks at their foot. One spire clearly taller than the other. Minimal orange fissure detail, mobile low-poly prop. Do not create an arch, bridge or separate ground slab. Complete rock geometry. No sea or sand. Approximate footprint 4 by 3 units.

## Reward chest

One chunky low-poly nautical treasure chest, warm walnut panels, simple amber metal bands and a large readable central latch. Closed lid, broad proportions, matte colors. No coins spilling out, no pedestal, no text, no skull, no environment. Suitable for reading as a tiny pickup from above. Approximately 1 unit wide. Separate lid optional for later animation.

## Authored variants and effects

Rammer: modify the raider with a blunt dark metal bow shield and one extra red stripe. Captain: modify the brig with a larger red pennant and simple amber prow ornament; do not regenerate a completely unrelated fleet style. Crate, barrel, dock modules, warning shapes, cannons, wakes and HUD icons can be built simply in DCC/Unity where that gives cleaner control than generation.

## Handoff record per asset

Record asset ID, source image version, exact prompt, generation date and ID, raw output path, cleaned FBX path, triangle counts, texture sizes, material count, axis/pivot check and reviewer notes. Keep each final mesh traceable to its art reference. Never treat a requested triangle number in a text prompt as a verified output count.
