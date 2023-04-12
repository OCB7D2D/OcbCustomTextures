using HarmonyLib;
using UnityEngine;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Linq;
using static OcbCustomTextures;

public class CustomBiomesPOC
{

    // Our AccessTools is too old and doesn't have this
    // Modern HarmonyX has `AccessTool.EnumeratorMoveNext`
    public static MethodInfo GetEnumeratorMoveNext(MethodBase method)
    {
        if (method is null)
        {
            Log.Out("AccessTools.EnumeratorMoveNext: method is null");
            return null;
        }

        var codes = PatchProcessor.ReadMethodBody(method).Where(pair => pair.Key == OpCodes.Newobj);
        if (codes.Count() != 1)
        {
            Log.Out($"AccessTools.EnumeratorMoveNext: {method.FullDescription()} contains no Newobj opcode");
            return null;
        }
        var ctor = codes.First().Value as ConstructorInfo;
        if (ctor == null)
        {
            Log.Out($"AccessTools.EnumeratorMoveNext: {method.FullDescription()} contains no constructor");
            return null;
        }
        var type = ctor.DeclaringType;
        if (type == null)
        {
            Log.Out($"AccessTools.EnumeratorMoveNext: {method.FullDescription()} refers to a global type");
            return null;
        }
        return AccessTools.Method(type, nameof(IEnumerator.MoveNext));
    }

    [HarmonyPatch] class GenerateWorldFromRawInitPatched
    {

        // Select the target dynamically to patch `MoveNext`
        // Coroutine/Enumerator is compiled to a hidden class
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return GetEnumeratorMoveNext(AccessTools.Method(typeof(ChunkProviderGenerateWorldFromRaw), "Init"));
        }


        // Function will be executed at the patched position
        static void ExecutePatched(ref Color32[] cols1, ref Color32[] cols2, byte biome, int idx)
        {
            if (CustomBiomeColors.TryGetValue(biome,
                out CustomBiomeColor value))
            {
                cols1[idx] = value.color1;
                cols2[idx] = value.color2;
            }
        }

        // Main function handling the IL patching
        static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            int i = 0;
            FieldInfo cols1 = null, cols2 = null;
            var codes = new List<CodeInstruction>(instructions);
            // Find and remember `Color32[] cols1`
            for (; i < codes.Count; i++)
            {
                if (codes[i].opcode != OpCodes.Stfld) continue;
                if (!(codes[i].operand is FieldInfo field)) continue;
                if (!typeof(Color32[]).IsAssignableFrom(field.FieldType)) continue;
                if (!field.Name.StartsWith("<cols1>")) continue;
                cols1 = field;
                break;
            }
            // Find and remember `Color32[] cols2`
            for (; i < codes.Count; i++)
            {
                if (codes[i].opcode != OpCodes.Stfld) continue;
                if (!(codes[i].operand is FieldInfo field)) continue;
                if (!typeof(Color32[]).IsAssignableFrom(field.FieldType)) continue;
                if (!field.Name.StartsWith("<cols2>")) continue;
                cols2 = field;
                break;
            }
            // Continue only if fields are found
            if (cols1 != null && cols2 != null)
            {
                for (; i < codes.Count; i++)
                {
                    // Find position right before `switch (biomeAt.m_Id)`
                    if (codes[i].opcode != OpCodes.Stloc_S) continue;
                    if (!(codes[i].operand is LocalVariableInfo idx)) continue;
                    if (++i >= codes.Count) break;
                    if (codes[i].opcode != OpCodes.Ldloc_S) continue;
                    if (!(codes[i].operand is LocalVariableInfo loc)) continue;
                    if (!typeof(BiomeDefinition).IsAssignableFrom(loc.LocalType)) continue;
                    if (++i >= codes.Count) break;
                    if (codes[i].opcode != OpCodes.Ldfld) continue;
                    if (++i >= codes.Count) break;
                    if (codes[i].opcode != OpCodes.Stloc_S) continue;
                    if (!(codes[i].operand is LocalVariableInfo dst)) continue;
                    // if (!loc.LocalType.IsAssignableFrom(typeof(BiomeDefinition))) continue;
                    if (++i >= codes.Count) break;
                    // Get field `cols1` from original enumerator (push result to stack)
                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldarg_0));
                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldflda, cols1));
                    // Get field `cols2` from original enumerator (push result to stack)
                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldarg_0));
                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldflda, cols2));
                    // Push the `BiomeID` onto the evaluation stack
                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldloc_S, dst));
                    // Push the color array `index` onto the evaluation stack
                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldloc_S, idx));
                    // Execute our static function to implement stuff
                    // Ensure the signature matches what is pushed above
                    codes.Insert(i++, CodeInstruction.Call(
                        typeof(GenerateWorldFromRawInitPatched),
                        "ExecutePatched"));
                    break;
                }
            }
            // Return the result
            return codes;
        }

    }

}
