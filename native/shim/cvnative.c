/* Single translation unit: the cubiomes sources followed by the cv_* API that the managed side binds to. */

/* Routes every structure position cubiomes computes through getStructureConfig_override in cv_rules.inc. */
#define STRUCT_CONFIG_OVERRIDE 1

#if defined(_MSC_VER)
#pragma warning(push, 0)
#endif

/* cubiomes' Beta 1.7 terrain sampler is set aside for the corrected one in cv_beta_noise.inc. */
#define sampleOctaveBeta17Terrain cv_cubiomes_sampleOctaveBeta17Terrain
#include "../cubiomes/noise.c"
#undef sampleOctaveBeta17Terrain
#include "cv_beta_noise.inc"
#include "../cubiomes/biomes.c"
#include "../cubiomes/layers.c"
#include "../cubiomes/biomenoise.c"
#include "../cubiomes/generator.c"
#include "../cubiomes/finders.c"
#include "../cubiomes/util.c"
#include "../cubiomes/quadbase.h"

#if defined(_MSC_VER)
#pragma warning(pop)
#endif

#include "cvnative.h"

#include "cv_meta.inc"
#include "cv_rules.inc"
#include "cv_world.inc"
#include "cv_structures.inc"
#include "cv_landmarks.inc"
#include "cv_layers.inc"
#include "cv_quad.inc"
