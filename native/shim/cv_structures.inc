static int cv_is_valid_structure(int32_t stype)
{
    return stype >= 0 && stype < FEATURE_NUM;
}

/* Feature only marks generation attempts before 1.13 and End_Island has no viability check in cubiomes. */
static int cv_is_locatable_structure(int32_t stype)
{
    return cv_is_valid_structure(stype) && stype != Feature && stype != End_Island;
}

static int cv_is_salt_replaced(int32_t stype)
{
    return ((cv_rules_in_use()->salted >> stype) & 1u) != 0;
}

/* The biome cubiomes judges an outpost by, sampled where its outpost check samples it. */
static int cv_outpost_biome(Generator *generator, int chunk_x, int chunk_z)
{
    if (generator->mc >= MC_1_18)
    {
        uint64_t rng = chunkGenerateRnd(generator->seed, chunk_x, chunk_z);
        int offset_x, offset_z;
        switch (nextInt(&rng, 4))
        {
        case 0: offset_x = +15; offset_z = +15; break;
        case 1: offset_x = -15; offset_z = +15; break;
        case 2: offset_x = -15; offset_z = -15; break;
        default: offset_x = +15; offset_z = -15; break;
        }
        return getBiomeAt(generator, 0, (chunk_x * 32 + offset_x) / 2 >> 2, 319 >> 2, (chunk_z * 32 + offset_z) / 2 >> 2);
    }

    Layer *entry = generator->entry;
    int biome;
    if (generator->mc >= MC_1_16_1)
    {
        generator->entry = &generator->ls.layers[L_RIVER_MIX_4];
        biome = getBiomeAt(generator, 0, chunk_x * 4 + 2, 319 >> 2, chunk_z * 4 + 2);
    }
    else
    {
        generator->entry = &generator->ls.layers[L_VORONOI_1];
        biome = getBiomeAt(generator, 0, chunk_x * 16 + 9, 319 >> 2, chunk_z * 16 + 9);
    }
    generator->entry = entry;
    return biome;
}

/* cubiomes keeps outposts away from the villages of the game's village salt; this is its outpost check with the salt
   of the rules in use, for worlds that replace it. */
static int cv_is_viable_outpost(Generator *generator, int x, int z)
{
    if (generator->mc <= MC_1_13)
        return 0;

    int chunk_x = x >> 4, chunk_z = z >> 4;
    uint64_t rng = generator->seed;
    setAttemptSeed(&rng, chunk_x, chunk_z);
    if (nextInt(&rng, 5) != 0)
        return 0;

    StructureConfig village;
    if (!getStructureConfig_override(Village, generator->mc, &village))
        return 0;

    int cx0 = chunk_x - 10, cx1 = chunk_x + 10, cz0 = chunk_z - 10, cz1 = chunk_z + 10;
    for (int rz = floordiv(cz0, village.regionSize); rz <= floordiv(cz1, village.regionSize); rz++)
    {
        for (int rx = floordiv(cx0, village.regionSize); rx <= floordiv(cx1, village.regionSize); rx++)
        {
            Pos pos = getFeaturePos(village, generator->seed, rx, rz);
            int cx = pos.x >> 4, cz = pos.z >> 4;
            if (cx < cx0 || cx > cx1 || cz < cz0 || cz > cz1)
                continue;
            if (generator->mc >= MC_1_16_1 || isViableStructurePos(Village, generator, pos.x, pos.z, 0))
                return 0;
        }
    }

    int biome = cv_outpost_biome(generator, chunk_x, chunk_z);
    return biome >= 0 && isViableFeatureBiome(generator->mc, Outpost, biome);
}

static int cv_is_viable_position(int32_t stype, Generator *generator, Pos pos)
{
    if (stype == Outpost && cv_is_salt_replaced(Village))
        return cv_is_viable_outpost(generator, pos.x, pos.z);
    return isViableStructurePos(stype, generator, pos.x, pos.z, 0);
}

/* cubiomes rolls whether a geode generates with the game's geode salt; this is its geode variant with the salt of the
   rules in use, for worlds that replace it. */
static int cv_get_geode_variant(StructureVariant *variant, int mc, uint64_t seed, int x, int z)
{
    StructureConfig geode;
    getStructureConfig_override(Geode, mc, &geode);
    memset(variant, 0, sizeof(*variant));
    variant->start = -1;
    variant->biome = -1;
    variant->y = 320;

    uint64_t population = getPopulationSeed(mc, seed, x & ~15, z & ~15) + geode.salt;
    if (mc >= MC_1_18)
    {
        Xoroshiro xr;
        xSetSeed(&xr, population);
        if (xNextFloat(&xr) >= geode.rarity)
            return 0;
        variant->x = xNextIntJ(&xr, 16) - (x & 15);
        variant->z = xNextIntJ(&xr, 16) - (z & 15);
        variant->y = xNextIntJ(&xr, 1 + 30 + 58) - 58;
        variant->size = xNextIntJ(&xr, 2) + 3;
        xSkipN(&xr, 2);
        variant->cracked = xNextFloat(&xr) < 0.95;
    }
    else
    {
        uint64_t rng;
        setSeed(&rng, population);
        if (nextFloat(&rng) >= geode.rarity)
            return 0;
        variant->x = nextInt(&rng, 16) - (x & 15);
        variant->z = nextInt(&rng, 16) - (z & 15);
        variant->y = nextInt(&rng, 1 + 46 - 6) + 6;
        variant->size = nextInt(&rng, 2) + 3;
        skipNextN(&rng, 2);
        variant->cracked = nextFloat(&rng) < 0.95;
    }

    variant->x += 5;
    variant->y += 5;
    variant->z += 5;
    return 1;
}

static int cv_get_variant(StructureVariant *variant, int32_t stype, const Generator *generator, int x, int z, int biome)
{
    if (stype == Geode && cv_is_salt_replaced(Geode))
        return cv_get_geode_variant(variant, generator->mc, generator->seed, x, z);
    return getVariant(variant, stype, generator->mc, generator->seed, x, z, biome);
}

static int cv_can_generate_at(cv_world *world, int32_t stype, Pos pos)
{
    Generator *generator = &world->generator;
    if (!cv_is_viable_position(stype, generator, pos))
        return 0;
    if (stype == End_City)
        return isViableEndCityTerrain(generator, cv_surface(world), pos.x, pos.z) != 0;
    return !cv_estimates_terrain() || isViableStructureTerrain(stype, generator, pos.x, pos.z) != 0;
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

/* The types isViableFeatureBiome knows; it ends the process for any other. */
static int cv_has_biome_rule(int32_t stype)
{
    switch (stype)
    {
    case Desert_Pyramid:
    case Jungle_Pyramid:
    case Swamp_Hut:
    case Igloo:
    case Village:
    case Ocean_Ruin:
    case Shipwreck:
    case Monument:
    case Mansion:
    case Outpost:
    case Ruined_Portal:
    case Ruined_Portal_N:
    case Ancient_City:
    case Treasure:
    case Mineshaft:
    case Desert_Well:
    case Fortress:
    case Bastion:
    case End_City:
    case End_Gateway:
    case Trail_Ruins:
    case Trial_Chambers:
        return 1;
    default:
        return 0;
    }
}

int32_t cv_structure_biome_viable(int32_t stype, int32_t mc, int32_t biome)
{
    if (!cv_is_valid_structure(stype) || !cv_is_valid_mc(mc) || biome < 0 || biome >= CV_BIOME_NONE)
        return CV_ERR_ARGUMENT;
    if (!cv_has_biome_rule(stype))
        return CV_ERR_UNSUPPORTED;
    return isViableFeatureBiome(mc, stype, biome) != 0;
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

static int32_t cv_find_structure_pos(int32_t stype, int32_t mc, uint64_t seed, int32_t reg_x, int32_t reg_z, int32_t *x, int32_t *z)
{
    if (!cv_is_valid_structure(stype) || !cv_is_valid_mc(mc) || !x || !z)
        return CV_ERR_ARGUMENT;

    Pos pos = {0, 0};
    int found = getStructurePos(stype, mc, seed, reg_x, reg_z, &pos);
    *x = pos.x;
    *z = pos.z;
    return found ? 1 : 0;
}

int32_t cv_structure_pos(const cv_rules *rules, int32_t stype, int32_t mc, uint64_t seed, int32_t reg_x, int32_t reg_z, int32_t *x, int32_t *z)
{
    if (!cv_are_valid_rules(rules))
        return CV_ERR_ARGUMENT;

    const cv_rules *previous = cv_enter_rules(rules);
    int32_t result = cv_find_structure_pos(stype, mc, seed, reg_x, reg_z, x, z);
    cv_enter_rules(previous);
    return result;
}

static int32_t cv_check_structure_viable(cv_world *world, int32_t stype, int32_t x, int32_t z)
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

int32_t cv_structure_viable(cv_world *world, int32_t stype, int32_t x, int32_t z)
{
    const cv_rules *previous = cv_enter_rules(world ? &world->rules : NULL);
    int32_t result = cv_check_structure_viable(world, stype, x, z);
    cv_enter_rules(previous);
    return result;
}

/* Collects the generation attempts of a structure type in [x0, x1) x [z0, z1), region by region.
   With a world only the attempts at which a structure can generate are kept. */
static int32_t cv_collect_structures(cv_world *world, int32_t stype, int32_t mc, uint64_t seed, const StructureConfig *config,
    int32_t x0, int32_t z0, int32_t x1, int32_t z1, int32_t *positions, int32_t capacity)
{
    if (x1 <= x0 || z1 <= z0 || !positions || capacity < 0)
        return CV_ERR_ARGUMENT;

    int region_blocks = config->regionSize * 16;
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
            if (!getStructurePos(stype, mc, seed, rx, rz, &pos))
                continue;
            if (pos.x < x0 || pos.x >= x1 || pos.z < z0 || pos.z >= z1)
                continue;
            if (world && !cv_can_generate_at(world, stype, pos))
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

static int32_t cv_find_structures_in_area(cv_world *world, int32_t stype, int32_t x0, int32_t z0, int32_t x1, int32_t z1, int32_t *positions, int32_t capacity)
{
    StructureConfig config;
    int exists = 0;
    int status = cv_check_structure_world(world, stype, &config, &exists);
    if (status != CV_OK)
        return status;
    if (!exists)
        return x1 <= x0 || z1 <= z0 || !positions || capacity < 0 ? CV_ERR_ARGUMENT : 0;

    const Generator *generator = &world->generator;
    return cv_collect_structures(world, stype, generator->mc, generator->seed, &config, x0, z0, x1, z1, positions, capacity);
}

int32_t cv_structures_in_area(cv_world *world, int32_t stype, int32_t x0, int32_t z0, int32_t x1, int32_t z1, int32_t *positions, int32_t capacity)
{
    const cv_rules *previous = cv_enter_rules(world ? &world->rules : NULL);
    int32_t result = cv_find_structures_in_area(world, stype, x0, z0, x1, z1, positions, capacity);
    cv_enter_rules(previous);
    return result;
}

static int32_t cv_find_attempts_in_area(int32_t stype, int32_t mc, uint64_t seed, int32_t x0, int32_t z0, int32_t x1, int32_t z1, int32_t *positions, int32_t capacity)
{
    if (!cv_is_valid_structure(stype) || !cv_is_valid_mc(mc))
        return CV_ERR_ARGUMENT;
    if (!cv_is_locatable_structure(stype))
        return CV_ERR_UNSUPPORTED;

    StructureConfig config;
    if (!getStructureConfig(stype, mc, &config))
        return x1 <= x0 || z1 <= z0 || !positions || capacity < 0 ? CV_ERR_ARGUMENT : 0;
    return cv_collect_structures(NULL, stype, mc, seed, &config, x0, z0, x1, z1, positions, capacity);
}

int32_t cv_structure_attempts_in_area(const cv_rules *rules, int32_t stype, int32_t mc, uint64_t seed, int32_t x0, int32_t z0, int32_t x1, int32_t z1, int32_t *positions, int32_t capacity)
{
    if (!cv_are_valid_rules(rules))
        return CV_ERR_ARGUMENT;

    const cv_rules *previous = cv_enter_rules(rules);
    int32_t result = cv_find_attempts_in_area(stype, mc, seed, x0, z0, x1, z1, positions, capacity);
    cv_enter_rules(previous);
    return result;
}

/* The managed side names these piece kinds; a changed order in cubiomes must not go unnoticed. */
typedef char cv_assert_end_ship[(END_SHIP == 12) ? 1 : -1];
typedef char cv_assert_spawner[(BRIDGE_SPAWNER == 5) ? 1 : -1];
typedef char cv_assert_nether_wart[(CORRIDOR_NETHER_WART == 13) ? 1 : -1];

/* The biome that decides the variant: villages report it through their viability check, ruined portals use the chunk centre. */
static int cv_variant_biome(cv_world *world, int32_t stype, Pos pos)
{
    Generator *generator = &world->generator;
    if (stype == Village)
    {
        int biome = isViableStructurePos(Village, generator, pos.x, pos.z, 0);
        return biome > 0 ? biome : -1;
    }
    if (stype == Ruined_Portal || stype == Ruined_Portal_N)
    {
        int y = generator->dim == DIM_OVERWORLD ? 64 >> 2 : 0;
        return getBiomeAt(generator, 4, (pos.x >> 2) + 2, y, (pos.z >> 2) + 2);
    }
    return -1;
}

static int32_t cv_find_structure_variant(cv_world *world, int32_t stype, int32_t x, int32_t z, cv_variant *variant)
{
    StructureConfig config;
    int exists = 0;
    int status = cv_check_structure_world(world, stype, &config, &exists);
    if (status != CV_OK)
        return status;
    if (!variant)
        return CV_ERR_ARGUMENT;

    memset(variant, 0, sizeof(*variant));
    if (!exists)
        return 0;

    Pos pos = {x, z};
    StructureVariant found;
    const Generator *generator = &world->generator;
    if (!cv_get_variant(&found, stype, generator, x, z, cv_variant_biome(world, stype, pos)))
        return 0;

    variant->abandoned = found.abandoned;
    variant->giant = found.giant;
    variant->underground = found.underground;
    variant->airpocket = found.airpocket;
    variant->basement = found.basement;
    variant->cracked = found.cracked;
    variant->size = found.size;
    variant->start = found.start == (uint8_t)-1 ? -1 : found.start;
    variant->biome = found.biome;
    variant->rotation = found.rotation;
    variant->mirror = found.mirror;
    variant->x = found.x;
    variant->y = found.y;
    variant->z = found.z;
    variant->sx = found.sx;
    variant->sy = found.sy;
    variant->sz = found.sz;
    return 1;
}

int32_t cv_structure_variant(cv_world *world, int32_t stype, int32_t x, int32_t z, cv_variant *variant)
{
    const cv_rules *previous = cv_enter_rules(world ? &world->rules : NULL);
    int32_t result = cv_find_structure_variant(world, stype, x, z, variant);
    cv_enter_rules(previous);
    return result;
}

int32_t cv_structure_pieces(cv_world *world, int32_t stype, int32_t x, int32_t z, cv_piece *pieces, int32_t capacity)
{
    StructureConfig config;
    int exists = 0;
    int status = cv_check_structure_world(world, stype, &config, &exists);
    if (status != CV_OK)
        return status;
    if (!pieces || capacity < 0)
        return CV_ERR_ARGUMENT;
    if (!exists || (stype != End_City && stype != Fortress))
        return 0;

    Piece *found = (Piece *)calloc(CV_MAX_PIECES, sizeof(Piece));
    if (!found)
        return CV_ERR_MEMORY;

    const Generator *generator = &world->generator;
    int count = stype == End_City
        ? getEndCityPieces(found, generator->seed, x >> 4, z >> 4)
        : getFortressPieces(found, CV_MAX_PIECES, generator->mc, generator->seed, x >> 4, z >> 4);
    if (count > capacity)
    {
        free(found);
        return CV_ERR_CAPACITY;
    }

    for (int i = 0; i < count; i++)
    {
        pieces[i].type = found[i].type;
        pieces[i].rotation = found[i].rot;
        pieces[i].depth = found[i].depth;
        pieces[i].x0 = found[i].bb0.x;
        pieces[i].y0 = found[i].bb0.y;
        pieces[i].z0 = found[i].bb0.z;
        pieces[i].x1 = found[i].bb1.x;
        pieces[i].y1 = found[i].bb1.y;
        pieces[i].z1 = found[i].bb1.z;
    }
    free(found);
    return count;
}
