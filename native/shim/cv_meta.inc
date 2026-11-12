static int cv_is_valid_mc(int32_t mc)
{
    return mc >= MC_B1_7 && mc <= MC_NEWEST;
}

static int cv_is_valid_dim(int32_t dim)
{
    return dim == DIM_NETHER || dim == DIM_OVERWORLD || dim == DIM_END;
}

static int cv_is_valid_scale(int32_t scale)
{
    return scale == 1 || scale == 4 || scale == 16 || scale == 64 || scale == 256 || scale == 1024 || scale == 4096;
}

static int cv_is_layered_overworld(const Generator *generator)
{
    return generator->dim == DIM_OVERWORLD && generator->mc >= MC_B1_8 && generator->mc <= MC_1_17;
}

int32_t cv_abi_version(void)
{
    return CV_ABI_VERSION;
}

int32_t cv_mc_oldest(void)
{
    return MC_B1_7;
}

int32_t cv_mc_newest(void)
{
    return MC_NEWEST;
}

const char *cv_mc_name(int32_t mc)
{
    return cv_is_valid_mc(mc) ? mc2str(mc) : NULL;
}

const char *cv_biome_name(int32_t mc, int32_t biome)
{
    if (!cv_is_valid_mc(mc) || biome < 0 || biome > 255)
        return NULL;
    return biome2str(mc, biome);
}

int32_t cv_biome_dimension(int32_t mc, int32_t biome, int32_t *dimension)
{
    if (!cv_is_valid_mc(mc) || biome < 0 || biome > 255 || !dimension)
        return CV_ERR_ARGUMENT;

    *dimension = getDimension(biome);
    if (*dimension == DIM_OVERWORLD)
        return isOverworld(mc, biome) != 0;
    return biomeExists(mc, biome) != 0;
}

int32_t cv_default_biome_colors(uint8_t *rgb, int32_t capacity)
{
    if (!rgb)
        return CV_ERR_ARGUMENT;
    if (capacity < CV_BIOME_COLOR_BYTES)
        return CV_ERR_CAPACITY;
    memset(rgb, 0, CV_BIOME_COLOR_BYTES);
    initBiomeColors((unsigned char (*)[3])rgb);
    return CV_BIOME_COLOR_BYTES;
}
