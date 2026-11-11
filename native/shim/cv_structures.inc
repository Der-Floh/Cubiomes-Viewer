static int cv_is_valid_structure(int32_t stype)
{
    return stype >= 0 && stype < FEATURE_NUM;
}

/* Feature only marks generation attempts before 1.13 and End_Island has no viability check in cubiomes. */
static int cv_is_locatable_structure(int32_t stype)
{
    return cv_is_valid_structure(stype) && stype != Feature && stype != End_Island;
}

static int cv_can_generate_at(cv_world *world, int32_t stype, Pos pos)
{
    Generator *generator = &world->generator;
    if (!isViableStructurePos(stype, generator, pos.x, pos.z, 0))
        return 0;
    if (stype == End_City)
        return isViableEndCityTerrain(generator, cv_surface(world), pos.x, pos.z) != 0;
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

int32_t cv_structure_variant(cv_world *world, int32_t stype, int32_t x, int32_t z, cv_variant *variant)
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
    if (!getVariant(&found, stype, generator->mc, generator->seed, x, z, cv_variant_biome(world, stype, pos)))
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
