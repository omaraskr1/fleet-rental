# syntax=docker/dockerfile:1

FROM node:22-alpine AS build
WORKDIR /src

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npx ng build --configuration docker

FROM nginx:alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist/frontend/browser /usr/share/nginx/html

EXPOSE 80
