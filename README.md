# ConviTEC: inventario distribuido y sistema de pedidos

Proyecto 2 de CE1103 Algoritmos y Estructuras de Datos 1, Instituto Tecnológico de Costa Rica.

ConviTEC simula una cadena de tiendas de conveniencia con cuatro sedes (Liberia, Alajuela, San José y Limón). Cada sede es una instancia independiente de la misma aplicación, con su API en C#, su frontend web y sus archivos locales. Las sedes se comunican por mensajes a través de RabbitMQ, y las entregas se planifican sobre un grafo de rutas con el algoritmo de Dijkstra.

## Requisitos

- .NET SDK 10 (comprobar con `dotnet --version`)
- Docker Desktop, para RabbitMQ
- VS Code con la extensión C# Dev Kit, o Visual Studio

## Estructura de la solución

```
ConviTEC.sln
docker-compose.yml        RabbitMQ
data/<SEDE>/              Archivos locales de cada sede (LIB, ALA, SJO, LIM)
src/
  ConviTEC.Core/          Lógica de negocio, estructuras de datos, algoritmos y grafo
  ConviTEC.Persistencia/  Lectura y escritura de los archivos de cada sede
  ConviTEC.Mensajeria/    Comunicación con RabbitMQ
  ConviTEC.Sede/          Programa de una sede: API REST + frontend (wwwroot)
tests/
  ConviTEC.Tests/         Pruebas unitarias (xUnit)
```

`Sede` depende de `Core`, `Persistencia` y `Mensajeria`; estos dos últimos dependen solo de `Core`.

## Cómo ejecutar

```bash
dotnet build                               # compilar todo
dotnet test                                # correr las pruebas
dotnet run --project src/ConviTEC.Sede     # levantar una sede
docker compose up -d                       # levantar RabbitMQ (con Docker Desktop abierto)
```

*Pendiente: instrucciones para levantar cada sede con su configuración y para ejecutar en dos computadoras.*

## Reglas del proyecto

1. No usar colecciones del lenguaje (`List`, `Dictionary`, `Queue`, LINQ, `Sort()`, `OrderBy()`, etc.) en el código del sistema. Las estructuras las implementa el equipo.
2. Toda la lógica vive en C#. El frontend solo pide datos a la API, los muestra y envía lo que escribe el dependiente.
3. Una sede nunca lee los archivos ni las estructuras de otra: todo lo que cruza entre sedes va por mensajes.

## Flujo de trabajo con Git

- `main`: versiones estables. `development`: integración del trabajo del equipo.
- Cada tarea en su propia rama creada desde `development` (por ejemplo `feature/arbol-avl`).
- Commits pequeños con mensajes descriptivos.
- Al terminar, Pull Request hacia `development`; otro integrante lo revisa antes de unir.

## Diagrama UML de clases

*Pendiente.*
