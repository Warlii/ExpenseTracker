FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ExpenseTracker.Api/ExpenseTracker.Api.csproj ExpenseTracker.Api/
RUN dotnet restore ExpenseTracker.Api/ExpenseTracker.Api.csproj
COPY ExpenseTracker.Api/ ExpenseTracker.Api/
RUN dotnet publish ExpenseTracker.Api/ExpenseTracker.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "ExpenseTracker.Api.dll"]
