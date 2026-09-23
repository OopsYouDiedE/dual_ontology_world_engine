@tool
class_name RPGAdapterConfig
extends Resource

## Configuration resource for RPG Asset Adapter.
## Saved as .tres to control batch import parameters.

const DEFAULT_CONFIG_PATH := "res://ShareAsset/Victorian/victorian_config.tres"

@export_dir var source_dir: String = "res://ShareAsset/Victorian/source"
@export_dir var output_dir: String = "res://ShareAsset/Victorian"
@export var theme_name: String = "Victorian"
@export_range(16, 64, 16) var tile_size: int = 32
@export var fps: float = 6.0
@export var apply_metadata: bool = true
@export var ignore_non_rm: bool = true
