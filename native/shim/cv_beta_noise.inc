/* The Beta 1.7 terrain noise of cubiomes, with the permutation table indexed as the game indexes its own: cubiomes reads
   sums of up to 511 from its table of 257, past its end into padding nobody initialises, so oceans came out at random. */

static void cv_sample_perlin_beta17_terrain(const PerlinNoise *noise, double *v, double d1, double d3, double yLacAmp)
{
    int genFlag = -1;
    double l1 = 0;
    double l3 = 0;
    double l5 = 0;
    double l7 = 0;

    d1 += noise->a;
    d3 += noise->c;
    const uint8_t *idx = noise->d;
    int i1 = (int)floor(d1);
    int i3 = (int)floor(d3);
    d1 -= i1;
    d3 -= i3;
    double t1 = d1 * d1 * d1 * (d1 * (d1 * 6.0 - 15.0) + 10.0);
    double t3 = d3 * d3 * d3 * (d3 * (d3 * 6.0 - 15.0) + 10.0);

    i1 &= 0xff;
    i3 &= 0xff;

    double d2;
    int i2, yi, yic = 0, gfCopy = 0;
    for (yi = 0; yi <= 7; yi++)
    {
        d2 = yi * noise->lacunarity * yLacAmp + noise->b;
        i2 = ((int)floor(d2)) & 0xff;
        if (yi == 0 || i2 != genFlag)
        {
            yic = yi;
            gfCopy = genFlag;
            genFlag = i2;
        }
    }
    genFlag = gfCopy;

    double t2;
    for (yi = yic; yi <= 8; yi++)
    {
        d2 = yi * noise->lacunarity * yLacAmp + noise->b;
        i2 = (int)floor(d2);
        d2 -= i2;
        t2 = d2 * d2 * d2 * (d2 * (d2 * 6.0 - 15.0) + 10.0);

        i2 &= 0xff;

        if (yi == 0 || i2 != genFlag)
        {
            genFlag = i2;
            int a1 = (idx[i1] + i2) & 0xff;
            int b1 = (idx[i1 + 1] + i2) & 0xff;

            int a2 = (idx[a1] + i3) & 0xff;
            int a3 = (idx[a1 + 1] + i3) & 0xff;
            int b2 = (idx[b1] + i3) & 0xff;
            int b3 = (idx[b1 + 1] + i3) & 0xff;

            double m1 = indexedLerp(idx[a2], d1, d2, d3);
            double l2 = indexedLerp(idx[b2], d1 - 1, d2, d3);
            double m3 = indexedLerp(idx[a3], d1, d2 - 1, d3);
            double l4 = indexedLerp(idx[b3], d1 - 1, d2 - 1, d3);
            double m5 = indexedLerp(idx[a2 + 1], d1, d2, d3 - 1);
            double l6 = indexedLerp(idx[b2 + 1], d1 - 1, d2, d3 - 1);
            double m7 = indexedLerp(idx[a3 + 1], d1, d2 - 1, d3 - 1);
            double l8 = indexedLerp(idx[b3 + 1], d1 - 1, d2 - 1, d3 - 1);

            l1 = lerp(t1, m1, l2);
            l3 = lerp(t1, m3, l4);
            l5 = lerp(t1, m5, l6);
            l7 = lerp(t1, m7, l8);
        }

        if (yi >= 7)
        {
            double n1 = lerp(t2, l1, l3);
            double n5 = lerp(t2, l5, l7);

            v[yi - 7] += lerp(t3, n1, n5) * noise->amplitude;
        }
    }
}

void sampleOctaveBeta17Terrain(const OctaveNoise *noise, double *v, double x, double z, int yLacFlag, double lacmin)
{
    v[0] = 0.0;
    v[1] = 0.0;
    for (int i = 0; i < noise->octcnt; i++)
    {
        PerlinNoise *p = noise->octaves + i;
        double lf = p->lacunarity;
        if (lacmin && lf > lacmin)
            continue;
        double ax = maintainPrecision(x * lf);
        double az = maintainPrecision(z * lf);
        cv_sample_perlin_beta17_terrain(p, v, ax, az, yLacFlag ? 0.5 : 1.0);
    }
}
