FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS build

ARG Version=0.0.0
ARG NUGET_KEY
ARG NUGET_URL
ARG NUGET_SYMBOL_URL
WORKDIR /sln

COPY . .

RUN dotnet restore
RUN dotnet build /p:Version=$Version -c Release --no-restore
RUN dotnet test --filter Category!=Integration --no-build -c Release
RUN dotnet pack /p:Version=$Version -c Release --no-restore --no-build -o /sln/artifacts -p:IncludeSymbols=false
RUN dotnet nuget push /sln/artifacts/*.nupkg --source $NUGET_URL --api-key $NUGET_KEY
