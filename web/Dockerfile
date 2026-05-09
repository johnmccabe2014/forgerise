# syntax=docker/dockerfile:1.7
# ForgeRise Web — Next.js standalone build, non-root, multi-stage.
# Build context: repository root (because pnpm-lock.yaml lives at the root).

ARG NODE_TAG=22-alpine

FROM node:${NODE_TAG} AS base
RUN corepack enable && corepack prepare pnpm@9.0.0 --activate

FROM base AS deps
WORKDIR /repo
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY web/package.json web/package.json
RUN pnpm install --frozen-lockfile --filter ./web...

FROM base AS builder
WORKDIR /repo
COPY --from=deps /repo/node_modules ./node_modules
COPY --from=deps /repo/web/node_modules ./web/node_modules
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY web ./web
ENV NEXT_TELEMETRY_DISABLED=1
RUN pnpm --filter ./web build

FROM node:${NODE_TAG} AS runner
WORKDIR /app
ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    PORT=3000 \
    HOSTNAME=0.0.0.0

# Non-root user.
RUN addgroup -S -g 10001 nextjs \
 && adduser  -S -u 10001 -G nextjs nextjs

# Standalone output bundles only the runtime files Next needs.
COPY --from=builder --chown=nextjs:nextjs /repo/web/public            ./public
COPY --from=builder --chown=nextjs:nextjs /repo/web/.next/standalone  ./
COPY --from=builder --chown=nextjs:nextjs /repo/web/.next/static      ./web/.next/static

USER nextjs
EXPOSE 3000

HEALTHCHECK --interval=30s --timeout=3s --start-period=15s --retries=3 \
    CMD wget -q -O - http://127.0.0.1:3000/ || exit 1

# server.js sits inside web/ within the standalone bundle.
CMD ["node", "web/server.js"]
