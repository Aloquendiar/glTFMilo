using glTFMilo.Source;
using MiloGLTFUtils.Source.Shared;
using MiloLib.Assets;
using MiloLib.Assets.Rnd;
using MiloLib.Classes;
using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MiloGLTFUtils.Source.glTFMilo.Core
{
    /// <summary>
    /// Processes nodes in the glTF and generates corresponding Rnd objects.
    /// </summary>
    public static class NodeProcessor
    {
        private static bool IsHairBoneNode(Node node, ModelRoot model)
        {
            return node != null &&
                NodeHelpers.IsBone(node, model) &&
                !string.IsNullOrEmpty(node.Name) &&
                node.Name.StartsWith("bone_hair_", StringComparison.OrdinalIgnoreCase);
        }
        // There's probably something much more elegant that could be done, like dynamic detection, but this was a favor, so I'm not going to do better.
        private static bool IsBreastBoneNode(Node node, ModelRoot model)
        {
            return node != null &&
                NodeHelpers.IsBone(node, model) &&
                !string.IsNullOrEmpty(node.Name) &&
                node.Name.StartsWith("bone_torso_breast", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSkirtBoneNode(Node node, ModelRoot model)
        {
            return node != null &&
                NodeHelpers.IsBone(node, model) &&
                !string.IsNullOrEmpty(node.Name) &&
                node.Name.StartsWith("bone_skirt", StringComparison.OrdinalIgnoreCase);
        }

        // detect non-hair bones under hair bones since this is a bone setup user error, we can still generate the hair but it will likely look or perform wrong so maybe this should be a hard stop
        private static void WarnAboutNonHairChildBones(Node node, ModelRoot model)
        {
            foreach (var child in node.VisualChildren)
            {
                if (NodeHelpers.IsBone(child, model) && !IsHairBoneNode(child, model))
                {
                    Logger.Warn($"Non-hair bone '{child.Name}' found under hair bone '{node.Name}'. It will not be included in CharHair strand generation.");
                }
            }
        }

        // detect non-Breast bones under Breast bones since this is a bone setup user error, we can still generate the hair but it will likely look or perform wrong so maybe this should be a hard stop
        private static void WarnAboutNonBreastChildBones(Node node, ModelRoot model)
        {
            foreach (var child in node.VisualChildren)
            {
                if (NodeHelpers.IsBone(child, model) && !IsBreastBoneNode(child, model))
                {
                    Logger.Warn($"Non-breast bone '{child.Name}' found under breast bone '{node.Name}'. It will not be included in CharHair strand generation.");
                }
            }
        }

        private static void WarnAboutNonSkirtChildBones(Node node, ModelRoot model)
        {
            foreach (var child in node.VisualChildren)
            {
                if (NodeHelpers.IsBone(child, model) && !IsSkirtBoneNode(child, model))
                {
                    Logger.Warn($"Non-skirt bone '{child.Name}' found under skirt bone '{node.Name}'. It will not be included in CharSkirt strand generation.");
                }
            }
        }
        
        // builds the list of hair chains as full root to leaf paths, duplicating shared bones across strands when the tree branches
        // this is just so i can a/b test a new branch splitter, which can be turned off with --disable-splitting and fallback to this behavior
        private static void CollectHairChains(Node node, ModelRoot model, HashSet<string> weightedHairBoneNames, List<Node> currentChain, List<List<Node>> chains)
        {
            currentChain.Add(node);

            WarnAboutNonHairChildBones(node, model);

            var hairChildren = node.VisualChildren
                .Where(child => IsHairBoneNode(child, model))
                .ToList();

            if (hairChildren.Count > 1)
            {
                Logger.Warn($"Hair bone '{node.Name}' branches into multiple hair chains and strand splitting is disabled. Bones above the branch will be simulated by multiple strands, which will likely behave incorrectly in-game.");
            }

            if (hairChildren.Count == 0)
            {
                if (currentChain.Any(chainNode => weightedHairBoneNames.Contains(chainNode.Name)))
                {
                    chains.Add(new List<Node>(currentChain));
                }
            }
            else
            {
                foreach (var hairChild in hairChildren)
                {
                    CollectHairChains(hairChild, model, weightedHairBoneNames, currentChain, chains);
                }
            }

            currentChain.RemoveAt(currentChain.Count - 1);
        }

        // builds the list of hair chains by walking the hair bone tree, ending a chain at every branch point so each bone becomes a point in exactly one strand
        // this implements how the decomp actually expects hair to be structured, so it seems to be the correct way to do this
        private static bool CollectHairChainsSplitAtBranches(Node node, ModelRoot model, HashSet<string> weightedHairBoneNames, bool ancestorWeighted, List<List<Node>> chains)
        {
            var segment = new List<Node>();
            var current = node;

            while (true)
            {
                segment.Add(current);

                WarnAboutNonHairChildBones(current, model);

                var hairChildren = current.VisualChildren
                    .Where(child => IsHairBoneNode(child, model))
                    .ToList();

                if (hairChildren.Count == 1)
                {
                    current = hairChildren[0];
                    continue;
                }

                // leaf or branch point, this segment ends here and every child starts its own segment
                bool segmentWeighted = segment.Any(segmentNode => weightedHairBoneNames.Contains(segmentNode.Name));
                var childChains = new List<List<Node>>();
                bool subtreeWeighted = false;

                foreach (var hairChild in hairChildren)
                {
                    subtreeWeighted |= CollectHairChainsSplitAtBranches(hairChild, model, weightedHairBoneNames, ancestorWeighted || segmentWeighted, childChains);
                }

                // emit the segment if a weighted bone exists anywhere on a root to leaf path through it
                if (segmentWeighted || ancestorWeighted || subtreeWeighted)
                {
                    chains.Add(segment);
                }

                chains.AddRange(childChains);
                return segmentWeighted || subtreeWeighted;
            }
        }

        private static void CollectBreastChains(Node node, ModelRoot model, HashSet<string> weightedBreastBoneNames, List<Node> currentChain, List<List<Node>> chains)
        {
            currentChain.Add(node);

            WarnAboutNonBreastChildBones(node, model);

            var breastChildren = node.VisualChildren
                .Where(child => IsBreastBoneNode(child, model))
                .ToList();

            if (breastChildren.Count > 1)
            {
                Logger.Warn($"Breast bone '{node.Name}' branches into multiple breast chains and strand splitting is disabled. Bones above the branch will be simulated by multiple strands, which will likely behave incorrectly in-game.");
            }

            if (breastChildren.Count == 0)
            {
                if (currentChain.Any(chainNode => weightedBreastBoneNames.Contains(chainNode.Name)))
                {
                    chains.Add(new List<Node>(currentChain));
                }
            }
            else
            {
                foreach (var breastChild in breastChildren)
                {
                    CollectBreastChains(breastChild, model, weightedBreastBoneNames, currentChain, chains);
                }
            }

            currentChain.RemoveAt(currentChain.Count - 1);
        }

        private static bool CollectBreastChainsSplitAtBranches(Node node, ModelRoot model, HashSet<string> weightedBreastBoneNames, bool ancestorWeighted, List<List<Node>> chains)
        {
            var segment = new List<Node>();
            var current = node;

            while (true)
            {
                segment.Add(current);

                WarnAboutNonBreastChildBones(current, model);

                var breastChildren = current.VisualChildren
                    .Where(child => IsBreastBoneNode(child, model))
                    .ToList();

                if (breastChildren.Count == 1)
                {
                    current = breastChildren[0];
                    continue;
                }

                // leaf or branch point, this segment ends here and every child starts its own segment
                bool segmentWeighted = segment.Any(segmentNode => weightedBreastBoneNames.Contains(segmentNode.Name));
                var childChains = new List<List<Node>>();
                bool subtreeWeighted = false;

                foreach (var breastChild in breastChildren)
                {
                    subtreeWeighted |= CollectBreastChainsSplitAtBranches(breastChild, model, weightedBreastBoneNames, ancestorWeighted || segmentWeighted, childChains);
                }

                // emit the segment if a weighted bone exists anywhere on a root to leaf path through it
                if (segmentWeighted || ancestorWeighted || subtreeWeighted)
                {
                    chains.Add(segment);
                }

                chains.AddRange(childChains);
                return segmentWeighted || subtreeWeighted;
            }
        }

        private static void CollectSkirtChains(Node node, ModelRoot model, HashSet<string> weightedSkirtBoneNames, List<Node> currentChain, List<List<Node>> chains)
        {
            currentChain.Add(node);

            WarnAboutNonSkirtChildBones(node, model);

            var skirtChildren = node.VisualChildren
                .Where(child => IsSkirtBoneNode(child, model))
                .ToList();

            if (skirtChildren.Count > 1)
            {
                Logger.Warn($"Skirt bone '{node.Name}' branches into multiple skirt chains and strand splitting is disabled. Bones above the branch will be simulated by multiple strands, which will likely behave incorrectly in-game.");
            }

            if (skirtChildren.Count == 0)
            {
                if (currentChain.Any(chainNode => weightedSkirtBoneNames.Contains(chainNode.Name)))
                {
                    chains.Add(new List<Node>(currentChain));
                }
            }
            else
            {
                foreach (var skirtChild in skirtChildren)
                {
                    CollectSkirtChains(skirtChild, model, weightedSkirtBoneNames, currentChain, chains);
                }
            }

            currentChain.RemoveAt(currentChain.Count - 1);
        }

        private static bool CollectSkirtChainsSplitAtBranches(Node node, ModelRoot model, HashSet<string> weightedSkirtBoneNames, bool ancestorWeighted, List<List<Node>> chains)
        {
            var segment = new List<Node>();
            var current = node;

            while (true)
            {
                segment.Add(current);

                WarnAboutNonSkirtChildBones(current, model);

                var skirtChildren = current.VisualChildren
                    .Where(child => IsSkirtBoneNode(child, model))
                    .ToList();

                if (skirtChildren.Count == 1)
                {
                    current = skirtChildren[0];
                    continue;
                }

                // leaf or branch point, this segment ends here and every child starts its own segment
                bool segmentWeighted = segment.Any(segmentNode => weightedSkirtBoneNames.Contains(segmentNode.Name));
                var childChains = new List<List<Node>>();
                bool subtreeWeighted = false;

                foreach (var skirtChild in skirtChildren)
                {
                    subtreeWeighted |= CollectSkirtChainsSplitAtBranches(skirtChild, model, weightedSkirtBoneNames, ancestorWeighted || segmentWeighted, childChains);
                }

                // emit the segment if a weighted bone exists anywhere on a root to leaf path through it
                if (segmentWeighted || ancestorWeighted || subtreeWeighted)
                {
                    chains.Add(segment);
                }

                chains.AddRange(childChains);
                return segmentWeighted || subtreeWeighted;
            }
        }

        // converts c# vector3 to milo
        private static MiloLib.Classes.Vector3 ToMiloVector3(System.Numerics.Vector3 value)
        {
            return new MiloLib.Classes.Vector3(value.X, value.Y, value.Z);
        }

        // gets a nodes world pos from its world matrix
        private static System.Numerics.Vector3 GetNodeWorldPos(Node node)
        {
            return new System.Numerics.Vector3(node.WorldMatrix.M41, node.WorldMatrix.M42, node.WorldMatrix.M43);
        }

        // gets the length of the hair point by looking at the next point in the chain, if it exists or just fallback
        private static float GetHairPointLength(IReadOnlyList<Node> chain, int pointIndex)
        {
            if (pointIndex < chain.Count - 1)
            {
                // world space distance so lengths stay consistent with the world space point positions, no matter how the bones are oriented or scaled
                return System.Numerics.Vector3.Distance(GetNodeWorldPos(chain[pointIndex]), GetNodeWorldPos(chain[pointIndex + 1]));
            }

            if (pointIndex > 0)
            {
                return GetHairPointLength(chain, pointIndex - 1);
            }

            // single point chain, so use the distance from the parent bone if there is one
            var parent = chain[pointIndex].VisualParent;
            if (parent != null)
            {
                float parentDistance = System.Numerics.Vector3.Distance(GetNodeWorldPos(parent), GetNodeWorldPos(chain[pointIndex]));
                if (parentDistance > 0.0f)
                {
                    return parentDistance;
                }
            }

            // if we cannot determine the length, use 5 :shrug:
            return 5.0f;
        }

        public static void ProcessBoneNode(Node node, ModelRoot model, DirectoryMeta meta, string type, string fallbackParent, bool convertCoordinates = false)
        {
            if (node.Name == "neutral_bone") return;

            if (type == "character" && BoneNames.rb3SkeletonBones.Contains(node.Name))
                return;

            var trans = RndTrans.New(9, 0);
            trans.objFields.SetObjectFieldsRevision(2);

            string parentName = NodeHelpers.GetParentBoneName(node, model) ?? fallbackParent;

            MatrixHelpers.CopyMatrix(node.LocalMatrix, trans.localXfm, convertCoordinates);
            MatrixHelpers.CopyMatrix(node.WorldMatrix, trans.worldXfm, convertCoordinates);
            trans.parentObj = parentName;

            meta.entries.Add(new DirectoryMeta.Entry("Trans", node.Name, trans));
        }

        public static void ProcessGroupNode(Node node, ModelRoot model, DirectoryMeta meta, MiloGame game, bool convertCoordinates = false)
        {
            if (node.Name == "Armature") return;

            string overriddenFilename = node.Name + ".grp";

            var rev = GameRevisions.GetRevision(game);

            var group = RndGroup.New(rev.GroupRevision, 0);
            group.objFields.SetObjectFieldsRevision(2);
            group.trans = RndTrans.New(rev.TransRevision, 0);
            MatrixHelpers.CopyMatrix(node.LocalMatrix, group.trans.localXfm, convertCoordinates);
            MatrixHelpers.CopyMatrix(node.WorldMatrix, group.trans.worldXfm, convertCoordinates);
            group.draw = RndDrawable.New(rev.DrawableRevision, 0);
            group.anim = RndAnimatable.New(rev.AnimatableRevision, 0);

            var children = NodeHelpers.GetAllDescendantNames(node);
            foreach (var child in children)
            {
                if (child != null) group.objects.Add(child);
            }

            // handle extras
            MiloExtras.AddToGroup(node, group, ref overriddenFilename);

            meta.entries.Add(new DirectoryMeta.Entry("Group", overriddenFilename, group));
        }

        public static void ProcessLightNode(Node node, DirectoryMeta meta, MiloGame game, bool convertCoordinates = false)
        {
            var rev = GameRevisions.GetRevision(game);

            string overriddenFilename = node.Name + ".lit";

            var light = new RndLight();
            typeof(RndLight).GetField("revision", BindingFlags.NonPublic | BindingFlags.Instance)
                            ?.SetValue(light, rev.LightRevision);

            light.objFields.SetObjectFieldsRevision(2);
            light.range = node.PunctualLight.Range;
            light.colorOwner = node.Name + ".lit";
            light.color = new HmxColor4(node.PunctualLight.Color.X, node.PunctualLight.Color.Y, node.PunctualLight.Color.Z, 1.0f);

            light.type = node.PunctualLight.LightType switch
            {
                PunctualLightType.Point => RndLight.Type.kPoint,
                PunctualLightType.Spot => RndLight.Type.kSpot,
                PunctualLightType.Directional => RndLight.Type.kDirectional,
                _ => RndLight.Type.kPoint
            };

            light.trans = RndTrans.New(rev.TransRevision, 0);
            MatrixHelpers.CopyMatrix(node.LocalMatrix, light.trans.localXfm, convertCoordinates);
            MatrixHelpers.CopyMatrix(node.WorldMatrix, light.trans.worldXfm, convertCoordinates);

            MiloExtras.AddToObject(node, light, ref overriddenFilename);

            meta.entries.Add(new DirectoryMeta.Entry("Light", overriddenFilename, light));
        }

        public static void ProcessCharHair(DirectoryMeta meta, MiloGame game, ModelRoot model, IEnumerable<string> weightedHairBoneNames, CharHairExtras physicsSettings, bool convertCoordinates = false, bool splitStrandsAtBranches = true)
        {
            var weightedHairBoneSet = new HashSet<string>(weightedHairBoneNames, StringComparer.OrdinalIgnoreCase);
            if (weightedHairBoneSet.Count == 0) return;

            var hair = new MiloLib.Assets.Char.CharHair();

            typeof(MiloLib.Assets.Char.CharHair)
                .GetField("revision", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(hair, (System.UInt16)11);

            hair.objFields.SetObjectFieldsRevision(2);

            hair.simulate = true;
            hair.stiffness = physicsSettings.Stiffness;
            hair.torsion = physicsSettings.Torsion;
            hair.inertia = physicsSettings.Inertia;
            hair.gravity = physicsSettings.Gravity;
            hair.weight = physicsSettings.Weight;
            hair.friction = physicsSettings.Friction;
            hair.wind = string.IsNullOrEmpty(physicsSettings.Wind) ? CharHairExtras.DefaultWind : physicsSettings.Wind;

            var rootNodes = new List<Node>();
            var seenRootNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var weightedHairNode in model.LogicalNodes.Where(node => !string.IsNullOrEmpty(node.Name) && weightedHairBoneSet.Contains(node.Name)))
            {
                var rootNode = weightedHairNode;
                while (true)
                {
                    var parent = NodeHelpers.GetParentNode(rootNode, model);
                    if (parent == null || !IsHairBoneNode(parent, model))
                    {
                        break;
                    }

                    rootNode = parent;
                }

                if (!seenRootNames.Add(rootNode.Name))
                {
                    continue;
                }

                rootNodes.Add(rootNode);
            }

            var chains = new List<List<Node>>();
            foreach (var rootNode in rootNodes)
            {
                if (splitStrandsAtBranches)
                {
                    CollectHairChainsSplitAtBranches(rootNode, model, weightedHairBoneSet, false, chains);
                }
                else
                {
                    CollectHairChains(rootNode, model, weightedHairBoneSet, new List<Node>(), chains);
                }
            }

            foreach (var chain in chains)
            {
                if (chain.Count == 0) continue;

                var strand = new MiloLib.Assets.Char.CharHair.Strand();
                strand.root = chain[0].Name;
                MatrixHelpers.CopyMatrix3(chain[0].LocalMatrix, strand.baseMat, convertCoordinates);
                MatrixHelpers.CopyMatrix3(chain[0].LocalMatrix, strand.rootMat, convertCoordinates);
                var strandRoot = chain[0];

                for (int pointIndex = 0; pointIndex < chain.Count; pointIndex++)
                {
                    var chainNode = chain[pointIndex];
                    var pointLength = GetHairPointLength(chain, pointIndex);
                    var currentPosition = GetNodeWorldPos(chainNode);
                    System.Numerics.Vector3 pointPosition;

                    if (pointIndex < chain.Count - 1)
                    {
                        pointPosition = GetNodeWorldPos(chain[pointIndex + 1]);
                    }
                    else
                    {
                        // no next bone at the tip, so point it along the node's local Y axis
                        var direction = new System.Numerics.Vector3(chainNode.WorldMatrix.M21, chainNode.WorldMatrix.M22, chainNode.WorldMatrix.M23);
                        if (direction.LengthSquared() <= float.Epsilon)
                        {
                            direction = System.Numerics.Vector3.UnitY;
                        }
                        else
                        {
                            direction = System.Numerics.Vector3.Normalize(direction);
                            if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y) || !float.IsFinite(direction.Z))
                            {
                                direction = System.Numerics.Vector3.UnitY;
                            }
                        }

                        pointPosition = currentPosition + (direction * pointLength);
                    }

                    // reset position is the world point transformed into the strand root parent's space
                    // i think this is right, works in testing at least :clueless:
                    var parent = NodeHelpers.GetParentNode(strandRoot, model);
                    var parentWorld = parent?.WorldMatrix ?? Matrix4x4.Identity;
                    var resetPosition = Matrix4x4.Invert(parentWorld, out var parentWorldInverse)? System.Numerics.Vector3.Transform(pointPosition, parentWorldInverse) : pointPosition;

                    float radius = 0.0f;
                    float outerRadius = 0.0f;
                    if (pointIndex < chain.Count - 1 && chain.Count > 1)
                    {
                        // taper the hair points as the chain goes down, to zero at the tip
                        // might need to revisit this tbqh, but this does actually work, it is just likely inaccurate for hair that *grows* from the root
                        float t = (float)pointIndex / (chain.Count - 1);
                        radius = MathF.Max(0.0f, 0.75f * (1.0f - (t * 0.5f)));
                        outerRadius = MathF.Max(0.0f, 2.0f * (1.0f - t));
                    }

                    // all the math above happens in gltf space, so only convert at the moment we write the values out
                    if (convertCoordinates)
                    {
                        pointPosition = MatrixHelpers.ConvertGltfVectorToMilo(pointPosition);
                        resetPosition = MatrixHelpers.ConvertGltfVectorToMilo(resetPosition);
                    }

                    var point = new MiloLib.Assets.Char.CharHair.Point();
                    point.bone = chainNode.Name;
                    point.pos = ToMiloVector3(pointPosition);
                    point.unk5c = ToMiloVector3(resetPosition);
                    point.sideLength = -1.0f;
                    point.radius = radius;
                    point.outerRadius = outerRadius;
                    point.length = pointLength;

                    strand.points.Add(point);
                }

                hair.strands.Add(strand);
            }

            if (hair.strands.Count == 0) return;

            string hairName = "hair.hair";
            meta.entries.Add(new DirectoryMeta.Entry("CharHair", hairName, hair));
        }

        public static void ProcessCharBreast(DirectoryMeta meta, MiloGame game, ModelRoot model, IEnumerable<string> weightedBreastBoneNames, CharHairBreastExtras physicsSettings, bool convertCoordinates = false, bool splitStrandsAtBranches = true)
        {
            var weightedBreastBoneSet = new HashSet<string>(weightedBreastBoneNames, StringComparer.OrdinalIgnoreCase);
            if (weightedBreastBoneSet.Count == 0) return;

            var breast = new MiloLib.Assets.Char.CharHair();

            typeof(MiloLib.Assets.Char.CharHair)
                .GetField("revision", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(breast, (System.UInt16)11);

            breast.objFields.SetObjectFieldsRevision(2);

            breast.simulate = true;
            breast.stiffness = physicsSettings.Stiffness;
            breast.torsion = physicsSettings.Torsion;
            breast.inertia = physicsSettings.Inertia;
            breast.gravity = physicsSettings.Gravity;
            breast.weight = physicsSettings.Weight;
            breast.friction = physicsSettings.Friction;
            breast.wind = string.IsNullOrEmpty(physicsSettings.Wind) ? CharHairBreastExtras.DefaultWind : physicsSettings.Wind;

            var rootNodes = new List<Node>();
            var seenRootNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var weightedBreastNode in model.LogicalNodes.Where(node => !string.IsNullOrEmpty(node.Name) && weightedBreastBoneSet.Contains(node.Name)))
            {
                var rootNode = weightedBreastNode;
                while (true)
                {
                    var parent = NodeHelpers.GetParentNode(rootNode, model);
                    if (parent == null || !IsBreastBoneNode(parent, model))
                    {
                        break;
                    }

                    rootNode = parent;
                }

                if (!seenRootNames.Add(rootNode.Name))
                {
                    continue;
                }

                rootNodes.Add(rootNode);
            }

            var chains = new List<List<Node>>();
            foreach (var rootNode in rootNodes)
            {
                if (splitStrandsAtBranches)
                {
                    CollectBreastChainsSplitAtBranches(rootNode, model, weightedBreastBoneSet, false, chains);
                }
                else
                {
                    CollectBreastChains(rootNode, model, weightedBreastBoneSet, new List<Node>(), chains);
                }
            }

            foreach (var chain in chains)
            {
                if (chain.Count == 0) continue;

                var strand = new MiloLib.Assets.Char.CharHair.Strand();
                strand.root = chain[0].Name;
                MatrixHelpers.CopyMatrix3(chain[0].LocalMatrix, strand.baseMat, convertCoordinates);
                MatrixHelpers.CopyMatrix3(chain[0].LocalMatrix, strand.rootMat, convertCoordinates);
                var strandRoot = chain[0];

                for (int pointIndex = 0; pointIndex < chain.Count; pointIndex++)
                {
                    var chainNode = chain[pointIndex];
                    var pointLength = GetHairPointLength(chain, pointIndex);
                    var currentPosition = GetNodeWorldPos(chainNode);
                    System.Numerics.Vector3 pointPosition;

                    if (pointIndex < chain.Count - 1)
                    {
                        pointPosition = GetNodeWorldPos(chain[pointIndex + 1]);
                    }
                    else
                    {
                        // no next bone at the tip, so point it along the node's local Y axis
                        var direction = new System.Numerics.Vector3(chainNode.WorldMatrix.M21, chainNode.WorldMatrix.M22, chainNode.WorldMatrix.M23);
                        if (direction.LengthSquared() <= float.Epsilon)
                        {
                            direction = System.Numerics.Vector3.UnitY;
                        }
                        else
                        {
                            direction = System.Numerics.Vector3.Normalize(direction);
                            if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y) || !float.IsFinite(direction.Z))
                            {
                                direction = System.Numerics.Vector3.UnitY;
                            }
                        }

                        pointPosition = currentPosition + (direction * pointLength);
                    }

                    // reset position is the world point transformed into the strand root parent's space
                    var parent = NodeHelpers.GetParentNode(strandRoot, model);
                    var parentWorld = parent?.WorldMatrix ?? Matrix4x4.Identity;
                    var resetPosition = Matrix4x4.Invert(parentWorld, out var parentWorldInverse) ? System.Numerics.Vector3.Transform(pointPosition, parentWorldInverse) : pointPosition;

                    float radius = 0.0f;
                    float outerRadius = 0.0f;
                    if (pointIndex < chain.Count - 1 && chain.Count > 1)
                    {
                        float t = (float)pointIndex / (chain.Count - 1);
                        radius = MathF.Max(0.0f, 0.75f * (1.0f - (t * 0.5f)));
                        outerRadius = MathF.Max(0.0f, 2.0f * (1.0f - t));
                    }

                    // all the math above happens in gltf space, so only convert at the moment we write the values out
                    if (convertCoordinates)
                    {
                        pointPosition = MatrixHelpers.ConvertGltfVectorToMilo(pointPosition);
                        resetPosition = MatrixHelpers.ConvertGltfVectorToMilo(resetPosition);
                    }

                    var point = new MiloLib.Assets.Char.CharHair.Point();
                    point.bone = chainNode.Name;
                    point.pos = ToMiloVector3(pointPosition);
                    point.unk5c = ToMiloVector3(resetPosition);
                    point.sideLength = -1.0f;
                    point.radius = radius;
                    point.outerRadius = outerRadius;
                    point.length = pointLength;

                    strand.points.Add(point);
                }

                breast.strands.Add(strand);
            }

            if (breast.strands.Count == 0) return;

            string breastName = "breast.hair";
            meta.entries.Add(new DirectoryMeta.Entry("CharHair", breastName, breast));
        }

        public static void ProcessCharSkirt(DirectoryMeta meta, MiloGame game, ModelRoot model, IEnumerable<string> weightedSkirtBoneNames, CharHairSkirtExtras physicsSettings, bool convertCoordinates = false, bool splitStrandsAtBranches = true)
        {
            var weightedSkirtBoneSet = new HashSet<string>(weightedSkirtBoneNames, StringComparer.OrdinalIgnoreCase);
            if (weightedSkirtBoneSet.Count == 0) return;

            var skirt = new MiloLib.Assets.Char.CharHair();

            typeof(MiloLib.Assets.Char.CharHair)
                .GetField("revision", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(skirt, (System.UInt16)11);

            skirt.objFields.SetObjectFieldsRevision(2);

            skirt.simulate = true;
            skirt.stiffness = physicsSettings.Stiffness;
            skirt.torsion = physicsSettings.Torsion;
            skirt.inertia = physicsSettings.Inertia;
            skirt.gravity = physicsSettings.Gravity;
            skirt.weight = physicsSettings.Weight;
            skirt.friction = physicsSettings.Friction;
            skirt.wind = string.IsNullOrEmpty(physicsSettings.Wind) ? CharHairSkirtExtras.DefaultWind : physicsSettings.Wind;

            var rootNodes = new List<Node>();
            var seenRootNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var weightedSkirtNode in model.LogicalNodes.Where(node => !string.IsNullOrEmpty(node.Name) && weightedSkirtBoneSet.Contains(node.Name)))
            {
                var rootNode = weightedSkirtNode;
                while (true)
                {
                    var parent = NodeHelpers.GetParentNode(rootNode, model);
                    if (parent == null || !IsSkirtBoneNode(parent, model))
                    {
                        break;
                    }

                    rootNode = parent;
                }

                if (!seenRootNames.Add(rootNode.Name))
                {
                    continue;
                }

                rootNodes.Add(rootNode);
            }

            var chains = new List<List<Node>>();
            foreach (var rootNode in rootNodes)
            {
                if (splitStrandsAtBranches)
                {
                    CollectSkirtChainsSplitAtBranches(rootNode, model, weightedSkirtBoneSet, false, chains);
                }
                else
                {
                    CollectSkirtChains(rootNode, model, weightedSkirtBoneSet, new List<Node>(), chains);
                }
            }

            foreach (var chain in chains)
            {
                if (chain.Count == 0) continue;

                var strand = new MiloLib.Assets.Char.CharHair.Strand();
                strand.root = chain[0].Name;
                MatrixHelpers.CopyMatrix3(chain[0].LocalMatrix, strand.baseMat, convertCoordinates);
                MatrixHelpers.CopyMatrix3(chain[0].LocalMatrix, strand.rootMat, convertCoordinates);
                var strandRoot = chain[0];

                for (int pointIndex = 0; pointIndex < chain.Count; pointIndex++)
                {
                    var chainNode = chain[pointIndex];
                    var pointLength = GetHairPointLength(chain, pointIndex);
                    var currentPosition = GetNodeWorldPos(chainNode);
                    System.Numerics.Vector3 pointPosition;

                    if (pointIndex < chain.Count - 1)
                    {
                        pointPosition = GetNodeWorldPos(chain[pointIndex + 1]);
                    }
                    else
                    {
                        // no next bone at the tip, so point it along the node's local Y axis
                        var direction = new System.Numerics.Vector3(chainNode.WorldMatrix.M21, chainNode.WorldMatrix.M22, chainNode.WorldMatrix.M23);
                        if (direction.LengthSquared() <= float.Epsilon)
                        {
                            direction = System.Numerics.Vector3.UnitY;
                        }
                        else
                        {
                            direction = System.Numerics.Vector3.Normalize(direction);
                            if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y) || !float.IsFinite(direction.Z))
                            {
                                direction = System.Numerics.Vector3.UnitY;
                            }
                        }

                        pointPosition = currentPosition + (direction * pointLength);
                    }

                    // reset position is the world point transformed into the strand root parent's space
                    var parent = NodeHelpers.GetParentNode(strandRoot, model);
                    var parentWorld = parent?.WorldMatrix ?? Matrix4x4.Identity;
                    var resetPosition = Matrix4x4.Invert(parentWorld, out var parentWorldInverse) ? System.Numerics.Vector3.Transform(pointPosition, parentWorldInverse) : pointPosition;

                    float radius = 0.0f;
                    float outerRadius = 0.0f;
                    if (pointIndex < chain.Count - 1 && chain.Count > 1)
                    {
                        float t = (float)pointIndex / (chain.Count - 1);
                        radius = MathF.Max(0.0f, 0.75f * (1.0f - (t * 0.5f)));
                        outerRadius = MathF.Max(0.0f, 2.0f * (1.0f - t));
                    }

                    // all the math above happens in gltf space, so only convert at the moment we write the values out
                    if (convertCoordinates)
                    {
                        pointPosition = MatrixHelpers.ConvertGltfVectorToMilo(pointPosition);
                        resetPosition = MatrixHelpers.ConvertGltfVectorToMilo(resetPosition);
                    }

                    var point = new MiloLib.Assets.Char.CharHair.Point();
                    point.bone = chainNode.Name;
                    point.pos = ToMiloVector3(pointPosition);
                    point.unk5c = ToMiloVector3(resetPosition);
                    point.sideLength = -1.0f;
                    point.radius = radius;
                    point.outerRadius = outerRadius;
                    point.length = pointLength;

                    strand.points.Add(point);
                }

                skirt.strands.Add(strand);
            }

            if (skirt.strands.Count == 0) return;

            string skirtName = "skirt.hair";
            meta.entries.Add(new DirectoryMeta.Entry("CharHair", skirtName, skirt));
        }

        public static void ProcessEmptyHairCollides(
            DirectoryMeta meta,
            MiloGame game,
            IEnumerable<(string MeshName, Matrix4x4 LocalMatrix, Matrix4x4 WorldMatrix)> hairMeshes,
            string parentName,
            bool convertCoordinates = false)
        {
            var rev = GameRevisions.GetRevision(game);
            var seenMeshes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var hairMesh in hairMeshes)
            {
                if (!seenMeshes.Add(hairMesh.MeshName))
                {
                    continue;
                }

                string collideName = hairMesh.MeshName.EndsWith(".mesh", StringComparison.OrdinalIgnoreCase)
                    ? hairMesh.MeshName[..^5] + ".coll"
                    : hairMesh.MeshName + ".coll";

                bool collideAlreadyExists = meta.entries.Any(entry =>
                    entry.type.value == "CharCollide" &&
                    string.Equals(entry.name.value, collideName, StringComparison.OrdinalIgnoreCase));

                if (collideAlreadyExists)
                {
                    continue;
                }

                // create a CharCollide for the hair even though it is empty, from looking at the decomp it seemed that there must be one or hair won't be sim, could be wrong
                // TODO: add actual collision support so eople can make basic collision meshes for their models to avoid clipping which is the next logical step for thisd
                var collide = new MiloLib.Assets.Char.CharCollide();

                // i really really need to implement some proper way to do stuff like this in milolib, using reflection to override private fields feels so bad even though it is technically fine
                typeof(MiloLib.Assets.Char.CharCollide).GetField("revision", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(collide, (ushort)7);

                collide.objFields.SetObjectFieldsRevision(2);
                collide.trans = RndTrans.New(rev.TransRevision, 0);
                MatrixHelpers.CopyMatrix(hairMesh.LocalMatrix, collide.trans.localXfm, convertCoordinates);
                MatrixHelpers.CopyMatrix(hairMesh.WorldMatrix, collide.trans.worldXfm, convertCoordinates);
                collide.trans.parentObj = parentName;
                collide.shape = MiloLib.Assets.Char.CharCollide.Shape.kSphere;
                collide.flags = 0;
                collide.mesh = hairMesh.MeshName;
                collide.meshYBias = false;

                collide.unkTransform.m11 = 1.0f;
                collide.unkTransform.m22 = 1.0f;
                collide.unkTransform.m33 = 1.0f;

                collide.structs.Clear();
                for (int i = 0; i < 8; i++)
                {
                    collide.structs.Add(new MiloLib.Assets.Char.CharCollide.CharCollideStruct());
                }

                meta.entries.Add(new DirectoryMeta.Entry("CharCollide", collideName, collide));
            }
        }

        public static void ProcessEmptyBreastCollides(
            DirectoryMeta meta,
            MiloGame game,
            IEnumerable<(string MeshName, Matrix4x4 LocalMatrix, Matrix4x4 WorldMatrix)> breastMeshes,
            string parentName,
            bool convertCoordinates = false)
        {
            var rev = GameRevisions.GetRevision(game);
            var seenMeshes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var breastMesh in breastMeshes)
            {
                if (!seenMeshes.Add(breastMesh.MeshName))
                {
                    continue;
                }

                string collideName = breastMesh.MeshName.EndsWith(".mesh", StringComparison.OrdinalIgnoreCase)
                    ? breastMesh.MeshName[..^5] + ".coll"
                    : breastMesh.MeshName + ".coll";

                bool collideAlreadyExists = meta.entries.Any(entry =>
                    entry.type.value == "CharCollide" &&
                    string.Equals(entry.name.value, collideName, StringComparison.OrdinalIgnoreCase));

                if (collideAlreadyExists)
                {
                    continue;
                }

                var collide = new MiloLib.Assets.Char.CharCollide();

                typeof(MiloLib.Assets.Char.CharCollide).GetField("revision", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(collide, (ushort)7);

                collide.objFields.SetObjectFieldsRevision(2);
                collide.trans = RndTrans.New(rev.TransRevision, 0);
                MatrixHelpers.CopyMatrix(breastMesh.LocalMatrix, collide.trans.localXfm, convertCoordinates);
                MatrixHelpers.CopyMatrix(breastMesh.WorldMatrix, collide.trans.worldXfm, convertCoordinates);
                collide.trans.parentObj = parentName;
                collide.shape = MiloLib.Assets.Char.CharCollide.Shape.kSphere;
                collide.flags = 0;
                collide.mesh = breastMesh.MeshName;
                collide.meshYBias = false;

                collide.unkTransform.m11 = 1.0f;
                collide.unkTransform.m22 = 1.0f;
                collide.unkTransform.m33 = 1.0f;

                collide.structs.Clear();
                for (int i = 0; i < 8; i++)
                {
                    collide.structs.Add(new MiloLib.Assets.Char.CharCollide.CharCollideStruct());
                }

                meta.entries.Add(new DirectoryMeta.Entry("CharCollide", collideName, collide));
            }
        }

        public static void ProcessEmptySkirtCollides(
            DirectoryMeta meta,
            MiloGame game,
            IEnumerable<(string MeshName, Matrix4x4 LocalMatrix, Matrix4x4 WorldMatrix)> skirtMeshes,
            string parentName,
            bool convertCoordinates = false)
        {
            var rev = GameRevisions.GetRevision(game);
            var seenMeshes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var skirtMesh in skirtMeshes)
            {
                if (!seenMeshes.Add(skirtMesh.MeshName))
                {
                    continue;
                }

                string collideName = skirtMesh.MeshName.EndsWith(".mesh", StringComparison.OrdinalIgnoreCase)
                    ? skirtMesh.MeshName[..^5] + ".coll"
                    : skirtMesh.MeshName + ".coll";

                bool collideAlreadyExists = meta.entries.Any(entry =>
                    entry.type.value == "CharCollide" &&
                    string.Equals(entry.name.value, collideName, StringComparison.OrdinalIgnoreCase));

                if (collideAlreadyExists)
                {
                    continue;
                }

                var collide = new MiloLib.Assets.Char.CharCollide();

                typeof(MiloLib.Assets.Char.CharCollide).GetField("revision", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(collide, (ushort)7);

                collide.objFields.SetObjectFieldsRevision(2);
                collide.trans = RndTrans.New(rev.TransRevision, 0);
                MatrixHelpers.CopyMatrix(skirtMesh.LocalMatrix, collide.trans.localXfm, convertCoordinates);
                MatrixHelpers.CopyMatrix(skirtMesh.WorldMatrix, collide.trans.worldXfm, convertCoordinates);
                collide.trans.parentObj = parentName;
                collide.shape = MiloLib.Assets.Char.CharCollide.Shape.kSphere;
                collide.flags = 0;
                collide.mesh = skirtMesh.MeshName;
                collide.meshYBias = false;

                collide.unkTransform.m11 = 1.0f;
                collide.unkTransform.m22 = 1.0f;
                collide.unkTransform.m33 = 1.0f;

                collide.structs.Clear();
                for (int i = 0; i < 8; i++)
                {
                    collide.structs.Add(new MiloLib.Assets.Char.CharCollide.CharCollideStruct());
                }

                meta.entries.Add(new DirectoryMeta.Entry("CharCollide", collideName, collide));
            }
        }

        private static void ApplyCollideFlags(MiloLib.Assets.Char.CharCollide collide, int flags)
        {
            var options = (MiloLib.Assets.Char.CharCollide.CollideFlagOptions)flags;
            collide.isBack = options.HasFlag(MiloLib.Assets.Char.CharCollide.CollideFlagOptions.HairBack);
            collide.isChest = options.HasFlag(MiloLib.Assets.Char.CharCollide.CollideFlagOptions.HairChest);
            collide.isForehead = options.HasFlag(MiloLib.Assets.Char.CharCollide.CollideFlagOptions.HairForehead);
            collide.isHead = options.HasFlag(MiloLib.Assets.Char.CharCollide.CollideFlagOptions.HairHead);
            collide.isLeftShoulder = options.HasFlag(MiloLib.Assets.Char.CharCollide.CollideFlagOptions.HairLeftShoulder);
            collide.isRightShoulder = options.HasFlag(MiloLib.Assets.Char.CharCollide.CollideFlagOptions.HairRightShoulder);
            collide.isFace = options.HasFlag(MiloLib.Assets.Char.CharCollide.CollideFlagOptions.HairFace);
            collide.isNeck = options.HasFlag(MiloLib.Assets.Char.CharCollide.CollideFlagOptions.HairNeck);
            collide.isLeftThigh = options.HasFlag(MiloLib.Assets.Char.CharCollide.CollideFlagOptions.HairLeftThigh);
            collide.isRightThigh = options.HasFlag(MiloLib.Assets.Char.CharCollide.CollideFlagOptions.HairRightThigh);
            collide.isLeftHip = options.HasFlag(MiloLib.Assets.Char.CharCollide.CollideFlagOptions.HairLeftHip);
            collide.isRightHip = options.HasFlag(MiloLib.Assets.Char.CharCollide.CollideFlagOptions.HairRightHip);
            collide.flags = flags;
        }

        private static void ApplyStrandHookupFlags(MiloLib.Assets.Char.CharHair.Strand strand, int flags)
        {
            var options = (MiloLib.Assets.Char.CharHair.Strand.HookupFlagOptions)flags;
            strand.hairBack = options.HasFlag(MiloLib.Assets.Char.CharHair.Strand.HookupFlagOptions.HairBack);
            strand.hairChest = options.HasFlag(MiloLib.Assets.Char.CharHair.Strand.HookupFlagOptions.HairChest);
            strand.hairForehead = options.HasFlag(MiloLib.Assets.Char.CharHair.Strand.HookupFlagOptions.HairForehead);
            strand.hairHead = options.HasFlag(MiloLib.Assets.Char.CharHair.Strand.HookupFlagOptions.HairHead);
            strand.hairLeftShoulder = options.HasFlag(MiloLib.Assets.Char.CharHair.Strand.HookupFlagOptions.HairLeftShoulder);
            strand.hairRightShoulder = options.HasFlag(MiloLib.Assets.Char.CharHair.Strand.HookupFlagOptions.HairRightShoulder);
            strand.hairFace = options.HasFlag(MiloLib.Assets.Char.CharHair.Strand.HookupFlagOptions.HairFace);
            strand.hairNeck = options.HasFlag(MiloLib.Assets.Char.CharHair.Strand.HookupFlagOptions.HairNeck);
            strand.hairLeftThigh = options.HasFlag(MiloLib.Assets.Char.CharHair.Strand.HookupFlagOptions.HairLeftThigh);
            strand.hairRightThigh = options.HasFlag(MiloLib.Assets.Char.CharHair.Strand.HookupFlagOptions.HairRightThigh);
            strand.hairLeftHip = options.HasFlag(MiloLib.Assets.Char.CharHair.Strand.HookupFlagOptions.HairLeftHip);
            strand.hairRightHip = options.HasFlag(MiloLib.Assets.Char.CharHair.Strand.HookupFlagOptions.HairRightHip);

            typeof(MiloLib.Assets.Char.CharHair.Strand)
                .GetField("hookupFlags", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(strand, flags);
        }

        public static void EnsureBoneTransEntry(
            Node node,
            ModelRoot model,
            DirectoryMeta meta,
            string type,
            string fallbackParent,
            bool convertCoordinates = false)
        {
            if (node == null || string.IsNullOrEmpty(node.Name)) return;
            if (node.Name == "neutral_bone" || node.Name == "Armature") return;
            if (type == "character" && BoneNames.rb3SkeletonBones.Contains(node.Name)) return;

            bool transExists = meta.entries.Any(entry =>
                entry.type.value == "Trans" &&
                string.Equals(entry.name.value, node.Name, StringComparison.OrdinalIgnoreCase));

            if (transExists) return;

            var trans = RndTrans.New(9, 0);
            trans.objFields.SetObjectFieldsRevision(2);

            var parentNode = NodeHelpers.GetParentNode(node, model);
            string parentName = (parentNode != null && !string.IsNullOrEmpty(parentNode.Name) && parentNode.Name != "Armature")
                ? parentNode.Name
                : fallbackParent;

            MatrixHelpers.CopyMatrix(node.LocalMatrix, trans.localXfm, convertCoordinates);
            MatrixHelpers.CopyMatrix(node.WorldMatrix, trans.worldXfm, convertCoordinates);
            trans.parentObj = parentName;

            meta.entries.Add(new DirectoryMeta.Entry("Trans", node.Name, trans));
        }

        public static bool ProcessCustomBoneCollide(
            Node node,
            ModelRoot model,
            DirectoryMeta meta,
            MiloGame game,
            MiloCollisionExtras collExtras,
            string type,
            string fallbackParent,
            bool convertCoordinates = false)
        {
            if (node == null || string.IsNullOrEmpty(node.Name) || collExtras == null || collExtras.Enabled == 0)
            {
                return false;
            }

            EnsureBoneTransEntry(node, model, meta, type, fallbackParent, convertCoordinates);

            string collideName = node.Name.EndsWith(".coll", StringComparison.OrdinalIgnoreCase)
                ? node.Name
                : $"{node.Name}.coll";

            bool collideAlreadyExists = meta.entries.Any(entry =>
                entry.type.value == "CharCollide" &&
                string.Equals(entry.name.value, collideName, StringComparison.OrdinalIgnoreCase));

            if (collideAlreadyExists)
            {
                return false;
            }

            var rev = GameRevisions.GetRevision(game);
            var collide = new MiloLib.Assets.Char.CharCollide();

            ushort collideRevision = game == MiloGame.TheBeatlesRockBand ? (ushort)5 : (ushort)7;
            typeof(MiloLib.Assets.Char.CharCollide)
                .GetField("revision", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(collide, collideRevision);

            collide.objFields.SetObjectFieldsRevision(2);
            collide.trans = RndTrans.New(rev.TransRevision, 0);

            var (rx, ry, rz) = collExtras.ResolvedRotationRad;
            if (MathF.Abs(rx) > 1e-8f || MathF.Abs(ry) > 1e-8f || MathF.Abs(rz) > 1e-8f)
            {
                // Euler XYZ rotation in local bone space (Milo row-vector convention matches CreateRotationX * CreateRotationY * CreateRotationZ)
                Matrix4x4 localRotGltf = Matrix4x4.CreateRotationX(rx) * Matrix4x4.CreateRotationY(ry) * Matrix4x4.CreateRotationZ(rz);
                Matrix4x4 rotatedWorldGltf = localRotGltf * node.WorldMatrix;
                MatrixHelpers.CopyMatrix(localRotGltf, collide.trans.localXfm, convertCoordinates);
                MatrixHelpers.CopyMatrix(rotatedWorldGltf, collide.trans.worldXfm, convertCoordinates);
                Logger.Info($"Created CharCollide '{collideName}' (parent: {node.Name}) with rotation rad=({rx:0.###}, {ry:0.###}, {rz:0.###}).");
            }
            else
            {
                MatrixHelpers.CopyMatrix(Matrix4x4.Identity, collide.trans.localXfm, convertGltfToMilo: false);
                MatrixHelpers.CopyMatrix(node.WorldMatrix, collide.trans.worldXfm, convertCoordinates);
            }

            collide.trans.parentObj = node.Name;

            int shapeInt = Math.Clamp(collExtras.ResolvedShapeInt, 0, 4);
            collide.shape = (MiloLib.Assets.Char.CharCollide.Shape)shapeInt;

            float l0 = MathF.Min(collExtras.Length0, collExtras.Length1);
            float l1 = MathF.Max(collExtras.Length0, collExtras.Length1);

            collide.origRadius[0] = collExtras.Radius0;
            collide.curRadius[0] = collExtras.Radius0;
            collide.origRadius[1] = collExtras.Radius1;
            collide.curRadius[1] = collExtras.Radius1;
            collide.origLength[0] = l0;
            collide.curLength[0] = l0;
            collide.origLength[1] = l1;
            collide.curLength[1] = l1;

            ApplyCollideFlags(collide, collExtras.ResolvedFlags);
            collide.mesh = string.Empty;
            collide.meshYBias = collExtras.MeshYBias != 0;

            collide.unkTransform.m11 = 1.0f;
            collide.unkTransform.m22 = 1.0f;
            collide.unkTransform.m33 = 1.0f;

            collide.structs.Clear();
            for (int i = 0; i < 8; i++)
            {
                collide.structs.Add(new MiloLib.Assets.Char.CharCollide.CharCollideStruct());
            }

            meta.entries.Add(new DirectoryMeta.Entry("CharCollide", collideName, collide));
            return true;
        }

        private static List<List<Node>> DetectAutoChainsByPrefix(
            ModelRoot model,
            string prefix,
            HashSet<string> weightedBoneSet,
            bool onlyWeighted,
            bool splitStrandsAtBranches)
        {
            var matchingNodes = model.LogicalNodes
                .Where(n => n != null && !string.IsNullOrEmpty(n.Name) && n.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matchingNodes.Count == 0)
            {
                return new List<List<Node>>();
            }

            HashSet<string> activeSet;
            if (onlyWeighted && weightedBoneSet.Count > 0)
            {
                activeSet = new HashSet<string>(
                    matchingNodes.Where(n => weightedBoneSet.Contains(n.Name)).Select(n => n.Name),
                    StringComparer.OrdinalIgnoreCase);
                if (activeSet.Count == 0)
                {
                    activeSet = new HashSet<string>(matchingNodes.Select(n => n.Name), StringComparer.OrdinalIgnoreCase);
                }
            }
            else
            {
                activeSet = new HashSet<string>(matchingNodes.Select(n => n.Name), StringComparer.OrdinalIgnoreCase);
            }

            var rootNodes = new List<Node>();
            var seenRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var node in matchingNodes.Where(n => activeSet.Contains(n.Name)))
            {
                var root = node;
                while (true)
                {
                    var parent = NodeHelpers.GetParentNode(root, model);
                    if (parent == null || string.IsNullOrEmpty(parent.Name) || !parent.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                    root = parent;
                }

                if (seenRoots.Add(root.Name))
                {
                    rootNodes.Add(root);
                }
            }

            var chains = new List<List<Node>>();

            void CollectFull(Node current, List<Node> currentChain)
            {
                currentChain.Add(current);
                var children = current.VisualChildren
                    .Where(c => c != null && !string.IsNullOrEmpty(c.Name) && c.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (children.Count == 0)
                {
                    if (currentChain.Any(cn => activeSet.Contains(cn.Name)))
                    {
                        chains.Add(new List<Node>(currentChain));
                    }
                }
                else
                {
                    foreach (var child in children)
                    {
                        CollectFull(child, currentChain);
                    }
                }
                currentChain.RemoveAt(currentChain.Count - 1);
            }

            bool CollectSplit(Node start, bool ancestorWeighted, List<List<Node>> outChains)
            {
                var segment = new List<Node>();
                var cur = start;
                while (true)
                {
                    segment.Add(cur);
                    var children = cur.VisualChildren
                        .Where(c => c != null && !string.IsNullOrEmpty(c.Name) && c.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (children.Count == 1)
                    {
                        cur = children[0];
                        continue;
                    }

                    bool segWeighted = segment.Any(sn => activeSet.Contains(sn.Name));
                    var childChains = new List<List<Node>>();
                    bool subWeighted = false;
                    foreach (var child in children)
                    {
                        subWeighted |= CollectSplit(child, ancestorWeighted || segWeighted, childChains);
                    }

                    if (segWeighted || ancestorWeighted || subWeighted)
                    {
                        outChains.Add(segment);
                    }
                    outChains.AddRange(childChains);
                    return segWeighted || subWeighted;
                }
            }

            foreach (var root in rootNodes)
            {
                if (splitStrandsAtBranches)
                {
                    CollectSplit(root, false, chains);
                }
                else
                {
                    CollectFull(root, new List<Node>());
                }
            }

            return chains;
        }

        public static bool ProcessCustomCharHair(
            DirectoryMeta meta,
            MiloGame game,
            ModelRoot model,
            MiloHairExtras hairExtras,
            string defaultRootName,
            string type,
            IEnumerable<string> weightedBoneNames,
            bool convertCoordinates = false,
            bool cliAllowSplitting = true)
        {
            if (hairExtras == null || hairExtras.Enabled == 0)
            {
                return false;
            }

            var weightedBoneSet = new HashSet<string>(weightedBoneNames ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var nodeByName = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in model.LogicalNodes)
            {
                if (n != null && !string.IsNullOrEmpty(n.Name) && !nodeByName.ContainsKey(n.Name))
                {
                    nodeByName[n.Name] = n;
                }
            }

            bool useCsharpPointCalc = hairExtras.UseCsharpPointCalc != 0;
            bool splitAtBranches = (hairExtras.SplitStrandsAtBranches != 0) && cliAllowSplitting;
            bool onlyWeighted = hairExtras.OnlyWeightedBones != 0;

            var filesToProcess = hairExtras.Files.Where(f => f.Export != 0).ToList();
            if (filesToProcess.Count == 0 && hairExtras.AutoCalculateNodes != 0 && hairExtras.Files.Count == 0)
            {
                filesToProcess.Add(new MiloHairFileExtras { Name = "hair", Stiffness = 0.04f, Torsion = 0.1f, Inertia = 0.7f, Gravity = 1.0f, Weight = 0.5f, Friction = 0.3f });
                filesToProcess.Add(new MiloHairFileExtras { Name = "breast", Stiffness = 0.225f, Torsion = 0.75f, Inertia = 0.5f, Gravity = 1.0f, Weight = 1.0f, Friction = 0.0f });
                filesToProcess.Add(new MiloHairFileExtras { Name = "skirt", Stiffness = 0.225f, Torsion = 0.75f, Inertia = 0.5f, Gravity = 1.0f, Weight = 1.0f, Friction = 0.0f });
            }

            var usedEntryNames = new HashSet<string>(
                meta.entries.Where(e => e.type.value == "CharHair").Select(e => e.name.value),
                StringComparer.OrdinalIgnoreCase);

            int unnamedCount = 0;
            int createdCount = 0;

            foreach (var fileCfg in filesToProcess)
            {
                string rawFileName = (fileCfg.Name ?? string.Empty).Trim();
                string baseName;
                if (!string.IsNullOrEmpty(rawFileName))
                {
                    baseName = rawFileName.EndsWith(".hair", StringComparison.OrdinalIgnoreCase)
                        ? rawFileName[..^5]
                        : rawFileName;
                }
                else
                {
                    unnamedCount++;
                    string cleanRoot = defaultRootName.EndsWith(".hair", StringComparison.OrdinalIgnoreCase)
                        ? defaultRootName[..^5]
                        : defaultRootName;
                    baseName = unnamedCount == 1 ? cleanRoot : $"{cleanRoot}_{unnamedCount}";
                }

                string entryName = $"{baseName}.hair";
                int suffix = 2;
                while (usedEntryNames.Contains(entryName))
                {
                    entryName = $"{baseName}_{suffix}.hair";
                    suffix++;
                }

                var hair = new MiloLib.Assets.Char.CharHair();
                typeof(MiloLib.Assets.Char.CharHair)
                    .GetField("revision", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(hair, (ushort)11);

                hair.objFields.SetObjectFieldsRevision(2);
                hair.simulate = fileCfg.Simulate != 0;
                hair.stiffness = fileCfg.Stiffness;
                hair.torsion = fileCfg.Torsion;
                hair.inertia = fileCfg.Inertia;
                hair.gravity = fileCfg.Gravity;
                hair.weight = fileCfg.Weight;
                hair.friction = fileCfg.Friction;
                hair.minSlack = fileCfg.MinSlack;
                hair.maxSlack = fileCfg.MaxSlack;
                hair.wind = string.IsNullOrWhiteSpace(fileCfg.Wind) ? CharHairExtras.DefaultWind : fileCfg.Wind;

                int fileHairFlags = fileCfg.ResolvedHairFlags;

                if (fileCfg.Strands.Count > 0)
                {
                    foreach (var strandCfg in fileCfg.Strands)
                    {
                        var chain = new List<(Node Node, int RawIndex)>();
                        for (int bIdx = 0; bIdx < strandCfg.Bones.Count; bIdx++)
                        {
                            string bName = strandCfg.Bones[bIdx];
                            if (!string.IsNullOrWhiteSpace(bName) && nodeByName.TryGetValue(bName.Trim(), out var bNode))
                            {
                                chain.Add((bNode, bIdx));
                            }
                        }

                        if (chain.Count == 0) continue;

                        var chainNodes = chain.Select(c => c.Node).ToList();
                        foreach (var cn in chainNodes)
                        {
                            EnsureBoneTransEntry(cn, model, meta, type, defaultRootName, convertCoordinates);
                        }

                        var strand = new MiloLib.Assets.Char.CharHair.Strand();
                        strand.root = chainNodes[0].Name;
                        strand.angle = strandCfg.Angle;
                        ApplyStrandHookupFlags(strand, strandCfg.GetEffectiveHookupFlags(fileHairFlags));
                        MatrixHelpers.CopyMatrix3(chainNodes[0].LocalMatrix, strand.baseMat, convertCoordinates);
                        MatrixHelpers.CopyMatrix3(chainNodes[0].LocalMatrix, strand.rootMat, convertCoordinates);

                        var strandRoot = chainNodes[0];
                        var parent = NodeHelpers.GetParentNode(strandRoot, model);
                        var parentWorld = parent?.WorldMatrix ?? Matrix4x4.Identity;
                        bool hasParentInv = Matrix4x4.Invert(parentWorld, out var parentWorldInverse);

                        for (int pointIndex = 0; pointIndex < chainNodes.Count; pointIndex++)
                        {
                            var chainNode = chainNodes[pointIndex];
                            int rawBoneIdx = chain[pointIndex].RawIndex;
                            float pointLength = GetHairPointLength(chainNodes, pointIndex);
                            var currentPosition = GetNodeWorldPos(chainNode);
                            System.Numerics.Vector3 pointPosition;
                            System.Numerics.Vector3 resetPosition;

                            if (useCsharpPointCalc)
                            {
                                if (pointIndex < chainNodes.Count - 1)
                                {
                                    pointPosition = GetNodeWorldPos(chainNodes[pointIndex + 1]);
                                }
                                else
                                {
                                    var direction = new System.Numerics.Vector3(chainNode.WorldMatrix.M21, chainNode.WorldMatrix.M22, chainNode.WorldMatrix.M23);
                                    if (direction.LengthSquared() <= float.Epsilon)
                                    {
                                        direction = System.Numerics.Vector3.UnitY;
                                    }
                                    else
                                    {
                                        direction = System.Numerics.Vector3.Normalize(direction);
                                        if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y) || !float.IsFinite(direction.Z))
                                        {
                                            direction = System.Numerics.Vector3.UnitY;
                                        }
                                    }
                                    pointPosition = currentPosition + (direction * pointLength);
                                }

                                resetPosition = hasParentInv
                                    ? System.Numerics.Vector3.Transform(pointPosition, parentWorldInverse)
                                    : pointPosition;
                            }
                            else
                            {
                                pointPosition = currentPosition;
                                resetPosition = System.Numerics.Vector3.Zero;
                            }

                            if (convertCoordinates)
                            {
                                pointPosition = MatrixHelpers.ConvertGltfVectorToMilo(pointPosition);
                                if (useCsharpPointCalc)
                                {
                                    resetPosition = MatrixHelpers.ConvertGltfVectorToMilo(resetPosition);
                                }
                            }

                            MiloHairPointExtras? ptExtra = null;
                            if (rawBoneIdx >= 0 && rawBoneIdx < strandCfg.Points.Count &&
                                string.Equals(strandCfg.Points[rawBoneIdx].Bone, chainNode.Name, StringComparison.OrdinalIgnoreCase))
                            {
                                ptExtra = strandCfg.Points[rawBoneIdx];
                            }
                            else
                            {
                                ptExtra = strandCfg.Points.FirstOrDefault(p => string.Equals(p.Bone, chainNode.Name, StringComparison.OrdinalIgnoreCase));
                            }

                            float radius;
                            if (rawBoneIdx >= 0 && rawBoneIdx < strandCfg.BoneRadii.Count)
                            {
                                radius = MathF.Max(0.0f, strandCfg.BoneRadii[rawBoneIdx]);
                            }
                            else if (ptExtra != null)
                            {
                                radius = MathF.Max(0.0f, ptExtra.Radius);
                            }
                            else if (strandCfg.TaperRadius != 0 && chainNodes.Count > 1)
                            {
                                float t = (float)pointIndex / (chainNodes.Count - 1);
                                radius = MathF.Max(0.0f, strandCfg.CollisionRadius * (1.0f - (t * 0.5f)));
                            }
                            else
                            {
                                radius = MathF.Max(0.0f, strandCfg.CollisionRadius);
                            }

                            float outerRadius;
                            if (rawBoneIdx >= 0 && rawBoneIdx < strandCfg.BoneOuterRadii.Count)
                            {
                                outerRadius = strandCfg.BoneOuterRadii[rawBoneIdx];
                            }
                            else if (ptExtra != null)
                            {
                                outerRadius = ptExtra.OuterRadius;
                            }
                            else if (strandCfg.OuterRadius >= 0.0f && strandCfg.TaperRadius != 0 && chainNodes.Count > 1)
                            {
                                float t = (float)pointIndex / (chainNodes.Count - 1);
                                outerRadius = MathF.Max(0.0f, strandCfg.OuterRadius * (1.0f - t));
                            }
                            else
                            {
                                outerRadius = strandCfg.OuterRadius;
                            }

                            outerRadius = MathF.Max(0.0f, outerRadius);

                            var point = new MiloLib.Assets.Char.CharHair.Point
                            {
                                bone = chainNode.Name,
                                pos = ToMiloVector3(pointPosition),
                                unk5c = ToMiloVector3(resetPosition),
                                sideLength = ptExtra?.SideLength ?? -1.0f,
                                radius = radius,
                                outerRadius = outerRadius,
                                length = pointLength,
                            };

                            strand.points.Add(point);
                        }

                        hair.strands.Add(strand);
                    }
                }
                else if (hairExtras.AutoCalculateNodes != 0)
                {
                    string prefix = baseName.ToLowerInvariant() switch
                    {
                        "breast" => "bone_torso_breast",
                        "skirt" => "bone_skirt",
                        _ => "bone_hair_",
                    };
                    int defaultFlags = baseName.ToLowerInvariant() switch
                    {
                        "breast" => 2,      // kHairChest
                        "skirt" => 3840,    // kHairLeftThigh | kHairRightThigh | kHairLeftHip | kHairRigh2hip
                        _ => 0,
                    };

                    var autoChains = DetectAutoChainsByPrefix(model, prefix, weightedBoneSet, onlyWeighted, splitAtBranches);
                    foreach (var chainNodes in autoChains)
                    {
                        if (chainNodes.Count == 0) continue;
                        foreach (var cn in chainNodes)
                        {
                            EnsureBoneTransEntry(cn, model, meta, type, defaultRootName, convertCoordinates);
                        }

                        var strand = new MiloLib.Assets.Char.CharHair.Strand();
                        strand.root = chainNodes[0].Name;
                        strand.angle = 0.0f;
                        ApplyStrandHookupFlags(strand, fileHairFlags != 0 ? fileHairFlags : defaultFlags);
                        MatrixHelpers.CopyMatrix3(chainNodes[0].LocalMatrix, strand.baseMat, convertCoordinates);
                        MatrixHelpers.CopyMatrix3(chainNodes[0].LocalMatrix, strand.rootMat, convertCoordinates);

                        var strandRoot = chainNodes[0];
                        var parent = NodeHelpers.GetParentNode(strandRoot, model);
                        var parentWorld = parent?.WorldMatrix ?? Matrix4x4.Identity;
                        bool hasParentInv = Matrix4x4.Invert(parentWorld, out var parentWorldInverse);

                        for (int pointIndex = 0; pointIndex < chainNodes.Count; pointIndex++)
                        {
                            var chainNode = chainNodes[pointIndex];
                            float pointLength = GetHairPointLength(chainNodes, pointIndex);
                            var currentPosition = GetNodeWorldPos(chainNode);
                            System.Numerics.Vector3 pointPosition;

                            if (pointIndex < chainNodes.Count - 1)
                            {
                                pointPosition = GetNodeWorldPos(chainNodes[pointIndex + 1]);
                            }
                            else
                            {
                                var direction = new System.Numerics.Vector3(chainNode.WorldMatrix.M21, chainNode.WorldMatrix.M22, chainNode.WorldMatrix.M23);
                                if (direction.LengthSquared() <= float.Epsilon)
                                {
                                    direction = System.Numerics.Vector3.UnitY;
                                }
                                else
                                {
                                    direction = System.Numerics.Vector3.Normalize(direction);
                                    if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y) || !float.IsFinite(direction.Z))
                                    {
                                        direction = System.Numerics.Vector3.UnitY;
                                    }
                                }
                                pointPosition = currentPosition + (direction * pointLength);
                            }

                            var resetPosition = hasParentInv
                                ? System.Numerics.Vector3.Transform(pointPosition, parentWorldInverse)
                                : pointPosition;

                            float radius = 0.0f;
                            float outerRadius = 0.0f;
                            if (pointIndex < chainNodes.Count - 1 && chainNodes.Count > 1)
                            {
                                float t = (float)pointIndex / (chainNodes.Count - 1);
                                radius = MathF.Max(0.0f, 0.75f * (1.0f - (t * 0.5f)));
                                outerRadius = MathF.Max(0.0f, 2.0f * (1.0f - t));
                            }

                            if (convertCoordinates)
                            {
                                pointPosition = MatrixHelpers.ConvertGltfVectorToMilo(pointPosition);
                                resetPosition = MatrixHelpers.ConvertGltfVectorToMilo(resetPosition);
                            }

                            var point = new MiloLib.Assets.Char.CharHair.Point
                            {
                                bone = chainNode.Name,
                                pos = ToMiloVector3(pointPosition),
                                unk5c = ToMiloVector3(resetPosition),
                                sideLength = -1.0f,
                                radius = radius,
                                outerRadius = outerRadius,
                                length = pointLength,
                            };

                            strand.points.Add(point);
                        }

                        hair.strands.Add(strand);
                    }
                }

                if (hair.strands.Count == 0) continue;

                usedEntryNames.Add(entryName);
                meta.entries.Add(new DirectoryMeta.Entry("CharHair", entryName, hair));
                createdCount++;
                Logger.Info($"[CharHair Custom] Created {entryName}: strands={hair.strands.Count}, sim={hair.simulate}, stiffness={hair.stiffness}, torsion={hair.torsion}, inertia={hair.inertia}, gravity={hair.gravity}, weight={hair.weight}, friction={hair.friction}, minSlack={hair.minSlack}, maxSlack={hair.maxSlack}");
            }

            return createdCount > 0;
        }
    }
}
