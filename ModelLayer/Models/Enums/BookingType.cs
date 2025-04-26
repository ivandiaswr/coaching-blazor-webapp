using System.ComponentModel.DataAnnotations;

namespace ModelLayer.Models.Enums
{
    public enum BookingType
    {
        [Display(Name = "Single Session")]
        SingleSession,
        [Display(Name = "Session Pack")]
        SessionPack,
        [Display(Name = "Subscription")]
        Subscription
    }
}