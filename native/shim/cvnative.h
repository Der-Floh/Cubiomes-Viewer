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

#define CV_ABI_VERSION 5

#define CV_BIOME_NONE 255
#define CV_BIOME_COLOR_BYTES 768
#define CV_MAX_AREA_SIDE 4096
#define CV_MAX_LAYERED_SCALE 256
#define CV_MAX_STRUCTURE_ATTEMPTS 65536
#define CV_MAX_STRONGHOLDS 128
#define CV_MAX_PIECES 421
#define CV_END_GATEWAYS 20
#define CV_CLIMATE_UNIT 10000
#define CV_HEIGHT_UNIT 16
#define CV_LAYER_RIVER 0
#define CV_LAYER_OCEAN_TEMPERATURE 1

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

/* Climate parameters 0 to 5: temperature, humidity, continentalness, erosion, depth, weirdness.
   cv_climate_supported returns 1 when the generator of a version and dimension has the parameter.
   cv_gen_climate writes one value per sample, the parameter times CV_CLIMATE_UNIT as the biome lookup sees it.
   With octaves > 0 only that many of the widest noise octaves contribute, without the local shift (1.18+). */
CV_API int32_t cv_climate_supported(int32_t mc, int32_t dim, int32_t parameter);
CV_API int32_t cv_gen_climate(cv_world *world, int32_t parameter, int32_t octaves, int32_t scale, int32_t x, int32_t z, int32_t sx, int32_t sz, int32_t y, int16_t *out, int32_t capacity);

/* An inner layer of the layered generator (CV_LAYER_*) as biome ids, CV_BIOME_NONE where the layer is empty. */
CV_API int32_t cv_layer_supported(int32_t mc, int32_t dim, int32_t layer, int32_t scale);
CV_API int32_t cv_gen_layer_u8(cv_world *world, int32_t layer, int32_t scale, int32_t x, int32_t z, int32_t sx, int32_t sz, uint8_t *out, int32_t capacity);

/* The approximate surface height in blocks times CV_HEIGHT_UNIT, at scales of 4 blocks per sample and coarser. */
CV_API int32_t cv_height_supported(int32_t mc, int32_t dim);
CV_API int32_t cv_gen_heights(cv_world *world, int32_t scale, int32_t x, int32_t z, int32_t sx, int32_t sz, int16_t *out, int32_t capacity);

/* Structures: a result of 1 means yes or found, 0 means no; positions are block coordinates. */
CV_API int32_t cv_structure_config(int32_t stype, int32_t mc, int32_t *dimension, int32_t *region_blocks);
CV_API int32_t cv_structure_pos(int32_t stype, int32_t mc, uint64_t seed, int32_t reg_x, int32_t reg_z, int32_t *x, int32_t *z);
CV_API int32_t cv_structure_viable(cv_world *world, int32_t stype, int32_t x, int32_t z);

/* Whether the biome rule of a structure type lets it generate in a biome; CV_ERR_UNSUPPORTED for a type without such a rule. */
CV_API int32_t cv_structure_biome_viable(int32_t stype, int32_t mc, int32_t biome);

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
