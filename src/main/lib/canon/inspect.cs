using System;
using System.Reflection;
using Canon.Eos.Framework;
using Canon.Eos.Framework.Eventing;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== INSPECTING EosCamera ===");
        InspectType(typeof(EosCamera));

        Console.WriteLine("\n=== INSPECTING EosImageEventArgs ===");
        InspectType(typeof(EosImageEventArgs));
    }

    static void InspectType(Type type)
    {
        Console.WriteLine("Properties:");
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            Console.WriteLine("  " + prop.PropertyType.FullName + " " + prop.Name);
        }

        Console.WriteLine("Methods:");
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            if (method.IsSpecialName) continue; // Skip property getters/setters
            Console.WriteLine("  " + method.ReturnType.FullName + " " + method.Name);
        }

        Console.WriteLine("Events:");
        foreach (var ev in type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            Console.WriteLine("  " + ev.EventHandlerType.FullName + " " + ev.Name);
        }
    }
}
