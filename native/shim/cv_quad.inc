/* Quad structures: four structures of one type that attempt to generate close enough together that one point lies
   within 128 blocks of all of them. Seeds are given by their lower 48 bits, which alone decide where structures
   attempt to generate. A quad base is such a seed plus the salt of the structure type, with the four structures in
   regions (0,0) to (1,1); bases are shared by every type with the same placement. */

#define CV_SEED48_MASK ((1ULL << 48) - 1)
#define CV_QUAD_HUT_SIZE_X (7 + 1)
#define CV_QUAD_HUT_SIZE_Y (7 + 1)
#define CV_QUAD_HUT_SIZE_Z (9 + 1)
#define CV_QUAD_HUT_LOW_BITS 20
#define CV_QUAD_HUT_HIGH_BITS 28

/* The placement every quad-hut base is checked with: 32-chunk regions with structures in their first 24 chunks. */
static const StructureConfig cv_quad_hut_placement = { 0, 32, 24, Swamp_Hut, 0, 0 };

static int cv_is_quad_type(int32_t stype)
{
    return stype == Swamp_Hut || stype == Desert_Pyramid || stype == Jungle_Pyramid || stype == Igloo;
}

/* Fills 'config', with the salt of the rules in use, and returns 1 when the version has quads of the type. */
static int cv_quad_config(int32_t stype, int32_t mc, StructureConfig *config)
{
    if (!cv_is_quad_type(stype) || !getStructureConfig_override(stype, mc, config))
        return 0;
    return config->regionSize == 32 && config->chunkRange == 24;
}

static int32_t cv_quad_hut_class_of_base(uint64_t base)
{
    const uint64_t *tables[] = { low20QuadIdeal, low20QuadClassic, low20QuadHutNormal, low20QuadHutBarely };
    uint64_t low20 = base & ((1ULL << CV_QUAD_HUT_LOW_BITS) - 1);
    int32_t quality;

    for (quality = 0; quality < 4; quality++)
    {
        const uint64_t *value;
        for (value = tables[quality]; *value; value++)
        {
            if (*value == low20)
                return quality + 1;
        }
    }
    return 0;
}

static float cv_quad_hut_radius_of_base(uint64_t base)
{
    return isQuadBaseFeature24(cv_quad_hut_placement, base & CV_SEED48_MASK, CV_QUAD_HUT_SIZE_X, CV_QUAD_HUT_SIZE_Y, CV_QUAD_HUT_SIZE_Z);
}

int32_t cv_quad_supported(int32_t stype, int32_t mc)
{
    StructureConfig config;
    if (!cv_is_valid_mc(mc))
        return CV_ERR_ARGUMENT;
    return cv_quad_config(stype, mc, &config);
}

int32_t cv_quad_hut_low_bits(int32_t quality, uint64_t *values, int32_t capacity)
{
    const uint64_t *tables[] = { low20QuadIdeal, low20QuadClassic, low20QuadHutNormal, low20QuadHutBarely };
    const uint64_t *value;
    int32_t count = 0;

    if (quality < 1 || quality > 4 || !values || capacity < 0)
        return CV_ERR_ARGUMENT;

    for (value = tables[quality - 1]; *value; value++)
    {
        if (count >= capacity)
            return CV_ERR_CAPACITY;
        values[count++] = *value;
    }
    return count;
}

int32_t cv_quad_hut_scan(uint64_t low20, uint32_t first_high, uint32_t count, uint64_t *bases, int32_t capacity)
{
    uint64_t high;
    uint64_t end = (uint64_t)first_high + count;
    int32_t found = 0;

    if (low20 >> CV_QUAD_HUT_LOW_BITS || end > (1ULL << CV_QUAD_HUT_HIGH_BITS) || !bases || capacity < 0)
        return CV_ERR_ARGUMENT;

    for (high = first_high; high < end; high++)
    {
        uint64_t base = (high << CV_QUAD_HUT_LOW_BITS) | low20;
        if (cv_quad_hut_radius_of_base(base) <= 0)
            continue;
        if (found >= capacity)
            return CV_ERR_CAPACITY;
        bases[found++] = base;
    }
    return found;
}

int32_t cv_quad_hut_base_info(uint64_t base, float *radius)
{
    if (base >> 48 || !radius)
        return CV_ERR_ARGUMENT;

    *radius = cv_quad_hut_radius_of_base(base);
    return *radius > 0 ? cv_quad_hut_class_of_base(base) : 0;
}

static int32_t cv_find_seed_of_base(int32_t stype, int32_t mc, uint64_t base, int32_t reg_x, int32_t reg_z, uint64_t *seed)
{
    StructureConfig config;
    if (!cv_is_valid_mc(mc) || base >> 48 || !seed)
        return CV_ERR_ARGUMENT;
    if (!cv_quad_config(stype, mc, &config))
        return CV_ERR_UNSUPPORTED;

    *seed = moveStructure((base - config.salt) & CV_SEED48_MASK, reg_x, reg_z);
    return CV_OK;
}

int32_t cv_quad_seed_of_base(const cv_rules *rules, int32_t stype, int32_t mc, uint64_t base, int32_t reg_x, int32_t reg_z, uint64_t *seed)
{
    if (!cv_are_valid_rules(rules))
        return CV_ERR_ARGUMENT;

    const cv_rules *previous = cv_enter_rules(rules);
    int32_t result = cv_find_seed_of_base(stype, mc, base, reg_x, reg_z, seed);
    cv_enter_rules(previous);
    return result;
}

/* Writes, for each quad whose regions start in [reg_x0, reg_x1] x [reg_z0, reg_z1], its first region and its
   quality class (1 ideal, 2 classic, 3 normal, 4 barely, 5 other) as triples, and returns the number of quads. */
static int32_t cv_find_quads_in_area(int32_t stype, int32_t mc, uint64_t seed, int32_t reg_x0, int32_t reg_z0, int32_t reg_x1, int32_t reg_z1, int32_t *quads, int32_t capacity)
{
    StructureConfig config;
    int32_t found = 0;
    int32_t rx, rz;

    if (!cv_is_valid_mc(mc) || reg_x1 < reg_x0 || reg_z1 < reg_z0 || !quads || capacity < 0)
        return CV_ERR_ARGUMENT;
    if ((int64_t)(reg_x1 - reg_x0 + 1) * (reg_z1 - reg_z0 + 1) > CV_MAX_STRUCTURE_ATTEMPTS)
        return CV_ERR_ARGUMENT;
    if (!cv_quad_config(stype, mc, &config))
        return cv_is_quad_type(stype) ? 0 : CV_ERR_UNSUPPORTED;

    for (rz = reg_z0; rz <= reg_z1; rz++)
    {
        for (rx = reg_x0; rx <= reg_x1; rx++)
        {
            uint64_t base = (moveStructure(seed & CV_SEED48_MASK, -rx, -rz) + config.salt) & CV_SEED48_MASK;
            int32_t quality;
            if (cv_quad_hut_radius_of_base(base) <= 0)
                continue;
            if (found >= capacity)
                return CV_ERR_CAPACITY;

            quality = cv_quad_hut_class_of_base(base);
            quads[3 * found] = rx;
            quads[3 * found + 1] = rz;
            quads[3 * found + 2] = quality ? quality : CV_QUAD_OTHER;
            found++;
        }
    }
    return found;
}

int32_t cv_quads_in_area(const cv_rules *rules, int32_t stype, int32_t mc, uint64_t seed, int32_t reg_x0, int32_t reg_z0, int32_t reg_x1, int32_t reg_z1, int32_t *quads, int32_t capacity)
{
    if (!cv_are_valid_rules(rules))
        return CV_ERR_ARGUMENT;

    const cv_rules *previous = cv_enter_rules(rules);
    int32_t result = cv_find_quads_in_area(stype, mc, seed, reg_x0, reg_z0, reg_x1, reg_z1, quads, capacity);
    cv_enter_rules(previous);
    return result;
}
