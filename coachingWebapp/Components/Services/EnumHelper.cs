using System.ComponentModel.DataAnnotations;
using System.Reflection;

public static class EnumHelper
{
    public static string GetDisplayName(this Enum enumValue)
    {
        return enumValue
            .GetType()
            .GetField(enumValue.ToString())
            ?.GetCustomAttribute<DisplayAttribute>()?
            .Name ?? enumValue.ToString();
    }
}