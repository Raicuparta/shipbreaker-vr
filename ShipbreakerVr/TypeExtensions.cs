﻿using System;
using System.Linq;
using System.Reflection;

namespace ShipbreakerVr;

public static class TypeExtensions
{
    private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public |
                                       BindingFlags.Static;

    private static MemberInfo GetAnyMember(this Type type, string name)
    {
        return type.GetMember(name, Flags).FirstOrDefault() ??
               type.BaseType?.GetMember(name, Flags).FirstOrDefault() ??
               type.BaseType?.BaseType?.GetMember(name, Flags).FirstOrDefault();
    }

    public static T GetValue<T>(this object obj, string name)
    {
        return obj.GetType().GetAnyMember(name) switch
        {
            FieldInfo field => (T)field.GetValue(obj),
            PropertyInfo property => (T)property.GetValue(obj, null),
            _ => default
        };
    }
}