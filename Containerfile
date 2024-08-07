FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY ConvertVideos.sln .
COPY src/. ./src/

RUN dotnet restore
RUN dotnet publish src/ConvertVideos/ConvertVideos.csproj -o /app -c Release -r linux-x64 --no-self-contained


# build runtime image
FROM fedora:40

RUN dnf install -y \
    https://download1.rpmfusion.org/free/fedora/rpmfusion-free-release-$(rpm -E %fedora).noarch.rpm \
    https://download1.rpmfusion.org/nonfree/fedora/rpmfusion-nonfree-release-$(rpm -E %fedora).noarch.rpm \
        && dnf clean all \
        && rm -rf /var/cache/yum

RUN dnf install -y \
    dotnet-runtime-8.0 \
    perl-Image-ExifTool \
    ffmpeg \
        && dnf clean all \
        && rm -rf /var/cache/yum

WORKDIR /convert-videos

COPY --from=build /app .

ENTRYPOINT [ "/convert-videos/ConvertVideos" ]
