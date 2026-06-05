FROM golang:1.22-alpine AS build
WORKDIR /src
COPY . .
RUN CGO_ENABLED=0 GOOS=linux go build -mod=vendor -ldflags="-s -w" -o /out/simulator .

FROM alpine:3.20
RUN adduser -D -u 10001 sim
WORKDIR /app
COPY --from=build /out/simulator /app/simulator
COPY config.example.yaml /app/config.example.yaml
USER sim
ENV CONFIG_PATH=/app/config.yaml
CMD ["/app/simulator"]
