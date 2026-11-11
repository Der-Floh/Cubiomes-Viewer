/* Single translation unit: the cubiomes sources followed by the cv_* API that the managed side binds to. */

#if defined(_MSC_VER)
#pragma warning(push, 0)
#endif

#include "../cubiomes/noise.c"
#include "../cubiomes/biomes.c"
#include "../cubiomes/layers.c"
#include "../cubiomes/biomenoise.c"
#include "../cubiomes/generator.c"
#include "../cubiomes/finders.c"
#include "../cubiomes/util.c"

#if defined(_MSC_VER)
#pragma warning(pop)
#endif

#include "cvnative.h"

#include "cv_meta.inc"
#include "cv_world.inc"
#include "cv_structures.inc"
#include "cv_landmarks.inc"
#include "cv_layers.inc"
