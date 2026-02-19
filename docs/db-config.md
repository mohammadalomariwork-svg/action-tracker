# Database Configuration

Server:   WSKU1401
Database: ActionTracker
User:     TestAccount
Password: Test@4321

Connection String:
Server=mkocenter;Database=ActionTracker;User Id=mkocenterAccount;Password=Mkocenter@1;TrustServerCertificate=True;MultipleActiveResultSets=True

## Setup in User Secrets (run from backend/ActionTracker.API/):
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" 
  "Server=mkocenter;Database=ActionTracker;User Id=mkocenterAccount;Password=Mkocenter@1;TrustServerCertificate=True;MultipleActiveResultSets=True"
