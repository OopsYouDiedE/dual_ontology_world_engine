# Assembled visual candidate; no authoritative gameplay state lives here.
extends Node3D

func find_component(instance_id: String) -> Node3D:
    for component in $Instances.get_children():
        if component.get("instance_id") == instance_id:
            return component as Node3D
    return null
