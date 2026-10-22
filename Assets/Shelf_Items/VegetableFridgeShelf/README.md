# Vegetable fridge shelf

An open-front refrigerated produce multideck inspired by the supplied reference photographs. Each bay is 3.24m wide, 2.37m high and 1.1m deep; local -Z is the customer side and the pivot is at floor level.

VegetableFridgeShelf_Stocked.prefab has four 15-degree sloping tiers, sixteen individually removable slatted plastic crates, white price tickets, a vented refrigeration base, silver end panels, teal canopy, temperature display and cool canopy lights. It holds 144 individual 2D items using the existing Carrot, Broccoli, Tomato, Cucumber and Potato prefabs. Items lie on the sloping trays instead of billboarding.

VegetableFridgeShelf_Empty.prefab has the vegetable groups disabled for custom stocking. VegetableFridgeShelf_Double.prefab joins two bays into a 6.46m display and retains their prefab connections.

Cabinet, crate, interior, base, ticket and diffuser colours use shared materials from Assets/ColoringMaterials (Pixel_Teal, Pixel_Forest, Pixel_White, Pixel_Charcoal and Pixel_Cream). Mesh parts have ordinary UVs and remain separately editable. Price tickets are example game prices. Existing item artwork and the shared SupermarketSignFont component are reused.

The builder places a clear double or single unit beside the existing produce area in Assets/Scenes/Superstore.unity. Placement.txt records the chosen prefab and position. Existing produce displays are retained. An existing placed fridge is not duplicated on rebuild.

Tools > Shelf Items > Build and place vegetable fridge in Superstore regenerates these assets. Duplicate customised prefabs before rebuilding. Preview and Validation.txt contain the verified output.
