# صورة الـ Backend فقط (لا تبني تطبيقات MAUI) — جاهزة لـ Railway.
# مرحلة البناء
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# نسخ ملفات المشاريع أولاً لاستغلال تخزين الطبقات (restore يُعاد فقط عند تغيّرها).
COPY src/Sallimni.Domain/Sallimni.Domain.csproj src/Sallimni.Domain/
COPY src/Sallimni.Application/Sallimni.Application.csproj src/Sallimni.Application/
COPY src/Sallimni.Infrastructure/Sallimni.Infrastructure.csproj src/Sallimni.Infrastructure/
COPY src/Sallimni.Api/Sallimni.Api.csproj src/Sallimni.Api/
RUN dotnet restore src/Sallimni.Api/Sallimni.Api.csproj

# نسخ بقية المصدر والنشر.
COPY src/ src/
RUN dotnet publish src/Sallimni.Api/Sallimni.Api.csproj -c Release -o /app /p:UseAppHost=false

# مرحلة التشغيل
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_ENVIRONMENT=Production
# Railway يحقن PORT تلقائياً؛ التطبيق يقرأه ويربط عليه.
ENTRYPOINT ["dotnet", "Sallimni.Api.dll"]
