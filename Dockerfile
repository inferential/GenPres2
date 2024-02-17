FROM mcr.microsoft.com/dotnet/sdk:8.0 as build

# Install node
# RUN curl -sL https://deb.nodesource.com/setup_14.x | bash
# RUN apt-get update && apt-get install -y nodejs
# update the repository sources list
# and install dependencies
RUN mkdir /usr/local/nvm
ENV NVM_DIR /usr/local/nvm
ENV NODE_VERSION 20.11.1
RUN curl https://raw.githubusercontent.com/nvm-sh/nvm/v0.39.1/install.sh | bash \
    && . $NVM_DIR/nvm.sh \
    && nvm install $NODE_VERSION \
    && nvm alias default $NODE_VERSION \
    && nvm use default

ENV NODE_PATH $NVM_DIR/v$NODE_VERSION/lib/node_modules
ENV PATH $NVM_DIR/versions/node/v$NODE_VERSION/bin:$PATH

WORKDIR /workspace
COPY .config .config
RUN dotnet tool restore
COPY .paket .paket
COPY paket.dependencies paket.lock ./

FROM build as server-build
COPY src/Informedica.Utils.Lib src/Informedica.Utils.Lib
COPY src/Informedica.ZIndex.Lib src/Informedica.ZIndex.Lib
COPY src/Informedica.ZForm.Lib src/Informedica.ZForm.Lib
COPY src/Informedica.KinderFormularium.Lib src/Informedica.KinderFormularium.Lib
COPY src/Informedica.GenUnits.Lib src/Informedica.GenUnits.Lib
COPY src/Informedica.GenCore.Lib src/Informedica.GenCore.Lib
COPY src/Informedica.GenSolver.Lib src/Informedica.GenSolver.Lib
COPY src/Informedica.GenForm.Lib src/Informedica.GenForm.Lib
COPY src/Informedica.GenOrder.Lib src/Informedica.GenOrder.Lib
COPY src/Shared src/Shared
COPY src/Server src/Server
RUN cd src/Server && dotnet publish -c release -o ../../deploy


FROM build as client-build
COPY package.json package-lock.json ./
RUN npm install
COPY vite.config.mts ./
COPY src/Shared src/Shared
COPY src/Client src/Client
RUN npm run build


FROM mcr.microsoft.com/dotnet/aspnet:8.0
COPY --from=server-build /workspace/deploy /app
COPY --from=client-build /workspace/deploy /app/public
COPY src/Server/data /app/data

ENV GENPRES_LOG="0"
ENV GENPRES_PROD="1"
ENV GENPRES_URL_ID="1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA"
WORKDIR /app
EXPOSE 8085
ENTRYPOINT [ "dotnet", "Server.dll" ]
