# Approval Demo Application

A full-stack approval request management system built with .NET 10 ASP.NET Core API and Angular.

## 🚀 Live Demo

The frontend is deployed on GitHub Pages: [View Demo](https://YOUR_USERNAME.github.io/YOUR_REPO_NAME/)

## 📋 Features

- Submit approval requests
- View pending approvals
- Approve/reject requests
- RESTful API backend
- Modern Angular frontend

## 🏗️ Architecture

- **Backend**: .NET 10 ASP.NET Core Web API
- **Frontend**: Angular 21 with TypeScript
- **Database**: SQL Server (local/development)

## 🛠️ Local Development

### Prerequisites

- .NET 10 SDK
- Node.js 20+
- SQL Server (or SQL Server Express)

### Backend Setup

1. Navigate to the API directory:
   ```bash
   cd ApprovalDemo/api
   ```

2. Restore dependencies:
   ```bash
   dotnet restore
   ```

3. Set up the database:
   ```bash
   # Run the database setup scripts in order:
   # 1. db_setup.sql
   # 2. seed_data.sql
   ```

4. Run the API:
   ```bash
   dotnet run
   ```

The API will be available at `https://localhost:5001`

### Frontend Setup

1. Navigate to the UI directory:
   ```bash
   cd ApprovalDemo/ui
   ```

2. Install dependencies:
   ```bash
   npm install
   ```

3. Run the development server:
   ```bash
   npm start
   ```

The frontend will be available at `http://localhost:4200`

## 🚀 Deployment

### GitHub Pages (Frontend Only)

The frontend is automatically deployed to GitHub Pages on every push to the main branch via GitHub Actions.

### Backend Deployment

The .NET API cannot run on GitHub Pages. For production deployment, consider:

- **Azure App Service**
- **Azure Container Apps**
- **AWS Elastic Beanstalk**
- **Docker containers**

## 📁 Project Structure

```
ApprovalDemo/
├── api/                    # .NET ASP.NET Core API
│   ├── Controllers/        # API controllers
│   ├── Data/              # Data access layer
│   ├── Models/            # Data models
│   └── Program.cs         # Application entry point
└── ui/                    # Angular frontend
    ├── src/
    │   ├── app/
    │   │   ├── components/    # Angular components
    │   │   ├── models/        # TypeScript models
    │   │   └── services/      # Angular services
    │   └── index.html
    └── dist/              # Build output (GitHub Pages)
```

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## 📄 License

This project is licensed under the MIT License.