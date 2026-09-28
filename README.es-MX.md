# Mayordomo Engine

[English (canónico)](README.md) · **Español (México)**

Mayordomo Engine es el repositorio de incubación de un motor reutilizable, determinista y autoritativo para juegos de tablero.

**Mayordomo — El juego que no es un juego** es el primer producto comercial real que prueba el motor, pero el engine no conoce la implementación de Mayordomo.

> Los productos conocen al engine. El engine nunca conoce a sus productos.

## Estado

**Pre-alpha / fundación activa.**

Aunque el repositorio todavía es privado, mantenemos todo su historial como **public-safe desde hoy** para que pueda hacerse público más adelante sin cirugía de Git.

El software reutilizable de este repositorio usa licencia **MIT**.

## Separación de repositorios

```text
ChicoDotNet/Mayordomo.Game   (privado)
  ├── reglas y lógica de Mayordomo
  ├── contenido oficial/autorizado
  ├── Mayordomo Studio
  ├── Web React
  ├── Unity
  ├── Avalonia
  └── integraciones comerciales
             │
             │ consume
             ▼
ChicoDotNet/Mayordomo        (publicable / MIT)
  └── engine reutilizable
```

La dependencia inversa está prohibida.

## Compilar y probar

```bash
dotnet restore Mayordomo.slnx
dotnet build Mayordomo.slnx --configuration Release --no-restore
dotnet test Mayordomo.slnx --configuration Release --no-build
node scripts/check-public-readiness.mjs
```

Antes de implementar comportamiento lee [CONTRIBUTING.md](CONTRIBUTING.md), [AGENTS.md](AGENTS.md) y [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

## Arquitectura

Consulta:

- [Arquitectura clean-room del engine](docs/architecture/clean-room-board-game-engine.md)
- [Frontera engine público / juego privado](docs/architecture/0001-public-engine-private-game-boundary.md)
- [Roadmap](docs/roadmap/README.md)

## Flujo de entrega

`working branch → squash → dev → squash → main → merge regular main→dev sin delta de contenido`

Las ramas de trabajo se conservan por defecto como historial detallado.
