# Package icon

Both distributable packages share `icon.png`: a navy penguin holding cyan
content brackets around a purple validation check. The opaque panel and pale
outline keep the mark visible on light and dark gallery backgrounds.

## Provenance

Created on 2026-11-14 with the built-in imagegen workflow for PIN-1281.
`icon-master.png` is the original generated raster master, retained unchanged.
No existing portfolio artwork was copied into the generation input.

Visual references inspected:

- [BlazorSitemap icon](https://github.com/PinguApps/BlazorSitemap/blob/3c3d589533e8ad791508f684ed743d4346c9efa0/src/PinguApps.BlazorSitemap/icon.png)
- [RazorStyle icon](https://github.com/PinguApps/RazorStyle/blob/aa5973ccabaab7cf87a1d8beba62bada4df7c2fd/src/PinguApps.RazorStyle.Build/icon.png)

Prompt:

> Use case: logo-brand. Create an original square NuGet package icon for PinguApps.BlazorContentContracts. Portfolio style: friendly navy penguin peeking over a charcoal square panel, white face and belly, tiny orange beak, thick pale outline, flat clean vector-like shapes. New distinctive motif on the panel: cyan enclosing square brackets around a purple check mark, representing validated component content. Transparent background. No text, no gradients, no shadows, no watermark. Centered balanced silhouette with generous outer padding, bold simple geometry legible at 32 and 64 pixels. Produce one high resolution square master.

## Export

The package image is a 128 × 128 RGBA PNG. Export with System.Drawing:
draw the master into the rectangle (4, 4, 120, 120) of a transparent 128 × 128
bitmap using HighQualityBicubic interpolation, then save as PNG.
Only this small export is packed; the master and provenance stay in the repository.

[NuGet icon requirements](https://learn.microsoft.com/en-us/nuget/reference/nuspec#icon)
require an included PNG/JPEG no larger than 1 MB and recommend 128 × 128.
The shared PackageIcon metadata points to the included root `icon.png`.
