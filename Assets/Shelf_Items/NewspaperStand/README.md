# Newspaper stand

NewspaperStand_Stocked.prefab contains ten distinct flat SpriteRenderer items. NewspaperStand_Empty.prefab has the stock group disabled; enable or replace its items as needed. Front faces local -Z, pivot at floor level, approximately 1.82m wide x 1.76m high x 0.68m deep. Drag the prefab into the scene. Uses the existing Pixel_Charcoal, Pixel_Slate and Pixel_Cream shelf palette, with separate frame, shelves and sign children.

Individual cover sprites, item definitions, materials, prefabs and the NewspapersCatalog live in Assets/Packages/How2ShopAssets/items/Newspapers. Prices are editable example game prices. Items use the existing ItemDefinition and ItemSpriteView system. No billboard behavior or added item physics.

Rebuild: Tools > Shelf Items > Build newspaper stand. This regenerates the stand and press assets; duplicate customized versions first. Preview.png and Validation.txt record the generated output. Existing scenes are unchanged.
