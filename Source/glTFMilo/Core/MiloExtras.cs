using MiloLib.Assets;
using MiloLib.Assets.Rnd;
using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
namespace MiloGLTFUtils.Source.glTFMilo.Core
{
    public class MiloExtras
    {
        [JsonPropertyName("milo_filename")]
        public string Filename { get; set; } = string.Empty;

        [JsonPropertyName("milo_obj_type")]
        public string ObjectType { get; set; } = string.Empty;
        [JsonPropertyName("milo_note")]
        public string Note { get; set; } = string.Empty;
        [JsonPropertyName("milo_is_showing")]
        public int IsShowing { get; set; }

        [JsonPropertyName("milo_draw_order")]
        public float DrawOrder { get; set; }

        [JsonPropertyName("milo_sphere_radius")]
        public float SphereRadius { get; set; }

        [JsonPropertyName("milo_sphere_center")]
        public float[] SphereCenter { get; set; } = new float[3];

        public static void AddToObject(Node node, MiloLib.Assets.Object obj, ref string fileName)
        {
            if (node.Extras != null)
            {
                string extrasJson = node.Extras.ToString();
                var miloExtras = JsonSerializer.Deserialize<MiloExtras>(extrasJson);
                if (miloExtras == null)
                {
                    Logger.Warn($"Node {node.Name} has extras but they could not be deserialized.");
                }
                else
                {
                    // Object
                    if (miloExtras.Filename != null && miloExtras.Filename != string.Empty)
                    {
                        fileName = miloExtras.Filename;
                    }
                    if (miloExtras.ObjectType != null && miloExtras.ObjectType != string.Empty)
                        obj.objFields.type = miloExtras.ObjectType;
                    if (miloExtras.Note != null && miloExtras.Note != string.Empty)
                        obj.objFields.note = miloExtras.Note;
                }
            }
        }

        public static void AddToMesh(Node node, RndMesh mesh, ref string fileName)
        {
            if (node.Extras != null)
            {
                string extrasJson = node.Extras.ToString();
                var miloExtras = JsonSerializer.Deserialize<MiloExtras>(extrasJson);

                if (miloExtras == null)
                {
                    Logger.Warn($"Node {node.Name} has extras but they could not be deserialized.");
                }
                else
                {

                    // Object
                    if (miloExtras.Filename != null && miloExtras.Filename != string.Empty)
                    {
                        fileName = miloExtras.Filename;
                    }

                    if (miloExtras.ObjectType != null && miloExtras.ObjectType != string.Empty)
                        mesh.objFields.type = miloExtras.ObjectType;

                    if (miloExtras.Note != null && miloExtras.Note != string.Empty)
                        mesh.objFields.note = miloExtras.Note;

                    // drawable
                    mesh.draw.showing = miloExtras?.IsShowing == 1;
                    mesh.draw.drawOrder = miloExtras?.DrawOrder ?? 0;
                    mesh.draw.sphere.radius = miloExtras?.SphereRadius ?? 10000.0f;
                    mesh.draw.sphere.x = miloExtras?.SphereCenter[0] ?? 0.0f;
                    mesh.draw.sphere.y = miloExtras?.SphereCenter[1] ?? 0.0f;
                    mesh.draw.sphere.z = miloExtras?.SphereCenter[2] ?? 0.0f;
                }
            }
        }

        public static void AddToGroup(Node node, RndGroup group, ref string fileName)
        {
            if (node.Extras != null)
            {
                string extrasJson = node.Extras.ToString();
                var miloExtras = JsonSerializer.Deserialize<MiloExtras>(extrasJson);
                if (miloExtras == null)
                {
                    Logger.Warn($"Node {node.Name} has extras but they could not be deserialized.");
                }
                else
                {
                    // Object
                    if (miloExtras.Filename != null && miloExtras.Filename != string.Empty)
                    {
                        fileName = miloExtras.Filename;
                    }
                    if (miloExtras.ObjectType != null && miloExtras.ObjectType != string.Empty)
                        group.objFields.type = miloExtras.ObjectType;
                    if (miloExtras.Note != null && miloExtras.Note != string.Empty)
                        group.objFields.note = miloExtras.Note;

                    // drawable
                    group.draw.showing = miloExtras?.IsShowing == 1;
                    group.draw.drawOrder = miloExtras?.DrawOrder ?? 0;
                    group.draw.sphere.radius = miloExtras?.SphereRadius ?? 10000.0f;
                    group.draw.sphere.x = miloExtras?.SphereCenter[0] ?? 0.0f;
                    group.draw.sphere.y = miloExtras?.SphereCenter[1] ?? 0.0f;
                    group.draw.sphere.z = miloExtras?.SphereCenter[2] ?? 0.0f;
                }
            }
        }

    }
    public class CharHairExtras
    {
        // default wind name, used by all venues and vignettes afaict
        public const string DefaultWind = "world.wind";

        [JsonPropertyName("milo_hair_stiffness")]
        public float Stiffness { get; set; } = 0.04f;

        [JsonPropertyName("milo_hair_torsion")]
        public float Torsion { get; set; } = 0.1f;

        [JsonPropertyName("milo_hair_inertia")]
        public float Inertia { get; set; } = 0.7f;

        [JsonPropertyName("milo_hair_gravity")]
        public float Gravity { get; set; } = 1.0f;

        [JsonPropertyName("milo_hair_friction")]
        public float Friction { get; set; } = 0.3f;

        [JsonPropertyName("milo_hair_weight")]
        public float Weight { get; set; } = 0.5f;

        [JsonPropertyName("milo_hair_wind")]
        public string Wind { get; set; } = DefaultWind;
    }
    public class CharHairBreastExtras
    {
        // default wind name, used by all venues and vignettes afaict
        // the values ​​come from the DC1 models and are most likely the same for the DC3.
        public const string DefaultWind = "world.wind";

        [JsonPropertyName("milo_breast_stiffness")]
        public float Stiffness { get; set; } = 0.225f;

        [JsonPropertyName("milo_breast_torsion")]
        public float Torsion { get; set; } = 0.75f;

        [JsonPropertyName("milo_breast_inertia")]
        public float Inertia { get; set; } = 0.5f;

        [JsonPropertyName("milo_breast_gravity")]
        public float Gravity { get; set; } = 1.0f;

        [JsonPropertyName("milo_breast_friction")]
        public float Friction { get; set; } = 0.0f;

        [JsonPropertyName("milo_breast_weight")]
        public float Weight { get; set; } = 1.0f;

        [JsonPropertyName("milo_breast_wind")]
        public string Wind { get; set; } = DefaultWind;
    }
    public class CharHairSkirtExtras
    {
        // default wind name, used by all venues and vignettes afaict
        // the values come from the DC1 models and are most likely the same for the DC3.
        public const string DefaultWind = "world.wind";

        [JsonPropertyName("milo_skirt_stiffness")]
        public float Stiffness { get; set; } = 0.225f;

        [JsonPropertyName("milo_skirt_torsion")]
        public float Torsion { get; set; } = 0.75f;

        [JsonPropertyName("milo_skirt_inertia")]
        public float Inertia { get; set; } = 0.5f;

        [JsonPropertyName("milo_skirt_gravity")]
        public float Gravity { get; set; } = 1.0f;

        [JsonPropertyName("milo_skirt_friction")]
        public float Friction { get; set; } = 0.0f;

        [JsonPropertyName("milo_skirt_weight")]
        public float Weight { get; set; } = 1.0f;

        [JsonPropertyName("milo_skirt_wind")]
        public string Wind { get; set; } = DefaultWind;
    }
    public class MaterialExtras
    {
        [JsonPropertyName("milo_z_mode")]
        public int ZMode { get; set; }
        [JsonPropertyName("milo_blend_mode")]
        public int BlendMode { get; set; }

        [JsonPropertyName("milo_alpha_cut")]
        public int AlphaCut { get; set; }

        [JsonPropertyName("milo_alpha_threshold")]
        public float AlphaThreshold { get; set; }
        [JsonPropertyName("milo_alpha_write")]
        public int AlphaWrite { get; set; }
        [JsonPropertyName("milo_use_environ")]
        public int UseEnvironment { get; set; }
        [JsonPropertyName("milo_emissive_multiplier")]
        public float EmissiveMultiplier { get; set; }
        [JsonPropertyName("milo_cull")]
        public int Cull { get; set; }
        [JsonPropertyName("milo_point_lights")]
        public int PointLights { get; set; }
        [JsonPropertyName("milo_proj_lights")]
        public int ProjectedLights { get; set; }

        [JsonPropertyName("milo_material_type")]
        public string MaterialType { get; set; } = string.Empty;

        [JsonPropertyName("milo_shader_variation")]
        public int ShaderVariation { get; set; }

        [JsonPropertyName("milo_material_prelit")]
        public int Prelit { get; set; }
        [JsonPropertyName("milo_normal_detail_map")]
        public string NormalDetailMap { get; set; } = string.Empty;
    }

    /// <summary>
    /// Custom property helper and models for the Blender addon `milo_hair_collision_addon.py`
    /// (`milo_collision` on bones and `milo_hair` / `milo_collisions` on the armature).
    /// </summary>
    public static class MiloAddonJsonHelpers
    {
        public static readonly Dictionary<string, int> HairCollisionFlagByName = new(StringComparer.OrdinalIgnoreCase)
        {
            { "kHairBack", 1 },
            { "kHairChest", 2 },
            { "kHairForehead", 4 },
            { "kHairHead", 8 },
            { "kHairLeftShoulder", 16 },
            { "kHairRightShoulder", 32 },
            { "kHairFace", 64 },
            { "kHairNeck", 128 },
            { "kHairLeftThigh", 256 },
            { "kHairRightThigh", 512 },
            { "kHairLeftHip", 1024 },
            { "kHairRigh2hip", 2048 },
        };

        public static int FlagsFromNames(IEnumerable<string>? names)
        {
            if (names == null) return 0;
            int mask = 0;
            foreach (var name in names)
            {
                if (!string.IsNullOrWhiteSpace(name) && HairCollisionFlagByName.TryGetValue(name.Trim(), out int bit))
                {
                    mask |= bit;
                }
            }
            return mask;
        }

        public static int ShapeNameToInt(string? shapeName, int fallback = 1)
        {
            if (string.IsNullOrWhiteSpace(shapeName)) return fallback;
            return shapeName.Trim().ToLowerInvariant() switch
            {
                "plane" or "kplane" => 0,
                "sphere" or "ksphere" => 1,
                "inside_sphere" or "insidesphere" or "kinsidesphere" => 2,
                "cigar" or "capsule" or "kcigar" => 3,
                "inside_cigar" or "insidecigar" or "kinsidecigar" => 4,
                _ => fallback,
            };
        }

        public static int GetInt(JsonElement obj, string propName, int defaultValue = 0)
        {
            if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(propName, out var el))
                return defaultValue;

            return el.ValueKind switch
            {
                JsonValueKind.True => 1,
                JsonValueKind.False => 0,
                JsonValueKind.Number when el.TryGetInt32(out int i) => i,
                JsonValueKind.Number when el.TryGetDouble(out double d) => (int)Math.Round(d),
                JsonValueKind.String when int.TryParse(el.GetString(), out int s) => s,
                JsonValueKind.String when bool.TryParse(el.GetString(), out bool b) => b ? 1 : 0,
                _ => defaultValue,
            };
        }

        public static bool HasProperty(JsonElement obj, string propName)
        {
            return obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(propName, out _);
        }

        public static float GetFloat(JsonElement obj, string propName, float defaultValue = 0.0f)
        {
            if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(propName, out var el))
                return defaultValue;

            return el.ValueKind switch
            {
                JsonValueKind.Number when el.TryGetSingle(out float f) => f,
                JsonValueKind.Number when el.TryGetDouble(out double d) => (float)d,
                JsonValueKind.String when float.TryParse(el.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float sf) => sf,
                _ => defaultValue,
            };
        }

        public static string GetString(JsonElement obj, string propName, string defaultValue = "")
        {
            if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(propName, out var el))
                return defaultValue;

            return el.ValueKind == JsonValueKind.String ? (el.GetString() ?? defaultValue) : defaultValue;
        }

        private static bool TryReadFloat(JsonElement item, out float value)
        {
            if (item.ValueKind == JsonValueKind.Number)
            {
                if (item.TryGetSingle(out value)) return true;
                if (item.TryGetDouble(out double d)) { value = (float)d; return true; }
            }
            else if (item.ValueKind == JsonValueKind.String &&
                float.TryParse(item.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value))
            {
                return true;
            }
            value = 0.0f;
            return false;
        }

        public static List<float> GetFloatList(JsonElement obj, string propName)
        {
            var list = new List<float>();
            if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(propName, out var el))
                return list;

            if (el.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in el.EnumerateArray())
                {
                    if (TryReadFloat(item, out float f))
                        list.Add(f);
                }
            }
            else if (el.ValueKind == JsonValueKind.Object)
            {
                // Handle Blender IDPropertyArray or Vector/Euler serialized as an object ({"0": x, "1": y, "2": z} or {"x": x, "y": y, "z": z})
                if (el.TryGetProperty("0", out var e0) && TryReadFloat(e0, out float f0) &&
                    el.TryGetProperty("1", out var e1) && TryReadFloat(e1, out float f1) &&
                    el.TryGetProperty("2", out var e2) && TryReadFloat(e2, out float f2))
                {
                    list.Add(f0);
                    list.Add(f1);
                    list.Add(f2);
                }
                else if (el.TryGetProperty("x", out var ex) && TryReadFloat(ex, out float fx) &&
                         el.TryGetProperty("y", out var ey) && TryReadFloat(ey, out float fy) &&
                         el.TryGetProperty("z", out var ez) && TryReadFloat(ez, out float fz))
                {
                    list.Add(fx);
                    list.Add(fy);
                    list.Add(fz);
                }
            }
            else if (el.ValueKind == JsonValueKind.String)
            {
                string? raw = el.GetString();
                if (!string.IsNullOrWhiteSpace(raw) && raw.TrimStart().StartsWith('['))
                {
                    try
                    {
                        using var arrDoc = JsonDocument.Parse(raw);
                        if (arrDoc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in arrDoc.RootElement.EnumerateArray())
                            {
                                if (TryReadFloat(item, out float f))
                                    list.Add(f);
                            }
                        }
                    }
                    catch
                    {
                        // ignore malformed float array string
                    }
                }
            }
            return list;
        }

        public static List<string> GetStringList(JsonElement obj, string propName)
        {
            var list = new List<string>();
            if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(propName, out var el) || el.ValueKind != JsonValueKind.Array)
                return list;

            foreach (var item in el.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    string? s = item.GetString();
                    if (!string.IsNullOrEmpty(s))
                        list.Add(s);
                }
            }
            return list;
        }
    }

    public class MiloCollisionExtras
    {
        [JsonPropertyName("enabled")]
        public int Enabled { get; set; } = 1;

        [JsonPropertyName("shape")]
        public string Shape { get; set; } = "sphere";

        [JsonPropertyName("shape_int")]
        public int ShapeInt { get; set; } = -1;

        [JsonPropertyName("radius0")]
        public float Radius0 { get; set; } = 0.5f;

        [JsonPropertyName("radius1")]
        public float Radius1 { get; set; } = 0.5f;

        [JsonPropertyName("length0")]
        public float Length0 { get; set; } = 0.0f;

        [JsonPropertyName("length1")]
        public float Length1 { get; set; } = 1.0f;

        [JsonPropertyName("rotation_rad")]
        public float[] RotationRad { get; set; } = new float[3];

        [JsonPropertyName("rotation_deg")]
        public float[] RotationDeg { get; set; } = new float[3];

        [JsonPropertyName("flags")]
        public int Flags { get; set; } = 0;

        [JsonPropertyName("flag_names")]
        public List<string> FlagNames { get; set; } = new();

        [JsonPropertyName("mesh_y_bias")]
        public int MeshYBias { get; set; } = 0;

        public int ResolvedShapeInt => ShapeInt >= 0 && ShapeInt <= 4
            ? ShapeInt
            : MiloAddonJsonHelpers.ShapeNameToInt(Shape, 1);

        public int ResolvedFlags => Flags | MiloAddonJsonHelpers.FlagsFromNames(FlagNames);

        public (float Rx, float Ry, float Rz) ResolvedRotationRad
        {
            get
            {
                if (RotationRad != null && RotationRad.Length >= 3 &&
                    (MathF.Abs(RotationRad[0]) > 1e-8f || MathF.Abs(RotationRad[1]) > 1e-8f || MathF.Abs(RotationRad[2]) > 1e-8f))
                {
                    return (RotationRad[0], RotationRad[1], RotationRad[2]);
                }
                if (RotationDeg != null && RotationDeg.Length >= 3 &&
                    (MathF.Abs(RotationDeg[0]) > 1e-8f || MathF.Abs(RotationDeg[1]) > 1e-8f || MathF.Abs(RotationDeg[2]) > 1e-8f))
                {
                    const float degToRad = MathF.PI / 180.0f;
                    return (RotationDeg[0] * degToRad, RotationDeg[1] * degToRad, RotationDeg[2] * degToRad);
                }
                return (0.0f, 0.0f, 0.0f);
            }
        }

        public static MiloCollisionExtras ParseElement(JsonElement el)
        {
            var coll = new MiloCollisionExtras
            {
                Enabled = MiloAddonJsonHelpers.GetInt(el, "enabled", 1),
                Shape = MiloAddonJsonHelpers.GetString(el, "shape", "sphere"),
                ShapeInt = MiloAddonJsonHelpers.HasProperty(el, "shape_int")
                    ? MiloAddonJsonHelpers.GetInt(el, "shape_int", 1)
                    : -1,
                Radius0 = MiloAddonJsonHelpers.GetFloat(el, "radius0", 0.5f),
                Radius1 = MiloAddonJsonHelpers.GetFloat(el, "radius1", 0.5f),
                Length0 = MiloAddonJsonHelpers.GetFloat(el, "length0", 0.0f),
                Length1 = MiloAddonJsonHelpers.GetFloat(el, "length1", 1.0f),
                Flags = MiloAddonJsonHelpers.GetInt(el, "flags", 0),
                FlagNames = MiloAddonJsonHelpers.GetStringList(el, "flag_names"),
                MeshYBias = MiloAddonJsonHelpers.GetInt(el, "mesh_y_bias", 0),
            };

            var rotRad = MiloAddonJsonHelpers.GetFloatList(el, "rotation_rad");
            if (rotRad.Count >= 3)
            {
                coll.RotationRad = new float[] { rotRad[0], rotRad[1], rotRad[2] };
            }
            else
            {
                var rotFallback = MiloAddonJsonHelpers.GetFloatList(el, "rotation");
                if (rotFallback.Count >= 3)
                {
                    coll.RotationRad = new float[] { rotFallback[0], rotFallback[1], rotFallback[2] };
                }
                else if (MiloAddonJsonHelpers.HasProperty(el, "rot_x") ||
                         MiloAddonJsonHelpers.HasProperty(el, "rot_y") ||
                         MiloAddonJsonHelpers.HasProperty(el, "rot_z"))
                {
                    coll.RotationRad = new float[]
                    {
                        MiloAddonJsonHelpers.GetFloat(el, "rot_x", 0.0f),
                        MiloAddonJsonHelpers.GetFloat(el, "rot_y", 0.0f),
                        MiloAddonJsonHelpers.GetFloat(el, "rot_z", 0.0f),
                    };
                }
            }

            var rotDeg = MiloAddonJsonHelpers.GetFloatList(el, "rotation_deg");
            if (rotDeg.Count >= 3)
                coll.RotationDeg = new float[] { rotDeg[0], rotDeg[1], rotDeg[2] };

            return coll;
        }

        private static bool TryParseCollisionFromJsonString(string? jsonText, out MiloCollisionExtras? collision)
        {
            collision = null;
            if (string.IsNullOrWhiteSpace(jsonText)) return false;
            try
            {
                using var doc = JsonDocument.Parse(jsonText);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    var parsed = ParseElement(doc.RootElement);
                    if (parsed.Enabled != 0)
                    {
                        collision = parsed;
                        return true;
                    }
                }
            }
            catch
            {
                // ignore malformed json string
            }
            return false;
        }

        public static bool TryExtractFromNode(Node? node, out MiloCollisionExtras? collision)
        {
            collision = null;
            if (node?.Extras == null) return false;

            try
            {
                string json = node.Extras.ToString();
                if (string.IsNullOrWhiteSpace(json) ||
                    (!json.Contains("milo_collision") && !json.Contains("milo_coll_")))
                    return false;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    return false;

                if (root.TryGetProperty("milo_collision", out var collEl))
                {
                    if (collEl.ValueKind == JsonValueKind.Object)
                    {
                        var parsed = ParseElement(collEl);
                        if (parsed.Enabled != 0)
                        {
                            // Also check flat rotation overrides if rotation arrays were stripped by Blender
                            if (MathF.Abs(parsed.ResolvedRotationRad.Rx) <= 1e-8f &&
                                MathF.Abs(parsed.ResolvedRotationRad.Ry) <= 1e-8f &&
                                MathF.Abs(parsed.ResolvedRotationRad.Rz) <= 1e-8f &&
                                (MiloAddonJsonHelpers.HasProperty(root, "milo_coll_rot_x") ||
                                 MiloAddonJsonHelpers.HasProperty(root, "milo_coll_rot_y") ||
                                 MiloAddonJsonHelpers.HasProperty(root, "milo_coll_rot_z")))
                            {
                                parsed.RotationRad = new float[]
                                {
                                    MiloAddonJsonHelpers.GetFloat(root, "milo_coll_rot_x", 0.0f),
                                    MiloAddonJsonHelpers.GetFloat(root, "milo_coll_rot_y", 0.0f),
                                    MiloAddonJsonHelpers.GetFloat(root, "milo_coll_rot_z", 0.0f),
                                };
                            }
                            collision = parsed;
                            return true;
                        }
                    }
                    else if (collEl.ValueKind == JsonValueKind.String &&
                             TryParseCollisionFromJsonString(collEl.GetString(), out collision))
                    {
                        return true;
                    }
                }

                if (root.TryGetProperty("milo_collision_json", out var collJsonEl) &&
                    collJsonEl.ValueKind == JsonValueKind.String &&
                    TryParseCollisionFromJsonString(collJsonEl.GetString(), out collision))
                {
                    return true;
                }

                if (MiloAddonJsonHelpers.GetInt(root, "milo_coll_enabled", 0) != 0)
                {
                    var flat = new MiloCollisionExtras
                    {
                        Enabled = 1,
                        Shape = MiloAddonJsonHelpers.GetString(root, "milo_coll_shape", "sphere"),
                        ShapeInt = MiloAddonJsonHelpers.HasProperty(root, "milo_coll_shape_int")
                            ? MiloAddonJsonHelpers.GetInt(root, "milo_coll_shape_int", 1)
                            : -1,
                        Radius0 = MiloAddonJsonHelpers.GetFloat(root, "milo_coll_radius0", 0.5f),
                        Radius1 = MiloAddonJsonHelpers.GetFloat(root, "milo_coll_radius1", 0.5f),
                        Length0 = MiloAddonJsonHelpers.GetFloat(root, "milo_coll_length0", 0.0f),
                        Length1 = MiloAddonJsonHelpers.GetFloat(root, "milo_coll_length1", 1.0f),
                        RotationRad = new float[]
                        {
                            MiloAddonJsonHelpers.GetFloat(root, "milo_coll_rot_x", 0.0f),
                            MiloAddonJsonHelpers.GetFloat(root, "milo_coll_rot_y", 0.0f),
                            MiloAddonJsonHelpers.GetFloat(root, "milo_coll_rot_z", 0.0f),
                        },
                        Flags = MiloAddonJsonHelpers.GetInt(root, "milo_coll_flags", 0),
                        MeshYBias = MiloAddonJsonHelpers.GetInt(root, "milo_coll_mesh_y_bias", 0),
                    };
                    collision = flat;
                    return true;
                }
            }
            catch
            {
                // ignore malformed extras
            }

            return false;
        }

        private static void PopulateCollisionMapFromElement(JsonElement mapEl, Dictionary<string, MiloCollisionExtras> result)
        {
            if (mapEl.ValueKind != JsonValueKind.Object) return;
            foreach (var prop in mapEl.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    var parsed = ParseElement(prop.Value);
                    if (parsed.Enabled != 0 && !string.IsNullOrWhiteSpace(prop.Name))
                    {
                        result[prop.Name] = parsed;
                    }
                }
                else if (prop.Value.ValueKind == JsonValueKind.String &&
                         !string.IsNullOrWhiteSpace(prop.Name) &&
                         TryParseCollisionFromJsonString(prop.Value.GetString(), out var parsed) &&
                         parsed != null)
                {
                    result[prop.Name] = parsed;
                }
            }
        }

        public static Dictionary<string, MiloCollisionExtras> ExtractCollisionMapFromNode(Node? node)
        {
            var result = new Dictionary<string, MiloCollisionExtras>(StringComparer.OrdinalIgnoreCase);
            if (node?.Extras == null) return result;

            try
            {
                string json = node.Extras.ToString();
                if (string.IsNullOrWhiteSpace(json) || !json.Contains("milo_collisions"))
                    return result;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    return result;

                if (root.TryGetProperty("milo_collisions_json", out var mapJsonEl) &&
                    mapJsonEl.ValueKind == JsonValueKind.String)
                {
                    string? rawMap = mapJsonEl.GetString();
                    if (!string.IsNullOrWhiteSpace(rawMap))
                    {
                        using var mapDoc = JsonDocument.Parse(rawMap);
                        PopulateCollisionMapFromElement(mapDoc.RootElement, result);
                    }
                }

                if (root.TryGetProperty("milo_collisions", out var mapEl))
                {
                    if (mapEl.ValueKind == JsonValueKind.Object)
                    {
                        PopulateCollisionMapFromElement(mapEl, result);
                    }
                    else if (mapEl.ValueKind == JsonValueKind.String)
                    {
                        string? rawMap = mapEl.GetString();
                        if (!string.IsNullOrWhiteSpace(rawMap))
                        {
                            using var mapDoc = JsonDocument.Parse(rawMap);
                            PopulateCollisionMapFromElement(mapDoc.RootElement, result);
                        }
                    }
                }
            }
            catch
            {
                // ignore malformed extras
            }

            return result;
        }
    }

    public class MiloHairPointExtras
    {
        public string Bone { get; set; } = string.Empty;
        public float[] Pos { get; set; } = new float[3];
        public float[] ResetPos { get; set; } = new float[3];
        public float Length { get; set; } = 0.0f;
        public float Radius { get; set; } = 0.5f;
        public float OuterRadius { get; set; } = -1.0f;
        public float SideLength { get; set; } = -1.0f;

        public static MiloHairPointExtras ParseElement(JsonElement el)
        {
            var pt = new MiloHairPointExtras
            {
                Bone = MiloAddonJsonHelpers.GetString(el, "bone", string.Empty),
                Length = MiloAddonJsonHelpers.GetFloat(el, "length", 0.0f),
                Radius = MiloAddonJsonHelpers.GetFloat(el, "radius", 0.5f),
                OuterRadius = MiloAddonJsonHelpers.GetFloat(el, "outer_radius", -1.0f),
                SideLength = MiloAddonJsonHelpers.GetFloat(el, "side_length", -1.0f),
            };

            var pos = MiloAddonJsonHelpers.GetFloatList(el, "pos");
            if (pos.Count >= 3)
                pt.Pos = new float[] { pos[0], pos[1], pos[2] };

            var resetPos = MiloAddonJsonHelpers.GetFloatList(el, "reset_pos");
            if (resetPos.Count >= 3)
                pt.ResetPos = new float[] { resetPos[0], resetPos[1], resetPos[2] };

            return pt;
        }
    }

    public class MiloHairStrandExtras
    {
        public string Name { get; set; } = string.Empty;
        public float Angle { get; set; } = 0.0f;
        public float CollisionRadius { get; set; } = 0.75f;
        public float OuterRadius { get; set; } = -1.0f;
        public int TaperRadius { get; set; } = 1;
        public int HookupFlags { get; set; } = 0;
        public int EffectiveHookupFlags { get; set; } = 0;
        public List<string> HookupFlagNames { get; set; } = new();
        public List<string> Bones { get; set; } = new();
        public List<float> BoneRadii { get; set; } = new();
        public List<float> BoneOuterRadii { get; set; } = new();
        public List<MiloHairPointExtras> Points { get; set; } = new();

        public int GetEffectiveHookupFlags(int fileHairFlags)
        {
            return EffectiveHookupFlags | HookupFlags | fileHairFlags | MiloAddonJsonHelpers.FlagsFromNames(HookupFlagNames);
        }

        public static MiloHairStrandExtras ParseElement(JsonElement el)
        {
            var st = new MiloHairStrandExtras
            {
                Name = MiloAddonJsonHelpers.GetString(el, "name", string.Empty),
                Angle = MiloAddonJsonHelpers.GetFloat(el, "angle", 0.0f),
                CollisionRadius = MiloAddonJsonHelpers.GetFloat(el, "collision_radius", 0.75f),
                OuterRadius = MiloAddonJsonHelpers.GetFloat(el, "outer_radius", -1.0f),
                TaperRadius = MiloAddonJsonHelpers.GetInt(el, "taper_radius", 1),
                HookupFlags = MiloAddonJsonHelpers.GetInt(el, "hookup_flags", 0),
                EffectiveHookupFlags = MiloAddonJsonHelpers.GetInt(el, "effective_hookup_flags", 0),
                HookupFlagNames = MiloAddonJsonHelpers.GetStringList(el, "hookup_flag_names"),
                Bones = MiloAddonJsonHelpers.GetStringList(el, "bones"),
                BoneRadii = MiloAddonJsonHelpers.GetFloatList(el, "bone_radii"),
                BoneOuterRadii = MiloAddonJsonHelpers.GetFloatList(el, "bone_outer_radii"),
            };

            if (el.ValueKind == JsonValueKind.Object &&
                el.TryGetProperty("points", out var ptsEl) &&
                ptsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var ptItem in ptsEl.EnumerateArray())
                {
                    if (ptItem.ValueKind == JsonValueKind.Object)
                    {
                        st.Points.Add(MiloHairPointExtras.ParseElement(ptItem));
                    }
                }
            }

            return st;
        }
    }

    public class MiloHairFileExtras
    {
        public string Name { get; set; } = "hair";
        public int Export { get; set; } = 1;
        public int HairFlags { get; set; } = 0;
        public List<string> HairFlagNames { get; set; } = new();
        public int Simulate { get; set; } = 1;
        public float Stiffness { get; set; } = 0.04f;
        public float Torsion { get; set; } = 0.1f;
        public float Inertia { get; set; } = 0.7f;
        public float Gravity { get; set; } = 1.0f;
        public float Weight { get; set; } = 0.5f;
        public float Friction { get; set; } = 0.3f;
        public float MinSlack { get; set; } = 0.0f;
        public float MaxSlack { get; set; } = 0.0f;
        public string Wind { get; set; } = CharHairExtras.DefaultWind;
        public List<MiloHairStrandExtras> Strands { get; set; } = new();

        public int ResolvedHairFlags => HairFlags | MiloAddonJsonHelpers.FlagsFromNames(HairFlagNames);

        public static MiloHairFileExtras ParseElement(JsonElement el)
        {
            var f = new MiloHairFileExtras
            {
                Name = MiloAddonJsonHelpers.GetString(el, "name", "hair"),
                Export = MiloAddonJsonHelpers.GetInt(el, "export", 1),
                HairFlags = MiloAddonJsonHelpers.GetInt(el, "hair_flags", 0),
                HairFlagNames = MiloAddonJsonHelpers.GetStringList(el, "hair_flag_names"),
                Simulate = MiloAddonJsonHelpers.GetInt(el, "simulate", 1),
                Stiffness = MiloAddonJsonHelpers.GetFloat(el, "stiffness", 0.04f),
                Torsion = MiloAddonJsonHelpers.GetFloat(el, "torsion", 0.1f),
                Inertia = MiloAddonJsonHelpers.GetFloat(el, "inertia", 0.7f),
                Gravity = MiloAddonJsonHelpers.GetFloat(el, "gravity", 1.0f),
                Weight = MiloAddonJsonHelpers.GetFloat(el, "weight", 0.5f),
                Friction = MiloAddonJsonHelpers.GetFloat(el, "friction", 0.3f),
                MinSlack = MiloAddonJsonHelpers.GetFloat(el, "min_slack", 0.0f),
                MaxSlack = MiloAddonJsonHelpers.GetFloat(el, "max_slack", 0.0f),
                Wind = MiloAddonJsonHelpers.GetString(el, "wind", CharHairExtras.DefaultWind),
            };

            if (el.ValueKind == JsonValueKind.Object &&
                el.TryGetProperty("strands", out var strandsEl) &&
                strandsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var stItem in strandsEl.EnumerateArray())
                {
                    if (stItem.ValueKind == JsonValueKind.Object)
                    {
                        f.Strands.Add(MiloHairStrandExtras.ParseElement(stItem));
                    }
                }
            }

            return f;
        }
    }

    public class MiloHairExtras
    {
        public int Enabled { get; set; } = 1;
        public int ShowHairCollisionRadius { get; set; } = 1;
        public int ShowHairOuterRadius { get; set; } = 0;
        public int AutoCalculateNodes { get; set; } = 1;
        public int SplitStrandsAtBranches { get; set; } = 1;
        public int OnlyWeightedBones { get; set; } = 1;
        public int UseCsharpPointCalc { get; set; } = 1;
        public List<MiloHairFileExtras> Files { get; set; } = new();

        public static MiloHairExtras ParseElement(JsonElement el)
        {
            var hair = new MiloHairExtras
            {
                Enabled = MiloAddonJsonHelpers.GetInt(el, "enabled", 1),
                ShowHairCollisionRadius = MiloAddonJsonHelpers.GetInt(el, "show_hair_collision_radius", 1),
                ShowHairOuterRadius = MiloAddonJsonHelpers.GetInt(el, "show_hair_outer_radius", 0),
                AutoCalculateNodes = MiloAddonJsonHelpers.GetInt(el, "auto_calculate_nodes", 1),
                SplitStrandsAtBranches = MiloAddonJsonHelpers.GetInt(el, "split_strands_at_branches", 1),
                OnlyWeightedBones = MiloAddonJsonHelpers.GetInt(el, "only_weighted_bones", 1),
                UseCsharpPointCalc = MiloAddonJsonHelpers.GetInt(el, "use_csharp_point_calc", 1),
            };

            if (el.ValueKind == JsonValueKind.Object &&
                el.TryGetProperty("files", out var filesEl) &&
                filesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var fileItem in filesEl.EnumerateArray())
                {
                    if (fileItem.ValueKind == JsonValueKind.Object)
                    {
                        hair.Files.Add(MiloHairFileExtras.ParseElement(fileItem));
                    }
                }
            }

            return hair;
        }

        public static bool TryExtractFromNode(Node? node, out MiloHairExtras? hairExtras)
        {
            hairExtras = null;
            if (node?.Extras == null) return false;

            try
            {
                string json = node.Extras.ToString();
                if (string.IsNullOrWhiteSpace(json) || !json.Contains("milo_hair"))
                    return false;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    return false;

                if (root.TryGetProperty("milo_hair_json", out var hairJsonEl) &&
                    hairJsonEl.ValueKind == JsonValueKind.String)
                {
                    string? rawHair = hairJsonEl.GetString();
                    if (!string.IsNullOrWhiteSpace(rawHair))
                    {
                        using var hairDoc = JsonDocument.Parse(rawHair);
                        if (hairDoc.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            var parsed = ParseElement(hairDoc.RootElement);
                            if (parsed.Enabled != 0)
                            {
                                hairExtras = parsed;
                                return true;
                            }
                        }
                    }
                }

                if (root.TryGetProperty("milo_hair", out var hairEl))
                {
                    if (hairEl.ValueKind == JsonValueKind.Object)
                    {
                        var parsed = ParseElement(hairEl);
                        if (parsed.Enabled != 0)
                        {
                            hairExtras = parsed;
                            return true;
                        }
                    }
                    else if (hairEl.ValueKind == JsonValueKind.String)
                    {
                        string? rawHair = hairEl.GetString();
                        if (!string.IsNullOrWhiteSpace(rawHair))
                        {
                            using var hairDoc = JsonDocument.Parse(rawHair);
                            if (hairDoc.RootElement.ValueKind == JsonValueKind.Object)
                            {
                                var parsed = ParseElement(hairDoc.RootElement);
                                if (parsed.Enabled != 0)
                                {
                                    hairExtras = parsed;
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // ignore malformed extras
            }

            return false;
        }
    }
}
