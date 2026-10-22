COLORABLE PIXEL CEILING

Drag SM_Ceiling_3.5x3.5_Colorable into your scene and position its root at the top of your walls. The underside grid sits at root Y; panels extend 0.18 units above it.

Duplicate in 3.5-unit X/Z steps to cover the room without gaps. Keep scale at 1 for the intended pixel texture size.

Repaint: select CeilingPanel children and drag any Assets/ColoringMaterials/Pixel_*.mat onto their Mesh Renderer material slot. Select Grid children separately to repaint the trim. Default: Pixel_Cream panels and Pixel_White grid. These reference your existing materials directly.

Solid cube geometry makes both the underside and top visible. No scripts or extra materials are required.
