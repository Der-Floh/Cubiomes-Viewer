int32_t cv_structure_pos(int32_t stype, int32_t mc, uint64_t seed, int32_t reg_x, int32_t reg_z, int32_t *x, int32_t *z)
{
    if (stype < 0 || stype >= FEATURE_NUM || !cv_is_valid_mc(mc) || !x || !z)
        return CV_ERR_ARGUMENT;

    Pos pos = {0, 0};
    int found = getStructurePos(stype, mc, seed, reg_x, reg_z, &pos);
    *x = pos.x;
    *z = pos.z;
    return found ? 1 : 0;
}
