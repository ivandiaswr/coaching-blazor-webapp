using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection.Metadata.Ecma335;

public enum SessionType {
    [Display(Name = "Life Coaching")]
    lifeCoaching,
    [Display(Name = "Career Coaching")]
    careerCoaching,
    [Display(Name = "Food Regulatory Consulting")]
    regulatoryConsulting,
    [Display(Name = "Food Regulatory Training")]
    regulatoryTraining
}