# Colour-editable supermarket items

Drag an item from this folder's **Prefabs** into the scene. Select its named material in **Materials**, then change **Packaging colour** to any colour. This replaces the selected packaging colour while retaining its shading. It does not simply multiply the original red by green.

Every item also has **Green**, **Pink** and **Red** preset materials in **Materials/Presets**. Drag the matching item's preset onto its SpriteRenderer. Use `Cola_Green` for Cola, `Water_Pink` for Water, and so on: each material includes that item's artwork.

Duplicate a material to give two copies of the same item different colours. Keep the SpriteRenderer's own Color white for the intended result. A material affects every renderer using it.

Neutral labels, silver lids and dark outlines are protected. The source colour is selected automatically from each item's dominant coloured pixels; neutral bags therefore change their coloured labels or trim. Pixels elsewhere with the same source hue can also change. Adjust **Original packaging colour (selection)** and **Colour selection width** to choose another colour region or include more shading. **Recolour amount** blends back to the original. White, grey and black replacement colours are supported.

All 20 sprites, 20 item definitions, 20 prefabs, the independent catalog, shader, 20 editable materials and 60 colour presets are contained here. Original sprite resolution/import settings, prefab scale/pivot and ItemSpriteView behaviour are retained from `Assets/_How2ShopAssets/items`; those originals are unchanged. Existing scene objects can use the matching new material directly, or be replaced with a new prefab.

The shader targets URP and is unlit like the source item sprites. These prefabs use individual textures; do not atlas-pack them without adapting the explicit artwork UV mapping. Use transform rotation/scale for orientation.

`Preview/ColaColours.png`: original, green, pink and red Cola. `Preview/AllItemColours.png`: pairs of original and pink versions for all items, alphabetically ordered. `Validation.txt` records Unity checks.

Rebuild with **Tools > Shelf Items > Build colour editable copies**. Existing material Packaging colour edits are retained; generated prefabs and copied item references are rebuilt.
