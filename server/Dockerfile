FROM golang:1.27-alpine AS build
WORKDIR /src
COPY go.mod go.sum ./
RUN go mod download
COPY . .
RUN CGO_ENABLED=0 go build -trimpath -ldflags="-s -w" -o /sandglass ./cmd/sandglass

FROM gcr.io/distroless/static-debian12:nonroot
COPY --from=build /sandglass /sandglass
EXPOSE 7350
ENV SANDGLASS_ADDR=:7350
ENTRYPOINT ["/sandglass"]
