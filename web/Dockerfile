# Web image (plan 014, S-61): the Angular production build served by nginx, which also proxies /api to the API container.
# Build context = web/. docker-compose.yml builds it as service `web`.

FROM node:24-alpine AS build
WORKDIR /web
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /web/dist/web/browser /usr/share/nginx/html
EXPOSE 80
