FruitShelf.prefab: empty three-tier fruit display, about 1.16m wide x 2.06m high x 0.76m deep. Pivot is at floor level; front faces local -Z.

Frame, CrateWood, and SignFace are separate plain URP/Lit materials. Assign your textures to their Base Map fields. The existing Box2 crate keeps its supplied UVs; the frame and blank sign use standard cube UVs. Edit crate angles and positions under Crates, and use the Fruit placement children as starting points for produce. One simple root collider prevents walking through the display.

Crate source: Assets/Packages/Wooden Boxes/Prefabs/Box2.prefab. The source asset is unchanged.
