#nullable enable
using System.Reflection;
using System.Reflection.Emit;

// The 1.4.0-rc1 review (design/REVIEW-PLAN-1.4.0-beta24.md, findings in design/REVIEW-FINDINGS-1.4.0-beta24.md), checks
// against the compiled mod and the installed game's assemblies: the main session: the release-readiness leads (R), beta24's Join box (B24) and the findings made while planning (P).
internal static class RcMainRuntimeChecks
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static void Run(Assembly mod, Action<string, Action> test)
    {
        Type Game(string assembly, string type) => Assembly.Load(assembly).GetType(type, true)!;
        MethodInfo Only(Type type, string name) =>
            type.GetMethods(All).SingleOrDefault(m => m.Name == name) ?? throw new Exception($"{type.FullName}.{name} is gone");

        test("P-1: the game's Reset resets one transmitter and Reset all the partition; the mod's replay does the same", () =>
        {
            Type fragment = Game("Timberborn.AutomationUI", "Timberborn.AutomationUI.SequentialTransmitterResetFragment");
            var reset = IlScan.Instructions(Only(fragment, "OnReset"));
            var resetAll = IlScan.Instructions(Only(fragment, "OnResetAll"));
            if (!reset.Any(i => i.Calls && i.Member?.Name == "Reset" && i.Member.DeclaringType?.Name == "ISequentialTransmitter"))
                throw new Exception("the game's OnReset no longer calls ISequentialTransmitter.Reset");
            if (!resetAll.Any(i => i.Calls && i.Member?.Name == "ResetPartition" && i.Member.DeclaringType?.Name == "AutomationResetter"))
                throw new Exception("the game's OnResetAll no longer calls AutomationResetter.ResetPartition");
            // The mod's replay: the resetAll branch (compiled first) resets the partition, the other one transmitter.
            Type ev = mod.GetType("BeaverBuddies.Events.ResetTransmitterEvent", true)!;
            var replay = IlScan.Instructions(ev.GetMethod("Replay", All)!);
            int partition = replay.FindIndex(i => i.Calls && i.Member?.Name == "ResetPartition");
            int single = replay.FindIndex(i => i.Calls && i.Member?.Name == "Reset" && i.Member.DeclaringType?.Name == "ISequentialTransmitter");
            if (partition < 0 || single < 0) throw new Exception("ResetTransmitterEvent.Replay no longer calls both resets");
            if (partition > single) throw new Exception("ResetTransmitterEvent.Replay resets one transmitter for Reset all (resetAll), the partition for Reset");
            // The recorders: OnReset records resetAll = false, OnResetAll resetAll = true.
            Type Patch(string n) => mod.GetType("BeaverBuddies.Events." + n, true)!;
            // false and true compile to ldc.i4.0 and ldc.i4.1, which carry no operand (IlScan leaves Number empty).
            int? Constant(IlScan.Instruction i) => i.Number ?? (i.Op == OpCodes.Ldc_I4_0 ? 0 : i.Op == OpCodes.Ldc_I4_1 ? 1 : null);
            bool Writes(Type patch, int value) => patch.GetNestedTypes(All).Append(patch).SelectMany(t => t.GetMethods(All))
                .Select(m => IlScan.Instructions(m)).Any(ins => Enumerable.Range(1, Math.Max(0, ins.Count - 1)).Any(k =>
                    ins[k].Member?.Name == "resetAll" && Constant(ins[k - 1]) == value));
            if (!Writes(Patch("SequentialTransmitterResetFragmentOnResetPatch"), 0)) throw new Exception("OnReset no longer records resetAll = false");
            if (!Writes(Patch("SequentialTransmitterResetFragmentOnResetAllPatch"), 1)) throw new Exception("OnResetAll no longer records resetAll = true");
        });
    }
}
