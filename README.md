# LPVTest: Light Propagation Volumes

A simple Unity implementation of Light Propagation Volumes.  Gives a real-time approximation
of indirect lights.  My goal is to improve the rendering of dynamic scenes that tend to be
quite geometric, i.e. made out of a combination of large flat polygons, but that can also be
locally very complex, e.g. with high-poly prefabricated parts in them.  It should be fast
enough to render these overall very complex scenes in VR if we don't recompute the indirect
lights every frame.

Example (trivial scene):

![sshot1](Screenshots/sshot1.jpg?raw=true "sshot1")

Note the green blob on the ceiling, and how the gray wall receives some red from the floor.
The same scene with the standard shader looks more boring:

![sshot0](Screenshots/sshot0.jpg?raw=true "sshot0")

The walls have no depth; the direct shadow bleeds through it in both cases (as computed by
Unity's built-in algorithms).  The purpose of this is only to improve on indirect lights.


## Next steps

* Add cascades, to support a wide range of scales

* Optionally, don't recompute the indirect lights every frame, but only on demand


## See also

https://github.com/arigo/unity-custom-shadow-experiments, which is a replacement for
Unity's built-in direct shadows, supporting only a single directional light but giving
nicer results for mobile VR.  (The current repo is aiming for PC VR.)
