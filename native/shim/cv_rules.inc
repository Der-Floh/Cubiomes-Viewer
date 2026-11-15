/* Structure rules (cv_rules). cubiomes asks its structure config provider, set once when the library loads, for the
   configuration behind every structure it places, without saying for which world, so the rules of the call in progress
   are kept per thread. */

#define CV_KNOWN_RULE_FLAGS CV_RULES_NO_TERRAIN_ESTIMATE
#define CV_SALTABLE_TYPES ((1u << CV_STRUCTURE_TYPES) - 1u)

typedef char cv_assert_structure_types[(FEATURE_NUM == CV_STRUCTURE_TYPES) ? 1 : -1];

static const cv_rules cv_game_rules;
static _Thread_local const cv_rules *cv_current_rules;

static int cv_are_valid_rules(const cv_rules *rules)
{
    return !rules || ((rules->flags & ~CV_KNOWN_RULE_FLAGS) == 0 && (rules->salted & ~CV_SALTABLE_TYPES) == 0);
}

/* Makes 'rules' the rules of what this thread computes next and returns the ones it replaces. */
static const cv_rules *cv_enter_rules(const cv_rules *rules)
{
    const cv_rules *previous = cv_current_rules;
    cv_current_rules = rules;
    return previous;
}

static const cv_rules *cv_rules_in_use(void)
{
    return cv_current_rules ? cv_current_rules : &cv_game_rules;
}

static int cv_estimates_terrain(void)
{
    return (cv_rules_in_use()->flags & CV_RULES_NO_TERRAIN_ESTIMATE) == 0;
}

static int cv_structure_config_provider(int stype, int mc, StructureConfig *sconf)
{
    const cv_rules *rules = cv_rules_in_use();
    int exists = getStructureConfig_default(stype, mc, sconf);
    if (exists && stype >= 0 && stype < CV_STRUCTURE_TYPES && ((rules->salted >> stype) & 1u))
        sconf->salt = rules->salts[stype];
    return exists;
}

__attribute__((constructor)) static void cv_use_structure_rules(void)
{
    setStructureConfigProvider(cv_structure_config_provider);
}

/* Mineshafts and the fortresses of 1.15 and before are placed without a salt. */
static int cv_uses_salt(int32_t stype, int32_t mc)
{
    switch (stype)
    {
    case Feature:
    case Mineshaft:
    case End_Island:
        return 0;
    case Fortress:
        return mc >= MC_1_16_1;
    default:
        return 1;
    }
}

int32_t cv_structure_salt(int32_t stype, int32_t mc, int32_t *salt)
{
    if (stype < 0 || stype >= CV_STRUCTURE_TYPES || !cv_is_valid_mc(mc) || !salt)
        return CV_ERR_ARGUMENT;

    StructureConfig config;
    int exists = getStructureConfig_default(stype, mc, &config);
    *salt = config.salt;
    return exists && cv_uses_salt(stype, mc) ? 1 : 0;
}
