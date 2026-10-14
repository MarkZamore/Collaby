ARG POSTGRES_BASE_IMAGE=postgres:16-alpine@sha256:20edbde7749f822887a1a022ad526fde0a47d6b2be9a8364433605cf65099416

FROM ${POSTGRES_BASE_IMAGE}

COPY ./00-extensions.sql /docker-entrypoint-initdb.d/
COPY ./00-roles-and-databases.sh /docker-entrypoint-initdb.d/

EXPOSE 5432
