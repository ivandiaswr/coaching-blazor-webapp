using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ModelLayer.Models;

namespace BusinessLayer.Services.Interfaces;

public interface IContactService
{
    Task<bool> ContactSubmitAsync(Contact contact);
    Task SendEmailAsync(Contact contact);
}
