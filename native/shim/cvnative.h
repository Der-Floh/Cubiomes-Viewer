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

#define CV_ABI_VERSION 16

#define CV_BIOME_NONE 255
#define CV_BIOME_COLOR_BYTES 768
#define CV_MAX_AREA_SIDE 4096
#define CV_MAX_LAYERED_SCALE 256
#define CV_MAX_STRUCTURE_ATTEMPTS 65536
#define CV_MAX_STRONGHOLDS 128
/* cubiomes writes fortress pieces without looking at the length of the buffer, so it is far longer than any fortress needs. */
#define CV_MAX_PIECES 16384
#define CV_END_GATEWAYS 20
#define CV_CLIMATE_UNIT 10000
#define CV_HEIGHT_UNIT 16
#define CV_MAX_SURFACE_SIDE 64
#define CV_LAYER_RIVER 0
#define CV_LAYER_OCEAN_TEMPERATURE 1
#define CV_LAYER_TEMPERATURE_CATEGORY 2
#define CV_TEMPERATURE_SPECIAL 4
#define CV_QUAD_OTHER 5
#define CV_STRUCTURE_TYPES 28
#define CV_RULES_NO_TERRAIN_ESTIMATE 1
#define CV_DEFLATE_MAX_LEVEL 12

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

/* How structures generate where a world differs from the game. Bit (1 << stype) of 'salted' replaces the salt of that
   structure type with salts[stype]; flags are CV_RULES_*. Zeroed rules, like a null pointer, are the game's own. */
typedef struct cv_rules
{
    uint32_t flags;
    uint32_t salted;
    int32_t salts[CV_STRUCTURE_TYPES];
} cv_rules;

/* How one structure generates. The box is the first piece, relative to the structure position; its size is 0 when unknown.
   dried_ghast is 1 for a nether fossil holding a dried ghast (1.21.6+); portal_eyes counts the eyes of ender a
   stronghold's end portal already holds (1.13+). */
typedef struct cv_variant
{
    int32_t abandoned, giant, underground, airpocket, basement, cracked;
    int32_t size, start, biome, rotation, mirror;
    int32_t x, y, z;
    int32_t sx, sy, sz;
    int32_t dried_ghast, portal_eyes;
} cv_variant;

/* One piece of a structure with its bounding box in blocks; both corners are part of the piece. */
typedef struct cv_piece
{
    int32_t type, rotation, depth;
    int32_t x0, y0, z0;
    int32_t x1, y1, z1;
} cv_piece;

#define CV_LOOT_STACKS 27
#define CV_LOOT_ENCHANTMENTS 16
#define CV_LOOT_PLAIN 1
#define CV_LOOT_ENCHANTED 2

/* One stack of items in a container: loot item, count, a potion or stew effect (-1 for none) and its duration in
   ticks, and the enchantments with their levels. */
typedef struct cv_loot_stack
{
    int32_t item, count, effect, effect_ticks;
    int32_t enchantment_count;
    int32_t enchantments[CV_LOOT_ENCHANTMENTS];
    int32_t levels[CV_LOOT_ENCHANTMENTS];
} cv_loot_stack;

/* One chest, barrel or dispenser: its loot table (cv_loot_table_name), where it stands, and its stacks. */
typedef struct cv_loot_container
{
    int32_t table, x, z;
    int32_t first_stack, stack_count;
} cv_loot_container;

/* Stacks of a loot item that count: kinds is CV_LOOT_PLAIN, CV_LOOT_ENCHANTED or both; with an enchantment above 0
   only stacks carrying it at min_level or higher count. */
typedef struct cv_loot_query
{
    int32_t item, kinds, enchantment, min_level;
} cv_loot_query;

#ifdef __cplusplus
extern "C"
{
#endif

CV_API int32_t cv_abi_version(void);
CV_API int32_t cv_mc_oldest(void);
CV_API int32_t cv_mc_newest(void);
CV_API const char *cv_mc_name(int32_t mc);
CV_API const char *cv_biome_name(int32_t mc, int32_t biome);

/* Writes the dimension a biome belongs to and returns 1 when it generates there in the version, 0 when it does not. */
CV_API int32_t cv_biome_dimension(int32_t mc, int32_t biome, int32_t *dimension);
CV_API int32_t cv_default_biome_colors(uint8_t *rgb, int32_t capacity);

CV_API cv_world *cv_world_create(int32_t mc, uint32_t flags);
CV_API void cv_world_destroy(cv_world *world);
CV_API int32_t cv_world_apply(cv_world *world, int32_t dim, uint64_t seed);

/* The structure rules the functions taking this world follow; a null pointer restores the game's own. */
CV_API int32_t cv_world_set_rules(cv_world *world, const cv_rules *rules);

CV_API int32_t cv_biome_at(cv_world *world, int32_t scale, int32_t x, int32_t y, int32_t z, int32_t *biome);
CV_API int32_t cv_gen_biomes_u8(cv_world *world, int32_t scale, int32_t x, int32_t z, int32_t sx, int32_t sz, int32_t y, uint8_t *out, int32_t capacity);

/* Climate parameters 0 to 5: temperature, humidity, continentalness, erosion, depth, weirdness.
   cv_climate_supported returns 1 when the generator of a version and dimension has the parameter.
   cv_gen_climate writes one value per sample, the parameter times CV_CLIMATE_UNIT as the biome lookup sees it.
   With octaves > 0 only that many of the widest noise octaves contribute, without the local shift (1.18+). */
CV_API int32_t cv_climate_supported(int32_t mc, int32_t dim, int32_t parameter);
CV_API int32_t cv_gen_climate(cv_world *world, int32_t parameter, int32_t octaves, int32_t scale, int32_t x, int32_t z, int32_t sx, int32_t sz, int32_t y, int16_t *out, int32_t capacity);

/* An inner layer of the layered generator (CV_LAYER_*) as biome ids, CV_BIOME_NONE where the layer is empty.
   CV_LAYER_TEMPERATURE_CATEGORY (1.7 to 1.17, scale 1024) writes the category instead: 0 oceanic, 1 warm, 2 lush,
   3 cold, 4 freezing, and warm, lush or cold plus CV_TEMPERATURE_SPECIAL where the special variant is set. With large
   biomes the biomes of a cell cover 4096 blocks, although the layer itself cannot tell. */
CV_API int32_t cv_layer_supported(int32_t mc, int32_t dim, int32_t layer, int32_t scale);
CV_API int32_t cv_gen_layer_u8(cv_world *world, int32_t layer, int32_t scale, int32_t x, int32_t z, int32_t sx, int32_t sz, uint8_t *out, int32_t capacity);

/* The approximate surface height in blocks times CV_HEIGHT_UNIT, at scales of 4 blocks per sample and coarser. */
CV_API int32_t cv_height_supported(int32_t mc, int32_t dim);
CV_API int32_t cv_gen_heights(cv_world *world, int32_t scale, int32_t x, int32_t z, int32_t sx, int32_t sz, int16_t *out, int32_t capacity);

/* The exact terrain of cubiomes' terrain generator, in the Overworld from 1.14 and in the End from 1.9. cv_gen_surface
   writes, row by row, the Y of the lowest air block above the highest solid block of each column, as the noise shapes
   it before water, caves, ravines and anything on the ground; areas of up to CV_MAX_SURFACE_SIDE blocks a side. */
CV_API int32_t cv_terrain_supported(int32_t mc, int32_t dim);
CV_API int32_t cv_gen_surface(cv_world *world, int32_t x, int32_t z, int32_t sx, int32_t sz, int16_t *out, int32_t capacity);

/* Structures: a result of 1 means yes or found, 0 means no; positions are block coordinates. Functions with a world
   follow the rules of the world, the others the rules they are given. */
CV_API int32_t cv_structure_config(int32_t stype, int32_t mc, int32_t *dimension, int32_t *region_blocks);

/* cubiomes' name of a structure type, so the managed side can check that its numbering still matches. */
CV_API const char *cv_structure_name(int32_t stype);

/* Writes the salt the game uses for a structure type and returns 1 when the type generates in the version and its
   positions depend on the salt, 0 when they do not. */
CV_API int32_t cv_structure_salt(int32_t stype, int32_t mc, int32_t *salt);
CV_API int32_t cv_structure_pos(const cv_rules *rules, int32_t stype, int32_t mc, uint64_t seed, int32_t reg_x, int32_t reg_z, int32_t *x, int32_t *z);
CV_API int32_t cv_structure_viable(cv_world *world, int32_t stype, int32_t x, int32_t z);

/* Whether the biome rule of a structure type lets it generate in a biome; CV_ERR_UNSUPPORTED for a type without such a rule. */
CV_API int32_t cv_structure_biome_viable(int32_t stype, int32_t mc, int32_t biome);

/* Writes the (x, z) pairs of the structures that generate in [x0, x1) x [z0, z1) and returns their number. */
CV_API int32_t cv_structures_in_area(cv_world *world, int32_t stype, int32_t x0, int32_t z0, int32_t x1, int32_t z1, int32_t *positions, int32_t capacity);

/* Like cv_structures_in_area, but without a world and without asking whether a structure can generate, so every
   structure of the seed is among the attempts. */
CV_API int32_t cv_structure_attempts_in_area(const cv_rules *rules, int32_t stype, int32_t mc, uint64_t seed, int32_t x0, int32_t z0, int32_t x1, int32_t z1, int32_t *positions, int32_t capacity);

/* Details of a structure found by cv_structures_in_area, or of a stronghold in the Overworld. cv_structure_variant
   returns 1 when a variant is known, which for a stronghold is how many eyes its portal holds; cv_structure_pieces returns the number of pieces of an end city, fortress,
   stronghold or mineshaft, and 0 for other types. Strongholds and mineshafts keep the height they start at. */
CV_API int32_t cv_structure_variant(cv_world *world, int32_t stype, int32_t x, int32_t z, cv_variant *variant);
CV_API int32_t cv_structure_pieces(cv_world *world, int32_t stype, int32_t x, int32_t z, cv_piece *pieces, int32_t capacity);

/* The Y a structure found by cv_structures_in_area is built at, where the terrain tells it exactly: end cities, nether
   fossils and, from 1.14, woodland mansions. Returns 1 when written, 0 when not known or when the terrain keeps the
   structure from generating. */
CV_API int32_t cv_structure_floor(cv_world *world, int32_t stype, int32_t x, int32_t z, int32_t *y);

/* The loot of structure chests (cv_loot.inc), from 1.13 on and only for the types whose chests cubiomes places.
   cv_loot_supported returns 1 when a type's loot can be read in a version. cv_structure_loot writes the containers of
   a structure and their stacks and returns how many containers there are; CV_ERR_UNSUPPORTED where the loot cannot be
   told. cv_structure_loot_count rolls the same chests and writes how many matching items lie in the fullest
   container and in all of them together. */
CV_API int32_t cv_loot_supported(int32_t stype, int32_t mc);
CV_API int32_t cv_structure_loot(cv_world *world, int32_t stype, int32_t x, int32_t z, cv_loot_container *containers,
    int32_t container_capacity, cv_loot_stack *stacks, int32_t stack_capacity);
CV_API int32_t cv_structure_loot_count(cv_world *world, int32_t stype, int32_t x, int32_t z, const cv_loot_query *query,
    int32_t *most, int32_t *total);

/* What the chests of a structure type can hold in a version: each loot item with the kinds it comes in, and for one
   item the enchantments it can carry with the highest level of each. */
CV_API int32_t cv_loot_items(int32_t stype, int32_t mc, int32_t *items, int32_t *kinds, int32_t capacity);
CV_API int32_t cv_loot_enchantments(int32_t stype, int32_t mc, int32_t item, int32_t *enchantments, int32_t *levels, int32_t capacity);

/* Names of loot items ("minecraft:diamond"), enchantments ("mending"), effects and tables; NULL for an unknown id. */
CV_API int32_t cv_loot_item_count(void);
CV_API const char *cv_loot_item_name(int32_t item, int32_t mc);
CV_API int32_t cv_loot_enchantment_count(void);
CV_API const char *cv_loot_enchantment_name(int32_t enchantment);
CV_API int32_t cv_loot_enchantment_max_level(int32_t enchantment);
CV_API int32_t cv_loot_effect_count(void);
CV_API const char *cv_loot_effect_name(int32_t effect);
CV_API int32_t cv_loot_table_count(void);
CV_API const char *cv_loot_table_name(int32_t table);

/* The 20 gateways around the main End island in generation order, each followed by the gateway it leads to:
   4 values (x, z, linked x, linked z) per gateway. The world must be applied to the End. */
CV_API int32_t cv_end_gateways(cv_world *world, int32_t *positions, int32_t capacity);

/* Overworld landmarks; the world must be applied to the Overworld. */
CV_API int32_t cv_spawn(cv_world *world, int32_t *x, int32_t *z);
CV_API int32_t cv_strongholds(cv_world *world, int32_t *positions, int32_t capacity);

/* Writes one byte per chunk, row by row: 1 for a slime chunk, 0 otherwise. Returns the number of chunks. */
CV_API int32_t cv_slime_chunks(uint64_t seed, int32_t chunk_x, int32_t chunk_z, int32_t size_x, int32_t size_z, uint8_t *out, int32_t capacity);

/* Quad structures (cv_quad.inc). Qualities are 1 ideal, 2 classic, 3 normal and 4 barely, after how close the four
   structures are; CV_QUAD_OTHER marks a quad outside these classes. A base is the lower 48 bits of a seed plus the
   structure salt, with the quad in regions (0,0) to (1,1). */
CV_API int32_t cv_quad_supported(int32_t stype, int32_t mc);
CV_API int32_t cv_quad_hut_low_bits(int32_t quality, uint64_t *values, int32_t capacity);
CV_API int32_t cv_quad_hut_scan(uint64_t low20, uint32_t first_high, uint32_t count, uint64_t *bases, int32_t capacity);
CV_API int32_t cv_quad_hut_base_info(uint64_t base, float *radius);
CV_API int32_t cv_quad_seed_of_base(const cv_rules *rules, int32_t stype, int32_t mc, uint64_t base, int32_t reg_x, int32_t reg_z, uint64_t *seed);
CV_API int32_t cv_quads_in_area(const cv_rules *rules, int32_t stype, int32_t mc, uint64_t seed, int32_t reg_x0, int32_t reg_z0, int32_t reg_x1, int32_t reg_z1, int32_t *quads, int32_t capacity);

/* Deflating PNG image data with libdeflate. cv_deflate_bound is the most bytes cv_deflate_band writes for 'length' bytes.
   cv_deflate_band deflates 'length' bytes at 'level' (0 to CV_DEFLATE_MAX_LEVEL) into a raw deflate stream; unless
   'is_last', the stream ends with an empty stored block instead of a final block, so that the next band can follow it in
   the same zlib stream. It returns the bytes written, CV_ERR_CAPACITY when 'capacity' is too small, or another status. */
CV_API int64_t cv_deflate_bound(int64_t length);
CV_API int64_t cv_deflate_band(const uint8_t *data, int64_t length, int32_t level, int32_t is_last, uint8_t *out, int64_t capacity);

#ifdef __cplusplus
}
#endif

#endif
