# 16 aisle supermarket

Open Assets/Scenes/Supermarket_16Aisle.unity. This is a separate 36m x 36m level based on the supplied reference: entrance at the rear (+Z), exit at the front (-Z), two banks of eight aisles, perimeter departments and six checkout lanes directly before the exit. Press Play for WASD / mouse walking from the entrance.

All standard shop fixtures are connected instances from Assets/Packages/How2ShopAssets/AssetsReady1/Prefabs/. No ReadyAssets fixtures are used. Product pictures reuse the existing shelf items; household and pet aisles use neutral paper-roll and tin proxies that can be replaced later. The existing stocked newsstand is included near the exit.

Architecture, aisles, departments, checkout area, lighting and player are separate root groups. The plain URP materials in Materials are intentionally simple placeholders for your own textures. Fixture overrides keep frame, body, shelves and cooler panels independently editable. Originals are unchanged.

The ceiling is hidden only in the editor using Scene Visibility so the layout is easy to edit. It remains visible in Play mode and builds. Toggle the eye icon on the Ceiling group to inspect it in the editor. An optional disabled overview camera is under Player and Viewpoints.

Preview contains overview, entrance and checkout renders. Validation.txt records counts, clearances and dependency checks. Tools > How2Shop > Build 16 Aisle Supermarket Scene regenerates this generated scene, so save a copy before using that command after manual edits. Other scenes are not modified.


## Population and lighting pass

The scene has densely stocked shelf facings and depth rows, simple combined meshes behind packaged 2D covers, four entrance promotions and two checkout impulse displays. Existing aisle spacing is retained. Household and pet stock remain neutral proxies.

Lighting uses downward ceiling spotlights, eight shadow-casting aisle lights, cool refrigerated lighting, warm fresh-food accents, a low-cost reflection probe, and a scene-local RetailLighting profile for restrained bloom and colour. No shared project pipeline settings are changed. Floor rendering is split into local panels so lighting works consistently with per-object light limits.

Tools > How2Shop > Populate and Light Existing Supermarket reapplies this pass to the existing scene. This replaces generated stock, promotion and lighting groups; other layout groups are retained.
