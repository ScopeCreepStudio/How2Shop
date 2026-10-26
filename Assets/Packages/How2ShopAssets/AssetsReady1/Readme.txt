ASSETS READY 1

Prefabs contains one clean prefab for every individual item in ReadyAssets. StoreProps_CompleteSet contains all of the new items laid out together. Scenes/Showcase is an optional preview scene.

TO COLOUR AN ITEM: select a clearly named child such as Body, Frame, Shelves, Panels, Label, or Cap, then drag any material from Assets/ColoringMaterials onto it or its Mesh Renderer Material slot. Open the prefab to change every instance. Drag onto each part to colour the whole item. These are ordinary MeshRenderers with ordinary material assignments; there are no custom colour scripts or joined shaders.

The familiar source shapes are retained. Geometry transforms are baked into new standalone mesh assets; roots and parts use scale 1 and rotation 0, with the front facing local -Z. Pivots are at the bottom centre. New box-projected UVs tile twice per metre on large fixtures and at least once across small props, so your colour textures cover the surfaces instead of sampling a tiny atlas region. Original atlas UVs and vertex colours have been removed. Original mesh normals are retained.

The source ReadyAssets folder and your existing scenes are untouched. The new meshes and prefabs have no dependency on ReadyAssets; they reference only the output meshes, standard Unity components, and your existing ColoringMaterials palette. The optional Joined duplicates are replaced by these same normal prefabs.
