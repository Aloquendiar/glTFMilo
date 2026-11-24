# pyright: reportInvalidTypeForm=false, reportMissingImports=false, reportMissingModuleSource=false, reportOptionalMemberAccess=false, reportAttributeAccessIssue=false, reportUnusedParameter=false, reportUnknownMemberType=false, reportUnknownVariableType=false, reportUnknownArgumentType=false, reportUnknownParameterType=false, reportMissingParameterType=false, reportUntypedBaseClass=false, reportImplicitOverride=false, reportUnusedCallResult=false, reportAny=false, reportExplicitAny=false, reportGeneralTypeIssues=false
# ruff: noqa
# flake8: noqa
# pylint: disable=all
# type: ignore

bl_info = {
    "name": "Milo Hair & Collision Setup",
    "author": "Axo",
    "version": (0, 0, 1),
    "blender": (5, 2, 0),
    "location": "Properties > Data (Armature) / Properties > Bone",
    "description": "Isolated Hair (.hair) and Collision (.coll) setup system for Harmonix Milo characters.",
    "category": "Rigging",
}

import json as _json
import math as _math
import bpy
from bpy.props import (
    StringProperty,
    BoolProperty,
    EnumProperty,
    IntProperty,
    FloatProperty,
    FloatVectorProperty,
    PointerProperty,
    CollectionProperty,
)

try:
    import gpu
    from gpu_extras.batch import batch_for_shader
except ImportError:
    gpu = None
    batch_for_shader = None

try:
    from mathutils import Vector, Euler, Matrix
except ImportError:
    Vector = None
    Euler = None
    Matrix = None


# Hair & Collision Bitmask Flags

kHairBack = 1
kHairChest = 2
kHairForehead = 4
kHairHead = 8
kHairLeftShoulder = 16
kHairRightShoulder = 32
kHairFace = 64
kHairNeck = 128
kHairLeftThigh = 256
kHairRightThigh = 512
kHairLeftHip = 1024
kHairRigh2hip = 2048

HAIR_COLLISION_FLAGS = (
    ("flag_kHairBack", "kHairBack", kHairBack),
    ("flag_kHairChest", "kHairChest", kHairChest),
    ("flag_kHairForehead", "kHairForehead", kHairForehead),
    ("flag_kHairHead", "kHairHead", kHairHead),
    ("flag_kHairLeftShoulder", "kHairLeftShoulder", kHairLeftShoulder),
    ("flag_kHairRightShoulder", "kHairRightShoulder", kHairRightShoulder),
    ("flag_kHairFace", "kHairFace", kHairFace),
    ("flag_kHairNeck", "kHairNeck", kHairNeck),
    ("flag_kHairLeftThigh", "kHairLeftThigh", kHairLeftThigh),
    ("flag_kHairRightThigh", "kHairRightThigh", kHairRightThigh),
    ("flag_kHairLeftHip", "kHairLeftHip", kHairLeftHip),
    ("flag_kHairRigh2hip", "kHairRigh2hip", kHairRigh2hip),
)


def _make_flag_getter(int_prop_name, bit):
    def _get(self):
        return bool(int(getattr(self, int_prop_name, 0)) & bit)
    return _get


def _make_flag_setter(int_prop_name, bit):
    def _set(self, value):
        cur = int(getattr(self, int_prop_name, 0))
        new_val = (cur | bit) if value else (cur & ~bit)
        if new_val != cur:
            setattr(self, int_prop_name, new_val)
    return _set


def _draw_hair_collision_flags(layout, data, int_prop_name, title="Collision Group Flags"):
    box = layout.box()
    box.use_property_split = False
    val = int(getattr(data, int_prop_name, 0))
    header = box.row()
    header.label(text=f"{title} (Bitmask: {val})", icon='CHECKBOX_HLT')
    grid = box.grid_flow(row_major=True, columns=2, even_columns=True, even_rows=True, align=True)
    for prop_name, label, _bit in HAIR_COLLISION_FLAGS:
        grid.prop(data, prop_name, text=label)


# Collision Shapes

COLL_SHAPE_PLANE = 0
COLL_SHAPE_SPHERE = 1
COLL_SHAPE_INSIDE_SPHERE = 2
COLL_SHAPE_CIGAR = 3
COLL_SHAPE_INSIDE_CIGAR = 4

COLL_SHAPE_ITEMS = (
    ('plane', "Plane", "Flat collision plane (kPlane), faces the bone's local +X"),
    ('sphere', "Sphere", "Spherical volume of radius0 (kSphere)"),
    ('inside_sphere', "Inside Sphere", "Sphere that collides from the inside (kInsideSphere)"),
    ('cigar', "Cigar / Capsule", "Capsule between two hemispheres along local X (kCigar)"),
    ('inside_cigar', "Inside Cigar", "Capsule that collides from the inside (kInsideCigar)"),
)

COLL_SHAPE_TO_INT = {
    'plane': COLL_SHAPE_PLANE,
    'sphere': COLL_SHAPE_SPHERE,
    'inside_sphere': COLL_SHAPE_INSIDE_SPHERE,
    'cigar': COLL_SHAPE_CIGAR,
    'inside_cigar': COLL_SHAPE_INSIDE_CIGAR,
}


# Automatic Hair / Breast / Skirt Bone Detection (Ported from NodeProcessor.cs)

AUTO_HAIR_CATEGORIES = (
    # (file_name, prefix_lower, custom_prop_prefix, default_physics_dict)
    (
        "hair",
        "bone_hair_",
        "milo_hair",
        {
            "simulate": True,
            "stiffness": 0.04,
            "torsion": 0.1,
            "inertia": 0.7,
            "gravity": 1.0,
            "weight": 0.5,
            "friction": 0.3,
            "min_slack": 0.0,
            "max_slack": 0.0,
        },
    ),
    (
        "breast",
        "bone_torso_breast",
        "milo_breast",
        {
            "simulate": True,
            "stiffness": 0.225,
            "torsion": 0.75,
            "inertia": 0.5,
            "gravity": 1.0,
            "weight": 1.0,
            "friction": 0.0,
            "min_slack": 0.0,
            "max_slack": 0.0,
        },
    ),
    (
        "skirt",
        "bone_skirt",
        "milo_skirt",
        {
            "simulate": True,
            "stiffness": 0.225,
            "torsion": 0.75,
            "inertia": 0.5,
            "gravity": 1.0,
            "weight": 1.0,
            "friction": 0.0,
            "min_slack": 0.0,
            "max_slack": 0.0,
        },
    ),
)


def _is_category_bone(bone_name, prefix_lower):
    if not bone_name:
        return False
    return bone_name.lower().startswith(prefix_lower)


def _collect_weighted_bone_names(armature_obj):
    """Returns the set of bone names (lowercase) that have > 0 skin weights on any mesh
    bound to `armature_obj` (mirrors Program.cs skin influence collection)."""
    weighted = set()
    has_skinned_meshes = False
    if armature_obj is None:
        return weighted, False
    for obj in getattr(bpy.data, "objects", ()):
        if obj.type != 'MESH' or obj.data is None:
            continue
        bound = (obj.parent == armature_obj)
        if not bound:
            for mod in getattr(obj, "modifiers", ()):
                if mod.type == 'ARMATURE' and getattr(mod, "object", None) == armature_obj:
                    bound = True
                    break
        if not bound or not obj.vertex_groups:
            continue
        has_skinned_meshes = True
        group_names = {g.index: g.name.lower() for g in obj.vertex_groups}
        for v in obj.data.vertices:
            for g in v.groups:
                if g.weight > 0.0:
                    gname = group_names.get(g.group)
                    if gname:
                        weighted.add(gname)
    return weighted, has_skinned_meshes


def _collect_chains_full_paths(bone, prefix_lower, weighted_lower, current_chain, chains):
    """Port of NodeProcessor.CollectHairChains (when strand splitting at branches is disabled).
    Builds full root-to-leaf paths, duplicating shared ancestor bones across branches."""
    current_chain.append(bone)
    cat_children = [c for c in bone.children if _is_category_bone(c.name, prefix_lower)]
    if not cat_children:
        if any(b.name.lower() in weighted_lower for b in current_chain):
            chains.append(list(current_chain))
    else:
        for child in cat_children:
            _collect_chains_full_paths(child, prefix_lower, weighted_lower, current_chain, chains)
    current_chain.pop()


def _collect_chains_split_at_branches(bone, prefix_lower, weighted_lower, ancestor_weighted, chains):
    segment = []
    current = bone
    while True:
        segment.append(current)
        cat_children = [c for c in current.children if _is_category_bone(c.name, prefix_lower)]
        if len(cat_children) == 1:
            current = cat_children[0]
            continue

        segment_weighted = any(b.name.lower() in weighted_lower for b in segment)
        child_chains = []
        subtree_weighted = False
        for child in cat_children:
            if _collect_chains_split_at_branches(
                child, prefix_lower, weighted_lower,
                ancestor_weighted or segment_weighted, child_chains,
            ):
                subtree_weighted = True

        if segment_weighted or ancestor_weighted or subtree_weighted:
            chains.append(segment)
        chains.extend(child_chains)
        return segment_weighted or subtree_weighted


def detect_auto_hair_chains_for_armature(
    armature_obj,
    split_at_branches=True,
    only_weighted=True,
):
    """Scans `armature_obj` for `bone_hair_*`, `bone_torso_breast*`, and `bone_skirt*`
    chains using the C# NodeProcessor algorithm.
    Returns a list of `(file_name, custom_prop_prefix, default_physics, chains_of_bones)`."""
    if armature_obj is None or armature_obj.type != 'ARMATURE' or armature_obj.data is None:
        return []
    bones = armature_obj.data.bones
    weighted_lower, has_meshes = _collect_weighted_bone_names(armature_obj)

    results = []
    for file_name, prefix_lower, prop_prefix, default_phys in AUTO_HAIR_CATEGORIES:
        matching_bones = [b for b in bones if _is_category_bone(b.name, prefix_lower)]
        if not matching_bones:
            continue

        if only_weighted and has_meshes:
            active_weighted = {
                b.name.lower() for b in matching_bones if b.name.lower() in weighted_lower
            }
            # Fallback if meshes exist in scene but none have weights on these bones yet
            if not active_weighted:
                active_weighted = {b.name.lower() for b in matching_bones}
        else:
            active_weighted = {b.name.lower() for b in matching_bones}

        root_bones = []
        seen_roots = set()
        for b in matching_bones:
            if b.name.lower() not in active_weighted:
                continue
            root = b
            while root.parent is not None and _is_category_bone(root.parent.name, prefix_lower):
                root = root.parent
            key = root.name.lower()
            if key not in seen_roots:
                seen_roots.add(key)
                root_bones.append(root)

        chains = []
        for root in root_bones:
            if split_at_branches:
                _collect_chains_split_at_branches(root, prefix_lower, active_weighted, False, chains)
            else:
                _collect_chains_full_paths(root, prefix_lower, active_weighted, [], chains)

        if chains:
            results.append((file_name, prop_prefix, default_phys, chains))
    return results


def compute_csharp_strand_points(armature_obj, chain_bones, bone_items=None):
    if not chain_bones or armature_obj is None:
        return []
    arm_mw = armature_obj.matrix_world
    world_positions = [arm_mw @ b.head_local for b in chain_bones]
    n = len(chain_bones)

    def _get_len(idx):
        if idx < n - 1:
            return (world_positions[idx + 1] - world_positions[idx]).length
        if idx > 0:
            return _get_len(idx - 1)
        parent = chain_bones[idx].parent
        if parent is not None:
            p_pos = arm_mw @ parent.head_local
            dist = (world_positions[idx] - p_pos).length
            if dist > 0.0:
                return dist
        return 5.0

    root_parent = chain_bones[0].parent
    if root_parent is not None:
        parent_world = arm_mw @ root_parent.matrix_local
        try:
            parent_world_inv = parent_world.inverted()
        except ValueError:
            parent_world_inv = None
    else:
        parent_world_inv = None

    item_by_name = {}
    if bone_items is not None:
        for bi in bone_items:
            item_by_name[bi.name] = bi

    points = []
    for i, bone in enumerate(chain_bones):
        pt_len = _get_len(i)
        cur_pos = world_positions[i]
        if i < n - 1:
            pt_pos = world_positions[i + 1]
        else:
            bone_world_mtx = arm_mw @ bone.matrix_local
            y_dir = bone_world_mtx.to_3x3() @ Vector((0.0, 1.0, 0.0)) if Vector is not None else None
            if y_dir is not None and y_dir.length > 1e-8:
                y_dir.normalize()
                pt_pos = cur_pos + y_dir * pt_len
            else:
                pt_pos = cur_pos

        if parent_world_inv is not None:
            reset_pos = parent_world_inv @ pt_pos
        else:
            reset_pos = pt_pos

        bi = item_by_name.get(bone.name)
        if bi is not None:
            radius = float(getattr(bi, "radius", 0.5))
            outer_radius = float(getattr(bi, "outer_radius", -1.0))
        else:
            radius = 0.0
            outer_radius = 0.0
            if i < n - 1 and n > 1:
                t = float(i) / float(n - 1)
                radius = max(0.0, 0.75 * (1.0 - (t * 0.5)))
                outer_radius = max(0.0, 2.0 * (1.0 - t))

        points.append({
            "bone": bone.name,
            "pos": [float(pt_pos.x), float(pt_pos.y), float(pt_pos.z)],
            "reset_pos": [float(reset_pos.x), float(reset_pos.y), float(reset_pos.z)],
            "length": float(pt_len),
            "radius": float(radius),
            "outer_radius": float(outer_radius),
            "side_length": -1.0,
        })
    return points


# Helpers to access Hair / Collision PropertyGroups

def _get_hair_settings(arm_data):
    if arm_data is None:
        return None
    return getattr(arm_data, "gltfmilo_hair", None) or getattr(arm_data, "milohc_hair", None)


def _get_collision_settings(bone):
    if bone is None:
        return None
    return getattr(bone, "gltfmilo_collision", None) or getattr(bone, "milohc_collision", None)


def _collision_local_matrix(coll):
    """Returns the 4x4 local rotation matrix for a collision volume from its Euler `rotation`."""
    if Matrix is None or Euler is None or coll is None:
        return None
    rot = getattr(coll, "rotation", (0.0, 0.0, 0.0))
    return Euler((float(rot[0]), float(rot[1]), float(rot[2])), 'XYZ').to_matrix().to_4x4()


def _tag_redraw_view3d(context=None):
    """Tag all 3D Viewports for redraw so collision and hair radius previews update live."""
    ctx = context if context is not None else getattr(bpy, "context", None)
    if ctx is None:
        return
    wm = getattr(ctx, "window_manager", None)
    if wm is None:
        return
    for window in wm.windows:
        for area in window.screen.areas:
            if area.type == 'VIEW_3D':
                area.tag_redraw()


def _apply_strand_radius_to_bones(strand):
    """Applies a strand's `collision_radius`, `outer_radius`, and `taper_radius` settings
    across its ordered bone chain (`strand.bones`)."""
    n = len(strand.bones)
    if n == 0:
        return
    base_rad = float(getattr(strand, "collision_radius", 0.75))
    base_out = float(getattr(strand, "outer_radius", -1.0))
    taper = bool(getattr(strand, "taper_radius", True))
    for i, b_item in enumerate(strand.bones):
        if taper and n > 1:
            t = float(i) / float(n - 1)
            b_item.radius = max(0.0, base_rad * (1.0 - 0.5 * t))
            b_item.outer_radius = max(0.0, base_out * (1.0 - t)) if base_out >= 0.0 else -1.0
        else:
            b_item.radius = max(0.0, base_rad)
            b_item.outer_radius = base_out


# Custom Property Sync 

def _to_plain_python(val):
    """Recursively convert Blender IDPropertyGroup / IDPropertyArray / mathutils types
    into plain JSON-serializable Python dicts, lists, ints, floats, and strings."""
    if val is None or isinstance(val, (bool, int, float, str)):
        return val
    if hasattr(val, "to_dict"):
        return {str(k): _to_plain_python(v) for k, v in val.to_dict().items()}
    if isinstance(val, dict):
        return {str(k): _to_plain_python(v) for k, v in val.items()}
    if hasattr(val, "to_list"):
        return [_to_plain_python(v) for v in val.to_list()]
    if hasattr(val, "__iter__"):
        return [_to_plain_python(v) for v in val]
    return val


def _build_collision_dict(coll):
    flags_val = int(coll.flags)
    rot_rad = [float(r) for r in getattr(coll, "rotation", (0.0, 0.0, 0.0))]
    rot_deg = [float(_math.degrees(r)) for r in rot_rad]
    return {
        "enabled": 1,
        "shape": str(coll.shape),
        "shape_int": int(COLL_SHAPE_TO_INT.get(coll.shape, COLL_SHAPE_SPHERE)),
        "radius0": float(coll.radius0),
        "radius1": float(coll.radius1),
        "length0": float(coll.length0),
        "length1": float(coll.length1),
        "rotation_rad": rot_rad,
        "rotation_deg": rot_deg,
        "flags": flags_val,
        "flag_names": [label for _p, label, bit in HAIR_COLLISION_FLAGS if flags_val & bit],
        "mesh_y_bias": int(bool(coll.mesh_y_bias)),
    }


def _write_flat_collision_props(target, d):
    if d is None:
        for key in (
            "milo_collision",
            "milo_collision_json",
            "milo_coll_enabled",
            "milo_coll_shape",
            "milo_coll_shape_int",
            "milo_coll_radius0",
            "milo_coll_radius1",
            "milo_coll_length0",
            "milo_coll_length1",
            "milo_coll_rot_x",
            "milo_coll_rot_y",
            "milo_coll_rot_z",
            "milo_coll_flags",
            "milo_coll_mesh_y_bias",
        ):
            if key in target:
                del target[key]
        return

    rot = d["rotation_rad"]
    target["milo_collision_json"] = _json.dumps(d)
    target["milo_coll_enabled"] = 1
    target["milo_coll_shape"] = d["shape"]
    target["milo_coll_shape_int"] = int(d["shape_int"])
    target["milo_coll_radius0"] = float(d["radius0"])
    target["milo_coll_radius1"] = float(d["radius1"])
    target["milo_coll_length0"] = float(d["length0"])
    target["milo_coll_length1"] = float(d["length1"])
    target["milo_coll_rot_x"] = float(rot[0])
    target["milo_coll_rot_y"] = float(rot[1])
    target["milo_coll_rot_z"] = float(rot[2])
    target["milo_coll_flags"] = int(d["flags"])
    target["milo_coll_mesh_y_bias"] = int(d["mesh_y_bias"])
    try:
        target["milo_collision"] = d
    except Exception:
        if "milo_collision" in target:
            del target["milo_collision"]


def _sync_collision_custom_props(arm_data):
    """Syncs bone collision settings to Blender ID custom properties
    (bone['milo_collision'], pose_bone['milo_collision'], flat scalar props, and JSON strings)
    so external tools and glTF exports preserve all fields including rotation."""
    if arm_data is None or not hasattr(arm_data, "bones"):
        return
    coll_by_bone = {}
    for bone in arm_data.bones:
        coll = _get_collision_settings(bone)
        if coll is not None and coll.enabled:
            d = _build_collision_dict(coll)
            _write_flat_collision_props(bone, d)
            coll_by_bone[bone.name] = d
        else:
            _write_flat_collision_props(bone, None)

    if coll_by_bone:
        arm_data["milo_collisions_json"] = _json.dumps(coll_by_bone)
        try:
            arm_data["milo_collisions"] = coll_by_bone
        except Exception:
            if "milo_collisions" in arm_data:
                del arm_data["milo_collisions"]
    else:
        if "milo_collisions" in arm_data:
            del arm_data["milo_collisions"]
        if "milo_collisions_json" in arm_data:
            del arm_data["milo_collisions_json"]

    for obj in getattr(bpy.data, "objects", ()):
        if obj.type == 'ARMATURE' and obj.data == arm_data:
            if coll_by_bone:
                obj["milo_collisions_json"] = _json.dumps(coll_by_bone)
                try:
                    obj["milo_collisions"] = coll_by_bone
                except Exception:
                    if "milo_collisions" in obj:
                        del obj["milo_collisions"]
            else:
                if "milo_collisions" in obj:
                    del obj["milo_collisions"]
                if "milo_collisions_json" in obj:
                    del obj["milo_collisions_json"]
            if obj.pose is not None:
                for pb in obj.pose.bones:
                    _write_flat_collision_props(pb, coll_by_bone.get(pb.name))


def _build_hair_dict(arm_data):
    """Builds the plain-Python milo_hair dictionary from armature hair settings."""
    hair = _get_hair_settings(arm_data)
    if arm_data is None or hair is None or not hair.enabled:
        return None

    armature_obj = None
    for obj in getattr(bpy.data, "objects", ()):
        if obj.type == 'ARMATURE' and obj.data == arm_data:
            armature_obj = obj
            break

    files_data = []
    for f in hair.files:
        f_flags = int(f.hair_flags)
        strands_data = []
        for st in f.strands:
            s_flags = int(st.hookup_flags)
            eff_flags = s_flags | f_flags
            bone_names = [str(b.name) for b in st.bones]
            bone_radii = [float(getattr(b, "radius", 0.5)) for b in st.bones]
            bone_outer_radii = [float(getattr(b, "outer_radius", -1.0)) for b in st.bones]
            pts_data = []
            if getattr(hair, "use_csharp_point_calc", True) and armature_obj is not None:
                chain_bones = [
                    arm_data.bones[bname]
                    for bname in bone_names
                    if bname in arm_data.bones
                ]
                if chain_bones:
                    pts_data = compute_csharp_strand_points(armature_obj, chain_bones, st.bones)

            strands_data.append({
                "name": str(st.name),
                "angle": float(st.angle),
                "collision_radius": float(getattr(st, "collision_radius", 0.75)),
                "outer_radius": float(getattr(st, "outer_radius", -1.0)),
                "taper_radius": int(bool(getattr(st, "taper_radius", True))),
                "hookup_flags": s_flags,
                "effective_hookup_flags": eff_flags,
                "hookup_flag_names": [
                    label for _p, label, bit in HAIR_COLLISION_FLAGS if eff_flags & bit
                ],
                "bones": bone_names,
                "bone_radii": bone_radii,
                "bone_outer_radii": bone_outer_radii,
                "points": pts_data,
            })

        files_data.append({
            "name": str(f.name),
            "export": int(bool(f.export)),
            "hair_flags": f_flags,
            "hair_flag_names": [
                label for _p, label, bit in HAIR_COLLISION_FLAGS if f_flags & bit
            ],
            "simulate": int(bool(f.simulate)),
            "stiffness": float(f.stiffness),
            "torsion": float(f.torsion),
            "inertia": float(f.inertia),
            "gravity": float(f.gravity),
            "weight": float(f.weight),
            "friction": float(f.friction),
            "min_slack": float(f.min_slack),
            "max_slack": float(f.max_slack),
            "strands": strands_data,
        })

    return {
        "enabled": 1,
        "show_hair_collision_radius": int(bool(getattr(hair, "show_hair_collision_radius", True))),
        "show_hair_outer_radius": int(bool(getattr(hair, "show_hair_outer_radius", False))),
        "auto_calculate_nodes": int(bool(getattr(hair, "auto_calculate_nodes", True))),
        "split_strands_at_branches": int(bool(getattr(hair, "split_strands_at_branches", True))),
        "only_weighted_bones": int(bool(getattr(hair, "only_weighted_bones", True))),
        "use_csharp_point_calc": int(bool(getattr(hair, "use_csharp_point_calc", True))),
        "files": files_data,
    }


def _sync_hair_custom_props(arm_data):
    """Syncs armature hair settings to Blender ID custom properties
    (armature['milo_hair_json'], armature_obj['milo_hair_json'], root bone['milo_hair_json'],
    and C#-compatible milo_hair_* / milo_breast_* / milo_skirt_* on root bones)."""
    hair = _get_hair_settings(arm_data)
    if arm_data is None or hair is None:
        return

    armature_objs = [
        obj for obj in getattr(bpy.data, "objects", ())
        if obj.type == 'ARMATURE' and obj.data == arm_data
    ]

    # Blender IDProperties cannot store a Python list of dicts (`files`),
    # so clear any stale `milo_hair` IDProperty and store the JSON string instead.
    if "milo_hair" in arm_data:
        del arm_data["milo_hair"]
    for obj in armature_objs:
        if "milo_hair" in obj:
            del obj["milo_hair"]

    for b in getattr(arm_data, "bones", ()):
        if "milo_hair_json" in b:
            del b["milo_hair_json"]
    for obj in armature_objs:
        if obj.pose is not None:
            for pb in obj.pose.bones:
                if "milo_hair_json" in pb:
                    del pb["milo_hair_json"]

    hair_dict = _build_hair_dict(arm_data)
    if hair_dict is not None:
        hair_json = _json.dumps(hair_dict)
        arm_data["milo_hair_json"] = hair_json
        for obj in armature_objs:
            obj["milo_hair_json"] = hair_json

        first_root_bname = None
        for f in hair.files:
            for st in f.strands:
                bone_names = [str(b.name) for b in st.bones]
                if not bone_names:
                    continue
                root_bname = bone_names[0]
                if first_root_bname is None:
                    first_root_bname = root_bname
                fname_low = f.name.strip().lower()
                if fname_low == "breast" or root_bname.lower().startswith("bone_torso_breast"):
                    cp_prefix = "milo_breast"
                elif fname_low == "skirt" or root_bname.lower().startswith("bone_skirt"):
                    cp_prefix = "milo_skirt"
                else:
                    cp_prefix = "milo_hair"
                targets = []
                if root_bname in arm_data.bones:
                    targets.append(arm_data.bones[root_bname])
                for obj in armature_objs:
                    if obj.pose is not None and root_bname in obj.pose.bones:
                        targets.append(obj.pose.bones[root_bname])
                for tgt in targets:
                    try:
                        tgt[f"{cp_prefix}_stiffness"] = float(f.stiffness)
                        tgt[f"{cp_prefix}_torsion"] = float(f.torsion)
                        tgt[f"{cp_prefix}_inertia"] = float(f.inertia)
                        tgt[f"{cp_prefix}_gravity"] = float(f.gravity)
                        tgt[f"{cp_prefix}_friction"] = float(f.friction)
                        tgt[f"{cp_prefix}_weight"] = float(f.weight)
                        tgt[f"{cp_prefix}_wind"] = "world.wind"
                    except Exception:
                        pass

        if first_root_bname is not None:
            if first_root_bname in arm_data.bones:
                arm_data.bones[first_root_bname]["milo_hair_json"] = hair_json
            for obj in armature_objs:
                if obj.pose is not None and first_root_bname in obj.pose.bones:
                    obj.pose.bones[first_root_bname]["milo_hair_json"] = hair_json
    else:
        if "milo_hair_json" in arm_data:
            del arm_data["milo_hair_json"]
        for obj in armature_objs:
            if "milo_hair_json" in obj:
                del obj["milo_hair_json"]


def populate_auto_hair_strands(armature_obj, hair, overwrite_existing=True):
    """Populates `hair.files` and their `strands` automatically using the C# detection
    algorithm (`bone_hair_*`, `bone_torso_breast*`, `bone_skirt*`)."""
    detected = detect_auto_hair_chains_for_armature(
        armature_obj,
        split_at_branches=getattr(hair, "split_strands_at_branches", True),
        only_weighted=getattr(hair, "only_weighted_bones", True),
    )
    if not detected:
        return 0, 0

    total_files = 0
    total_strands = 0
    for file_name, _prop_prefix, default_phys, chains in detected:
        target_file = None
        for existing in hair.files:
            if existing.name.strip().lower() == file_name.lower():
                target_file = existing
                break
        if target_file is None:
            target_file = hair.files.add()
            target_file.name = file_name
            for k, v in default_phys.items():
                if hasattr(target_file, k):
                    setattr(target_file, k, v)
        elif overwrite_existing:
            target_file.strands.clear()

        if len(target_file.strands) == 0:
            for chain in chains:
                st = target_file.strands.add()
                st.name = f"{chain[0].name}"
                for bone in chain:
                    b_item = st.bones.add()
                    b_item.name = bone.name
                _apply_strand_radius_to_bones(st)
                total_strands += 1
            target_file.active_strand_index = 0
            total_files += 1

    hair.active_file_index = max(0, min(hair.active_file_index, len(hair.files) - 1))
    _sync_hair_custom_props(armature_obj.data)
    _tag_redraw_view3d()
    return total_files, total_strands


def _hair_prop_update(self, context):
    arm_data = getattr(self, "id_data", None)
    if arm_data is not None:
        hair = _get_hair_settings(arm_data)
        if (
            hair is not None
            and hair.enabled
            and getattr(hair, "auto_calculate_nodes", False)
            and len(hair.files) == 0
            and len(hair.strands) == 0
        ):
            arm_obj = getattr(context, "object", None) if context is not None else None
            if arm_obj is None or arm_obj.data != arm_data:
                for obj in getattr(bpy.data, "objects", ()):
                    if obj.type == 'ARMATURE' and obj.data == arm_data:
                        arm_obj = obj
                        break
            if arm_obj is not None:
                populate_auto_hair_strands(arm_obj, hair, overwrite_existing=False)
    _sync_hair_custom_props(arm_data)
    _tag_redraw_view3d(context)


def _strand_radius_update(self, context):
    """When a strand's `collision_radius`, `outer_radius`, or `taper_radius` is edited,
    propagate the new radius values to its bones and refresh the 3D viewport."""
    _apply_strand_radius_to_bones(self)
    _hair_prop_update(self, context)


# Hair PropertyGroups, Operators, UILists, and Panel

class MILOHC_PG_hair_bone_item(bpy.types.PropertyGroup):
    """One bone in a strand's ordered root-to-tip chain, with its own collision radius."""
    name: StringProperty(name="Bone")  # type: ignore
    radius: FloatProperty(
        name="Radius",
        description="CharHair.Point.radius - collision radius for this bone in the hair strand",
        default=0.5,
        min=0.0,
        soft_max=5.0,
        update=_hair_prop_update,
    )  # type: ignore
    outer_radius: FloatProperty(
        name="Outer Radius",
        description="CharHair.Point.outer_radius - outer flatten collision radius (-1 = unused)",
        default=-1.0,
        min=-1.0,
        soft_max=5.0,
        update=_hair_prop_update,
    )  # type: ignore


class MILOHC_PG_hair_strand(bpy.types.PropertyGroup):
    """One hair strand - an ordered chain of bones (root first) plus per-strand
    starting-flip angle and collision radius."""
    name: StringProperty(name="Strand Name", default="Strand", update=_hair_prop_update)  # type: ignore
    angle: FloatProperty(
        name="Angle",
        description="CharHair.Strand.angle - starting flip angle in degrees",
        default=0.0,
        update=_hair_prop_update,
    )  # type: ignore
    collision_radius: FloatProperty(
        name="Collision Radius",
        description=(
            "Base collision radius for this hair strand. Editing this updates the "
            "collision radius of all bones in this strand"
        ),
        default=0.75,
        min=0.0,
        soft_max=5.0,
        update=_strand_radius_update,
    )  # type: ignore
    outer_radius: FloatProperty(
        name="Outer Radius",
        description=(
            "Base outer collision radius for this hair strand (-1 = disabled). Editing "
            "this updates the outer radius of all bones in this strand"
        ),
        default=-1.0,
        min=-1.0,
        soft_max=5.0,
        update=_strand_radius_update,
    )  # type: ignore
    taper_radius: BoolProperty(
        name="Taper Radius (Root -> Tip)",
        description=(
            "Automatically taper the strand's collision radius from 100% at the root bone "
            "to 50% at the tip bone. Uncheck to apply the exact same radius to all bones in the strand"
        ),
        default=True,
        update=_strand_radius_update,
    )  # type: ignore
    hookup_flags: IntProperty(
        name="Hookup Flags",
        description="CharHair.Strand.hookup_flags - collision-group bitmask",
        default=1,
        min=0,
        update=_hair_prop_update,
    )  # type: ignore
    flag_kHairBack: BoolProperty(
        name="kHairBack", description="kHairBack (1)",
        get=_make_flag_getter("hookup_flags", kHairBack),
        set=_make_flag_setter("hookup_flags", kHairBack),
    )  # type: ignore
    flag_kHairChest: BoolProperty(
        name="kHairChest", description="kHairChest (2)",
        get=_make_flag_getter("hookup_flags", kHairChest),
        set=_make_flag_setter("hookup_flags", kHairChest),
    )  # type: ignore
    flag_kHairForehead: BoolProperty(
        name="kHairForehead", description="kHairForehead (4)",
        get=_make_flag_getter("hookup_flags", kHairForehead),
        set=_make_flag_setter("hookup_flags", kHairForehead),
    )  # type: ignore
    flag_kHairHead: BoolProperty(
        name="kHairHead", description="kHairHead (8)",
        get=_make_flag_getter("hookup_flags", kHairHead),
        set=_make_flag_setter("hookup_flags", kHairHead),
    )  # type: ignore
    flag_kHairLeftShoulder: BoolProperty(
        name="kHairLeftShoulder", description="kHairLeftShoulder (16)",
        get=_make_flag_getter("hookup_flags", kHairLeftShoulder),
        set=_make_flag_setter("hookup_flags", kHairLeftShoulder),
    )  # type: ignore
    flag_kHairRightShoulder: BoolProperty(
        name="kHairRightShoulder", description="kHairRightShoulder (32)",
        get=_make_flag_getter("hookup_flags", kHairRightShoulder),
        set=_make_flag_setter("hookup_flags", kHairRightShoulder),
    )  # type: ignore
    flag_kHairFace: BoolProperty(
        name="kHairFace", description="kHairFace (64)",
        get=_make_flag_getter("hookup_flags", kHairFace),
        set=_make_flag_setter("hookup_flags", kHairFace),
    )  # type: ignore
    flag_kHairNeck: BoolProperty(
        name="kHairNeck", description="kHairNeck (128)",
        get=_make_flag_getter("hookup_flags", kHairNeck),
        set=_make_flag_setter("hookup_flags", kHairNeck),
    )  # type: ignore
    flag_kHairLeftThigh: BoolProperty(
        name="kHairLeftThigh", description="kHairLeftThigh (256)",
        get=_make_flag_getter("hookup_flags", kHairLeftThigh),
        set=_make_flag_setter("hookup_flags", kHairLeftThigh),
    )  # type: ignore
    flag_kHairRightThigh: BoolProperty(
        name="kHairRightThigh", description="kHairRightThigh (512)",
        get=_make_flag_getter("hookup_flags", kHairRightThigh),
        set=_make_flag_setter("hookup_flags", kHairRightThigh),
    )  # type: ignore
    flag_kHairLeftHip: BoolProperty(
        name="kHairLeftHip", description="kHairLeftHip (1024)",
        get=_make_flag_getter("hookup_flags", kHairLeftHip),
        set=_make_flag_setter("hookup_flags", kHairLeftHip),
    )  # type: ignore
    flag_kHairRigh2hip: BoolProperty(
        name="kHairRigh2hip", description="kHairRigh2hip (2048)",
        get=_make_flag_getter("hookup_flags", kHairRigh2hip),
        set=_make_flag_setter("hookup_flags", kHairRigh2hip),
    )  # type: ignore
    bones: CollectionProperty(type=MILOHC_PG_hair_bone_item)  # type: ignore


HAIR_PHYSICS_KEYS = (
    'simulate', 'stiffness', 'torsion', 'inertia', 'gravity', 'weight',
    'friction', 'min_slack', 'max_slack',
)


class MILOHC_PG_hair_file(bpy.types.PropertyGroup):
    """One CharHair (.hair) file: its own strands and physics settings."""
    name: StringProperty(
        name="File Name",
        description="Exported as <name>.hair. Leave empty to use the milo's own name",
        default="",
        update=_hair_prop_update,
    )  # type: ignore
    export: BoolProperty(
        name="Export",
        description="Include this .hair file in the export",
        default=True,
        update=_hair_prop_update,
    )  # type: ignore
    hair_flags: IntProperty(
        name="Hair Flags",
        description="A collision-group bitmask applied to every strand in this .hair file",
        default=0,
        min=0,
        update=_hair_prop_update,
    )  # type: ignore
    flag_kHairBack: BoolProperty(
        name="kHairBack", description="kHairBack (1)",
        get=_make_flag_getter("hair_flags", kHairBack),
        set=_make_flag_setter("hair_flags", kHairBack),
    )  # type: ignore
    flag_kHairChest: BoolProperty(
        name="kHairChest", description="kHairChest (2)",
        get=_make_flag_getter("hair_flags", kHairChest),
        set=_make_flag_setter("hair_flags", kHairChest),
    )  # type: ignore
    flag_kHairForehead: BoolProperty(
        name="kHairForehead", description="kHairForehead (4)",
        get=_make_flag_getter("hair_flags", kHairForehead),
        set=_make_flag_setter("hair_flags", kHairForehead),
    )  # type: ignore
    flag_kHairHead: BoolProperty(
        name="kHairHead", description="kHairHead (8)",
        get=_make_flag_getter("hair_flags", kHairHead),
        set=_make_flag_setter("hair_flags", kHairHead),
    )  # type: ignore
    flag_kHairLeftShoulder: BoolProperty(
        name="kHairLeftShoulder", description="kHairLeftShoulder (16)",
        get=_make_flag_getter("hair_flags", kHairLeftShoulder),
        set=_make_flag_setter("hair_flags", kHairLeftShoulder),
    )  # type: ignore
    flag_kHairRightShoulder: BoolProperty(
        name="kHairRightShoulder", description="kHairRightShoulder (32)",
        get=_make_flag_getter("hair_flags", kHairRightShoulder),
        set=_make_flag_setter("hair_flags", kHairRightShoulder),
    )  # type: ignore
    flag_kHairFace: BoolProperty(
        name="kHairFace", description="kHairFace (64)",
        get=_make_flag_getter("hair_flags", kHairFace),
        set=_make_flag_setter("hair_flags", kHairFace),
    )  # type: ignore
    flag_kHairNeck: BoolProperty(
        name="kHairNeck", description="kHairNeck (128)",
        get=_make_flag_getter("hair_flags", kHairNeck),
        set=_make_flag_setter("hair_flags", kHairNeck),
    )  # type: ignore
    flag_kHairLeftThigh: BoolProperty(
        name="kHairLeftThigh", description="kHairLeftThigh (256)",
        get=_make_flag_getter("hair_flags", kHairLeftThigh),
        set=_make_flag_setter("hair_flags", kHairLeftThigh),
    )  # type: ignore
    flag_kHairRightThigh: BoolProperty(
        name="kHairRightThigh", description="kHairRightThigh (512)",
        get=_make_flag_getter("hair_flags", kHairRightThigh),
        set=_make_flag_setter("hair_flags", kHairRightThigh),
    )  # type: ignore
    flag_kHairLeftHip: BoolProperty(
        name="kHairLeftHip", description="kHairLeftHip (1024)",
        get=_make_flag_getter("hair_flags", kHairLeftHip),
        set=_make_flag_setter("hair_flags", kHairLeftHip),
    )  # type: ignore
    flag_kHairRigh2hip: BoolProperty(
        name="kHairRigh2hip", description="kHairRigh2hip (2048)",
        get=_make_flag_getter("hair_flags", kHairRigh2hip),
        set=_make_flag_setter("hair_flags", kHairRigh2hip),
    )  # type: ignore
    simulate: BoolProperty(
        name="Simulate Physics?",
        description="CharHair.simulate - run the hair/cloth physics simulation",
        default=True,
        update=_hair_prop_update,
    )  # type: ignore
    stiffness: FloatProperty(
        name="Stiffness",
        description="CharHair.stiffness - stiffness of each strand",
        default=0.1,
        update=_hair_prop_update,
    )  # type: ignore
    torsion: FloatProperty(
        name="Torsion",
        description="CharHair.torsion - rotational stiffness of each strand",
        default=0.1,
        update=_hair_prop_update,
    )  # type: ignore
    inertia: FloatProperty(
        name="Inertia",
        description="CharHair.inertia - inertia of the hair, zero means none",
        default=0.65,
        update=_hair_prop_update,
    )  # type: ignore
    gravity: FloatProperty(
        name="Gravity",
        description="CharHair.gravity - gravity of the hair, one is normal",
        default=1.0,
        update=_hair_prop_update,
    )  # type: ignore
    weight: FloatProperty(
        name="Weight",
        description="CharHair.weight - weight of the hair, one is normal",
        default=0.5,
        update=_hair_prop_update,
    )  # type: ignore
    friction: FloatProperty(
        name="Friction",
        description="CharHair.friction - hair friction against each other",
        default=0.0,
        update=_hair_prop_update,
    )  # type: ignore
    min_slack: FloatProperty(
        name="Min Slack",
        description="CharHair.minSlack - if using sides, how far in it could go",
        default=0.0,
        update=_hair_prop_update,
    )  # type: ignore
    max_slack: FloatProperty(
        name="Max Slack",
        description="CharHair.maxSlack - if using sides, how far out it could go",
        default=0.0,
        update=_hair_prop_update,
    )  # type: ignore
    strands: CollectionProperty(type=MILOHC_PG_hair_strand)  # type: ignore
    active_strand_index: IntProperty(default=0)  # type: ignore


class MILOHC_PG_hair_settings(bpy.types.PropertyGroup):
    """Armature-level hair setup: the list of .hair files this armature exports."""
    enabled: BoolProperty(
        name="Export CharHair (.hair) for this Armature",
        description="When on, enables CharHair (.hair) data on this armature",
        default=False,
        update=_hair_prop_update,
    )  # type: ignore
    show_hair_collision_radius: BoolProperty(
        name="Preview Hair Bone Collision Radius",
        description=(
            "Show live wireframe spheres in the 3D viewport for the collision radius "
            "of each bone/strand in the .hair setup"
        ),
        default=True,
        update=_hair_prop_update,
    )  # type: ignore
    show_hair_outer_radius: BoolProperty(
        name="Preview Outer Radius",
        description=(
            "Also show wireframe spheres in the 3D viewport for the outer collision "
            "radius of each hair bone (when Outer Radius > 0)"
        ),
        default=False,
        update=_hair_prop_update,
    )  # type: ignore
    auto_calculate_nodes: BoolProperty(
        name="Auto-Calculate Hair Nodes (C# Style)",
        description=(
            "Automatically detect bone_hair_*, bone_torso_breast*, and bone_skirt* chains "
            "from the armature like the C# glTFMilo tool. Disable if you prefer to build "
            "and manage strands manually"
        ),
        default=True,
        update=_hair_prop_update,
    )  # type: ignore
    split_strands_at_branches: BoolProperty(
        name="Split Strands at Branches",
        description=(
            "End a strand at every branch point so each bone belongs to one strand "
            "(matches C# default). Uncheck to duplicate shared ancestor bones across "
            "branches (--disable-splitting)"
        ),
        default=True,
        update=_hair_prop_update,
    )  # type: ignore
    only_weighted_bones: BoolProperty(
        name="Only Weighted Bones",
        description=(
            "Only include hair/breast/skirt chains that have at least one vertex weight "
            "on a mesh bound to this armature (falls back to all matching bones if no "
            "meshes are weighted yet)"
        ),
        default=True,
        update=_hair_prop_update,
    )  # type: ignore
    use_csharp_point_calc: BoolProperty(
        name="Use C# Point Math (Taper Radius & Tip Offset)",
        description=(
            "Calculate point positions (next bone / local +Y tip), unk5c reset position, "
            "and tapered collision radius/outerRadius using NodeProcessor.cs math. "
            "Uncheck if you prefer zero radius and bone head positions"
        ),
        default=True,
        update=_hair_prop_update,
    )  # type: ignore
    files: CollectionProperty(type=MILOHC_PG_hair_file)  # type: ignore
    active_file_index: IntProperty(default=0, update=_hair_prop_update)  # type: ignore

    # pre-multi-file layout (one .hair per armature)
    simulate: BoolProperty(
        name="Simulate Physics?",
        description="CharHair.simulate - run the hair/cloth physics simulation",
        default=True,
        update=_hair_prop_update,
    )  # type: ignore
    stiffness: FloatProperty(
        name="Stiffness",
        description="CharHair.stiffness - stiffness of each strand",
        default=0.1,
        update=_hair_prop_update,
    )  # type: ignore
    torsion: FloatProperty(
        name="Torsion",
        description="CharHair.torsion - rotational stiffness of each strand",
        default=0.1,
        update=_hair_prop_update,
    )  # type: ignore
    inertia: FloatProperty(
        name="Inertia",
        description="CharHair.inertia - inertia of the hair, zero means none",
        default=0.65,
        update=_hair_prop_update,
    )  # type: ignore
    gravity: FloatProperty(
        name="Gravity",
        description="CharHair.gravity - gravity of the hair, one is normal",
        default=1.0,
        update=_hair_prop_update,
    )  # type: ignore
    weight: FloatProperty(
        name="Weight",
        description="CharHair.weight - weight of the hair, one is normal",
        default=0.5,
        update=_hair_prop_update,
    )  # type: ignore
    friction: FloatProperty(
        name="Friction",
        description="CharHair.friction - hair friction against each other",
        default=0.0,
        update=_hair_prop_update,
    )  # type: ignore
    min_slack: FloatProperty(
        name="Min Slack",
        description="CharHair.minSlack - if using sides, how far in it could go",
        default=0.0,
        update=_hair_prop_update,
    )  # type: ignore
    max_slack: FloatProperty(
        name="Max Slack",
        description="CharHair.maxSlack - if using sides, how far out it could go",
        default=0.0,
        update=_hair_prop_update,
    )  # type: ignore
    strands: CollectionProperty(type=MILOHC_PG_hair_strand)  # type: ignore
    active_strand_index: IntProperty(default=0, update=_hair_prop_update)  # type: ignore


def hair_file_label(hair_file, index):
    """How a hair file is shown in the UI: its export name, or the milo-name default."""
    if hair_file.name.strip():
        return f"{hair_file.name.strip()}.hair"
    return "<milo name>.hair" if index == 0 else f"<milo name>_{index + 1}.hair"


def _active_hair_file(hair):
    if hair is not None and 0 <= hair.active_file_index < len(hair.files):
        return hair.files[hair.active_file_index]
    return None


def _new_hair_file(hair):
    """Adds a hair file. The first one takes over a pre-multi-file setup (its strands
    and physics values), so nothing authored before is lost."""
    first = len(hair.files) == 0
    f = hair.files.add()
    if first:
        f.name = ""
        if len(hair.strands):
            for key in HAIR_PHYSICS_KEYS:
                setattr(f, key, getattr(hair, key))
            for old in hair.strands:
                st = f.strands.add()
                st.name, st.angle, st.hookup_flags = old.name, old.angle, old.hookup_flags
                st.collision_radius = getattr(old, "collision_radius", 0.75)
                st.outer_radius = getattr(old, "outer_radius", -1.0)
                st.taper_radius = getattr(old, "taper_radius", True)
                for b in old.bones:
                    nb = st.bones.add()
                    nb.name = b.name
                    nb.radius = getattr(b, "radius", 0.5)
                    nb.outer_radius = getattr(b, "outer_radius", -1.0)
            hair.strands.clear()
            hair.active_strand_index = 0
    else:
        f.name = f"hair_{len(hair.files)}"
    hair.active_file_index = len(hair.files) - 1
    _sync_hair_custom_props(getattr(hair, "id_data", None))
    _tag_redraw_view3d()
    return f


def _selected_bone_names(context, armature_obj):
    """Returns the set of selected bone names, working across Blender modes."""
    names = set()
    for attr in ("selected_pose_bones", "selected_bones", "selected_editable_bones"):
        seq = getattr(context, attr, None)
        if seq:
            for b in seq:
                name = getattr(b, "name", None)
                if name and name in armature_obj.data.bones:
                    names.add(name)
            if names:
                return names
    return names


def _ordered_bones_from_selection(context, armature_obj):
    """Given an armature with some bones selected (in pose or edit mode), returns the
    selected bones (as data.bones entries) ordered root-first along their parent chain."""
    selected_set = _selected_bone_names(context, armature_obj)
    if not selected_set:
        raise ValueError(
            "No bones selected. Enter Pose or Edit mode and select the bones of a "
            "single hair strand first."
        )

    bones = armature_obj.data.bones
    selected = [bones[name] for name in selected_set]

    roots = [b for b in selected if (b.parent is None or b.parent.name not in selected_set)]
    if len(roots) != 1:
        raise ValueError(
            f"Selection has {len(roots)} root bones - a strand must be a single "
            f"connected chain with exactly one root (a bone whose parent is unselected)."
        )

    ordered = []
    current = roots[0]
    while current is not None:
        ordered.append(current)
        sel_children = [c for c in current.children if c.name in selected_set]
        if len(sel_children) > 1:
            raise ValueError(
                f"Bone '{current.name}' has {len(sel_children)} selected children - "
                f"a strand must be a single linear chain, not a branching tree."
            )
        current = sel_children[0] if sel_children else None

    if len(ordered) != len(selected):
        raise ValueError(
            "Selected bones don't form one connected chain - some selected bones "
            "aren't part of the root's parent chain."
        )
    return ordered


class ARMATURE_OT_milohc_auto_generate_hair(bpy.types.Operator):
    """Automatically detect bone_hair_*, bone_torso_breast*, and bone_skirt* chains on this
    armature using the C# NodeProcessor algorithm and populate the .hair files and strands"""
    bl_idname = "armature.milohc_auto_generate_hair"
    bl_label = "Auto-Calculate Hair Nodes (C#)"
    bl_options = {'REGISTER', 'UNDO'}

    @classmethod
    def poll(cls, context):
        obj = context.object
        return obj is not None and obj.type == 'ARMATURE'

    def execute(self, context):
        armature_obj = context.object
        if armature_obj is None or armature_obj.type != 'ARMATURE':
            return {'CANCELLED'}
        hair = _get_hair_settings(armature_obj.data)
        if hair is None:
            return {'CANCELLED'}
        n_files, n_strands = populate_auto_hair_strands(armature_obj, hair, overwrite_existing=True)
        if n_strands == 0:
            self.report(
                {'WARNING'},
                "No bone_hair_*, bone_torso_breast*, or bone_skirt* bones found on this armature.",
            )
            return {'CANCELLED'}
        self.report(
            {'INFO'},
            f"Auto-calculated {n_strands} strand(s) across {n_files} .hair file(s) (C# NodeProcessor logic).",
        )
        return {'FINISHED'}


class ARMATURE_OT_milohc_create_hair_strand(bpy.types.Operator):
    """Create a hair strand from the currently selected armature bones. The bones must
    form a single connected chain (one root, no branching); they're recorded root-first
    so the exporter knows the physics bone order and parenting chain."""
    bl_idname = "armature.milohc_create_hair_strand"
    bl_label = "Create Hair Strand from Selected Bones"
    bl_options = {'REGISTER', 'UNDO'}

    @classmethod
    def poll(cls, context):
        obj = context.object
        return obj is not None and obj.type == 'ARMATURE'

    def execute(self, context):
        armature_obj = context.object
        if armature_obj is None or armature_obj.type != 'ARMATURE':
            return {'CANCELLED'}
        try:
            ordered = _ordered_bones_from_selection(context, armature_obj)
        except ValueError as e:
            self.report({'ERROR'}, str(e))
            return {'CANCELLED'}

        hair = _get_hair_settings(armature_obj.data)
        if hair is None:
            return {'CANCELLED'}
        hair_file = _active_hair_file(hair) or _new_hair_file(hair)
        strand = hair_file.strands.add()
        strand.name = f"Strand {len(hair_file.strands)}"
        for bone in ordered:
            item = strand.bones.add()
            item.name = bone.name
        _apply_strand_radius_to_bones(strand)
        hair_file.active_strand_index = len(hair_file.strands) - 1
        _sync_hair_custom_props(armature_obj.data)
        _tag_redraw_view3d(context)

        used = {
            b.name: hair_file_label(f, i)
            for i, f in enumerate(hair.files)
            for st in f.strands if st != strand
            for b in st.bones
        }
        clash = [b.name for b in ordered if b.name in used]
        msg = (
            f"Added '{strand.name}' to {hair_file_label(hair_file, hair.active_file_index)} "
            f"from {len(ordered)} bone(s): {' -> '.join(b.name for b in ordered)}"
        )
        if clash:
            self.report(
                {'WARNING'},
                msg + f". Already used by another strand: {', '.join(clash)}",
            )
        else:
            self.report({'INFO'}, msg)
        return {'FINISHED'}


class ARMATURE_OT_milohc_remove_hair_strand(bpy.types.Operator):
    """Remove the selected hair strand."""
    bl_idname = "armature.milohc_remove_hair_strand"
    bl_label = "Remove Hair Strand"
    bl_options = {'REGISTER', 'UNDO'}

    @classmethod
    def poll(cls, context):
        obj = context.object
        if obj is None or obj.type != 'ARMATURE':
            return False
        f = _active_hair_file(_get_hair_settings(obj.data))
        return f is not None and len(f.strands) > 0

    def execute(self, context):
        obj = context.object
        if obj is None or obj.type != 'ARMATURE':
            return {'CANCELLED'}
        arm_data = obj.data
        f = _active_hair_file(_get_hair_settings(arm_data))
        if f is None:
            return {'CANCELLED'}
        idx = f.active_strand_index
        if 0 <= idx < len(f.strands):
            f.strands.remove(idx)
            f.active_strand_index = max(0, idx - 1)
        _sync_hair_custom_props(arm_data)
        _tag_redraw_view3d(context)
        return {'FINISHED'}


class ARMATURE_OT_milohc_add_hair_file(bpy.types.Operator):
    """Add a .hair file to this armature's hair setup. Strands created from selected bones
    go into the active file"""
    bl_idname = "armature.milohc_add_hair_file"
    bl_label = "Add Hair File"
    bl_options = {'REGISTER', 'UNDO'}

    @classmethod
    def poll(cls, context):
        obj = context.object
        return obj is not None and obj.type == 'ARMATURE'

    def execute(self, context):
        obj = context.object
        if obj is None or obj.type != 'ARMATURE':
            return {'CANCELLED'}
        arm_data = obj.data
        hair = _get_hair_settings(arm_data)
        if hair is None:
            return {'CANCELLED'}
        had_legacy = len(hair.files) == 0 and len(hair.strands) > 0
        f = _new_hair_file(hair)
        _sync_hair_custom_props(arm_data)
        _tag_redraw_view3d(context)
        if had_legacy:
            self.report(
                {'INFO'},
                f"Moved the existing {len(f.strands)} strand(s) into {hair_file_label(f, 0)}",
            )
        return {'FINISHED'}


class ARMATURE_OT_milohc_remove_hair_file(bpy.types.Operator):
    """Remove the selected .hair file and its strands"""
    bl_idname = "armature.milohc_remove_hair_file"
    bl_label = "Remove Hair File"
    bl_options = {'REGISTER', 'UNDO'}

    @classmethod
    def poll(cls, context):
        obj = context.object
        return (
            obj is not None
            and obj.type == 'ARMATURE'
            and _active_hair_file(_get_hair_settings(obj.data)) is not None
        )

    def execute(self, context):
        obj = context.object
        if obj is None or obj.type != 'ARMATURE':
            return {'CANCELLED'}
        arm_data = obj.data
        hair = _get_hair_settings(arm_data)
        if hair is None:
            return {'CANCELLED'}
        idx = hair.active_file_index
        if 0 <= idx < len(hair.files):
            hair.files.remove(idx)
            hair.active_file_index = max(0, min(idx, len(hair.files) - 1))
        _sync_hair_custom_props(arm_data)
        _tag_redraw_view3d(context)
        return {'FINISHED'}


class ARMATURE_OT_milohc_select_hair_bones(bpy.types.Operator):
    """Select this .hair file's bones in Pose mode, to see which bones it drives"""
    bl_idname = "armature.milohc_select_hair_bones"
    bl_label = "Select Bones"
    bl_options = {'REGISTER', 'UNDO'}

    @classmethod
    def poll(cls, context):
        obj = context.object
        return (
            obj is not None
            and obj.type == 'ARMATURE'
            and obj.mode == 'POSE'
            and _active_hair_file(_get_hair_settings(obj.data)) is not None
        )

    def execute(self, context):
        obj = context.object
        if obj is None or obj.type != 'ARMATURE' or obj.pose is None:
            return {'CANCELLED'}
        f = _active_hair_file(_get_hair_settings(obj.data))
        if f is None:
            return {'CANCELLED'}
        names = {b.name for st in f.strands for b in st.bones}
        for pb in obj.pose.bones:
            sel = pb.name in names
            for target in (pb, getattr(pb, 'bone', None)):
                if target is not None and hasattr(target, 'select'):
                    try:
                        target.select = sel
                    except (AttributeError, TypeError):
                        pass
        self.report({'INFO'}, f"Selected {len(names)} bone(s)")
        return {'FINISHED'}


class DATA_UL_milohc_hair_files(bpy.types.UIList):
    def draw_item(self, _context, layout, _data, item, _icon, _active_data, _active_propname, index):
        row = layout.row(align=True)
        row.prop(item, "export", text="")
        row.label(text=hair_file_label(item, index), icon='FILE')
        n_bones = sum(len(st.bones) for st in item.strands)
        row.label(text=f"{len(item.strands)} strand(s), {n_bones} bone(s)")


class DATA_UL_milohc_hair_strands(bpy.types.UIList):
    def draw_item(self, _context, layout, _data, item, _icon, _active_data, _active_propname, _index):
        row = layout.row(align=True)
        row.label(text=item.name, icon='STRANDS')
        row.label(text=f"{len(item.bones)} bone(s)")
        row.prop(item, "collision_radius", text="Radius")


def _hair_bone_owners(hair):
    """{bone name: [(file label, strand name)]} across every .hair file of an armature."""
    owners = {}
    for i, f in enumerate(hair.files):
        for st in f.strands:
            for b in st.bones:
                owners.setdefault(b.name, []).append((hair_file_label(f, i), st.name))
    return owners


class DATA_PT_milohc_hair(bpy.types.Panel):
    bl_label = "Rock Band Hair Setup"
    bl_idname = "DATA_PT_milohc_hair"
    bl_space_type = 'PROPERTIES'
    bl_region_type = 'WINDOW'
    bl_context = "data"

    @classmethod
    def poll(cls, context):
        return context.object is not None and context.object.type == 'ARMATURE'

    def draw(self, context):
        layout = self.layout
        arm = context.object
        if arm is None or arm.type != 'ARMATURE':
            return
        hair = _get_hair_settings(arm.data)
        if hair is None:
            return

        layout.prop(hair, "enabled")
        col = layout.column()
        col.enabled = hair.enabled
        col.label(text="Exported with Rock Band 3 and Dance Central 1 only", icon='INFO')

        # --- Viewport Hair Collision Radius Preview ---
        preview_box = col.box()
        prev_row = preview_box.row(align=True)
        prev_row.prop(hair, "show_hair_collision_radius", icon='SHADING_WIRE')
        if getattr(hair, "show_hair_collision_radius", True):
            prev_row.prop(hair, "show_hair_outer_radius", icon='PROP_ON')

        # --- Auto-Calculation (C# NodeProcessor style) vs Manual ---
        auto_box = col.box()
        auto_box.prop(hair, "auto_calculate_nodes")
        if getattr(hair, "auto_calculate_nodes", True):
            sub_auto = auto_box.column(align=True)
            sub_auto.prop(hair, "split_strands_at_branches")
            sub_auto.prop(hair, "only_weighted_bones")
            sub_auto.prop(hair, "use_csharp_point_calc")
            sub_auto.operator("armature.milohc_auto_generate_hair", icon='AUTO')
        else:
            auto_box.label(text="Manual mode: create and edit strands manually below.", icon='TOOL_SETTINGS')

        if len(hair.files) == 0 and len(hair.strands):
            warn = col.box()
            warn.label(
                text=f"{len(hair.strands)} strand(s) from the single-file setup",
                icon='ERROR',
            )
            warn.label(text="They still export as one .hair. Add Hair File moves them")
            warn.label(text="into the list below so more files can be added.")

        # --- .hair files ---
        col.label(text=".hair Files", icon='FILE')
        row = col.row()
        row.template_list(
            "DATA_UL_milohc_hair_files", "", hair, "files",
            hair, "active_file_index", rows=3,
        )
        side = row.column(align=True)
        side.operator("armature.milohc_add_hair_file", icon='ADD', text="")
        side.operator("armature.milohc_remove_hair_file", icon='REMOVE', text="")

        hair_file = _active_hair_file(hair)
        if hair_file is None:
            col.operator("armature.milohc_create_hair_strand", icon='ADD')
            col.label(text="Creating a strand also creates the first .hair file")
            return

        box = col.box()
        box.prop(hair_file, "name")
        _draw_hair_collision_flags(box, hair_file, "hair_flags", title="Hair Flags")

        # --- strands of the active file ---
        col.operator("armature.milohc_create_hair_strand", icon='ADD')
        row = col.row()
        row.template_list(
            "DATA_UL_milohc_hair_strands", "", hair_file, "strands",
            hair_file, "active_strand_index", rows=3,
        )
        side = row.column(align=True)
        side.operator("armature.milohc_remove_hair_strand", icon='REMOVE', text="")

        if 0 <= hair_file.active_strand_index < len(hair_file.strands):
            strand = hair_file.strands[hair_file.active_strand_index]
            box = col.box()
            box.prop(strand, "name")
            box.prop(strand, "angle")

            rad_box = box.box()
            rad_box.label(text="Strand Collision Radius:", icon='MESH_UVSPHERE')
            rad_row = rad_box.row(align=True)
            rad_row.prop(strand, "collision_radius", text="Radius")
            rad_row.prop(strand, "outer_radius", text="Outer Radius")
            rad_box.prop(strand, "taper_radius")

            _draw_hair_collision_flags(box, strand, "hookup_flags", title="Hookup Flags")
            if hair_file.hair_flags:
                box.label(
                    text=(
                        f"Exported hookup flags: "
                        f"{strand.hookup_flags | hair_file.hair_flags} "
                        f"(with the file's Hair Flags)"
                    ),
                    icon='INFO',
                )

        # --- every bone this .hair file drives, read live from its strands ---
        owners = _hair_bone_owners(hair)
        idx = hair.active_file_index
        bones_box = col.box()
        head = bones_box.row()
        head.label(
            text=f"Bones in {hair_file_label(hair_file, idx)} (root -> tip)",
            icon='BONE_DATA',
        )
        head.operator("armature.milohc_select_hair_bones", text="", icon='RESTRICT_SELECT_OFF')
        if not hair_file.strands:
            bones_box.label(text="No strands yet - select a bone chain and click Create")
        show_outer = bool(getattr(hair, "show_hair_outer_radius", False))
        for si, st in enumerate(hair_file.strands):
            active = si == hair_file.active_strand_index
            st_header = bones_box.row(align=True)
            st_header.label(text=st.name, icon='STRANDS' if not active else 'RIGHTARROW')
            st_header.prop(st, "collision_radius", text="Strand Radius")
            sub = bones_box.column(align=True)
            for b in st.bones:
                missing = b.name not in arm.data.bones
                shared = len(owners.get(b.name, ())) > 1
                r = sub.row(align=True)
                r.alert = missing or shared
                note = (
                    " (missing)" if missing
                    else (" (shared)" if shared else "")
                )
                r.label(
                    text=f"    {b.name}{note}",
                    icon='ERROR' if (missing or shared) else 'BONE_DATA',
                )
                r.prop(b, "radius", text="Radius")
                if show_outer:
                    r.prop(b, "outer_radius", text="Outer")

        # --- physics, per file ---
        col.separator()
        box = col.box()
        box.label(text=f"Physics Settings ({hair_file_label(hair_file, idx)})", icon='PHYSICS')
        for key in HAIR_PHYSICS_KEYS:
            box.prop(hair_file, key)
        box.label(text="Wind: world.wind (fixed)", icon='FORCE_WIND')


# Collision PropertyGroup, Viewport Draw (In Front + Rotation + Hair Bone Radii), and Panel

def _coll_redraw(self, context):
    """Sync custom properties and tag all 3D viewports for redraw when a collision
    property changes, so the wireframe preview updates live."""
    arm_data = getattr(self, "id_data", None)
    _sync_collision_custom_props(arm_data)
    _tag_redraw_view3d(context)


class MILOHC_PG_collision_settings(bpy.types.PropertyGroup):
    """Per-bone CharCollide (.coll) settings. Stored on bpy.types.Bone."""
    enabled: BoolProperty(
        name="Export Collision Volume",
        description=(
            "Emit a CharCollide (.coll) volume attached to this bone. Also shows a "
            "live wireframe preview of the volume in the viewport"
        ),
        default=False,
        update=_coll_redraw,
    )  # type: ignore
    shape: EnumProperty(
        name="Shape",
        description="CharCollide.shape - the collision volume type",
        items=COLL_SHAPE_ITEMS,
        default='sphere',
        update=_coll_redraw,
    )  # type: ignore
    radius0: FloatProperty(
        name="Radius 0",
        description=(
            "CharCollide.radius0 - radius of the sphere, or of the length0 hemisphere "
            "if this is a cigar/capsule"
        ),
        default=0.5,
        min=0.0,
        soft_max=5.0,
        update=_coll_redraw,
    )  # type: ignore
    radius1: FloatProperty(
        name="Radius 1",
        description="CharCollide.radius1 - cigar only: radius of the length1 hemisphere",
        default=0.5,
        min=0.0,
        soft_max=5.0,
        update=_coll_redraw,
    )  # type: ignore
    length0: FloatProperty(
        name="Length 0",
        description=(
            "CharCollide.length0 - cigar only: placement of the radius0 hemisphere "
            "along the bone's local X axis. Must be <= length1"
        ),
        default=0.0,
        soft_min=-5.0,
        soft_max=5.0,
        update=_coll_redraw,
    )  # type: ignore
    length1: FloatProperty(
        name="Length 1",
        description=(
            "CharCollide.length1 - cigar only: placement of the radius1 hemisphere "
            "along the bone's local X axis. Must be >= length0"
        ),
        default=1.0,
        soft_min=-5.0,
        soft_max=5.0,
        update=_coll_redraw,
    )  # type: ignore
    rotation: FloatVectorProperty(
        name="Rotation",
        description=(
            "Local Euler XYZ rotation of the collision volume relative to its parent bone. "
            "Rotates the live wireframe preview and the exported CharCollide localXfm/worldXfm"
        ),
        subtype='EULER',
        unit='ROTATION',
        size=3,
        default=(0.0, 0.0, 0.0),
        update=_coll_redraw,
    )  # type: ignore
    flags: IntProperty(
        name="Collision Group Flags",
        description="CharCollide.flags - collision-group bitmask",
        default=1,
        min=0,
        update=_coll_redraw,
    )  # type: ignore
    flag_kHairBack: BoolProperty(
        name="kHairBack", description="kHairBack (1)",
        get=_make_flag_getter("flags", kHairBack),
        set=_make_flag_setter("flags", kHairBack),
    )  # type: ignore
    flag_kHairChest: BoolProperty(
        name="kHairChest", description="kHairChest (2)",
        get=_make_flag_getter("flags", kHairChest),
        set=_make_flag_setter("flags", kHairChest),
    )  # type: ignore
    flag_kHairForehead: BoolProperty(
        name="kHairForehead", description="kHairForehead (4)",
        get=_make_flag_getter("flags", kHairForehead),
        set=_make_flag_setter("flags", kHairForehead),
    )  # type: ignore
    flag_kHairHead: BoolProperty(
        name="kHairHead", description="kHairHead (8)",
        get=_make_flag_getter("flags", kHairHead),
        set=_make_flag_setter("flags", kHairHead),
    )  # type: ignore
    flag_kHairLeftShoulder: BoolProperty(
        name="kHairLeftShoulder", description="kHairLeftShoulder (16)",
        get=_make_flag_getter("flags", kHairLeftShoulder),
        set=_make_flag_setter("flags", kHairLeftShoulder),
    )  # type: ignore
    flag_kHairRightShoulder: BoolProperty(
        name="kHairRightShoulder", description="kHairRightShoulder (32)",
        get=_make_flag_getter("flags", kHairRightShoulder),
        set=_make_flag_setter("flags", kHairRightShoulder),
    )  # type: ignore
    flag_kHairFace: BoolProperty(
        name="kHairFace", description="kHairFace (64)",
        get=_make_flag_getter("flags", kHairFace),
        set=_make_flag_setter("flags", kHairFace),
    )  # type: ignore
    flag_kHairNeck: BoolProperty(
        name="kHairNeck", description="kHairNeck (128)",
        get=_make_flag_getter("flags", kHairNeck),
        set=_make_flag_setter("flags", kHairNeck),
    )  # type: ignore
    flag_kHairLeftThigh: BoolProperty(
        name="kHairLeftThigh", description="kHairLeftThigh (256)",
        get=_make_flag_getter("flags", kHairLeftThigh),
        set=_make_flag_setter("flags", kHairLeftThigh),
    )  # type: ignore
    flag_kHairRightThigh: BoolProperty(
        name="kHairRightThigh", description="kHairRightThigh (512)",
        get=_make_flag_getter("flags", kHairRightThigh),
        set=_make_flag_setter("flags", kHairRightThigh),
    )  # type: ignore
    flag_kHairLeftHip: BoolProperty(
        name="kHairLeftHip", description="kHairLeftHip (1024)",
        get=_make_flag_getter("flags", kHairLeftHip),
        set=_make_flag_setter("flags", kHairLeftHip),
    )  # type: ignore
    flag_kHairRigh2hip: BoolProperty(
        name="kHairRigh2hip", description="kHairRigh2hip (2048)",
        get=_make_flag_getter("flags", kHairRigh2hip),
        set=_make_flag_setter("flags", kHairRigh2hip),
    )  # type: ignore
    mesh_y_bias: BoolProperty(
        name="Mesh Y Bias",
        description=(
            "CharCollide.mesh_y_bias - for spheres/cigars, biases the fit toward the "
            "bone's local +Y (green) axis. Used by the game for chest/back volumes"
        ),
        default=False,
        update=_coll_redraw,
    )  # type: ignore


_coll_draw_handle = None


def _coll_line_shader():
    if gpu is None:
        return None
    try:
        return gpu.shader.from_builtin('UNIFORM_COLOR')
    except Exception:
        return gpu.shader.from_builtin('3D_UNIFORM_COLOR')


def _circle_points(center, axis_u, axis_v, radius, segments=24):
    """Return a list of points forming a circle in the plane spanned by axis_u/axis_v."""
    pts = []
    for i in range(segments):
        a = (i / segments) * 2.0 * _math.pi
        pts.append(center + axis_u * (radius * _math.cos(a)) + axis_v * (radius * _math.sin(a)))
    return pts


def _loop_segments(points):
    """Convert a closed loop of points into GPU LINE segment pairs."""
    segs = []
    n = len(points)
    for i in range(n):
        segs.append(points[i])
        segs.append(points[(i + 1) % n])
    return segs


def _sphere_wire_segments(center, rx, ry, rz, radius, segments=24):
    """Three orthogonal great-circle rings (XY, XZ, YZ) approximating a sphere."""
    segs = []
    segs += _loop_segments(_circle_points(center, rx, ry, radius, segments))
    segs += _loop_segments(_circle_points(center, rx, rz, radius, segments))
    segs += _loop_segments(_circle_points(center, ry, rz, radius, segments))
    return segs


def _capsule_wire_segments(world_mtx, radius0, radius1, length0, length1, segments=16):
    """Cigar/capsule wireframe along local X: a hemisphere of radius0 at x=length0 and a
    hemisphere of radius1 at x=length1, joined by tube lines."""
    if Vector is None:
        return []
    origin = world_mtx.to_translation()
    m = world_mtx.to_3x3()
    x_axis = m @ Vector((1.0, 0.0, 0.0))
    y_axis = m @ Vector((0.0, 1.0, 0.0))
    z_axis = m @ Vector((0.0, 0.0, 1.0))
    for v in (x_axis, y_axis, z_axis):
        if v.length > 1e-8:
            v.normalize()

    c0 = origin + x_axis * length0
    c1 = origin + x_axis * length1
    segs = []
    segs += _loop_segments(_circle_points(c0, y_axis, z_axis, radius0, segments))
    segs += _loop_segments(_circle_points(c1, y_axis, z_axis, radius1, segments))
    for ax in (y_axis, z_axis, -y_axis, -z_axis):
        segs.append(c0 + ax * radius0)
        segs.append(c1 + ax * radius1)

    def half_ring(center, axis_long, axis_side, radius, sign):
        pts = []
        steps = max(4, segments // 2)
        for i in range(steps + 1):
            a = (i / steps) * _math.pi
            pts.append(
                center
                + axis_long * (sign * radius * _math.sin(a))
                + axis_side * (radius * _math.cos(a))
            )
        out = []
        for i in range(len(pts) - 1):
            out.append(pts[i])
            out.append(pts[i + 1])
        return out

    segs += half_ring(c0, x_axis, y_axis, radius0, -1.0)
    segs += half_ring(c0, x_axis, z_axis, radius0, -1.0)
    segs += half_ring(c1, x_axis, y_axis, radius1, +1.0)
    segs += half_ring(c1, x_axis, z_axis, radius1, +1.0)
    return segs


def _plane_wire_segments(world_mtx, size=1.0):
    """A square plane facing bone-local +X, with a short normal stub."""
    if Vector is None:
        return []
    origin = world_mtx.to_translation()
    m = world_mtx.to_3x3()
    x_axis = m @ Vector((1.0, 0.0, 0.0))
    y_axis = m @ Vector((0.0, 1.0, 0.0))
    z_axis = m @ Vector((0.0, 0.0, 1.0))
    for v in (x_axis, y_axis, z_axis):
        if v.length > 1e-8:
            v.normalize()
    corners = [
        origin + y_axis * size + z_axis * size,
        origin - y_axis * size + z_axis * size,
        origin - y_axis * size - z_axis * size,
        origin + y_axis * size - z_axis * size,
    ]
    segs = _loop_segments(corners)
    segs.append(origin)
    segs.append(origin + x_axis * size)
    return segs


def _bone_world_matrix(armature_obj, bone, coll=None):
    """World matrix of a bone's collision volume (armature world @ bone.matrix_local @ local_rot)."""
    wm = armature_obj.matrix_world @ bone.matrix_local
    local_rot = _collision_local_matrix(coll)
    if local_rot is not None:
        wm = wm @ local_rot
    return wm


def _draw_collision_volumes():
    if gpu is None or batch_for_shader is None or Vector is None:
        return
    context = bpy.context
    if context is None or context.view_layer is None:
        return
    coll_segs = []
    hair_active_segs = []
    hair_other_segs = []
    hair_outer_segs = []

    for obj in context.view_layer.objects:
        if obj.type != 'ARMATURE' or not obj.visible_get():
            continue
        arm = obj.data
        # 1. Body / Bone CharCollide (.coll) volumes (Red)
        for bone in arm.bones:
            coll = _get_collision_settings(bone)
            if coll is None or not coll.enabled:
                continue
            wm = _bone_world_matrix(obj, bone, coll)
            origin = wm.to_translation()
            m = wm.to_3x3()
            rx = m @ Vector((1.0, 0.0, 0.0))
            ry = m @ Vector((0.0, 1.0, 0.0))
            rz = m @ Vector((0.0, 0.0, 1.0))
            for v in (rx, ry, rz):
                if v.length > 1e-8:
                    v.normalize()
            shape = coll.shape
            if shape in ('sphere', 'inside_sphere'):
                coll_segs += _sphere_wire_segments(origin, rx, ry, rz, coll.radius0)
            elif shape in ('cigar', 'inside_cigar'):
                lo, hi = coll.length0, coll.length1
                if lo > hi:
                    lo, hi = hi, lo
                coll_segs += _capsule_wire_segments(wm, coll.radius0, coll.radius1, lo, hi)
            elif shape == 'plane':
                coll_segs += _plane_wire_segments(wm, size=max(coll.radius0, 0.5))

        # 2. CharHair (.hair) strand bone collision radii (Cyan / Gold)
        hair = _get_hair_settings(arm)
        if hair is not None and hair.enabled and getattr(hair, "show_hair_collision_radius", True):
            show_outer = bool(getattr(hair, "show_hair_outer_radius", False))
            file_groups = []
            if len(hair.files) > 0:
                for fi, f in enumerate(hair.files):
                    if f.export:
                        file_groups.append((fi == hair.active_file_index, f.strands, f.active_strand_index))
            elif len(hair.strands) > 0:
                file_groups.append((True, hair.strands, hair.active_strand_index))

            for is_active_file, strands, active_st_idx in file_groups:
                for si, st in enumerate(strands):
                    is_active_strand = is_active_file and (si == active_st_idx)
                    target_list = hair_active_segs if is_active_strand else hair_other_segs
                    prev_origin = None
                    for b_item in st.bones:
                        bone = arm.bones.get(b_item.name)
                        if bone is None:
                            continue
                        if obj.mode == 'POSE' and obj.pose is not None and b_item.name in obj.pose.bones:
                            wm = obj.matrix_world @ obj.pose.bones[b_item.name].matrix
                        else:
                            wm = obj.matrix_world @ bone.matrix_local
                        origin = wm.to_translation()
                        if prev_origin is not None:
                            target_list.append(prev_origin)
                            target_list.append(origin)
                        prev_origin = origin

                        m = wm.to_3x3()
                        rx = m @ Vector((1.0, 0.0, 0.0))
                        ry = m @ Vector((0.0, 1.0, 0.0))
                        rz = m @ Vector((0.0, 0.0, 1.0))
                        for v in (rx, ry, rz):
                            if v.length > 1e-8:
                                v.normalize()

                        rad = float(getattr(b_item, "radius", 0.5))
                        if rad > 0.0:
                            target_list += _sphere_wire_segments(origin, rx, ry, rz, rad, segments=16)

                        if show_outer:
                            out_rad = float(getattr(b_item, "outer_radius", -1.0))
                            if out_rad > 0.0:
                                hair_outer_segs += _sphere_wire_segments(
                                    origin, rx, ry, rz, out_rad, segments=16,
                                )

    if not (coll_segs or hair_active_segs or hair_other_segs or hair_outer_segs):
        return

    shader = _coll_line_shader()
    if shader is None:
        return
    gpu.state.line_width_set(1.5)
    gpu.state.blend_set('ALPHA')
    # Draw always in front of all elements (X-Ray / no depth occlusion)
    try:
        gpu.state.depth_test_set('NONE')
        gpu.state.depth_mask_set(False)
    except Exception:
        pass
    shader.bind()

    if coll_segs:
        batch_coll = batch_for_shader(shader, 'LINES', {"pos": coll_segs})
        shader.uniform_float("color", (1.0, 0.15, 0.15, 0.9))
        batch_coll.draw(shader)

    if hair_other_segs:
        batch_other = batch_for_shader(shader, 'LINES', {"pos": hair_other_segs})
        shader.uniform_float("color", (0.15, 0.70, 0.95, 0.70))
        batch_other.draw(shader)

    if hair_active_segs:
        batch_active = batch_for_shader(shader, 'LINES', {"pos": hair_active_segs})
        shader.uniform_float("color", (0.15, 0.95, 1.0, 0.95))
        batch_active.draw(shader)

    if hair_outer_segs:
        batch_outer = batch_for_shader(shader, 'LINES', {"pos": hair_outer_segs})
        shader.uniform_float("color", (1.0, 0.75, 0.15, 0.65))
        batch_outer.draw(shader)

    gpu.state.blend_set('NONE')


def _register_coll_draw_handler():
    global _coll_draw_handle
    if _coll_draw_handle is None:
        _coll_draw_handle = bpy.types.SpaceView3D.draw_handler_add(
            _draw_collision_volumes, (), 'WINDOW', 'POST_VIEW',
        )


def _unregister_coll_draw_handler():
    global _coll_draw_handle
    if _coll_draw_handle is not None:
        try:
            bpy.types.SpaceView3D.draw_handler_remove(_coll_draw_handle, 'WINDOW')
        except Exception:
            pass
        _coll_draw_handle = None


class BONE_PT_milohc_collision(bpy.types.Panel):
    bl_label = "Rock Band Collision Setup"
    bl_idname = "BONE_PT_milohc_collision"
    bl_space_type = 'PROPERTIES'
    bl_region_type = 'WINDOW'
    bl_context = "bone"

    @classmethod
    def poll(cls, context):
        return context.bone is not None or context.edit_bone is not None

    def draw_header(self, context):
        coll = _get_collision_settings(context.bone)
        if coll is not None:
            self.layout.prop(coll, "enabled", text="")

    def draw(self, context):
        layout = self.layout
        layout.use_property_split = True
        layout.use_property_decorate = False

        bone = context.bone
        if bone is None:
            layout.label(
                text="Select a bone in Pose or Object mode to edit collision.",
                icon='INFO',
            )
            return

        coll = _get_collision_settings(bone)
        if coll is None:
            return
        col = layout.column()
        col.active = coll.enabled
        col.prop(coll, "shape")

        shape = coll.shape
        if shape in ('sphere', 'inside_sphere'):
            col.prop(coll, "radius0", text="Radius")
        elif shape in ('cigar', 'inside_cigar'):
            col.prop(coll, "radius0")
            col.prop(coll, "radius1")
            col.prop(coll, "length0")
            col.prop(coll, "length1")
            if coll.length0 > coll.length1:
                col.label(
                    text="length0 > length1 (will be swapped for preview)",
                    icon='ERROR',
                )
            col.prop(coll, "mesh_y_bias")
        elif shape == 'plane':
            col.prop(coll, "radius0", text="Size")

        col.prop(coll, "rotation")

        _draw_hair_collision_flags(col, coll, "flags", title="Collision Group Flags")
        layout.label(text="Exported as <bone>.coll with the milo", icon='INFO')


# glTF 2.0 Exporter Hook (Automatically syncs & embeds milo_hair and milo_collision extras)

def _sync_all_armature_custom_props():
    for arm in getattr(bpy.data, "armatures", ()):
        try:
            _sync_collision_custom_props(arm)
        except Exception:
            pass
        try:
            _sync_hair_custom_props(arm)
        except Exception:
            pass


class glTF2ExportUserExtension:
    """Automatic hook for Blender's built-in glTF 2.0 exporter (`io_scene_gltf2`).
    Ensures `milo_hair`, `milo_collisions`, and per-bone `milo_collision` custom properties
    are freshly synced and written into `node.extras` on glTF export."""

    def __init__(self):
        _sync_all_armature_custom_props()

    def gather_node_hook(self, gltf2_node, blender_object, export_settings):
        if gltf2_node is None or blender_object is None:
            return
        if getattr(blender_object, "type", None) != 'ARMATURE':
            return
        arm_data = getattr(blender_object, "data", None)
        if arm_data is None:
            return
        try:
            _sync_collision_custom_props(arm_data)
        except Exception:
            pass
        try:
            _sync_hair_custom_props(arm_data)
        except Exception:
            pass
        extras_to_write = {}
        hair_dict = _build_hair_dict(arm_data)
        if hair_dict is not None:
            extras_to_write["milo_hair"] = hair_dict
            extras_to_write["milo_hair_json"] = _json.dumps(hair_dict)
        elif "milo_hair_json" in arm_data:
            extras_to_write["milo_hair_json"] = str(arm_data["milo_hair_json"])
        elif "milo_hair" in arm_data:
            extras_to_write["milo_hair"] = _to_plain_python(arm_data["milo_hair"])
        coll_map = {}
        for bone in getattr(arm_data, "bones", ()):
            coll = _get_collision_settings(bone)
            if coll is not None and coll.enabled:
                coll_map[bone.name] = _build_collision_dict(coll)
        if coll_map:
            extras_to_write["milo_collisions"] = coll_map
        elif "milo_collisions" in arm_data:
            extras_to_write["milo_collisions"] = _to_plain_python(arm_data["milo_collisions"])
        if extras_to_write:
            if getattr(gltf2_node, "extras", None) is None or not isinstance(gltf2_node.extras, dict):
                gltf2_node.extras = {}
            for k, v in extras_to_write.items():
                gltf2_node.extras[k] = v

    def gather_joint_hook(self, gltf2_node, blender_bone, export_settings):
        if gltf2_node is None or blender_bone is None:
            return
        bone = getattr(blender_bone, "bone", blender_bone)
        if bone is None:
            return
        coll = _get_collision_settings(bone)
        if coll is not None and coll.enabled:
            if getattr(gltf2_node, "extras", None) is None or not isinstance(gltf2_node.extras, dict):
                gltf2_node.extras = {}
            gltf2_node.extras["milo_collision"] = _build_collision_dict(coll)
        elif "milo_collision" in bone:
            if getattr(gltf2_node, "extras", None) is None or not isinstance(gltf2_node.extras, dict):
                gltf2_node.extras = {}
            gltf2_node.extras["milo_collision"] = _to_plain_python(bone["milo_collision"])
        if "milo_hair_json" in bone:
            if getattr(gltf2_node, "extras", None) is None or not isinstance(gltf2_node.extras, dict):
                gltf2_node.extras = {}
            gltf2_node.extras["milo_hair_json"] = str(bone["milo_hair_json"])


# Registration

classes = (
    MILOHC_PG_hair_bone_item,
    MILOHC_PG_hair_strand,
    MILOHC_PG_hair_file,
    MILOHC_PG_hair_settings,
    ARMATURE_OT_milohc_auto_generate_hair,
    ARMATURE_OT_milohc_create_hair_strand,
    ARMATURE_OT_milohc_remove_hair_strand,
    ARMATURE_OT_milohc_add_hair_file,
    ARMATURE_OT_milohc_remove_hair_file,
    ARMATURE_OT_milohc_select_hair_bones,
    DATA_UL_milohc_hair_files,
    DATA_UL_milohc_hair_strands,
    DATA_PT_milohc_hair,
    MILOHC_PG_collision_settings,
    BONE_PT_milohc_collision,
)

_owned_armature_prop = False
_owned_bone_prop = False


def register():
    global _owned_armature_prop, _owned_bone_prop
    for cls in classes:
        bpy.utils.register_class(cls)
    bpy.types.Armature.milohc_hair = PointerProperty(type=MILOHC_PG_hair_settings)
    bpy.types.Bone.milohc_collision = PointerProperty(type=MILOHC_PG_collision_settings)
    if not hasattr(bpy.types.Armature, "gltfmilo_hair"):
        bpy.types.Armature.gltfmilo_hair = PointerProperty(type=MILOHC_PG_hair_settings)
        _owned_armature_prop = True
    if not hasattr(bpy.types.Bone, "gltfmilo_collision"):
        bpy.types.Bone.gltfmilo_collision = PointerProperty(type=MILOHC_PG_collision_settings)
        _owned_bone_prop = True
    _register_coll_draw_handler()


def unregister():
    global _owned_armature_prop, _owned_bone_prop
    _unregister_coll_draw_handler()
    if _owned_bone_prop and hasattr(bpy.types.Bone, "gltfmilo_collision"):
        del bpy.types.Bone.gltfmilo_collision
        _owned_bone_prop = False
    if _owned_armature_prop and hasattr(bpy.types.Armature, "gltfmilo_hair"):
        del bpy.types.Armature.gltfmilo_hair
        _owned_armature_prop = False
    if hasattr(bpy.types.Bone, "milohc_collision"):
        del bpy.types.Bone.milohc_collision
    if hasattr(bpy.types.Armature, "milohc_hair"):
        del bpy.types.Armature.milohc_hair
    for cls in reversed(classes):
        try:
            bpy.utils.unregister_class(cls)
        except Exception:
            pass


if __name__ == "__main__":
    register()
