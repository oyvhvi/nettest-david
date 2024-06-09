FROM mcr.microsoft.com/dotnet/sdk:8.0 as build
WORKDIR /app

COPY ./atn062024/atn062024.csproj ./atn062024/
#COPY ./atn062024.Tests/atn062024.Tests.csproj ./atn062024.Tests/
RUN dotnet restore ./atn062024
#RUN dotnet restore ./atn062024.Tests

COPY ./atn062024/src/ ./atn062024/src/
COPY ./atn062024/appsettings.json ./atn062024/appsettings.json

#COPY ./SharedResources/ ./SharedResources/
#COPY ./atn062024.Tests/src/ ./atn062024.Tests/src/
#RUN dotnet build --no-restore ./atn062024.Tests
#RUN dotnet test --no-restore --no-build --verbosity normal ./atn062024.Tests

# TODO run tests before publish. need to connect docker due to testcontainers

RUN dotnet publish --no-restore -c release -o pub ./atn062024

FROM mcr.microsoft.com/dotnet/aspnet:8.0 as runtime
WORKDIR /app
COPY --from=build /app/pub .
ENTRYPOINT ["dotnet", "atn062024.dll"]