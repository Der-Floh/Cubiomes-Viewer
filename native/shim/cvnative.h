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

#define CV_ABI_VERSION 3

#define CV_BIOME_NONE 255
#define CV_BIOME_COLOR_BYTES 768
#define CV_MAX_AREA_SIDE 4096
#define CV_MAX_LAYERED_SCALE 256
#define CV_MAX_STRUCTURE_ATTEMPTS 65536
#define CV_MAX_STRONGHOLDS 128
#define CV_MAX_PIECES 421
#define CV_END_GATEWAYS 20

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

/* How one structure generates. The box is the first piece, relative to the structure position; its size is 0 when unknown. */
typedef struct cv_variant
{
    int32_t abandoned, giant, underground, airpocket, basement, cracked;
    int32_t size, start, biome, rotation, mirror;
    int32_t x, y, z;
    int32_t sx, sy, sz;
} cv_variant;

/* One piece of a structure with its bounding box in blocks; both corners are part of the piece. */
typedef struct cv_piece
{
    int32_t type, rotation, depth;
    int32_t x0, y0, z0;
    int32_t x1, y1, z1;
} cv_piece;

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

/* Structures: a result of 1 means yes or found, 0 means no; positions are block coordinates. */
CV_API int32_t cv_structure_config(int32_t stype, int32_t mc, int32_t *dimension, int32_t *region_blocks);
CV_API int32_t cv_structure_pos(int32_t stype, int32_t mc, uint64_t seed, int32_t reg_x, int32_t reg_z, int32_t *x, int32_t *z);
CV_API int32_t cv_structure_viable(cv_world *world, int32_t stype, int32_t x, int32_t z);

/* Writes the (x, z) pairs of the structures that generate in [x0, x1) x [z0, z1) and returns their number. */
CV_API int32_t cv_structures_in_area(cv_world *world, int32_t stype, int32_t x0, int32_t z0, int32_t x1, int32_t z1, int32_t *positions, int32_t capacity);

/* Details of a structure found by cv_structures_in_area. cv_structure_variant returns 1 when a variant is known;
   cv_structure_pieces returns the number of pieces, which is 0 for types other than end cities and fortresses. */
CV_API int32_t cv_structure_variant(cv_world *world, int32_t stype, int32_t x, int32_t z, cv_variant *variant);
CV_API int32_t cv_structure_pieces(cv_world *world, int32_t stype, int32_t x, int32_t z, cv_piece *pieces, int32_t capacity);

/* The 20 gateways around the main End island in generation order, each followed by the gateway it leads to:
   4 values (x, z, linked x, linked z) per gateway. The world must be applied to the End. */
CV_API int32_t cv_end_gateways(cv_world *world, int32_t *positions, int32_t capacity);

/* Overworld landmarks; the world must be applied to the Overworld. */
CV_API int32_t cv_spawn(cv_world *world, int32_t *x, int32_t *z);
CV_API int32_t cv_strongholds(cv_world *world, int32_t *positions, int32_t capacity);

/* Writes one byte per chunk, row by row: 1 for a slime chunk, 0 otherwise. Returns the number of chunks. */
CV_API int32_t cv_slime_chunks(uint64_t seed, int32_t chunk_x, int32_t chunk_z, int32_t size_x, int32_t size_z, uint8_t *out, int32_t capacity);

#ifdef __cplusplus
}
#endif

#endif
