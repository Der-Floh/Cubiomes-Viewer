#include "../cubiomes/finders.h"
#include "../cubiomes/generator.h"
#include "../cubiomes/quadbase.h"
#include "../cubiomes/terrainnoise.h"
#include "../cubiomes/util.h"
#include "../cubiomes/features/end_city.h"
#include "../cubiomes/features/fortress.h"
#include "../cubiomes/features/mineshaft.h"
#include "../cubiomes/features/stronghold.h"
/* The loot headers leave parameter names out of an inline definition, which C before C23 only allows as an extension. */
#if defined(__clang__)
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wc23-extensions"
#endif
#include "../cubiomes/loot/items.h"
#include "../cubiomes/loot/loot_functions.h"
#include "../cubiomes/loot/loot_tables.h"
#if defined(__clang__)
#pragma clang diagnostic pop
#endif

#include <stdatomic.h>
#include <stdlib.h>
#include <string.h>

#include "cvnative.h"

#include "cv_meta.inc"
#include "cv_rules.inc"
#include "cv_world.inc"
#include "cv_terrain.inc"
#include "cv_structures.inc"
#include "cv_loot.inc"
#include "cv_landmarks.inc"
#include "cv_layers.inc"
#include "cv_quad.inc"
