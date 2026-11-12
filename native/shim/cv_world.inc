struct cv_world
{
    Generator generator;
    int *cache;
    size_t cache_length;
    int is_seeded;
    SurfaceNoise surface;
    int has_surface;
    BiomeNoise *octave_noise;
    int octave_parameter;
    int octave_count;
};

/* The terrain noise of the dimension the world is applied to; valid for the Overworld and the End. */
static const SurfaceNoise *cv_surface(cv_world *world)
{
    if (!world->has_surface)
    {
        initSurfaceNoise(&world->surface, world->generator.dim, world->generator.seed);
        world->has_surface = 1;
    }
    return &world->surface;
}

static int cv_ensure_cache(cv_world *world, size_t length)
{
    if (length == 0)
        return CV_ERR_GENERATION;
    if (length > world->cache_length)
    {
        int *cache = (int *)realloc(world->cache, length * sizeof(int));
        if (!cache)
            return CV_ERR_MEMORY;
        world->cache = cache;
        world->cache_length = length;
    }
    memset(world->cache, 0, length * sizeof(int));
    return CV_OK;
}

static int cv_generate(cv_world *world, int32_t scale, int32_t x, int32_t z, int32_t sx, int32_t sz, int32_t y)
{
    Range range = {scale, x, z, sx, sz, y, 1};
    int status = cv_ensure_cache(world, getMinCacheSize(&world->generator, scale, sx, 1, sz));
    if (status != CV_OK)
        return status;
    return genBiomes(&world->generator, world->cache, range) == 0 ? CV_OK : CV_ERR_GENERATION;
}

static int cv_check_area_shape(const cv_world *world, int32_t scale, int32_t sx, int32_t sz)
{
    if (!world)
        return CV_ERR_ARGUMENT;
    if (!world->is_seeded)
        return CV_ERR_STATE;
    if (!cv_is_valid_scale(scale) || sx <= 0 || sz <= 0 || sx > CV_MAX_AREA_SIDE || sz > CV_MAX_AREA_SIDE)
        return CV_ERR_ARGUMENT;
    return CV_OK;
}

static int cv_check_area(const cv_world *world, int32_t scale, int32_t sx, int32_t sz)
{
    int status = cv_check_area_shape(world, scale, sx, sz);
    if (status != CV_OK)
        return status;
    if (scale > CV_MAX_LAYERED_SCALE && cv_is_layered_overworld(&world->generator))
        return CV_ERR_UNSUPPORTED;
    return CV_OK;
}

cv_world *cv_world_create(int32_t mc, uint32_t flags)
{
    const uint32_t known_flags = LARGE_BIOMES | NO_BETA_OCEAN | FORCE_OCEAN_VARIANTS;
    if (!cv_is_valid_mc(mc) || (flags & ~known_flags) != 0)
        return NULL;

    cv_world *world = (cv_world *)calloc(1, sizeof(cv_world));
    if (!world)
        return NULL;
    setupGenerator(&world->generator, mc, flags);
    return world;
}

void cv_world_destroy(cv_world *world)
{
    if (!world)
        return;
    free(world->cache);
    free(world->octave_noise);
    free(world);
}

int32_t cv_world_apply(cv_world *world, int32_t dim, uint64_t seed)
{
    if (!world || !cv_is_valid_dim(dim))
        return CV_ERR_ARGUMENT;
    applySeed(&world->generator, dim, seed);
    world->is_seeded = 1;
    world->has_surface = 0;
    world->octave_parameter = -1;
    return CV_OK;
}

int32_t cv_biome_at(cv_world *world, int32_t scale, int32_t x, int32_t y, int32_t z, int32_t *biome)
{
    int status = cv_check_area(world, scale, 1, 1);
    if (status != CV_OK)
        return status;
    if (!biome)
        return CV_ERR_ARGUMENT;

    status = cv_generate(world, scale, x, z, 1, 1, y);
    if (status != CV_OK)
        return status;
    *biome = world->cache[0];
    return CV_OK;
}

int32_t cv_gen_biomes_u8(cv_world *world, int32_t scale, int32_t x, int32_t z, int32_t sx, int32_t sz, int32_t y, uint8_t *out, int32_t capacity)
{
    int status = cv_check_area(world, scale, sx, sz);
    if (status != CV_OK)
        return status;
    if (!out)
        return CV_ERR_ARGUMENT;

    int32_t count = sx * sz;
    if (capacity < count)
        return CV_ERR_CAPACITY;

    status = cv_generate(world, scale, x, z, sx, sz, y);
    if (status != CV_OK)
        return status;

    for (int32_t i = 0; i < count; i++)
    {
        int id = world->cache[i];
        out[i] = (id < 0 || id > 254) ? CV_BIOME_NONE : (uint8_t)id;
    }
    return count;
}
