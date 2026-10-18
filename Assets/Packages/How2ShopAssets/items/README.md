# Supermarket shelf items

Drag any of the **20 prefabs in `Assets/items/Prefabs`** into your scene. Each uses the same `SpriteRenderer` + `ItemSpriteView` + `ItemDefinition` setup as `Assets/Prefabs/Milk1L`.

The pack includes orange juice, apple juice, cola, water, bread, cereal, rice, pasta, flour, sugar, coffee, tea, tomato soup, baked beans, peas, tuna, strawberry jam, peanut butter, crisps and chocolate.

Sprites have transparent backgrounds, Point filtering, no mipmaps or compression, and a 64-pixel import limit to match the existing pixel-art scale. The full-resolution generated PNGs are retained in `Sprites`. Bottom-centred silhouette pivots make shelf placement easy. Prefabs are sized relative to Milk1L, with smaller tins and taller cartons. Scale or rotate the prefab to suit your shelf. These are flat sprites facing the same direction as Milk1L, without automatic billboarding or added physics.

Each item has its own data asset under `Data`, containing a unique ID, display name, example price in pence, category tags, icon and world sprite. Prices are editable placeholder game values. `Data/SupermarketItems.asset` is a separate catalog containing all 20 items; the existing catalog and Milk1L are unchanged.

Each prefab has its own material in `Materials`, based on Milk1L's sprite shader, with its sprite texture explicitly assigned. This keeps different items rendering correctly together and allows independent tint changes. `Preview/AllItems.png` shows the full set. `Validation.txt` records the prefab and catalog checks.

Rebuild with **Tools > Shelf Items > Build supermarket items**. Rebuilding overwrites these generated prefabs and data using `Editor/items.json`; duplicate customised prefabs/data before rebuilding. Artwork was created with the built-in image generation tool; the prompt set is in `GenerationPrompts.md`.

