using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace ModelLayer.Models.Enums
{
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
}