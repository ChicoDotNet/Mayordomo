# Mayordomo Engine

[English (canónico)](README.md) · **Español (México)**

Mayordomo Engine es un motor reutilizable, determinista y autoritativo para juegos de tablero, **open source desde el primer día** bajo licencia MIT.

El repositorio sólo permanece privado durante el bootstrap inicial de seguridad/publicabilidad. El código, el gobierno, las contribuciones y el historial se mantienen public-safe desde el origen.

**Mayordomo — El juego que no es un juego** es el primer producto comercial real que prueba el motor, pero el engine no conoce la implementación de Mayordomo.

> Los productos conocen al engine. El engine nunca conoce a sus productos.

## La historia

El engine nació de un problema concreto: digitalizar un juego de tablero real sin convertir reglas, clientes, contenido, networking y herramientas de autoría en una sola aplicación inseparable.

Mayordomo es la prueba real. El objetivo del engine es que otro desarrollador pueda usarlo en su propio juego sin cargar con nuestra aplicación.

## Separación

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
ChicoDotNet/Mayordomo        (open source / MIT)
  └── engine reutilizable
```

La dependencia inversa está prohibida.

## Pequeño y rápido por contrato

No queremos sólo decir que el engine es ligero. Lo vamos a medir.

Presupuesto inicial del Core:

- objetivo de diseño: `Mayordomo.Core.dll ≤ 256 KiB`;
- límite duro de CI: `≤ 512 KiB`;
- dependencias runtime obligatorias de terceros: **0**;
- ejecución headless obligatoria;
- benchmarks públicos y reproducibles en cuanto exista el primer walking skeleton significativo.

Consulta [Performance y footprint](docs/performance/README.md).

## Compilar y probar

```bash
dotnet restore Mayordomo.slnx
dotnet build Mayordomo.slnx --configuration Release --no-restore
dotnet test Mayordomo.slnx --configuration Release --no-build
node scripts/check-engine-budget.mjs
node scripts/check-public-readiness.mjs
```

Antes de implementar comportamiento lee [CONTRIBUTING.md](CONTRIBUTING.md), [AGENTS.md](AGENTS.md) y [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

## Arquitectura

- [Arquitectura clean-room](docs/architecture/clean-room-board-game-engine.md)
- [Frontera engine / producto privado](docs/architecture/0001-public-engine-private-game-boundary.md)
- [Presupuestos de tamaño y rendimiento](docs/architecture/0002-performance-and-footprint-budgets.md)
- [Roadmap](docs/roadmap/README.md)

## Flujo de entrega

`working branch → squash → dev → squash → main → merge regular main→dev sin delta de contenido`

Las ramas de trabajo se conservan por defecto como historial detallado.
