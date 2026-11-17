/* The loot of structure chests, from the pieces cubiomes builds and its loot tables. The tables are shared static
   objects that are not safe to use from several threads, so they are set up under a lock and every chest is rolled
   on a copy. */

#define CV_LOOT_SMALL_PIECES 16
#define CV_LOOT_CHESTS_PER_PIECE 4
#define CV_LOOT_TABLES_PER_TYPE 3

static const char *const cv_loot_table_names[] = {
    "chests/desert_pyramid",
    "chests/jungle_temple",
    "chests/jungle_temple_dispenser",
    "chests/igloo_chest",
    "chests/pillager_outpost",
    "chests/shipwreck_supply",
    "chests/shipwreck_map",
    "chests/shipwreck_treasure",
    "chests/buried_treasure",
    "chests/ruined_portal",
    "chests/abandoned_mineshaft",
    "chests/nether_bridge",
    "chests/bastion_bridge",
    "chests/bastion_other",
    "chests/end_city_treasure",
    "chests/stronghold_corridor",
    "chests/stronghold_crossing",
    "chests/stronghold_library",
    "barrels/abandoned_camp_barrel",
    "chests/abandoned_camp_common_chest",
    "chests/abandoned_camp_secret_chest",
};

#define CV_LOOT_TABLE_COUNT ((int)(sizeof(cv_loot_table_names) / sizeof(cv_loot_table_names[0])))

typedef char cv_assert_last_enchantment[(LUNGE == 43) ? 1 : -1];

static atomic_flag cv_loot_lock = ATOMIC_FLAG_INIT;

/* The functions the loot tables enchant items with, which the library keeps private, taken from probes it builds. */
typedef struct cv_loot_functions
{
    int ready;
    void (*one_enchantment)(RandomSource *, ItemStack *, const void *);
    void (*random_enchantment)(RandomSource *, ItemStack *, const void *);
    void (*levelled_enchantment)(RandomSource *, ItemStack *, const void *);
    void (*fixed_enchantments)(RandomSource *, ItemStack *, const void *);
    int (*location_predicate)(RandomSource *, const void *);
} cv_loot_functions;

static cv_loot_functions cv_loot_known;

static void cv_lock_loot(void)
{
    while (atomic_flag_test_and_set_explicit(&cv_loot_lock, memory_order_acquire))
        ;
}

static void cv_unlock_loot(void)
{
    atomic_flag_clear_explicit(&cv_loot_lock, memory_order_release);
}

static void cv_free_levelled_probe(LootFunction *probe)
{
    for (int i = 0; i < probe->varparams_int_arr_size; i++)
        free(probe->varparams_int_arr[i]);
    free(probe->varparams_int_arr);
}

/* Builds one probe of each enchanting function and of the location predicate; called with the lock held. */
static void cv_find_loot_functions(void)
{
    if (cv_loot_known.ready)
        return;

    LootFunction probe;
    create_enchant_randomly_one_enchant(&probe, SHARPNESS);
    cv_loot_known.one_enchantment = probe.fun;

    const Enchantment list[1] = {SHARPNESS};
    create_enchant_randomly_list(&probe, list, 1);
    cv_loot_known.random_enchantment = probe.fun;
    free(probe.varparams_int);

    const int levels[1] = {1};
    create_set_enchantments(&probe, list, levels, 1);
    cv_loot_known.fixed_enchantments = probe.fun;
    free(probe.varparams_int);

    create_enchant_with_levels(&probe, MC_NEWEST, "minecraft:book", BOOK, 1, 1, 0);
    cv_loot_known.levelled_enchantment = probe.fun;
    cv_free_levelled_probe(&probe);

    LootItemCondition condition;
    create_location_predicate(&condition, NULL, 0);
    cv_loot_known.location_predicate = condition.fun;
    cv_loot_known.ready = 1;
}

/* The shared table of a version, or NULL when the library has none for it; called with the lock held. */
static const LootTableContext *cv_find_loot_table(int table, int mc)
{
    LootTableContext *context = NULL;
    if (!init_loot_table_name(&context, cv_loot_table_names[table], mc))
        return NULL;
    return context;
}

/* Rolls one chest on a copy of its table. */
static int cv_roll_loot(int table, int mc, uint64_t seed, LootTableContext *rolled)
{
    cv_lock_loot();
    const LootTableContext *shared = cv_find_loot_table(table, mc);
    if (shared)
        *rolled = *shared;
    cv_unlock_loot();
    if (!shared)
        return 0;

    set_loot_seed(rolled, seed);
    generate_loot(rolled);
    return 1;
}

static int cv_loot_table_index(const char *name)
{
    if (!name)
        return -1;
    for (int i = 0; i < CV_LOOT_TABLE_COUNT; i++)
    {
        if (strcmp(cv_loot_table_names[i], name) == 0)
            return i;
    }
    return -1;
}

/* The tables the chests of a structure type draw from; 0 for types whose loot is not read. */
static int cv_loot_tables_of(int32_t stype, int tables[CV_LOOT_TABLES_PER_TYPE])
{
    switch (stype)
    {
    case Desert_Pyramid: tables[0] = 0; return 1;
    case Jungle_Pyramid: tables[0] = 1; tables[1] = 2; return 2;
    case Igloo: tables[0] = 3; return 1;
    case Outpost: tables[0] = 4; return 1;
    case Shipwreck: tables[0] = 5; tables[1] = 6; tables[2] = 7; return 3;
    case Treasure: tables[0] = 8; return 1;
    case Ruined_Portal:
    case Ruined_Portal_N: tables[0] = 9; return 1;
    case Mineshaft: tables[0] = 10; return 1;
    case Fortress: tables[0] = 11; return 1;
    case Bastion: tables[0] = 12; tables[1] = 13; return 2;
    case End_City: tables[0] = 14; return 1;
    case Stronghold: tables[0] = 15; tables[1] = 16; tables[2] = 17; return 3;
    case Abandoned_Camp: tables[0] = 18; tables[1] = 19; tables[2] = 20; return 3;
    default: return 0;
    }
}

static int cv_needs_loot_variant(int32_t stype)
{
    return stype == Igloo || stype == Outpost || stype == Shipwreck || stype == Bastion || stype == Abandoned_Camp
        || stype == Ruined_Portal || stype == Ruined_Portal_N;
}

static int cv_has_many_pieces(int32_t stype)
{
    return stype == Fortress || stype == End_City || stype == Stronghold || stype == Mineshaft;
}

/* A shipwreck is beached or in the ocean by the biome its viability check reads. */
static int cv_shipwreck_biome(const Generator *generator, int32_t x, int32_t z)
{
    int chunk_x = x >> 4, chunk_z = z >> 4;
    if (generator->mc <= MC_1_15)
    {
        int offset = 8 + (generator->mc > MC_1_8_9);
        return getBiomeAt(generator, 1, chunk_x * 16 + offset, 0, chunk_z * 16 + offset);
    }
    return getBiomeAt(generator, 4, chunk_x * 4 + 2, 319 >> 2, chunk_z * 4 + 2);
}

/* An abandoned camp takes its biome where its start piece is, as its viability check reads it. */
static int cv_camp_biome(const Generator *generator, int32_t x, int32_t z)
{
    StructureVariant probe;
    getVariant(&probe, Abandoned_Camp, generator->mc, generator->seed, x, z, -1);
    int sample_x = (((x >> 4) * 32 + 2 * probe.x + probe.sx - 1) / 2) >> 2;
    int sample_z = (((z >> 4) * 32 + 2 * probe.z + probe.sz - 1) / 2) >> 2;
    return getBiomeAt(generator, 4, sample_x, probe.y >> 2, sample_z);
}

/* The biome that picks a structure's loot salt or variant; the mineshaft and old end city ones are close guesses. */
static int cv_loot_biome(cv_world *world, int32_t stype, int32_t x, int32_t z)
{
    const Generator *generator = &world->generator;
    Pos pos = {x, z};
    switch (stype)
    {
    case Shipwreck: return cv_shipwreck_biome(generator, x, z);
    case Abandoned_Camp: return cv_camp_biome(generator, x, z);
    case Ruined_Portal:
    case Ruined_Portal_N: return cv_variant_biome(world, stype, pos);
    case Mineshaft: return generator->mc >= MC_1_18 ? getBiomeAt(generator, 4, (x >> 4) * 4 + 2, 50 >> 2, (z >> 4) * 4 + 2) : -1;
    case End_City: return generator->mc < MC_1_16_1 ? getBiomeAt(generator, 4, (x >> 4) * 4 + 2, 0, (z >> 4) * 4 + 2) : -1;
    default: return -1;
    }
}

/* Variants whose values the piece code switches over without a default that is safe to reach. */
static int cv_is_sane_loot_variant(int32_t stype, int mc, const StructureVariant *variant)
{
    switch (stype)
    {
    case Shipwreck: return variant->rotation <= 3 && variant->start < (isOceanic(variant->biome) ? 20 : 11);
    case Bastion: return variant->start <= 3 && variant->rotation <= 3;
    case Abandoned_Camp:
        return variant->start >= 1 && variant->start <= 10 && variant->rotation <= 3
            && variant->biome >= 0 && isViableFeatureBiome(mc, Abandoned_Camp, variant->biome);
    case Igloo: return variant->rotation <= 3 && variant->mirror <= 1;
    case Outpost: return variant->rotation <= 3;
    default: return 1;
    }
}

/* Builds the pieces of a structure with their chests; 0 when the generator cannot tell its loot. */
static int cv_build_loot_pieces(cv_world *world, int32_t stype, int32_t x, int32_t z, Piece **found)
{
    const Generator *generator = &world->generator;
    int biome = cv_loot_biome(world, stype, x, z);
    StructureVariant variant;
    memset(&variant, 0, sizeof(variant));
    if (cv_needs_loot_variant(stype))
    {
        if (!getVariant(&variant, stype, generator->mc, generator->seed, x, z, biome))
            return 0;
        if (!cv_is_sane_loot_variant(stype, generator->mc, &variant))
            return 0;
    }

    StructureSaltConfig salts;
    int salt_biome = stype == Ruined_Portal || stype == Ruined_Portal_N ? variant.biome : biome;
    if (!getStructureSaltConfig(stype, generator->mc, salt_biome, &salts))
        return 0;

    int capacity = cv_has_many_pieces(stype) ? CV_MAX_PIECES : CV_LOOT_SMALL_PIECES;
    *found = (Piece *)calloc((size_t)capacity, sizeof(Piece));
    if (!*found)
        return CV_ERR_MEMORY;

    int count = getStructurePieces(*found, capacity, stype, salts, &variant, generator->mc, generator->seed, x, z);
    if (count > 0)
        return count;

    free(*found);
    *found = NULL;
    return 0;
}

/* Where a container's stacks go: the buffers of cv_structure_loot, or the tally of cv_structure_loot_count. */
typedef struct cv_loot_sink
{
    cv_loot_container *containers;
    int32_t container_capacity;
    cv_loot_stack *stacks;
    int32_t stack_capacity;
    int32_t stack_count;
    const cv_loot_query *query;
    int32_t most;
    int32_t total;
} cv_loot_sink;

static int cv_stack_matches(const ItemStack *stack, int item, const cv_loot_query *query)
{
    if (item != query->item)
        return 0;

    int kind = stack->enchantment_count > 0 ? CV_LOOT_ENCHANTED : CV_LOOT_PLAIN;
    if (!(query->kinds & kind))
        return 0;
    if (query->enchantment <= 0)
        return 1;

    for (int i = 0; i < stack->enchantment_count && i < CV_LOOT_ENCHANTMENTS; i++)
    {
        if (stack->enchantments[i].enchantment == query->enchantment && stack->enchantments[i].level >= query->min_level)
            return 1;
    }
    return 0;
}

static int32_t cv_tally_container(cv_loot_sink *sink, LootTableContext *rolled)
{
    int32_t matching = 0;
    for (int i = 0; i < rolled->generated_item_count; i++)
    {
        const ItemStack *stack = &rolled->generated_items[i];
        if (stack->count > 0 && cv_stack_matches(stack, get_global_item_id(rolled, stack->item), sink->query))
            matching += stack->count;
    }

    sink->total += matching;
    if (matching > sink->most)
        sink->most = matching;
    return CV_OK;
}

static int32_t cv_store_container(cv_loot_sink *sink, int32_t index, int table, Pos at, LootTableContext *rolled)
{
    if (index >= sink->container_capacity || sink->stack_count + rolled->generated_item_count > sink->stack_capacity)
        return CV_ERR_CAPACITY;

    cv_loot_container *container = &sink->containers[index];
    container->table = table;
    container->x = at.x;
    container->z = at.z;
    container->first_stack = sink->stack_count;

    for (int i = 0; i < rolled->generated_item_count; i++)
    {
        const ItemStack *stack = &rolled->generated_items[i];
        if (stack->count <= 0)
            continue;

        cv_loot_stack *out = &sink->stacks[sink->stack_count++];
        memset(out, 0, sizeof(*out));
        out->item = get_global_item_id(rolled, stack->item);
        out->count = stack->count;
        out->effect = stack->mob_effect.effect;
        out->effect_ticks = stack->mob_effect.duration;
        out->enchantment_count = stack->enchantment_count < CV_LOOT_ENCHANTMENTS ? stack->enchantment_count : CV_LOOT_ENCHANTMENTS;
        for (int e = 0; e < out->enchantment_count; e++)
        {
            out->enchantments[e] = stack->enchantments[e].enchantment;
            out->levels[e] = stack->enchantments[e].level;
        }
    }
    container->stack_count = sink->stack_count - container->first_stack;
    return CV_OK;
}

/* Rolls every chest of a structure into the sink and returns how many there are. */
static int32_t cv_roll_structure_loot(cv_world *world, int32_t stype, int32_t x, int32_t z, cv_loot_sink *sink)
{
    int exists = 0;
    int status = cv_check_details_world(world, stype, &exists);
    if (status != CV_OK)
        return status;

    int tables[CV_LOOT_TABLES_PER_TYPE];
    if (!exists || cv_loot_tables_of(stype, tables) == 0)
        return CV_ERR_UNSUPPORTED;

    Piece *found = NULL;
    int count = cv_build_loot_pieces(world, stype, x, z, &found);
    if (count <= 0)
        return count < 0 ? count : CV_ERR_UNSUPPORTED;

    int32_t containers = 0;
    int32_t result = CV_OK;
    LootTableContext rolled;
    for (int i = 0; i < count && result == CV_OK; i++)
    {
        const Piece *piece = &found[i];
        int chests = piece->chestCount < CV_LOOT_CHESTS_PER_PIECE ? piece->chestCount : CV_LOOT_CHESTS_PER_PIECE;
        for (int c = 0; c < chests && result == CV_OK; c++)
        {
            int table = cv_loot_table_index(piece->lootTables[c]);
            if (table < 0)
                continue;
            if (!cv_roll_loot(table, world->generator.mc, piece->lootSeeds[c], &rolled))
            {
                result = CV_ERR_UNSUPPORTED;
                break;
            }

            result = sink->query
                ? cv_tally_container(sink, &rolled)
                : cv_store_container(sink, containers, table, piece->chestPoses[c], &rolled);
            containers++;
        }
    }

    free(found);
    return result == CV_OK ? containers : result;
}

int32_t cv_structure_loot(cv_world *world, int32_t stype, int32_t x, int32_t z, cv_loot_container *containers,
    int32_t container_capacity, cv_loot_stack *stacks, int32_t stack_capacity)
{
    if (!containers || !stacks || container_capacity < 0 || stack_capacity < 0)
        return CV_ERR_ARGUMENT;

    cv_loot_sink sink;
    memset(&sink, 0, sizeof(sink));
    sink.containers = containers;
    sink.container_capacity = container_capacity;
    sink.stacks = stacks;
    sink.stack_capacity = stack_capacity;

    const cv_rules *previous = cv_enter_rules(world ? &world->rules : NULL);
    int32_t result = cv_roll_structure_loot(world, stype, x, z, &sink);
    cv_enter_rules(previous);
    return result;
}

int32_t cv_structure_loot_count(cv_world *world, int32_t stype, int32_t x, int32_t z, const cv_loot_query *query,
    int32_t *most, int32_t *total)
{
    if (!query || !most || !total)
        return CV_ERR_ARGUMENT;

    cv_loot_sink sink;
    memset(&sink, 0, sizeof(sink));
    sink.query = query;

    const cv_rules *previous = cv_enter_rules(world ? &world->rules : NULL);
    int32_t result = cv_roll_structure_loot(world, stype, x, z, &sink);
    cv_enter_rules(previous);
    *most = sink.most;
    *total = sink.total;
    return result;
}

/* ---- what the tables of a structure type can hold, for pickers ---- */

static int cv_is_enchanting(const LootFunction *function)
{
    return function->fun == cv_loot_known.one_enchantment || function->fun == cv_loot_known.random_enchantment
        || function->fun == cv_loot_known.levelled_enchantment || function->fun == cv_loot_known.fixed_enchantments;
}

static int cv_is_location_gated(const LootPool *pool, int entry)
{
    for (int c = 0; c < pool->pool_condition_count; c++)
    {
        if (pool->conditions[c].fun == cv_loot_known.location_predicate)
            return 1;
    }

    int first = pool->entry_conditions_index[entry];
    for (int c = 0; c < pool->entry_conditions_count[entry]; c++)
    {
        if (pool->conditions[first + c].fun == cv_loot_known.location_predicate)
            return 1;
    }
    return 0;
}

/* Calls visit for every entry of the tables of a type that can generate, with the item it gives; 0 when unsupported. */
static int cv_visit_loot_entries(int32_t stype, int mc, void (*visit)(const LootTableContext *, const LootPool *, int, int, void *), void *state)
{
    int tables[CV_LOOT_TABLES_PER_TYPE];
    int table_count = cv_loot_tables_of(stype, tables);
    if (table_count == 0)
        return 0;

    cv_lock_loot();
    cv_find_loot_functions();
    int supported = 1;
    for (int t = 0; t < table_count && supported; t++)
    {
        const LootTableContext *context = cv_find_loot_table(tables[t], mc);
        if (!context)
        {
            supported = 0;
            break;
        }

        int pools = context->pool_count;
        for (int s = 0; s < context->subtable_count; s++)
            pools += context->subtable_pool_count[s];

        for (int p = 0; p < pools; p++)
        {
            const LootPool *pool = &context->loot_pools[p];
            for (int e = 0; e < pool->entry_count; e++)
            {
                int item = pool->entry_to_item[e];
                if (item < 0 || item >= context->item_count || cv_is_location_gated(pool, e))
                    continue;
                visit(context, pool, e, context->global_item_ids[item], state);
            }
        }
    }
    cv_unlock_loot();
    return supported;
}

typedef struct cv_item_kinds
{
    int32_t kinds[NUM_ITEMS];
} cv_item_kinds;

static void cv_note_item_kind(const LootTableContext *context, const LootPool *pool, int entry, int item, void *state)
{
    (void)context;
    if (item < 0 || item >= NUM_ITEMS)
        return;

    int enchanted = 0;
    int first = pool->entry_functions_index[entry];
    for (int f = 0; f < pool->entry_functions_count[entry]; f++)
        enchanted |= cv_is_enchanting(&pool->loot_functions[first + f]);

    ((cv_item_kinds *)state)->kinds[item] |= enchanted ? CV_LOOT_ENCHANTED : CV_LOOT_PLAIN;
}

typedef struct cv_item_enchantments
{
    int item;
    int32_t levels[LUNGE + 1];
} cv_item_enchantments;

static void cv_note_level(cv_item_enchantments *found, int enchantment, int level)
{
    if (enchantment > NO_ENCHANTMENT && enchantment <= LUNGE && level > found->levels[enchantment])
        found->levels[enchantment] = level;
}

static void cv_note_function_enchantments(cv_item_enchantments *found, const LootFunction *function)
{
    if (function->fun == cv_loot_known.one_enchantment)
    {
        cv_note_level(found, function->params_int[0], function->params_int[1]);
    }
    else if (function->fun == cv_loot_known.random_enchantment || function->fun == cv_loot_known.fixed_enchantments)
    {
        const int *values = function->varparams_int;
        for (int i = 0; values && i < values[0]; i++)
            cv_note_level(found, values[1 + 2 * i], values[2 + 2 * i]);
    }
    else if (function->fun == cv_loot_known.levelled_enchantment)
    {
        for (int v = 1; v < function->varparams_int_arr_size; v++)
        {
            const int *vector = function->varparams_int_arr[v];
            for (int i = 0; vector && i < vector[0]; i++)
                cv_note_level(found, vector[2 + 3 * i], vector[3 + 3 * i]);
        }
    }
}

static void cv_note_item_enchantments(const LootTableContext *context, const LootPool *pool, int entry, int item, void *state)
{
    (void)context;
    cv_item_enchantments *found = (cv_item_enchantments *)state;
    if (item != found->item)
        return;

    int first = pool->entry_functions_index[entry];
    for (int f = 0; f < pool->entry_functions_count[entry]; f++)
        cv_note_function_enchantments(found, &pool->loot_functions[first + f]);
}

int32_t cv_loot_items(int32_t stype, int32_t mc, int32_t *items, int32_t *kinds, int32_t capacity)
{
    if (!cv_is_valid_structure(stype) || !cv_is_valid_mc(mc) || !items || !kinds || capacity < 0)
        return CV_ERR_ARGUMENT;

    cv_item_kinds *found = (cv_item_kinds *)calloc(1, sizeof(cv_item_kinds));
    if (!found)
        return CV_ERR_MEMORY;
    if (!cv_visit_loot_entries(stype, mc, cv_note_item_kind, found))
    {
        free(found);
        return CV_ERR_UNSUPPORTED;
    }

    int32_t count = 0;
    for (int item = 0; item < NUM_ITEMS; item++)
    {
        if (!found->kinds[item])
            continue;
        if (count >= capacity)
        {
            free(found);
            return CV_ERR_CAPACITY;
        }
        items[count] = item;
        kinds[count] = found->kinds[item];
        count++;
    }
    free(found);
    return count;
}

int32_t cv_loot_enchantments(int32_t stype, int32_t mc, int32_t item, int32_t *enchantments, int32_t *levels, int32_t capacity)
{
    if (!cv_is_valid_structure(stype) || !cv_is_valid_mc(mc) || item < 0 || item >= NUM_ITEMS || !enchantments || !levels || capacity < 0)
        return CV_ERR_ARGUMENT;

    cv_item_enchantments found;
    memset(&found, 0, sizeof(found));
    found.item = item;
    if (!cv_visit_loot_entries(stype, mc, cv_note_item_enchantments, &found))
        return CV_ERR_UNSUPPORTED;

    int32_t count = 0;
    for (int enchantment = NO_ENCHANTMENT + 1; enchantment <= LUNGE; enchantment++)
    {
        if (!found.levels[enchantment])
            continue;
        if (count >= capacity)
            return CV_ERR_CAPACITY;
        enchantments[count] = enchantment;
        levels[count] = found.levels[enchantment];
        count++;
    }
    return count;
}

static int cv_loot_structure_exists(int32_t stype, int32_t mc)
{
    if (stype == Stronghold)
        return mc >= MC_1_8;

    StructureConfig config;
    return getStructureConfig(stype, mc, &config);
}

int32_t cv_loot_supported(int32_t stype, int32_t mc)
{
    if (!cv_is_valid_structure(stype) || !cv_is_valid_mc(mc))
        return CV_ERR_ARGUMENT;

    int tables[CV_LOOT_TABLES_PER_TYPE];
    int table_count = cv_loot_tables_of(stype, tables);
    if (table_count == 0 || !cv_loot_structure_exists(stype, mc))
        return 0;

    StructureSaltConfig salts;
    int biome = stype == Abandoned_Camp ? forest : -1;
    if (!getStructureSaltConfig(stype, mc, biome, &salts))
        return 0;

    cv_lock_loot();
    int supported = 1;
    for (int t = 0; t < table_count && supported; t++)
        supported = cv_find_loot_table(tables[t], mc) != NULL;
    cv_unlock_loot();
    return supported;
}

/* ---- names ---- */

int32_t cv_loot_item_count(void)
{
    return NUM_ITEMS;
}

const char *cv_loot_item_name(int32_t item, int32_t mc)
{
    if (item < 0 || item >= NUM_ITEMS || !cv_is_valid_mc(mc))
        return NULL;
    return global_id2item_name(item, mc);
}

int32_t cv_loot_enchantment_count(void)
{
    return LUNGE + 1;
}

const char *cv_loot_enchantment_name(int32_t enchantment)
{
    if (enchantment <= NO_ENCHANTMENT || enchantment > LUNGE)
        return NULL;
    return get_enchantment_name((Enchantment)enchantment);
}

int32_t cv_loot_enchantment_max_level(int32_t enchantment)
{
    if (enchantment <= NO_ENCHANTMENT || enchantment > LUNGE)
        return CV_ERR_ARGUMENT;

    LootFunction probe;
    create_enchant_randomly_one_enchant(&probe, (Enchantment)enchantment);
    return probe.params_int[1];
}

int32_t cv_loot_effect_count(void)
{
    return EFFECT_NUM;
}

const char *cv_loot_effect_name(int32_t effect)
{
    if (effect < 0 || effect >= EFFECT_NUM)
        return NULL;
    return MOB_EFFECTS[effect].effect_name;
}

int32_t cv_loot_table_count(void)
{
    return CV_LOOT_TABLE_COUNT;
}

const char *cv_loot_table_name(int32_t table)
{
    if (table < 0 || table >= CV_LOOT_TABLE_COUNT)
        return NULL;
    return cv_loot_table_names[table];
}
