@tool
class_name RegexRules
extends RefCounted

## Pre-compiled regular expressions for asset recognition and classification.

# KR Non-RM Autotiles
static var REGEX_NON_RM_A1: RegEx = RegEx.create_from_string("(?i)non[-_ ]?rm.*a1.*square([123])")
static var REGEX_NON_RM_A2: RegEx = RegEx.create_from_string("(?i)non[-_ ]?rm.*a2.*square")
static var REGEX_NON_RM_A3: RegEx = RegEx.create_from_string("(?i)non[-_ ]?rm.*a3(?:[_\\-\\s]|$)")
static var REGEX_NON_RM_A4: RegEx = RegEx.create_from_string("(?i)non[-_ ]?rm.*a4.*square")

# Standard RPG Maker Autotiles and Tilesets
static var REGEX_A1: RegEx = RegEx.create_from_string("(?i)(?:^|[_\\-\\s])A1(?:[_\\-\\s]|$)")
static var REGEX_A2: RegEx = RegEx.create_from_string("(?i)(?:^|[_\\-\\s])A2(?:[_\\-\\s]|$)")
static var REGEX_A3: RegEx = RegEx.create_from_string("(?i)(?:^|[_\\-\\s])A3(?:[_\\-\\s]|$)")
static var REGEX_A4: RegEx = RegEx.create_from_string("(?i)(?:^|[_\\-\\s])A4(?:[_\\-\\s]|$)")
static var REGEX_A5: RegEx = RegEx.create_from_string("(?i)(?:^|[_\\-\\s])A5(?:[_\\-\\s]|$)")

static var REGEX_B: RegEx = RegEx.create_from_string("(?i)(?:^|[_\\-\\s])B(?:[_\\-\\s]|$)")
static var REGEX_C: RegEx = RegEx.create_from_string("(?i)(?:^|[_\\-\\s])C(?:[_\\-\\s]|$)")
static var REGEX_D: RegEx = RegEx.create_from_string("(?i)(?:^|[_\\-\\s])D(?:[_\\-\\s]|$)")
static var REGEX_E: RegEx = RegEx.create_from_string("(?i)(?:^|[_\\-\\s])E(?:[_\\-\\s]|$)")
static var REGEX_F: RegEx = RegEx.create_from_string("(?i)(?:^|[_\\-\\s])F(?:[_\\-\\s]|$)")

# Entities and Animations
static var REGEX_DOOR: RegEx = RegEx.create_from_string("(?i)(?:^|[_\\-\\s])doors?(?:[_\\-\\s]|$)")
static var REGEX_CHEST: RegEx = RegEx.create_from_string("(?i)(treasure[_\\-\\s]?chest|chest)")
static var REGEX_GENERIC_ANIMATION: RegEx = RegEx.create_from_string("(?i)(animation|animations|anim|fountainwater|water[_\\-\\s]?anim)")

# Dimensions and Variants
static var REGEX_SIZE_PX: RegEx = RegEx.create_from_string("(?i)(?:^|[_\\-\\s])(16|32|48|64)(?:px)?(?:[_\\-\\s]|$)")
static var REGEX_SIZE_WXH: RegEx = RegEx.create_from_string("(?i)(?:^|[_\\-\\s])(16|32|48|64)x(16|32|48|64)(?:[_\\-\\s]|$)")
static var REGEX_VARIANT: RegEx = RegEx.create_from_string("(?i)(brown|gray|grey)")


## Helper function to safely execute regex search on a string.
static func regex_search(pattern: RegEx, text: String) -> RegExMatch:
	if pattern == null or text.is_empty():
		return null
	return pattern.search(text)
