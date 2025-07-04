# coaching-blazor-webapp - Academic Blazor Project

## Overview

Welcome to **coaching-blazor-webapp**, an academic project called **Ítala Veloso** developed as part of a bachelors degree at ISTEC. This web application, built with **Blazor Server on .NET 9**, is accessible at [https://www.italaveloso.com/](https://www.italaveloso.com/). It offers a seamless platform for scheduling and managing personalized coaching sessions and includes an integrated AI chat powered by **Open Router**. The project demonstrates modern web development, payment processing with **Stripe**, and a dynamic **Blazor** interface, serving as an educational exploration of full-stack development.

## Features

- **Session Booking**: Schedule single sessions, session packs, or subscribe to recurring coaching plans.
- **AI Chat Integration**: Engage with an AI assistant using Open Router for real-time support.
- **Payment Integration**: Secure payments via Stripe.
- **Session Management**: Admins and users can manage session statuses, payments, and video sessions.
- **Responsive UI**: A Blazor-based interface optimized for desktop and mobile users.
- **Admin and User Dashboards**: Dedicated dashboards for admins to manage the platform and users to track their sessions and payments.

## Technologies

- **Framework**: Blazor Server with .NET 9
- **Frontend**: Razor components, HTLM, CSS
- **Backend**: C# with ASP.NET Core
- **Database**: Entity Framework Core with SQLite
- **AI Integration**: Open Router API
- **Payment Gateway**: Stripe API
- **Other Tools**: Logging services, currency conversion utilities with ExchangeRate API

## Project Structure

- **BusinessLayer**: Contains service logic, interfaces, and services for database access with Entity Framework Core, including payment and session management.
- **DataAccessLayer**: Handles the DbContext and migrations for database setup.
- **ModelLayer**: Defines data models.
- **coachingWebapp**: Hosts Blazor components, API controllers.

## License

This project is licensed under the Apache 2.0 License - see the [LICENSE](LICENSE) file for details.

## Contact

For questions or inquiries, please contact me at [ivandiaspersonal@protonmail.com](mailto:ivandiaspersonal@protonmail.com)
