using System;
using System.Reflection;

namespace IMV.Config;

public static class AssemblyExtentions
{
    public static T? GetCustomAttribute<T>(Assembly asm) where T : Attribute =>
        (T?)Attribute.GetCustomAttribute(asm, typeof(T));

    public static string Company(this Assembly asm)
    {
        var attr = asm.GetCustomAttribute<AssemblyCompanyAttribute>();
        return attr!=null ? attr.Company : String.Empty;
    }

    public static string Copyright(this Assembly asm)
    {
        var attr = asm.GetCustomAttribute<AssemblyCopyrightAttribute>();
        return attr != null ? attr.Copyright : String.Empty;
    }

    public static string Description(this Assembly asm)
    {
        var attr = asm.GetCustomAttribute<AssemblyDescriptionAttribute>();
        return attr != null ? attr.Description : String.Empty;
    }

    public static string Product(this Assembly asm)
    {
        var attr = asm.GetCustomAttribute<AssemblyProductAttribute>();
        return attr != null ? attr.Product : String.Empty;
    }

    public static string Title(this Assembly asm)
    {
        var attr = asm.GetCustomAttribute<AssemblyTitleAttribute>();
        return attr != null ? attr.Title : String.Empty;
    }

    public static string Trademark(this Assembly asm)
    {
        var attr = asm.GetCustomAttribute<AssemblyTrademarkAttribute>();
        return attr != null ? attr.Trademark : String.Empty;
    }

    public static string AssemblyVersion(this Assembly asm)
    {
        var ver = asm.GetName().Version;
        return ver != null ? ver.ToString() : string.Empty;
    }

    public static string FileVersion(this Assembly asm)
    {            
        var attr = asm.GetCustomAttribute<AssemblyFileVersionAttribute>();
        return attr != null ? attr.Version : String.Empty;
    }

    public static string InformationalVersion(this Assembly asm)
    {
        var attr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        return attr != null ? attr.InformationalVersion : String.Empty;
    }
}
