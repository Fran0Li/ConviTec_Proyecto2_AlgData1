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

## RabbitMQ

Las sedes se comunican por RabbitMQ, que corre en un contenedor de Docker definido en `docker-compose.yml`.

**¿Cuándo hace falta?** Solo para correr algo que mande o reciba mensajes, como una sede completa o una prueba de pedidos entre sedes. Para trabajar en `Core` (estructuras, grafo, algoritmos) y correr `dotnet test` no se necesita.

**Levantarlo:**

1. Abrir Docker Desktop y esperar a que diga *Engine running*.
2. En la raíz del repositorio:
   ```bash
   docker compose up -d
   ```
   La primera vez descarga la imagen (unos 200 MB).
3. Comprobar que está corriendo con `docker ps`. Debe aparecer `convitec-rabbitmq`.
4. Panel web: http://localhost:15672 (usuario `convitec`, contraseña `convitec2026`).

**Apagarlo:**

| Comando | Qué hace |
|---|---|
| `docker compose stop` | Lo apaga y conserva colas y mensajes. Se vuelve a encender con `docker compose up -d`. |
| `docker compose down` | Borra el contenedor, junto con sus colas y mensajes. |

**Para tener en cuenta:**

- El contenedor no arranca solo al encender la computadora. Después de reiniciar, hay que volver a correr `docker compose up -d`.
- No hay que crear exchanges ni colas a mano en el panel. Cada sede declara los suyos al arrancar, y si ya existen RabbitMQ no hace nada.
- Si `docker ps` muestra otros contenedores, como bases de datos de otros proyectos, no afectan mientras no usen los puertos 5672 ni 15672.

## Reglas del proyecto

1. No usar colecciones del lenguaje (`List`, `Dictionary`, `Queue`, LINQ, `Sort()`, `OrderBy()`, etc.) en el código del sistema. Las estructuras las implementa el equipo.
2. Toda la lógica vive en C#. El frontend solo pide datos a la API, los muestra y envía lo que escribe el dependiente.
3. Una sede nunca lee los archivos ni las estructuras de otra: todo lo que cruza entre sedes va por mensajes.

## Flujo de trabajo con Git

- `development` es la rama por defecto y de integración del trabajo del equipo.
- Cada tarea en su propia rama creada desde `development` (por ejemplo `feature/arbol-avl`).
- Commits pequeños con mensajes descriptivos.
- Al terminar, se une la rama directamente a `development` (sin Pull Request).
- `main` recibe `development` solo en hitos (primera versión: 22 de noviembre).

## Diagrama UML de clases

*Pendiente.*
