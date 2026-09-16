FROM mcr.microsoft.com/dotnet/sdk:10.0

# Install Node.js 26.x
RUN curl -fsSL https://deb.nodesource.com/setup_26.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && apt-get clean && rm -rf /var/lib/apt/lists/*

# Create non-root user for development
ARG USERNAME=devuser
ARG USER_UID=1000
ARG USER_GID=${USER_UID}
RUN groupadd --gid ${USER_GID} ${USERNAME} \
    && useradd --uid ${USER_UID} --gid ${USER_GID} -m ${USERNAME} \
    && mkdir -p /home/${USERNAME}/.dotnet/tools \
    && chown -R ${USERNAME}:${USERNAME} /home/${USERNAME}

# Install EF Core CLI tools for the non-root user
USER ${USERNAME}
RUN dotnet tool install --global dotnet-ef

# Ensure dotnet tools are on PATH
ENV PATH="${PATH}:/home/devuser/.dotnet/tools"
