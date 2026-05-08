# BitacoraTech

Plataforma SaaS para automatizar investigacion SEO, generacion de articulos con IA, revision humana y publicacion en WordPress.

## Stack

- ASP.NET Core 9
- Angular 20
- SQL Server 2022
- Docker / Docker Compose
- WordPress REST API
- Gemini, OpenAI y Claude via abstracciones

## Modulos del sistema

### `src/BitacoraTech.Api`
API HTTP principal. Expone autenticacion JWT, articulos, keywords, SEO y sitios bajo `/api/v1`.

### `src/BitacoraTech.Web`
Frontend Angular. Consume la API usando `'/api/v1'` relativo, pensado para ejecutarse detras de Nginx o bajo el mismo host.

### `src/BitacoraTech.Worker`
Proceso en segundo plano. Ejecuta ciclos de investigacion de tendencias, generacion de articulos, imagenes, SEO y cola de publicacion.

### `src/BitacoraTech.Application`
Casos de uso y contratos internos del negocio.

### `src/BitacoraTech.Domain`
Entidades y reglas de negocio. Aqui vive la validacion clave de publicacion con aprobacion humana previa.

### `src/BitacoraTech.Infrastructure`
Persistencia EF Core, seguridad, JWT, cifrado, proveedores IA, WordPress, almacenamiento local y scheduler.

### `src/BitacoraTech.Contracts`
DTOs y contratos compartidos entre capas.

### `tests`
Pruebas unitarias, de integracion y de arquitectura.

### `deploy`
Artefactos de despliegue con Docker Compose y Nginx.

### `docs`
Documentacion complementaria:

- [Arquitectura](docs/ARCHITECTURE.md)
- [API](docs/API.md)
- [Base de datos](docs/DATABASE.md)

## Arquitectura resumida

Monolito modular con enfoque Clean Architecture:

- `Web -> Api -> Application -> Domain`
- `Api -> Infrastructure -> Application -> Domain`
- `Worker -> Application + Infrastructure`

Colas/pasos principales del worker:

- keywords
- articles
- seo
- media
- publishing
- maintenance

## Requisitos

- .NET SDK 9
- Node.js 22+
- npm 10+
- Docker Desktop
- SQL Server accesible en `localhost:1433` para ejecucion local del backend

## Variables relevantes

Basate en [`.env.example`](.env.example).

- `JWT_SECRET`: minimo 32 caracteres
- `CONNECTION_STRING`: conexion a SQL Server
- `MSSQL_SA_PASSWORD`: password del contenedor SQL Server
- `WORDPRESS_ENCRYPTION_KEY`: clave compartida para cifrar credenciales WordPress
- `GEMINI_API_KEY`
- `OPENAI_API_KEY`
- `CLAUDE_API_KEY`
- `OPENROUTER_API_KEY`
- `KeywordResearch__DefaultCountry`
- `KeywordResearch__SchedulerIntervalMinutes`

## Despliegue local

Objetivo: dejar el entorno listo para que en el dia a dia solo ejecutes backend y frontend.

### Preparacion inicial

1. Copiar variables:

```powershell
Copy-Item .env.example .env
```

2. Levantar SQL Server local una sola vez:

```powershell
docker compose -f .\deploy\docker-compose.local.yml up -d
```

Notas:

- El contenedor queda persistido con volumen local.
- Mientras no lo borres, luego no hace falta volver a recrearlo.

### Ejecucion diaria

Con SQL Server ya levantado, solo necesitas correr backend y frontend.

1. Backend:

```powershell
dotnet run --project .\src\BitacoraTech.Api
```

2. Frontend:

```powershell
cd .\src\BitacoraTech.Web
npm ci
npm start
```

3. Abrir:

- Frontend: `http://localhost:4200`
- API health: `http://localhost:5250/health`
- Swagger: `http://localhost:5250/swagger`

### Que ya quedo resuelto

- El frontend ahora usa proxy dev hacia `http://localhost:5250`.
- El backend en `Development` aplica migraciones automaticamente al arrancar.
- Ya no hace falta configurar un proxy Angular manual.

### Backend local

Ideal para API, Swagger, pruebas y worker.

1. Restaurar y compilar:

```powershell
dotnet restore BitacoraTech.sln
dotnet build BitacoraTech.sln --no-restore
```

2. Levantar la API:

```powershell
dotnet run --project .\src\BitacoraTech.Api
```

La API queda en:

- `http://localhost:5250`
- `https://localhost:7182`

Healthcheck:

- `http://localhost:5250/health`

Swagger en desarrollo:

- `http://localhost:5250/swagger`

3. Levantar el worker en otra terminal:

```powershell
dotnet run --project .\src\BitacoraTech.Worker
```

Notas:

- El worker usa `KeywordResearch__SchedulerIntervalMinutes`, pero nunca baja de 15 minutos.
- Existe un seed admin por configuracion:
  - email: `admin@bitacoratech.local`
  - password: `Admin123!`

### Frontend local

```powershell
cd .\src\BitacoraTech.Web
npm ci
npm start
```

Importante:

- El frontend llama a `'/api/v1'` relativo.
- El dev server ya incluye proxy a `http://localhost:5250`.
- Si el backend cambia de puerto, actualiza `src/BitacoraTech.Web/proxy.conf.json`.

## Despliegue con Docker

El `docker-compose` actual levanta:

- `api`
- `angular`
- `sqlserver`
- `nginx`

No levanta el `worker`.

### Pasos

1. Copiar variables:

```powershell
Copy-Item .env.example .env
```

2. Levantar stack:

```powershell
docker compose -f .\deploy\docker-compose.yml up --build -d
```

3. Abrir:

- App: `http://localhost:8080`
- Health: `http://localhost:8080/health`

### Topologia Docker

- `nginx` expone `:8080`
- `nginx` enruta `/api/` hacia `api:8080`
- `nginx` enruta `/` hacia `angular:80`
- `sqlserver` expone `:1433`

### Worker en Docker

Hay `Dockerfile` para el worker en `src/BitacoraTech.Worker/Dockerfile`, pero no esta incluido en `deploy/docker-compose.yml`.

Si lo necesitas en contenedores, hay que agregar un servicio adicional al compose.

## Endpoints principales

Base path: `/api/v1`

- `POST /auth/register`
- `POST /auth/login`
- `POST /auth/refresh`
- `POST /auth/logout`
- `GET /articles`
- `GET /articles/{id}`
- `POST /articles/generate`
- `POST /articles/{id}/seo/analyze`
- `POST /articles/{id}/approve`
- `POST /articles/{id}/publish`
- `GET /keywords`
- `GET /keywords/clusters`
- `POST /keywords/research`
- `POST /seo/analyze`
- `GET /sites`
- `POST /sites`
- `PUT /sites/{id}/wordpress-settings`

## Flujo funcional

1. Registrar o autenticar usuario.
2. Crear sitio.
3. Configurar credenciales WordPress del sitio.
4. Investigar keywords y clusters.
5. Generar articulo.
6. Analizar SEO.
7. Aprobar manualmente.
8. Publicar en WordPress.

Regla importante:

- Un articulo no deberia publicarse sin aprobacion humana explicita.

## Base de datos

Relaciones principales:

- Tenant -> Users
- Tenant -> Sites
- Site -> Articles
- Site -> Keywords
- Article -> SeoAnalyses
- Article -> Images
- Article -> Approval
- Article -> PublishingJobs

El modelo esta preparado para multi-tenant y contenido por sitio.

## Pruebas

```powershell
dotnet test BitacoraTech.sln
```

## Observaciones relevantes

- El frontend trae datos semilla/fallback en `workspace.store.ts` para demo visual cuando la API no responde.
- Las credenciales WordPress se protegen con cifrado simetrico via `WORDPRESS_ENCRYPTION_KEY`.
- La seleccion de proveedor IA usa abstracciones y prioridad configurada.
- En Docker, la API puede aplicar migraciones al iniciar porque el compose define `Database__ApplyMigrationsOnStartup=true`.
- En local `Development`, la API ahora aplica migraciones automaticamente al arrancar.
