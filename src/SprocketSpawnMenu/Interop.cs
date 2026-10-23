using System;
using System.Reflection;

namespace SprocketSpawnMenu;

/// <summary>
/// Reflection helpers for the IL2CPP proxy types. Il2CppInterop turns IL2CPP fields into
/// C# properties on the proxy types in most cases, but some end up as real fields (and
/// private fields are not always public), so every accessor looks for both.
/// </summary>
internal static class Interop
{
    internal const BindingFlags InstanceAny =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    internal const BindingFlags StaticAny =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    internal static Type FindType(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = SafeGetType(asm, fullName);
            if (t != null) return t;
        }

        return null;
    }

    private static Type SafeGetType(Assembly asm, string name)
    {
        try { return asm.GetType(name, false); }
        catch { return null; }
    }

    internal static MemberInfo FindMember(Type type, string name, bool isStatic)
    {
        var flags = isStatic ? StaticAny : InstanceAny;

        for (var t = type; t != null; t = t.BaseType)
        {
            var prop = t.GetProperty(name, flags);
            if (prop != null) return prop;

            var field = t.GetField(name, flags);
            if (field != null) return field;
        }

        return null;
    }

    internal static bool TryGetValue(Type type, object instance, string name, out object value)
    {
        value = null;
        var member = FindMember(type, name, instance == null);
        if (member == null) return false;

        try
        {
            switch (member)
            {
                case PropertyInfo p:
                    value = p.GetValue(instance);
                    return true;
                case FieldInfo f:
                    value = f.GetValue(instance);
                    return true;
                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
    }

    internal static bool TrySetValue(Type type, object instance, string name, object value)
    {
        var member = FindMember(type, name, instance == null);
        if (member == null) return false;

        try
        {
            switch (member)
            {
                case PropertyInfo p:
                    p.SetValue(instance, value);
                    return true;
                case FieldInfo f:
                    f.SetValue(instance, value);
                    return true;
                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Logs every instance field/property whose declared type is TeamID or GroupID.</summary>
    internal static void LogTeamLikeFields(Type type, object instance, Action<string> log)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            foreach (var p in t.GetProperties(InstanceAny | BindingFlags.DeclaredOnly))
            {
                if (!IsTeamLike(p.PropertyType)) continue;
                try { log($"{t.Name}.{p.Name} = {p.GetValue(instance)}"); }
                catch { log($"{t.Name}.{p.Name} = <unreadable>"); }
            }

            foreach (var f in t.GetFields(InstanceAny | BindingFlags.DeclaredOnly))
            {
                if (!IsTeamLike(f.FieldType)) continue;
                try { log($"{t.Name}.{f.Name} = {f.GetValue(instance)}"); }
                catch { log($"{t.Name}.{f.Name} = <unreadable>"); }
            }
        }
    }

    private static bool IsTeamLike(Type t)
    {
        var n = t.Name;
        return n == "TeamID" || n == "GroupID";
    }
}
