/* Data for the map layers other than biomes: climate parameters, inner layers of the layered generator and the
   approximate surface height. Areas are addressed like cv_gen_biomes_u8. */

#define CV_CLIMATE_CELL_BLOCKS 4
#define CV_RIVER_SCALE 4
#define CV_RIVER_MAX_SCALE 64
#define CV_RIVER_BAND_CELLS (1 << 20)
#define CV_HEIGHT_SCALE 4
#define CV_END_CELL_BLOCKS 8
#define CV_END_COLUMN_MIN 2
#define CV_END_COLUMN_MAX 18

static int16_t cv_to_int16(double value)
{
    if (value >= INT16_MAX)
        return INT16_MAX;
    if (value <= INT16_MIN)
        return INT16_MIN;
    return (int16_t)value;
}

static int cv_is_noise_overworld(const Generator *generator)
{
    return generator->dim == DIM_OVERWORLD && generator->mc >= MC_1_18;
}

static int cv_check_output(const void *out, int32_t sx, int32_t sz, int32_t capacity)
{
    if (!out)
        return CV_ERR_ARGUMENT;
    return capacity < sx * sz ? CV_ERR_CAPACITY : CV_OK;
}

/* ---- climate ---- */

int32_t cv_climate_supported(int32_t mc, int32_t dim, int32_t parameter)
{
    if (!cv_is_valid_mc(mc) || !cv_is_valid_dim(dim) || parameter < 0 || parameter >= NP_MAX)
        return CV_ERR_ARGUMENT;
    if (dim != DIM_OVERWORLD)
        return 0;
    if (mc >= MC_1_18)
        return 1;
    return mc <= MC_B1_7 && (parameter == NP_TEMPERATURE || parameter == NP_HUMIDITY);
}

/* Samples where the biome map of the same scale samples, so that both show the same places. */
static void cv_gen_noise_climate(const BiomeNoise *noise, int32_t parameter, int32_t scale,
    int32_t x, int32_t z, int32_t sx, int32_t sz, int32_t y, int16_t *out)
{
    int cells = scale / CV_CLIMATE_CELL_BLOCKS;
    int mid = cells / 2;
    uint32_t flags = SAMPLE_NO_BIOME;
    if (scale > CV_CLIMATE_CELL_BLOCKS)
        flags |= SAMPLE_NO_SHIFT;
    if (parameter != NP_DEPTH)
        flags |= SAMPLE_NO_DEPTH;

    for (int32_t j = 0; j < sz; j++)
    {
        for (int32_t i = 0; i < sx; i++)
        {
            int64_t climate[NP_MAX];
            sampleBiomeNoise(noise, climate, (x + i) * cells + mid, y, (z + j) * cells + mid, NULL, flags);
            *out++ = cv_to_int16((double)climate[parameter]);
        }
    }
}

static int cv_prepare_octaves(cv_world *world, int32_t parameter, int32_t octaves)
{
    if (!world->octave_noise)
    {
        world->octave_noise = (BiomeNoise *)malloc(sizeof(BiomeNoise));
        if (!world->octave_noise)
            return CV_ERR_MEMORY;
        world->octave_parameter = -1;
    }
    if (world->octave_parameter != parameter || world->octave_count != octaves)
    {
        const Generator *generator = &world->generator;
        initBiomeNoise(world->octave_noise, generator->mc);
        setClimateParaSeed(world->octave_noise, generator->seed, (generator->flags & LARGE_BIOMES) != 0, parameter, octaves);
        world->octave_parameter = parameter;
        world->octave_count = octaves;
    }
    return CV_OK;
}

/* The generator samples the depth of a single parameter at y = 0; the depth falls by one unit every 32 cells of height. */
static void cv_gen_octave_climate(const BiomeNoise *noise, int32_t parameter, int32_t scale,
    int32_t x, int32_t z, int32_t sx, int32_t sz, int32_t y, int16_t *out)
{
    int cells = scale / CV_CLIMATE_CELL_BLOCKS;
    int mid = cells / 2;
    double offset = parameter == NP_DEPTH ? y * (CV_CLIMATE_UNIT / 32.0) : 0;
    for (int32_t j = 0; j < sz; j++)
    {
        for (int32_t i = 0; i < sx; i++)
        {
            int64_t climate[NP_MAX];
            sampleClimatePara(noise, climate, (x + i) * cells + mid, (z + j) * cells + mid);
            *out++ = cv_to_int16((double)climate[parameter] - offset);
        }
    }
}

static void cv_gen_beta_climate(const BiomeNoiseBeta *noise, int32_t parameter, int32_t scale,
    int32_t x, int32_t z, int32_t sx, int32_t sz, int16_t *out)
{
    int mid = scale >> 1;
    for (int32_t j = 0; j < sz; j++)
    {
        for (int32_t i = 0; i < sx; i++)
        {
            double climate[2];
            sampleBiomeNoiseBeta(noise, NULL, climate, (x + i) * scale + mid, (z + j) * scale + mid);
            *out++ = cv_to_int16(CV_CLIMATE_UNIT * climate[parameter]);
        }
    }
}

int32_t cv_gen_climate(cv_world *world, int32_t parameter, int32_t octaves, int32_t scale,
    int32_t x, int32_t z, int32_t sx, int32_t sz, int32_t y, int16_t *out, int32_t capacity)
{
    int status = cv_check_area(world, scale, sx, sz);
    if (status != CV_OK)
        return status;
    if (parameter < 0 || parameter >= NP_MAX || octaves < 0)
        return CV_ERR_ARGUMENT;
    status = cv_check_output(out, sx, sz, capacity);
    if (status != CV_OK)
        return status;

    const Generator *generator = &world->generator;
    if (cv_climate_supported(generator->mc, generator->dim, parameter) != 1)
        return CV_ERR_UNSUPPORTED;

    if (!cv_is_noise_overworld(generator))
    {
        if (octaves != 0)
            return CV_ERR_UNSUPPORTED;
        cv_gen_beta_climate(&generator->bnb, parameter, scale, x, z, sx, sz, out);
    }
    else if (scale < CV_CLIMATE_CELL_BLOCKS)
    {
        return CV_ERR_UNSUPPORTED;
    }
    else if (octaves == 0)
    {
        cv_gen_noise_climate(&generator->bn, parameter, scale, x, z, sx, sz, y, out);
    }
    else
    {
        status = cv_prepare_octaves(world, parameter, octaves);
        if (status != CV_OK)
            return status;
        cv_gen_octave_climate(world->octave_noise, parameter, scale, x, z, sx, sz, y, out);
    }
    return sx * sz;
}

/* ---- inner layers of the layered generator ---- */

/* The special layer keeps the category in the low bits and marks the special variant in bits 8 to 11; the variant
   changes only warm, lush and cold land. */
static uint8_t cv_temperature_category(int value)
{
    int category = value & 0xff;
    int special = (value & 0xf00) != 0 && (category == Warm || category == Lush || category == Cold);
    return (uint8_t)(category + (special ? CV_TEMPERATURE_SPECIAL : 0));
}

static int cv_is_known_layer(int32_t layer)
{
    return layer == CV_LAYER_RIVER || layer == CV_LAYER_OCEAN_TEMPERATURE || layer == CV_LAYER_TEMPERATURE_CATEGORY;
}

static int cv_find_layer_id(int32_t mc, int32_t layer, int32_t scale)
{
    if (layer == CV_LAYER_TEMPERATURE_CATEGORY)
        return mc >= MC_1_7 && scale == 1024 ? L_SPECIAL_1024 : -1;
    if (layer == CV_LAYER_RIVER)
        return scale >= CV_RIVER_SCALE && scale <= CV_RIVER_MAX_SCALE ? L_SMOOTH_4_RIVER : -1;
    if (layer != CV_LAYER_OCEAN_TEMPERATURE || mc < MC_1_13)
        return -1;
    switch (scale)
    {
    case 4:   return L_ZOOM_4_OCEAN;
    case 16:  return L_ZOOM_16_OCEAN;
    case 64:  return L_ZOOM_64_OCEAN;
    case 256: return L_OCEAN_TEMP_256;
    default:  return -1;
    }
}

int32_t cv_layer_supported(int32_t mc, int32_t dim, int32_t layer, int32_t scale)
{
    if (!cv_is_valid_mc(mc) || !cv_is_valid_dim(dim) || !cv_is_valid_scale(scale) || !cv_is_known_layer(layer))
        return CV_ERR_ARGUMENT;
    if (dim != DIM_OVERWORLD || mc < MC_B1_8 || mc > MC_1_17)
        return 0;
    return cv_find_layer_id(mc, layer, scale) >= 0;
}

static int cv_has_river(const int *cells, int width, int left, int top, int side)
{
    for (int j = 0; j < side; j++)
    {
        for (int i = 0; i < side; i++)
        {
            if (cells[(top + j) * width + left + i] == river)
                return 1;
        }
    }
    return 0;
}

/* Rivers are one cell wide, so a coarse sample shows a river when any of the cells it covers has one.
   The cells are generated in bands to keep the cache small. */
static int cv_gen_merged_rivers(cv_world *world, const Layer *source, int32_t scale,
    int32_t x, int32_t z, int32_t sx, int32_t sz, uint8_t *out)
{
    int side = scale / CV_RIVER_SCALE;
    int width = sx * side;
    int band_rows = CV_RIVER_BAND_CELLS / (width * side);
    if (band_rows < 1)
        band_rows = 1;

    for (int32_t row = 0; row < sz; row += band_rows)
    {
        int rows = sz - row < band_rows ? sz - row : band_rows;
        int status = cv_ensure_cache(world, getMinLayerCacheSize(source, width, rows * side));
        if (status != CV_OK)
            return status;
        if (genArea(source, world->cache, x * side, (z + row) * side, width, rows * side) != 0)
            return CV_ERR_GENERATION;

        for (int j = 0; j < rows; j++)
        {
            for (int32_t i = 0; i < sx; i++)
                out[(row + j) * sx + i] = cv_has_river(world->cache, width, i * side, j * side, side) ? (uint8_t)river : CV_BIOME_NONE;
        }
    }
    return CV_OK;
}

int32_t cv_gen_layer_u8(cv_world *world, int32_t layer, int32_t scale,
    int32_t x, int32_t z, int32_t sx, int32_t sz, uint8_t *out, int32_t capacity)
{
    /* The special layer lies above the scales the layered generator makes biomes at. */
    int status = layer == CV_LAYER_TEMPERATURE_CATEGORY ? cv_check_area_shape(world, scale, sx, sz) : cv_check_area(world, scale, sx, sz);
    if (status != CV_OK)
        return status;
    if (!cv_is_known_layer(layer))
        return CV_ERR_ARGUMENT;
    status = cv_check_output(out, sx, sz, capacity);
    if (status != CV_OK)
        return status;

    const Generator *generator = &world->generator;
    if (cv_layer_supported(generator->mc, generator->dim, layer, scale) != 1)
        return CV_ERR_UNSUPPORTED;

    const Layer *source = &generator->ls.layers[cv_find_layer_id(generator->mc, layer, scale)];
    if (layer == CV_LAYER_RIVER && scale > CV_RIVER_SCALE)
    {
        status = cv_gen_merged_rivers(world, source, scale, x, z, sx, sz, out);
        return status == CV_OK ? sx * sz : status;
    }

    status = cv_ensure_cache(world, getMinLayerCacheSize(source, sx, sz));
    if (status != CV_OK)
        return status;
    if (genArea(source, world->cache, x, z, sx, sz) != 0)
        return CV_ERR_GENERATION;

    int32_t count = sx * sz;
    for (int32_t i = 0; i < count; i++)
    {
        int id = world->cache[i];
        if (layer == CV_LAYER_TEMPERATURE_CATEGORY)
            out[i] = cv_temperature_category(id);
        else
            out[i] = (id < 0 || id > 254) ? CV_BIOME_NONE : (uint8_t)id;
    }
    return count;
}

/* ---- approximate surface height ---- */

int32_t cv_height_supported(int32_t mc, int32_t dim)
{
    if (!cv_is_valid_mc(mc) || !cv_is_valid_dim(dim))
        return CV_ERR_ARGUMENT;
    if (dim == DIM_OVERWORLD)
        return mc >= MC_B1_8;
    return dim == DIM_END && mc >= MC_1_9;
}

static int cv_map_heights(cv_world *world, float *heights, int32_t x, int32_t z, int32_t sx, int32_t sz)
{
    return mapApproxHeight(heights, NULL, &world->generator, cv_surface(world), x, z, sx, sz) == 0 ? CV_OK : CV_ERR_GENERATION;
}

/* One noise column per sample: the height at the corner of the 8-block cell in the middle of the sample. */
static void cv_gen_sparse_end_heights(cv_world *world, int32_t scale, int32_t x, int32_t z, int32_t sx, int32_t sz, float *heights)
{
    int cells = scale / CV_END_CELL_BLOCKS;
    int mid = cells / 2;
    const SurfaceNoise *surface = cv_surface(world);
    double column[CV_END_COLUMN_MAX - CV_END_COLUMN_MIN + 1];

    for (int32_t j = 0; j < sz; j++)
    {
        for (int32_t i = 0; i < sx; i++)
        {
            sampleNoiseColumnEnd(column, surface, &world->generator.en, (x + i) * cells + mid, (z + j) * cells + mid,
                CV_END_COLUMN_MIN, CV_END_COLUMN_MAX);
            heights[j * sx + i] = (float)getSurfaceHeight(column, column, column, column,
                CV_END_COLUMN_MIN, CV_END_COLUMN_MAX, CV_HEIGHT_SCALE, 0, 0);
        }
    }
}

/* The generator maps heights of a connected area at 1:4 only, so coarser scales take one sample per call. */
static int cv_gen_sparse_heights(cv_world *world, int32_t scale, int32_t x, int32_t z, int32_t sx, int32_t sz, float *heights)
{
    if (world->generator.dim == DIM_END)
    {
        cv_gen_sparse_end_heights(world, scale, x, z, sx, sz, heights);
        return CV_OK;
    }

    int cells = scale / CV_HEIGHT_SCALE;
    int mid = cells / 2;
    for (int32_t j = 0; j < sz; j++)
    {
        for (int32_t i = 0; i < sx; i++)
        {
            int status = cv_map_heights(world, heights + j * sx + i, (x + i) * cells + mid, (z + j) * cells + mid, 1, 1);
            if (status != CV_OK)
                return status;
        }
    }
    return CV_OK;
}

int32_t cv_gen_heights(cv_world *world, int32_t scale, int32_t x, int32_t z, int32_t sx, int32_t sz, int16_t *out, int32_t capacity)
{
    int status = cv_check_area(world, scale, sx, sz);
    if (status != CV_OK)
        return status;
    status = cv_check_output(out, sx, sz, capacity);
    if (status != CV_OK)
        return status;

    const Generator *generator = &world->generator;
    if (cv_height_supported(generator->mc, generator->dim) != 1 || scale < CV_HEIGHT_SCALE)
        return CV_ERR_UNSUPPORTED;

    int32_t count = sx * sz;
    float *heights = (float *)calloc((size_t)count, sizeof(float));
    if (!heights)
        return CV_ERR_MEMORY;

    status = scale == CV_HEIGHT_SCALE
        ? cv_map_heights(world, heights, x, z, sx, sz)
        : cv_gen_sparse_heights(world, scale, x, z, sx, sz, heights);
    for (int32_t i = 0; status == CV_OK && i < count; i++)
        out[i] = cv_to_int16(floor((double)heights[i] * CV_HEIGHT_UNIT + 0.5));

    free(heights);
    return status == CV_OK ? count : status;
}
