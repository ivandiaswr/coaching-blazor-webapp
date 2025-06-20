using System.ComponentModel.DataAnnotations;

namespace ModelLayer.Models.Enums
{
    public enum SessionType
    {
        [Display(Name = "Life Coaching")]
        lifeCoaching,
        [Display(Name = "Career Coaching")]
        careerCoaching,
        [Display(Name = "Food Regulatory Consulting")]
        regulatoryConsulting,
        [Display(Name = "Food Regulatory Training")]
        regulatoryTraining
    }
}