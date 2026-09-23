#nullable enable
using System.Reflection;
using System.Reflection.Emit;

// The host's placement check, against the compiled mod: the throwaway copy of the building it makes may not move the
// host's random numbers. Unity's random state is native, so this reads the check's IL instead of running it.
internal static class PlacementRandomChecks
{
    public static void Run(Assembly mod, Action<string, Action> test)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        test("Placement: the host's check puts back any random numbers its copy of the building drew", () =>
        {
            // Only the host checks a replayed placement (BuildingPlacedEvent.MayPlace), by making a copy of the building
            // whose components wake up (Awake) and are disabled again. The game's own draw no game random numbers there,
            // but another mod's might, and then the host's random state would move on and the guests' would not. The
            // check keeps the state from before the copy and puts that same value back in a finally around it.
            Type placed = mod.GetType("BeaverBuddies.Events.BuildingPlacedEvent", true)!;
            MethodInfo check = placed.GetMethod("IsPlacementValidTimed", all)
                ?? throw new Exception("BuildingPlacedEvent.IsPlacementValidTimed is gone");
            List<Instruction> code = Instructions(check);
            int copy = code.FindIndex(i => i.Method?.DeclaringType?.Name == "TemplateInstantiator" && i.Method.Name == "Instantiate");
            if (copy < 0) throw new Exception("the check no longer makes its copy with TemplateInstantiator.Instantiate");
            int save = code.FindIndex(i => IsRandomState(i.Method, "get_state"));
            if (save < 0 || save > copy) throw new Exception("UnityEngine.Random.state is not saved before the copy is made");
            int? saved = save + 1 < code.Count ? StoredLocal(code[save + 1]) : null;
            if (saved == null) throw new Exception("the saved random state is not kept in a local");
            // Everything the copy runs, not only its making: a mod's preview parts (MarkAsPreviewAndInitialize,
            // Reposition) or its validator (IsValid) could draw too.
            var steps = new[] { "MarkAsPreviewAndInitialize", "Reposition", "IsValid" }
                .Select(name => (Name: name, At: code.FindIndex(i => i.Method?.DeclaringType?.Name == "BlockObject" && i.Method.Name == name)))
                .ToList();
            foreach (var step in steps)
                if (step.At < 0) throw new Exception($"the check no longer calls BlockObject.{step.Name}");
            int copyAt = code[copy].Offset;
            ExceptionHandlingClause? guard = check.GetMethodBody()!.ExceptionHandlingClauses
                .Where(c => c.Flags == ExceptionHandlingClauseOptions.Finally && Covers(c, copyAt))
                .FirstOrDefault(c => RestoresAlways(code, c, saved.Value));
            if (guard == null) throw new Exception("the saved UnityEngine.Random.state is not put back in a finally around the copy");
            var outside = steps.Where(step => !Covers(guard, code[step.At].Offset)).Select(step => step.Name).ToList();
            if (outside.Count > 0)
                throw new Exception("BlockObject." + string.Join(", BlockObject.", outside) + (outside.Count == 1 ? " runs" : " run")
                    + " outside the finally that puts the random state back");
        });
    }

    static bool Covers(ExceptionHandlingClause clause, int offset) =>
        clause.TryOffset <= offset && offset < clause.TryOffset + clause.TryLength;

    /// <summary>
    /// The finally sets UnityEngine.Random.state from the saved local, and nothing in it before that jumps past it:
    /// the state is put back on every path, not only when, say, the copy was made.
    /// </summary>
    static bool RestoresAlways(List<Instruction> code, ExceptionHandlingClause clause, int saved)
    {
        bool InHandler(int offset) => offset >= clause.HandlerOffset && offset < clause.HandlerOffset + clause.HandlerLength;
        for (int n = 1; n < code.Count; n++)
        {
            if (!IsRandomState(code[n].Method, "set_state") || !InHandler(code[n].Offset) || LoadedLocal(code[n - 1]) != saved) continue;
            int restoreAt = code[n].Offset;
            bool skippable = code.Any(i => InHandler(i.Offset) && i.Offset < restoreAt && i.Target > restoreAt);
            if (!skippable) return true;
        }
        return false;
    }

    static bool IsRandomState(MethodBase? method, string name) =>
        method != null && method.DeclaringType?.FullName == "UnityEngine.Random" && method.Name == name;

    static int? StoredLocal(Instruction i)
    {
        if (i.Op == OpCodes.Stloc_0) return 0;
        if (i.Op == OpCodes.Stloc_1) return 1;
        if (i.Op == OpCodes.Stloc_2) return 2;
        if (i.Op == OpCodes.Stloc_3) return 3;
        if (i.Op == OpCodes.Stloc_S || i.Op == OpCodes.Stloc) return i.Operand;
        return null;
    }

    static int? LoadedLocal(Instruction i)
    {
        if (i.Op == OpCodes.Ldloc_0) return 0;
        if (i.Op == OpCodes.Ldloc_1) return 1;
        if (i.Op == OpCodes.Ldloc_2) return 2;
        if (i.Op == OpCodes.Ldloc_3) return 3;
        if (i.Op == OpCodes.Ldloc_S || i.Op == OpCodes.Ldloc) return i.Operand;
        return null;
    }

    record Instruction(int Offset, OpCode Op, int Operand, MethodBase? Method, int? Target);

    /// <summary>
    /// A method's IL, one instruction each: its offset, opcode, small operand (a local's number), the method it calls
    /// and where it branches to.
    /// </summary>
    static List<Instruction> Instructions(MethodBase method)
    {
        var code = new List<Instruction>();
        byte[]? body = method.GetMethodBody()?.GetILAsByteArray();
        if (body == null) return code;
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => (ushort)o.Value);
        int position = 0;
        while (position < body.Length)
        {
            int offset = position;
            ushort value = body[position++];
            if (value == 0xfe) value = (ushort)(0xfe00 | body[position++]);
            OpCode op = opcodes[value];
            int operand = 0;
            MethodBase? called = null;
            int? target = null;
            switch (op.OperandType)
            {
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineVar: operand = body[position]; position += 1; break;
                case OperandType.ShortInlineBrTarget: target = position + 1 + (sbyte)body[position]; position += 1; break;
                case OperandType.InlineBrTarget: target = position + 4 + BitConverter.ToInt32(body, position); position += 4; break;
                case OperandType.ShortInlineI: position += 1; break;
                case OperandType.InlineVar: operand = BitConverter.ToUInt16(body, position); position += 2; break;
                case OperandType.InlineI8: case OperandType.InlineR: position += 8; break;
                case OperandType.InlineSwitch: position += 4 + 4 * BitConverter.ToInt32(body, position); break;
                case OperandType.InlineMethod:
                    try { called = method.Module.ResolveMethod(BitConverter.ToInt32(body, position)); }
                    catch (Exception) { called = null; }
                    position += 4;
                    break;
                default: position += 4; break;
            }
            code.Add(new Instruction(offset, op, operand, called, target));
        }
        return code;
    }
}
