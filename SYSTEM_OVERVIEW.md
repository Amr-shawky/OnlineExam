# OnlineExam System Overview

OnlineExam is a **.NET 8 Web API** for managing online exams, categories, questions, and user attempts. It provides account/authentication features, exam workflows, and admin reporting endpoints.

## Core Stack

- **ASP.NET Core Minimal APIs**
- **Entity Framework Core (SQLite)**
- **ASP.NET Identity + JWT authentication**
- **MediatR + FluentValidation**
- **Redis distributed cache**
- **Serilog logging**

## Project Structure

- `Domain/`: entities, enums, and interfaces
- `Infrastructure/`: database context, repositories, and unit of work
- `Features/`: business features grouped by module (Accounts, Categories, Exams, Questions, UserAnswers, Dashboard, Profile)
- `Middlewares/`: cross-cutting request pipeline behaviors
- `Shared/`: shared helpers, responses, and seed logic
- `Program.cs`: dependency registration, middleware setup, endpoint mapping, migrations, and seeding

## Main Functional Areas

- **Accounts**: register, login, logout, email confirmation, password reset, refresh token handling
- **Categories**: create/update/delete and list/retrieve categories
- **Exams**: list exams, exam details, create/edit/delete exams, start/submit attempts
- **Questions**: add/update/delete questions and retrieve question details
- **User Answers**: user attempt/history retrieval
- **Dashboard**: admin statistics and activity insights

## Runtime Notes

- Uses EF Core migrations and database seeding on startup.
- Uses Redis for caching selected GET endpoints and cache test endpoint.
- Uses Swagger in development for API exploration.
