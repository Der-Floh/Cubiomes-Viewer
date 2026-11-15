/* noise.c is compiled only here, so its Beta 1.7 terrain sampler can be set aside for the corrected one in
   cv_beta_noise.inc, which needs noise.c's file-static helpers. */
#define sampleOctaveBeta17Terrain cv_cubiomes_sampleOctaveBeta17Terrain
#include "../cubiomes/noise.c"
#undef sampleOctaveBeta17Terrain
#include "cv_beta_noise.inc"
