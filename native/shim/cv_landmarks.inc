static int cv_check_overworld(const cv_world *world)
{
    if (!world)
        return CV_ERR_ARGUMENT;
    if (!world->is_seeded || world->generator.dim != DIM_OVERWORLD)
        return CV_ERR_STATE;
    return CV_OK;
}

int32_t cv_spawn(cv_world *world, int32_t *x, int32_t *z)
{
    int status = cv_check_overworld(world);
    if (status != CV_OK)
        return status;
    if (!x || !z)
        return CV_ERR_ARGUMENT;

    /* Before Beta 1.8 the spawn is a random sand block and cannot be predicted. */
    if (world->generator.mc <= MC_B1_7)
        return 0;

    Pos spawn = getSpawn(&world->generator);
    *x = spawn.x;
    *z = spawn.z;
    return 1;
}

int32_t cv_strongholds(cv_world *world, int32_t *positions, int32_t capacity)
{
    int status = cv_check_overworld(world);
    if (status != CV_OK)
        return status;
    if (!positions || capacity < 0)
        return CV_ERR_ARGUMENT;

    const Generator *generator = &world->generator;
    StrongholdIter iterator;
    initFirstStronghold(&iterator, generator->mc, generator->seed);

    int32_t count = 0;
    while (count < capacity && count < CV_MAX_STRONGHOLDS && nextStronghold(&iterator, generator) > 0)
    {
        positions[2 * count] = iterator.pos.x;
        positions[2 * count + 1] = iterator.pos.z;
        count++;
    }
    return count;
}

int32_t cv_slime_chunks(uint64_t seed, int32_t chunk_x, int32_t chunk_z, int32_t size_x, int32_t size_z, uint8_t *out, int32_t capacity)
{
    if (!out || size_x <= 0 || size_z <= 0 || size_x > CV_MAX_AREA_SIDE || size_z > CV_MAX_AREA_SIDE)
        return CV_ERR_ARGUMENT;

    int32_t count = size_x * size_z;
    if (capacity < count)
        return CV_ERR_CAPACITY;

    for (int32_t j = 0; j < size_z; j++)
    {
        for (int32_t i = 0; i < size_x; i++)
            out[j * size_x + i] = (uint8_t)isSlimeChunk(seed, chunk_x + i, chunk_z + j);
    }
    return count;
}

int32_t cv_end_gateways(cv_world *world, int32_t *positions, int32_t capacity)
{
    if (!world)
        return CV_ERR_ARGUMENT;
    if (!world->is_seeded || world->generator.dim != DIM_END)
        return CV_ERR_STATE;
    if (!positions || capacity < 0)
        return CV_ERR_ARGUMENT;

    /* cubiomes can only follow gateways from 1.13 on. */
    const Generator *generator = &world->generator;
    if (generator->mc <= MC_1_12)
        return 0;
    if (capacity < CV_END_GATEWAYS)
        return CV_ERR_CAPACITY;

    Pos gateways[CV_END_GATEWAYS];
    getFixedEndGateways(generator->mc, generator->seed, gateways);

    const SurfaceNoise *surface = cv_surface(world);
    for (int i = 0; i < CV_END_GATEWAYS; i++)
    {
        Pos linked = getLinkedGatewayPos(&generator->en, surface, generator->seed, gateways[i]);
        positions[4 * i] = gateways[i].x;
        positions[4 * i + 1] = gateways[i].z;
        positions[4 * i + 2] = linked.x;
        positions[4 * i + 3] = linked.z;
    }
    return CV_END_GATEWAYS;
}
