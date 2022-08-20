using HarmonyLib;
using System;
using System.Reflection;

namespace OCBNET
{
    class HarmonyFieldProxy<T>
    {

        FieldInfo Field;

        public HarmonyFieldProxy(Type type, string name)
        {
            Field = AccessTools.Field(type, name);
        }

        public T Get(object instance)
        {
            return (T)Field.GetValue(instance);
        }

        public void Set(object instance, T value)
        {
            Field.SetValue(instance, value);
        }

    }
}
