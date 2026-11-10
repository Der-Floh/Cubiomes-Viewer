static int cv_is_valid_structure(int32_t stype)
{
    return stype >= 0 && stype < FEATURE_NUM;
}

/* Feature only marks generation attempts before 1.13 and End_Island has no viability check in cubiomes. */
static int cv_is_locatable_structure(int32_t stype)
{
    return cv_is_valid_structure(stype) && stype != Feature && stype != End_Island;
}

static const SurfaceNoise *cv_end_surface(cv_world *world)
{
    if (!world->has_end_surface)
    {
        initSurfaceNoise(&world->end_surface, DIM_END, world->generator.seed);
        world->has_end_surface = 1;
    }
    return &world->end_surface;
}

static int cv_can_generate_at(cv_world *world, int32_t stype, Pos pos)
{
    Generator *generator = &world->generator;
    if (!isViableStructurePos(stype, generator, pos.x, pos.z, 0))
        return 0;
    if (stype == End_City)
        return isViableEndCityTerrain(generator, cv_end_surface(world), pos.x, pos.z) != 0;
    return isViableStructureTerrain(stype, generator, pos.x, pos.z) != 0;
}

/* CV_OK when the world can answer questions about the structure type; 'config' is then filled. */
static int cv_check_structure_world(const cv_world *world, int32_t stype, StructureConfig *config, int *exists)
{
    if (!world || !cv_is_valid_structure(stype))
        return CV_ERR_ARGUMENT;
    if (!world->is_seeded)
        return CV_ERR_STATE;
    if (!cv_is_locatable_structure(stype))
        return CV_ERR_UNSUPPORTED;

    *exists = getStructureConfig(stype, world->generator.mc, config);
    if (*exists && config->dim != world->generator.dim)
        return CV_ERR_STATE;
    return CV_OK;
}

int32_t cv_structure_config(int32_t stype, int32_t mc, int32_t *dimension, int32_t *region_blocks)
{
    if (!cv_is_valid_structure(stype) || !cv_is_valid_mc(mc) || !dimension || !region_blocks)
        return CV_ERR_ARGUMENT;

    StructureConfig config;
    int exists = getStructureConfig(stype, mc, &config);
    *dimension = config.dim;
    *region_blocks = config.regionSize * 16;
    return exists ? 1 : 0;
}

int32_t cv_structure_pos(int32_t stype, int32_t mc, uint64_t seed, int32_t reg_x, int32_t reg_z, int32_t *x, int32_t *z)
{
    if (!cv_is_valid_structure(stype) || !cv_is_valid_mc(mc) || !x || !z)
        return CV_ERR_ARGUMENT;

    Pos pos = {0, 0};
    int found = getStructurePos(stype, mc, seed, reg_x, reg_z, &pos);
    *x = pos.x;
    *z = pos.z;
    return found ? 1 : 0;
}

int32_t cv_structure_viable(cv_world *world, int32_t stype, int32_t x, int32_t z)
{
    StructureConfig config;
    int exists = 0;
    int status = cv_check_structure_world(world, stype, &config, &exists);
    if (status != CV_OK)
        return status;
    if (!exists)
        return 0;

    Pos pos = {x, z};
    return cv_can_generate_at(world, stype, pos);
}

int32_t cv_structures_in_area(cv_world *world, int32_t stype, int32_t x0, int32_t z0, int32_t x1, int32_t z1, int32_t *positions, int32_t capacity)
{
    StructureConfig config;
    int exists = 0;
    int status = cv_check_structure_world(world, stype, &config, &exists);
    if (status != CV_OK)
        return status;
    if (x1 <= x0 || z1 <= z0 || !positions || capacity < 0)
        return CV_ERR_ARGUMENT;
    if (!exists)
        return 0;

    const Generator *generator = &world->generator;
    int region_blocks = config.regionSize * 16;
    int rx0 = floordiv(x0, region_blocks), rx1 = floordiv(x1 - 1, region_blocks);
    int rz0 = floordiv(z0, region_blocks), rz1 = floordiv(z1 - 1, region_blocks);
    if ((int64_t)(rx1 - rx0 + 1) * (rz1 - rz0 + 1) > CV_MAX_STRUCTURE_ATTEMPTS)
        return CV_ERR_ARGUMENT;

    int32_t count = 0;
    for (int rz = rz0; rz <= rz1; rz++)
    {
        for (int rx = rx0; rx <= rx1; rx++)
        {
            Pos pos = {0, 0};
            if (!getStructurePos(stype, generator->mc, generator->seed, rx, rz, &pos))
                continue;
            if (pos.x < x0 || pos.x >= x1 || pos.z < z0 || pos.z >= z1)
                continue;
            if (!cv_can_generate_at(world, stype, pos))
                continue;
            if (count >= capacity)
                return CV_ERR_CAPACITY;

            positions[2 * count] = pos.x;
            positions[2 * count + 1] = pos.z;
            count++;
        }
    }
    return count;
}
