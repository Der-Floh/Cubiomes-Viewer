/* The exact terrain of cubiomes' terrain generator: which blocks of a column are solid, from the noise alone, before
   the game fills in water, carves caves and ravines or places anything on the ground. */

/* The noise cells of a dimension: width blocks across and height blocks high, column_max + 1 of them per column. */
typedef struct cv_terrain_cells
{
    int width;
    int height;
    int column_max;
    int min_y;
    int interpolation;
} cv_terrain_cells;

int32_t cv_terrain_supported(int32_t mc, int32_t dim)
{
    if (!cv_is_valid_mc(mc) || !cv_is_valid_dim(dim))
        return CV_ERR_ARGUMENT;
    if (dim == DIM_OVERWORLD)
        return mc >= MC_1_14;
    return dim == DIM_END && mc >= MC_1_9;
}

/* The terrain noise of the dimension the world is applied to, set up on first use. */
static TerrainNoise *cv_terrain(cv_world *world)
{
    if (world->has_terrain)
        return world->terrain;
    if (!world->terrain && !(world->terrain = (TerrainNoise *)malloc(sizeof(TerrainNoise))))
        return NULL;

    const Generator *generator = &world->generator;
    memset(world->terrain, 0, sizeof(TerrainNoise));
    setupTerrainNoise(world->terrain, generator->mc, generator->flags);
    initTerrainNoise(world->terrain, generator->seed, generator->dim);
    world->has_terrain = 1;
    return world->terrain;
}

static cv_terrain_cells cv_get_terrain_cells(const Generator *generator)
{
    cv_terrain_cells cells;
    cells.interpolation = generator->mc >= MC_1_18 ? INTERP_1_18 : INTERP_PRE_1_18;
    if (generator->dim == DIM_END)
    {
        cells.width = 8;
        cells.height = 4;
        cells.column_max = 32;
        cells.min_y = 0;
        return cells;
    }

    cells.width = 4;
    cells.height = 8;
    cells.column_max = generator->mc >= MC_1_18 ? 48 : 32;
    cells.min_y = generator->mc >= MC_1_18 ? -64 : 0;
    return cells;
}

/* Writes, row by row, the Y of the lowest air block above the highest solid block of each column. */
static int32_t cv_fill_surface(cv_world *world, int32_t x, int32_t z, int32_t sx, int32_t sz, int16_t *out)
{
    TerrainNoise *terrain = cv_terrain(world);
    if (!terrain)
        return CV_ERR_MEMORY;

    cv_terrain_cells cells = cv_get_terrain_cells(&world->generator);
    size_t column_length = (size_t)cells.column_max + 1;
    int cell_x0 = floordiv(x, cells.width);
    int cell_z0 = floordiv(z, cells.width);
    int cells_x = floordiv(x + sx - 1, cells.width) - cell_x0 + 2;
    int cells_z = floordiv(z + sz - 1, cells.width) - cell_z0 + 2;
    double *columns = (double *)malloc((size_t)cells_x * cells_z * column_length * sizeof(double));
    if (!columns)
        return CV_ERR_MEMORY;

    for (int i = 0; i < cells_x; i++)
    {
        for (int j = 0; j < cells_z; j++)
            sampleNoiseColumn(terrain, cell_x0 + i, cell_z0 + j, 0, cells.column_max, columns + ((size_t)i * cells_z + j) * column_length);
    }

    for (int32_t dz = 0; dz < sz; dz++)
    {
        for (int32_t dx = 0; dx < sx; dx++)
        {
            int block_x = x + dx;
            int block_z = z + dz;
            int i = floordiv(block_x, cells.width) - cell_x0;
            int j = floordiv(block_z, cells.width) - cell_z0;
            const double *column = columns + ((size_t)i * cells_z + j) * column_length;
            double offset_x = (block_x - (cell_x0 + i) * cells.width) / (double)cells.width;
            double offset_z = (block_z - (cell_z0 + j) * cells.width) / (double)cells.width;
            out[dz * sx + dx] = (int16_t)generateColumn(NULL, column, column + column_length, column + cells_z * column_length,
                column + (cells_z + 1) * column_length, 0, cells.column_max, cells.height, offset_x, offset_z, cells.interpolation, cells.min_y, 1);
        }
    }

    free(columns);
    return sx * sz;
}

int32_t cv_gen_surface(cv_world *world, int32_t x, int32_t z, int32_t sx, int32_t sz, int16_t *out, int32_t capacity)
{
    if (!world || !out || sx <= 0 || sz <= 0 || sx > CV_MAX_SURFACE_SIDE || sz > CV_MAX_SURFACE_SIDE)
        return CV_ERR_ARGUMENT;
    if (!world->is_seeded)
        return CV_ERR_STATE;
    if (capacity < sx * sz)
        return CV_ERR_CAPACITY;
    if (cv_terrain_supported(world->generator.mc, world->generator.dim) != 1)
        return CV_ERR_UNSUPPORTED;
    return cv_fill_surface(world, x, z, sx, sz, out);
}
