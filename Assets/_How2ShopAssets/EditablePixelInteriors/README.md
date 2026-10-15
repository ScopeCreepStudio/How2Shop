# Editable Pixel Interiors

Drag assets from `Prefabs/InteriorWalls` or `Prefabs/MallKit` into your scene.

Contains **21 individual pieces**, **7 assembled sets**, **7 URP materials**, their **64 x 64 pixel textures**, and standalone **FBX models**. Everything used by the prefabs is in this folder, apart from Unity's installed URP shader package.

## Interior walls — 10 pieces

Wall, trimmed wall, corner post, trimmed corner post, door, door frame, trimmed door frame, window wall, trimmed window wall, and floor tile. The door is independent of its frame. The standalone door has a bottom-edge hinge pivot; rotate its root around Unity's Y axis to open it. Other pieces have bottom-centred pivots. The standalone floor tile is laid flat (the original displays it vertically).

The complete set preserves the original model's arrangement. Select its children to move, rotate, scale, duplicate, or remove individual parts. These are ordinary editable prefabs, not locked model-prefab variants. Mesh vertex editing remains available by importing the FBXs into Blender or another modelling tool.

## Mall kit — 11 pieces

Checkout body, belt and lamp; floor tile; shelving gondola; register and drawer; three wall-opening sections; wet-floor sign. Complete sets retain the original relationship between parts, including the checkout and register assemblies. The gondola remains one mesh, as in the source.

## Appearance

Materials use the same URP Lit shader family as `ReadyAssets/RetroPixel_PixelArt_Copy`, with a matching cream, teal, coral, mustard, plum, lilac and rose palette. New UVs and pixel textures provide irregular chunky pixel patches, without stripes, checker trim or border lines. Patterns wrap seamlessly, and modular wall UVs use whole horizontal repeats so equal-height pieces at their original scale join cleanly. Use the same material and bottom height on adjoining walls; non-uniform scaling changes the pixel density. Textures use Point filtering, no mipmaps and no compression. Materials have low gloss and respond to scene lighting. These are a coordinated new treatment, not a direct reuse of the store-prop atlas, whose UV layout does not fit these meshes. Original mall texture artwork is replaced by the new treatment.

Change the shared materials to recolour the kit, or duplicate a material for an individual object's Renderer. Prefab dimensions retain the source scale; the preview enlarges small objects for visibility.

## Colliders and editing

Prefabs include non-convex MeshColliders for static scenery, preserving door and window openings. No door interaction or animation is included. Use suitable primitive or compound colliders if adding a dynamic Rigidbody. Source assets and the current scene are not modified by the builder.

## Rebuild

`Tools > Pixel Interiors > Build prefabs and materials` rebuilds generated prefabs and materials. Save custom prefab/material edits as separate copies before rebuilding. `Editor/build_pixel_interiors.py` regenerates FBXs and textures using Blender 5.0; run it with Blender's background Python option from this project. Regeneration requires the original source packs and reference material. Existing exported assets work without Blender.

`Validation.txt` records Unity's mesh, material and dependency checks. `manifest.json` records source geometry counts and dimensions before standalone floor reorientation. `Preview/Collection.png` shows the current collection; `Preview/WallJoin.png` shows three adjoining walls. Unity's appearance depends on scene lighting. Original asset licences still apply.


