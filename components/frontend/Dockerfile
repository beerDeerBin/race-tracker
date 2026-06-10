# Frontend (TP-FE) image: build the Vite SPA, then serve the static bundle with nginx.
# Built and run by Tilt as a Compose service like the backend services (no host Node needed).

# ── build stage ───────────────────────────────────────────────────────────────
FROM node:22-alpine AS build
WORKDIR /app

# Install dependencies first (cached unless the lockfile changes).
COPY package.json package-lock.json ./
RUN npm ci --no-fund --no-audit

# Build the SPA. VITE_* origins default to the local stack's host ports (see src/utils/config.ts),
# which is what the browser talks to; override at build time for other environments.
COPY . .
RUN npm run build

# ── serve stage ─────────────────────────────────────────────────────────────────
FROM nginx:1.27-alpine AS serve
# SPA history-API fallback so client routes (e.g. /vehicles/:guid) resolve to index.html.
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
