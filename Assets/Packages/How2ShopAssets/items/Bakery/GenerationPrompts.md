# Bakery sprite generation

Created with the built-in image generator. Five separate transparent images: SourdoughLoaf, Baguette, Croissant, Pretzel, CinnamonRoll.

Prompt used for each, replacing SUBJECT with its item name:
Create one isolated SUBJECT bakery food item sprite for a pixel art supermarket game. Chunky true low resolution 32x32 pixel art enlarged with nearest-neighbor crisp square pixels, limited warm golden brown palette, dark brown pixel outline and simple clustered shading. Three-quarter front/top view, centered, whole item, transparent background. No text, no plate, no shadow outside silhouette, no gradients, no smooth vector edges. Save asset suitable for Unity 2D sprite.

Source PNGs retain their transparency and original resolution. Unity imports them at 64 pixels maximum with Point filtering, no mipmaps, and no compression, matching the other pixelated store items.
