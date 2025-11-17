using System;
using System.Reflection;
using System.Reflection.Emit;

namespace MetaverseCloudEngine.Unity.Scripting.Components
{
    /// <summary>
    /// Performs IL-level scanning of compiled script types to detect usage of blocked CLR APIs.
    /// </summary>
    internal static class MetaverseDotNetScriptILScanner
    {
        private static readonly OpCode[] SingleByteOpCodes = new OpCode[0x100];
        private static readonly OpCode[] MultiByteOpCodes = new OpCode[0x100];

        static MetaverseDotNetScriptILScanner()
        {
            // Build opcode lookup tables once using reflection over System.Reflection.Emit.OpCodes
            foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.GetValue(null) is not OpCode op)
                    continue;

                var value = unchecked((ushort)op.Value);
                if (value < 0x100)
                {
                    SingleByteOpCodes[value] = op;
                }
                else if ((value & 0xff00) == 0xfe00)
                {
                    MultiByteOpCodes[value & 0xff] = op;
                }
            }
        }

        internal static bool ValidateTypeIL(Type type, ref string message)
        {
            if (type == null)
                return true;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (var method in type.GetMethods(flags))
            {
                if (method == null)
                    continue;

                try
                {
                    if (!ValidateMethodIL(method, ref message))
                        return false;
                }
                catch (Exception ex)
                {
                    message = $"Security IL validation failed for method '{type.FullName}.{method.Name}': {ex.Message}";
                    return false;
                }
            }

            foreach (var ctor in type.GetConstructors(flags))
            {
                if (ctor == null)
                    continue;

                try
                {
                    if (!ValidateMethodIL(ctor, ref message))
                        return false;
                }
                catch (Exception ex)
                {
                    message = $"Security IL validation failed for constructor '{type.FullName}.ctor': {ex.Message}";
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateMethodIL(MethodBase method, ref string message)
        {
            MethodBody body;
            try
            {
                body = method.GetMethodBody();
            }
            catch
            {
                // Abstract/external methods can be ignored.
                return true;
            }

            if (body == null)
                return true;

            var il = body.GetILAsByteArray();
            if (il == null || il.Length == 0)
                return true;

            var module = method.Module;
            var position = 0;

            while (position < il.Length)
            {
                var opCode = ReadOpCode(il, ref position);

                switch (opCode.OperandType)
                {
                    case OperandType.InlineMethod:
                    {
                        var token = ReadInt32(il, ref position);
                        MethodBase target;
                        try { target = module.ResolveMethod(token); }
                        catch { break; }

                        if (target != null)
                        {
                            if (TryReportBlockedType(target.DeclaringType, method, ref message))
                                return false;

                            if (target is MethodInfo mi && TryReportBlockedType(mi.ReturnType, method, ref message))
                                return false;

                            foreach (var p in target.GetParameters())
                            {
                                if (TryReportBlockedType(p.ParameterType, method, ref message))
                                    return false;
                            }
                        }
                        break;
                    }
                    case OperandType.InlineField:
                    {
                        var token = ReadInt32(il, ref position);
                        FieldInfo field;
                        try { field = module.ResolveField(token); }
                        catch { break; }

                        if (field != null && TryReportBlockedType(field.FieldType, method, ref message))
                            return false;
                        break;
                    }
                    case OperandType.InlineType:
                    {
                        var token = ReadInt32(il, ref position);
                        Type type;
                        try { type = module.ResolveType(token); }
                        catch { break; }

                        if (TryReportBlockedType(type, method, ref message))
                            return false;
                        break;
                    }
                    case OperandType.InlineTok:
                    {
                        var token = ReadInt32(il, ref position);
                        MemberInfo member;
                        try { member = module.ResolveMember(token); }
                        catch { break; }

                        if (member is Type mt)
                        {
                            if (TryReportBlockedType(mt, method, ref message))
                                return false;
                        }
                        else if (member is FieldInfo fi)
                        {
                            if (TryReportBlockedType(fi.FieldType, method, ref message))
                                return false;
                        }
                        else if (member is MethodBase mb)
                        {
                            if (TryReportBlockedType(mb.DeclaringType, method, ref message))
                                return false;

                            if (mb is MethodInfo mi && TryReportBlockedType(mi.ReturnType, method, ref message))
                                return false;

                            foreach (var p in mb.GetParameters())
                            {
                                if (TryReportBlockedType(p.ParameterType, method, ref message))
                                    return false;
                            }
                        }
                        break;
                    }
                    case OperandType.InlineSwitch:
                    {
                        var count = ReadInt32(il, ref position);
                        position += count * 4;
                        break;
                    }
                    default:
                    {
                        position += GetOperandSize(opCode.OperandType);
                        break;
                    }
                }
            }

            return true;
        }

        private static OpCode ReadOpCode(byte[] il, ref int position)
        {
            byte code = il[position++];
            if (code != 0xfe)
                return SingleByteOpCodes[code];

            byte second = il[position++];
            return MultiByteOpCodes[second];
        }

        private static int ReadInt32(byte[] il, ref int position)
        {
            int value = il[position] | (il[position + 1] << 8) | (il[position + 2] << 16) | (il[position + 3] << 24);
            position += 4;
            return value;
        }

        private static int GetOperandSize(OperandType operandType)
        {
            switch (operandType)
            {
                case OperandType.InlineNone:
                    return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    return 1;
                case OperandType.InlineVar:
                    return 2;
                case OperandType.InlineI:
                case OperandType.InlineBrTarget:
                case OperandType.InlineString:
                case OperandType.InlineSig:
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                    return 4;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;
                case OperandType.ShortInlineR:
                    return 4;
                case OperandType.InlineSwitch:
                    // Handled separately
                    return 0;
                default:
                    return 0;
            }
        }

        private static bool TryReportBlockedType(Type type, MethodBase method, ref string message)
        {
            if (type == null)
                return false;

            if (!MetaverseDotNetScriptSecurity.IsTypeBlockedDeep(type, out var reason))
                return false;

            var owner = method.DeclaringType?.FullName ?? "<unknown>";
            message = $"Method '{owner}.{method.Name}' uses blocked API via IL reference: {reason}";
            return true;
        }
    }
}

