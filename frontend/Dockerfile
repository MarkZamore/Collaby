ARG NODE_BASE_IMAGE=node:24.16.0-alpine@sha256:2bdb65ed1dab192432bc31c95f94155ca5ad7fc1392fb7eb7526ab682fa5bf14
ARG CADDY_BASE_IMAGE=caddy:2.11.4-alpine@sha256:77c07d5ebfa5be9fd6c820d2094ae662c9e7eeb9bf98346b7f639900263ee2a2

FROM ${NODE_BASE_IMAGE} AS build

WORKDIR /app

ARG VITE_API_BASE_URL=/api
ARG VITE_API_TIMEOUT=10000
ARG VITE_BUILD_ID=
ARG VITE_COMMIT_SHA=
ARG VITE_REF_NAME=
ARG VITE_BUILD_TIMESTAMP=

ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
ENV VITE_API_TIMEOUT=$VITE_API_TIMEOUT
ENV VITE_BUILD_ID=$VITE_BUILD_ID
ENV VITE_COMMIT_SHA=$VITE_COMMIT_SHA
ENV VITE_REF_NAME=$VITE_REF_NAME
ENV VITE_BUILD_TIMESTAMP=$VITE_BUILD_TIMESTAMP

COPY frontend/package*.json ./
# npm может пропустить ошибку optional download; такой слой нельзя кешировать как готовый.
RUN attempt=1; while :; do \
      npm ci --include=dev --include=optional --prefer-offline --no-audit --progress=false || exit $?; \
      if node --input-type=module -e "try { await import('rolldown'); await import('@tailwindcss/oxide'); await import('lightningcss'); console.log('Native build dependencies verified'); } catch (error) { console.error(error); const missing = e => e && (['MODULE_NOT_FOUND', 'ERR_MODULE_NOT_FOUND'].includes(e.code) || missing(e.cause)); process.exit(missing(error) ? 42 : 1); }"; then break; else native_status=$?; fi; \
      if [ "$native_status" -ne 42 ] || [ "$attempt" -ge 2 ]; then exit "$native_status"; fi; \
      echo 'Optional native package is missing; retrying locked npm ci once'; \
      attempt=$((attempt + 1)); \
    done

COPY frontend/ ./
COPY panels/admin /panels/admin
RUN npm run build

FROM ${CADDY_BASE_IMAGE}

COPY frontend/Caddyfile /etc/caddy/Caddyfile
COPY --from=build /app/dist /srv
COPY panels/swagger/site /srv/swagger

EXPOSE 80
