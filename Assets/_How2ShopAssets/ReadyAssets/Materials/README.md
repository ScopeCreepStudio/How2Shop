# ReadyAssets colour materials

## Recolour the existing pixel art

`Individual` contains one material per prop (27 total), already assigned to the corresponding ReadyAssets prefab and the matching complete-set mesh. Select a material and change the colour beside **Base Map** in Unity's Inspector. This tints the existing artwork; white restores its original colours. The original atlas, labels, mesh UVs and geometry are preserved.

Each asset type is independent. To give two instances of the same prop different colours, duplicate its material and drag that copy onto the instance's Geometry renderer. Existing scene material overrides take precedence over prefab defaults; assign the new named material to those renderers if needed.

## Joined shelving and fixtures

Use `ReadyAssets/Prefabs/Joinable` for runs of shelves, racks, gondolas, cabinets or door frames. These 15 optional prefabs use the corresponding materials in `Materials/Joinable`.

Their **Top colour** and **Side colour** controls recolour horizontal and vertical faces independently. The shared texture is an actual patch from the existing pixel atlas, converted to a tintable, seamless tile. This uses the existing pixel-art marks, but does not retain each face's individual atlas decorations; the normal prefabs retain those exactly.

The pattern is mapped in world space so adjacent coplanar surfaces share the same pixel grid, even when pieces are moved, rotated or scaled. Use the same **Pattern size**, **Pixel contrast**, and colours on adjoining pieces. The default Pattern size is 1 metre. No manual texture offsets are necessary. Match the geometry edges and shelf heights: materials cannot remove physical gaps, overlapping faces or end posts. Changes of surface direction at corners may change projection. World-space patterns stay fixed in the scene while a prop moves, so use the original materials for moving props.

Point filtering, Repeat wrapping and uncompressed textures keep the joins crisp. The joined material responds to URP lights and shadows. It is intended for opaque fixtures, not transparent glass. A preview of adjoining racks is saved in `Preview/JoinedColourMaterials.png`.

The rebuild command is **Tools > ReadyAssets > Create independent colour materials**. Existing named materials retain user colour edits when rebuilding. Validation results and the exact sampled atlas location are recorded alongside this file.
