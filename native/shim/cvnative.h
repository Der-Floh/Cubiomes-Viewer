#ifndef CVNATIVE_H_
#define CVNATIVE_H_

#include <stdint.h>

#if defined(_WIN32)
#define CV_API __declspec(dllexport)
#elif defined(__GNUC__)
#define CV_API __attribute__((visibility("default")))
#else
#define CV_API
#endif

#define CV_ABI_VERSION 1

#define CV_BIOME_NONE 255
#define CV_BIOME_COLOR_BYTES 768
#define CV_MAX_AREA_SIDE 4096
#define CV_MAX_LAYERED_SCALE 256

enum cv_status
{
    CV_OK = 0,
    CV_ERR_ARGUMENT = -1,
    CV_ERR_UNSUPPORTED = -2,
    CV_ERR_CAPACITY = -3,
    CV_ERR_MEMORY = -4,
    CV_ERR_GENERATION = -5,
    CV_ERR_STATE = -6,
};

typedef struct cv_world cv_world;

#ifdef __cplusplus
extern "C"
{
#endif

CV_API int32_t cv_abi_version(void);
CV_API int32_t cv_mc_oldest(void);
CV_API int32_t cv_mc_newest(void);
CV_API const char *cv_mc_name(int32_t mc);
CV_API const char *cv_biome_name(int32_t mc, int32_t biome);
CV_API int32_t cv_default_biome_colors(uint8_t *rgb, int32_t capacity);

CV_API cv_world *cv_world_create(int32_t mc, uint32_t flags);
CV_API void cv_world_destroy(cv_world *world);
CV_API int32_t cv_world_apply(cv_world *world, int32_t dim, uint64_t seed);

CV_API int32_t cv_biome_at(cv_world *world, int32_t scale, int32_t x, int32_t y, int32_t z, int32_t *biome);
CV_API int32_t cv_gen_biomes_u8(cv_world *world, int32_t scale, int32_t x, int32_t z, int32_t sx, int32_t sz, int32_t y, uint8_t *out, int32_t capacity);

CV_API int32_t cv_structure_pos(int32_t stype, int32_t mc, uint64_t seed, int32_t reg_x, int32_t reg_z, int32_t *x, int32_t *z);

#ifdef __cplusplus
}
#endif

#endif
