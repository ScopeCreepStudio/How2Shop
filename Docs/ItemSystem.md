# Item system

Item data lives in Unity assets. `Assets/Data/ItemCatalog.asset` lists the available `ItemDefinition` assets; each item has a stable ID, display name, price in pence, tags, and 2D sprites.

To add an item, create **Assets > Create > How2Shop > Item Definition**, fill its fields, then add it to the catalog. IDs should be unique and stay the same when the display name changes. The catalog inspector reports missing IDs, duplicate IDs, negative prices, and missing sprites.

Prices are stored as whole pence, so `199` means **£1.99**. Use `item.PriceLabel` for a GBP display string. An item can use one sprite for both UI and the world: set `Icon` and leave `World Sprite` empty. Set both if the two views need different art.

Use `catalog.TryGetById("milk_1l", out var item)` to find an item. For a 2D world object, add `ItemSpriteView` and `SpriteRenderer`, then assign the item. `Assets/Prefabs/Milk1L.prefab` is a working example.
